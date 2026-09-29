using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Echo_Windows.Services;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record AudioDeviceRoute(DataFlow Flow, string? SelectedId, string ActiveId, string Name);

public static class AudioEndpointChangePolicy
{
    public static AudioDeviceRoute? FindUnavailableRoute(IEnumerable<AudioDeviceRoute> routes, string deviceId, DeviceState state)
        => state == DeviceState.Active ? null : routes.FirstOrDefault(route => route.ActiveId == deviceId);

    public static bool IsFollowingDefault(AudioDeviceRoute route, DataFlow flow, Role role, string? newDefaultId)
        => role == Role.Multimedia && route.Flow == flow && route.SelectedId is null && route.ActiveId != newDefaultId;

    public static bool ShouldRefreshDefaultAfterUnavailable(AudioDeviceRoute route) => route.SelectedId is null;
}

public sealed class AudioCapture : IDisposable
{
    private readonly object gate = new();
    private readonly List<Source> sources = [];
    private readonly object notificationGate = new();
    private readonly MMDeviceEnumerator notificationEnumerator = new();
    private readonly MMDeviceNotificationClient notificationClient;
    private List<AudioDeviceRoute> routes = [];
    private bool notificationsEnabled;
    private bool notificationDisposed;
    private int failureRaised;
    private bool disposing;
    public event Action<Exception>? Failed;
    public event Action<DataFlow>? DefaultDeviceChanged;
    public AudioCapture()
    {
        notificationClient = notificationEnumerator.CreateNotificationClient(useSynchronizationContext: false);
        notificationClient.DeviceStateChanged += OnDeviceStateChanged;
        notificationClient.DeviceRemoved += OnDeviceRemoved;
        notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }
    public string? ActiveOutputId { get { lock (notificationGate) return routes.FirstOrDefault(r => r.Flow == DataFlow.Render)?.ActiveId; } }
    public string? ActiveInputId { get { lock (notificationGate) return routes.FirstOrDefault(r => r.Flow == DataFlow.Capture)?.ActiveId; } }
    public bool IsFollowingDefault(DataFlow flow) { lock (notificationGate) return routes.Any(r => r.Flow == flow && r.SelectedId is null); }
    public static List<AudioDevice> Devices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        return devices.Select(d => new AudioDevice(d.ID, d.FriendlyName)).ToList();
    }
    public void Start(int mode, string? outputId, string? inputId)
    {
        lock (gate) StartLocked(mode, outputId, inputId);
    }
    public void Restart(int mode, string? outputId, string? inputId)
    {
        lock (gate)
        {
            DisposeSourcesLocked();
            StartLocked(mode, outputId, inputId);
        }
    }
    private void StartLocked(int mode, string? outputId, string? inputId)
    {
        using var enumerator = new MMDeviceEnumerator();
        disposing = false;
        var nextRoutes = new List<AudioDeviceRoute>();
        try
        {
            if (mode is 0 or 2)
            {
                var device = outputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(outputId);
                nextRoutes.Add(new(DataFlow.Render, outputId, device.ID, device.FriendlyName));
                Add(new WasapiLoopbackCapture(device));
            }
            if (mode is 1 or 2)
            {
                var device = inputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(inputId);
                nextRoutes.Add(new(DataFlow.Capture, inputId, device.ID, device.FriendlyName));
                Add(new WasapiCapture(device));
            }
            foreach (var source in sources) source.Capture.StartRecording();
            lock (notificationGate) { routes = nextRoutes; notificationsEnabled = true; }
            Interlocked.Exchange(ref failureRaised, 0);
        }
        catch { DisposeSourcesLocked(); throw; }
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
    {
        AudioDeviceRoute? route;
        lock (notificationGate)
            route = notificationsEnabled ? AudioEndpointChangePolicy.FindUnavailableRoute(routes, e.DeviceId, e.NewState) : null;
        if (route is null) return;
        if (AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(route)) DefaultDeviceChanged?.Invoke(route.Flow);
        else ReportDeviceFailure($"音频设备“{route.Name}”已停用或断开。");
    }

    private void OnDeviceRemoved(object? sender, DeviceNotificationEventArgs e)
    {
        AudioDeviceRoute? route;
        lock (notificationGate) route = notificationsEnabled ? routes.FirstOrDefault(r => r.ActiveId == e.DeviceId) : null;
        if (route is null) return;
        if (AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(route)) DefaultDeviceChanged?.Invoke(route.Flow);
        else ReportDeviceFailure($"音频设备“{route.Name}”已移除。");
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        bool following;
        lock (notificationGate)
            following = notificationsEnabled && routes.Any(route => AudioEndpointChangePolicy.IsFollowingDefault(route, e.Flow, e.Role, e.DeviceId));
        if (!following) return;
        if (string.IsNullOrWhiteSpace(e.DeviceId))
        {
            ReportDeviceFailure($"系统已取消默认{(e.Flow == DataFlow.Render ? "播放设备" : "录音设备")}，无法继续采集。");
            return;
        }
        DefaultDeviceChanged?.Invoke(e.Flow);
    }

    private void ReportDeviceFailure(string message)
    {
        if (Interlocked.Exchange(ref failureRaised, 1) == 0)
            Failed?.Invoke(new IOException(message + "录音已停止，已收到的字幕会保存；选择可用设备后重新开始。"));
    }
    private void Add(WasapiCapture capture)
    {
        var buffer = new BufferedWaveProvider(capture.WaveFormat, TimeSpan.FromSeconds(2)) { ReadFully = true, DiscardOnBufferOverflow = false };
        ISampleProvider sample = buffer.ToSampleProvider();
        if (sample.WaveFormat.Channels == 2) sample = new StereoToMonoSampleProvider(sample);
        else if (sample.WaveFormat.Channels != 1) sample = new AverageChannels(sample);
        var source = new Source(capture, buffer, new WdlResamplingSampleProvider(sample, 16000));
        sources.Add(source);
        source.DataHandler = (_, e) => { if (source.Stopping) return; try { buffer.AddSamples(e.Buffer, 0, e.BytesRecorded); } catch (Exception error) { if (!source.Stopping) Failed?.Invoke(error); } };
        source.StoppedHandler = (_, e) => { if (!source.Stopping && !disposing) Failed?.Invoke(e.Exception ?? new IOException("音频设备停止采集。")); };
        capture.DataAvailable += source.DataHandler;
        capture.RecordingStopped += source.StoppedHandler;
    }
    public byte[] ReadFrame(out double level)
    {
        lock (gate)
        {
            var mixed = new float[320];
            foreach (var s in sources)
            {
                s.ReadOutputFrame();
                for (int i = 0; i < mixed.Length; i++) mixed[i] += s.OutputFrame[i] / sources.Count;
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
    }
    public void Dispose()
    {
        lock (gate)
        {
            DisposeSourcesLocked();
            if (!notificationDisposed)
            {
                notificationDisposed = true;
                notificationClient.Dispose();
                notificationEnumerator.Dispose();
            }
        }
    }
    public void StopInputs()
    {
        lock (gate)
        {
            disposing = true;
            lock (notificationGate) notificationsEnabled = false;
            foreach (var source in sources) { source.Stopping = true; try { source.Capture.StopRecording(); } catch { } }
        }
    }
    public int TailFrames { get { lock (gate) return sources.Count == 0 ? 0 : Math.Min(102, (int)Math.Ceiling(sources.Max(s => s.Buffer.BufferedDuration.TotalSeconds) * 50) + 2); } }
    private void DisposeSourcesLocked()
    {
        disposing = true;
        lock (notificationGate) { notificationsEnabled = false; routes = []; }
        foreach (var source in sources)
        {
            source.Stopping = true;
            source.Capture.DataAvailable -= source.DataHandler;
            source.Capture.RecordingStopped -= source.StoppedHandler;
            try { source.Capture.StopRecording(); } catch { }
            source.Capture.Dispose();
        }
        sources.Clear();
    }
    private sealed class Source(WasapiCapture capture, BufferedWaveProvider buffer, ISampleProvider resampled)
    {
        public WasapiCapture Capture { get; } = capture;
        public BufferedWaveProvider Buffer { get; } = buffer;
        public ISampleProvider Resampled { get; } = resampled;
        public ClockDriftController Drift { get; } = new();
        public float[] InputFrame { get; } = new float[320 + ClockDriftController.MaximumFrameAdjustment];
        public float[] OutputFrame { get; } = new float[320];
        public volatile bool Stopping;
        public EventHandler<WaveInEventArgs> DataHandler = null!;
        public EventHandler<StoppedEventArgs> StoppedHandler = null!;
        public void ReadOutputFrame()
        {
            int inputFrames = Drift.InputFramesFor(OutputFrame.Length, Buffer.BufferedDuration.TotalSeconds);
            var input = InputFrame.AsSpan(0, inputFrames);
            int read = Resampled.Read(input);
            if (read < inputFrames) input[read..].Clear();
            double sourceStride = (double)(inputFrames - 1) / (OutputFrame.Length - 1);
            for (int i = 0; i < OutputFrame.Length; i++)
            {
                double position = i * sourceStride;
                int left = (int)position;
                int right = Math.Min(left + 1, inputFrames - 1);
                float fraction = (float)(position - left);
                OutputFrame[i] = input[left] + (input[right] - input[left]) * fraction;
            }
        }
    }
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
