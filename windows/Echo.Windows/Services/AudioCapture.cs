using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Echo_Windows.Services;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed class AudioCapture : IDisposable
{
    private readonly List<Source> sources = [];
    private bool disposing;
    public event Action<Exception>? Failed;
    public static List<AudioDevice> Devices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        return devices.Select(d => new AudioDevice(d.ID, d.FriendlyName)).ToList();
    }
    public void Start(int mode, string? outputId, string? inputId)
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            if (mode is 0 or 2) Add(new WasapiLoopbackCapture(outputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(outputId)));
            if (mode is 1 or 2) Add(new WasapiCapture(inputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(inputId)));
            foreach (var source in sources) source.Capture.StartRecording();
        }
        catch { Dispose(); throw; }
    }
    private void Add(WasapiCapture capture)
    {
        var buffer = new BufferedWaveProvider(capture.WaveFormat, TimeSpan.FromSeconds(2)) { ReadFully = true, DiscardOnBufferOverflow = false };
        ISampleProvider sample = buffer.ToSampleProvider();
        if (sample.WaveFormat.Channels == 2) sample = new StereoToMonoSampleProvider(sample);
        else if (sample.WaveFormat.Channels != 1) sample = new AverageChannels(sample);
        var source = new Source(capture, buffer, new WdlResamplingSampleProvider(sample, 16000));
        sources.Add(source);
        capture.DataAvailable += (_, e) => { try { buffer.AddSamples(e.Buffer, 0, e.BytesRecorded); } catch (Exception error) { Failed?.Invoke(error); } };
        capture.RecordingStopped += (_, e) => { if (!disposing) Failed?.Invoke(e.Exception ?? new IOException("音频设备停止采集。")); };
    }
    public byte[] ReadFrame(out double level)
    {
        var mixed = new float[320];
        foreach (var s in sources)
        {
            var part = new float[320]; int count = s.Resampled.Read(part.AsSpan());
            for (int i = 0; i < count; i++) mixed[i] += part[i] / sources.Count;
        }
        level = mixed.Max(x => Math.Abs(x));
        var bytes = new byte[640];
        for (int i = 0; i < mixed.Length; i++)
        {
            short v = (short)(Math.Clamp(mixed[i], -1f, 1f) * short.MaxValue);
            bytes[i * 2] = (byte)v; bytes[i * 2 + 1] = (byte)(v >> 8);
        }
        return bytes;
    }
    public void Dispose()
    {
        StopInputs();
        foreach (var source in sources) source.Capture.Dispose();
        sources.Clear();
    }
    public void StopInputs()
    {
        disposing = true;
        foreach (var source in sources) { try { source.Capture.StopRecording(); } catch { } }
    }
    public int TailFrames => sources.Count == 0 ? 0 : Math.Min(102, (int)Math.Ceiling(sources.Max(s => s.Buffer.BufferedDuration.TotalSeconds) * 50) + 2);
    private sealed record Source(WasapiCapture Capture, BufferedWaveProvider Buffer, ISampleProvider Resampled);
    private sealed class AverageChannels(ISampleProvider input) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate, 1);
        public int Read(Span<float> buffer)
        {
            var data = new float[buffer.Length]; int read = Read(data, 0, data.Length);
            data.AsSpan(0, read).CopyTo(buffer); return read;
        }
        public int Read(float[] buffer, int offset, int count)
        {
            int channels = input.WaveFormat.Channels;
            var scratch = new float[count * channels]; int frames = input.Read(scratch.AsSpan()) / channels;
            for (int i = 0; i < frames; i++) { float sum = 0; for (int c = 0; c < channels; c++) sum += scratch[i * channels + c]; buffer[offset + i] = sum / channels; }
            return frames;
        }
    }
}
