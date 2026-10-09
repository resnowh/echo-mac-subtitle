using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Echo_Windows.Core;

namespace Echo_Windows.Services;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record AudioDeviceRoute(DataFlow Flow, string? SelectedId, string ActiveId, string Name);

public interface ISpeechSessionCapture : IDisposable
{
    event Action<Exception>? Failed;
    event Action<DataFlow>? DefaultDeviceChanged;
    string? ActiveOutputId { get; }
    string? ActiveInputId { get; }
    double BufferedSeconds { get; }
    int TailFrames { get; }
    bool IsFollowingDefault(DataFlow flow);
    void Start(int mode, string? outputId, string? inputId);
    void Restart(int mode, string? outputId, string? inputId);
    void PrepareRestart(int mode, string? outputId, string? inputId);
    void CommitPreparedRestart();
    void AbortPreparedRestart();
    byte[] ReadFrame(out double level);
    void StopInputs();
}

public static class AudioEndpointChangePolicy
{
    public static AudioDeviceRoute? FindUnavailableRoute(IEnumerable<AudioDeviceRoute> routes, string deviceId, DeviceState state)
        => state == DeviceState.Active ? null : routes.FirstOrDefault(route => route.ActiveId == deviceId);

    public static bool IsFollowingDefault(AudioDeviceRoute route, DataFlow flow, Role role, string? newDefaultId)
        => role == Role.Multimedia && route.Flow == flow && route.SelectedId is null && route.ActiveId != newDefaultId;

    public static bool ShouldRefreshDefaultAfterUnavailable(AudioDeviceRoute route) => route.SelectedId is null;
}

public static class AudioCaptureErrorPresentation
{
    private const int ErrorAccessDenied = 5;
    private const int EAccessDenied = unchecked((int)0x80070005);

    public static bool IsAccessDenied(Exception error)
    {
        if (error is UnauthorizedAccessException
            || error is Win32Exception { NativeErrorCode: ErrorAccessDenied }
            || error is COMException { HResult: EAccessDenied }) return true;
        if (error is AggregateException aggregate && aggregate.InnerExceptions.Any(IsAccessDenied)) return true;
        return error.InnerException is not null && IsAccessDenied(error.InnerException);
    }

    public static string GetUserMessage(Exception error, bool microphoneRequested)
    {
        if (!IsAccessDenied(error)) return error.Message;
        return microphoneRequested
            ? "Windows 拒绝了 Echo 访问麦克风。请打开“设置 → 隐私和安全性 → 麦克风”，启用“麦克风访问”和“让桌面应用访问麦克风”，然后重试。"
            : "Windows 拒绝了 Echo 访问音频设备。请检查设备状态和 Windows 音频隐私设置后重试。";
    }
}

public sealed class AudioCapture : ISpeechSessionCapture
{
    public const double PrebufferSeconds = 2.5;
    private readonly object gate = new();
    private readonly List<Source> sources = [];
    private List<Source>? preparedSources;
    private List<AudioDeviceRoute>? preparedRoutes;
    private readonly object notificationGate = new();
    private readonly MMDeviceEnumerator notificationEnumerator = new();
    private readonly MMDeviceNotificationClient notificationClient;
    private List<AudioDeviceRoute> routes = [];
    private bool notificationsEnabled;
    private bool notificationDisposed;
    private int failureRaised;
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
    public double BufferedSeconds { get { lock (gate) return sources.Count == 0 ? 0 : sources.Max(s => s.Buffer.BufferedSeconds); } }
    public bool IsFollowingDefault(DataFlow flow) { lock (notificationGate) return routes.Any(r => r.Flow == flow && r.SelectedId is null); }
    public static List<AudioDevice> Devices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        return devices.Select(d => new AudioDevice(d.ID, d.FriendlyName)).ToList();
    }
    public static ISampleProvider ToMono16k(ISampleProvider sample)
    {
        if (sample.WaveFormat.Channels == 2) sample = new StereoToMonoSampleProvider(sample);
        else if (sample.WaveFormat.Channels != 1) sample = new AverageChannels(sample);
        return new WdlResamplingSampleProvider(sample, 16000);
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
    public void PrepareRestart(int mode, string? outputId, string? inputId)
    {
        lock (gate)
        {
            DisposePreparedSourcesLocked();
            var nextSources = new List<Source>();
            try
            {
                List<AudioDeviceRoute> nextRoutes = StartSourcesLocked(mode, outputId, inputId, nextSources, staged: true);
                preparedSources = nextSources;
                preparedRoutes = nextRoutes;
            }
            catch
            {
                DisposeSourceListLocked(nextSources);
                throw;
            }
        }
    }
    public void CommitPreparedRestart()
    {
        lock (gate)
        {
            if (preparedSources is null || preparedRoutes is null)
                throw new InvalidOperationException("没有可提交的音频源切换。");
            Exception? startupFailure = preparedSources.Select(source => Volatile.Read(ref source.StartupFailure)).FirstOrDefault(error => error is not null);
            if (startupFailure is not null) throw new IOException("新音源在切换前意外停止。", startupFailure);

            DisposeSourcesLocked();
            foreach (var source in preparedSources)
            {
                source.Buffer.Clear();
                source.Staged = false;
            }
            sources.AddRange(preparedSources);
            startupFailure = preparedSources.Select(source => Volatile.Read(ref source.StartupFailure)).FirstOrDefault(error => error is not null);
            preparedSources = null;
            lock (notificationGate) { routes = preparedRoutes; notificationsEnabled = true; }
            preparedRoutes = null;
            Interlocked.Exchange(ref failureRaised, 0);
            if (startupFailure is not null) ReportCaptureFailure(startupFailure);
        }
    }
    public void AbortPreparedRestart()
    {
        lock (gate) DisposePreparedSourcesLocked();
    }
    private void StartLocked(int mode, string? outputId, string? inputId)
    {
        DisposePreparedSourcesLocked();
        Interlocked.Exchange(ref failureRaised, 0);
        try
        {
            List<AudioDeviceRoute> nextRoutes = StartSourcesLocked(mode, outputId, inputId, sources, staged: false);
            lock (notificationGate) { routes = nextRoutes; notificationsEnabled = true; }
        }
        catch { DisposeSourcesLocked(); throw; }
    }
    private List<AudioDeviceRoute> StartSourcesLocked(int mode, string? outputId, string? inputId, List<Source> target, bool staged)
    {
        using var enumerator = new MMDeviceEnumerator();
        var nextRoutes = new List<AudioDeviceRoute>();
        try
        {
            if (mode is 0 or 2)
            {
                var device = outputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(outputId);
                nextRoutes.Add(new(DataFlow.Render, outputId, device.ID, device.FriendlyName));
                Add(new WasapiRecorderBuilder().WithDevice(device).WithLoopbackCapture().Build(), target, staged);
            }
            if (mode is 1 or 2)
            {
                var device = inputId is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(inputId);
                nextRoutes.Add(new(DataFlow.Capture, inputId, device.ID, device.FriendlyName));
                Add(new WasapiRecorderBuilder().WithDevice(device).Build(), target, staged);
            }
            foreach (var source in target.Skip(target.Count - nextRoutes.Count)) source.Capture.StartRecording();
            return nextRoutes;
        }
        catch
        {
            if (staged) DisposeSourceListLocked(target);
            throw;
        }
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
        ReportCaptureFailure(new IOException(message + "录音已停止，已收到的字幕会保存；选择可用设备后重新开始。"));
    }

    private void ReportCaptureFailure(Exception error)
    {
        if (Interlocked.Exchange(ref failureRaised, 1) == 0) Failed?.Invoke(error);
    }
    private void Add(WasapiRecorder capture, List<Source> target, bool staged)
    {
        var buffer = new BoundedAudioPrebuffer(capture.WaveFormat, PrebufferSeconds);
        var source = new Source(capture, buffer, ToMono16k(buffer.Samples)) { Staged = staged };
        target.Add(source);
        source.DataHandler = (data, _, _, _) =>
        {
            if (source.Stopping) return;
            try { buffer.AddSamples(data); }
            catch (Exception error)
            {
                if (source.Stopping) return;
                if (source.Staged) Interlocked.CompareExchange(ref source.StartupFailure, error, null);
                else ReportCaptureFailure(error);
            }
        };
        source.StoppedHandler = (_, e) =>
        {
            if (source.Stopping) return;
            Exception error = e.Exception ?? new IOException("音频设备停止采集。");
            if (source.Staged) Interlocked.CompareExchange(ref source.StartupFailure, error, null);
            else ReportCaptureFailure(error);
        };
        capture.DataAvailable += source.DataHandler;
        capture.RecordingStopped += source.StoppedHandler;
    }
    public byte[] ReadFrame(out double level)
    {
        lock (gate)
        {
            var mixed = new float[320];
            int firstValid = 0, secondValid = 0;
            if (sources.Count > 0) firstValid = sources[0].ReadOutputFrame();
            if (sources.Count > 1) secondValid = sources[1].ReadOutputFrame();
            AudioFrameMixer.Mix(
                sources.Count > 0 ? sources[0].OutputFrame : ReadOnlySpan<float>.Empty, firstValid,
                sources.Count > 1 ? sources[1].OutputFrame : ReadOnlySpan<float>.Empty, secondValid,
                mixed);
            level = 0;
            if (sources.Count > 0) level = AudioLevelHistory.MeasureRms(sources[0].OutputFrame, firstValid);
            if (sources.Count > 1) level = Math.Max(level, AudioLevelHistory.MeasureRms(sources[1].OutputFrame, secondValid));
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
            DisposePreparedSourcesLocked();
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
            lock (notificationGate) notificationsEnabled = false;
            foreach (var source in sources) { source.Stopping = true; try { source.Capture.StopRecording(); } catch { } }
        }
    }
    public int TailFrames { get { lock (gate) return sources.Count == 0 ? 0 : Math.Min((int)Math.Ceiling(PrebufferSeconds * 50) + 2, (int)Math.Ceiling(sources.Max(s => s.Buffer.BufferedSeconds) * 50) + 2); } }
    private void DisposeSourcesLocked()
    {
        lock (notificationGate) { notificationsEnabled = false; routes = []; }
        DisposeSourceListLocked(sources);
    }
    private void DisposePreparedSourcesLocked()
    {
        if (preparedSources is not null) DisposeSourceListLocked(preparedSources);
        preparedSources = null;
        preparedRoutes = null;
    }
    private static void DisposeSourceListLocked(List<Source> target)
    {
        foreach (var source in target)
        {
            source.Stopping = true;
            source.Capture.DataAvailable -= source.DataHandler;
            source.Capture.RecordingStopped -= source.StoppedHandler;
            try { source.Capture.StopRecording(); } catch { }
            source.Capture.Dispose();
        }
        target.Clear();
    }
    private sealed class Source(WasapiRecorder capture, BoundedAudioPrebuffer buffer, ISampleProvider resampled)
    {
        public WasapiRecorder Capture { get; } = capture;
        public BoundedAudioPrebuffer Buffer { get; } = buffer;
        public ISampleProvider Resampled { get; } = resampled;
        public ClockDriftController Drift { get; } = new();
        public float[] InputFrame { get; } = new float[320 + ClockDriftController.MaximumFrameAdjustment];
        public float[] OutputFrame { get; } = new float[320];
        public volatile bool Stopping;
        public volatile bool Staged;
        public Exception? StartupFailure;
        public CaptureDataAvailableHandler DataHandler = null!;
        public EventHandler<StoppedEventArgs> StoppedHandler = null!;
        public int ReadOutputFrame()
        {
            Array.Clear(OutputFrame);
            int inputFrames = Drift.InputFramesFor(OutputFrame.Length, Buffer.BufferedSeconds);
            var input = InputFrame.AsSpan(0, inputFrames);
            int read = Resampled.Read(input);
            if (read < inputFrames) input[read..].Clear();
            if (read <= 0) return 0;
            double sourceStride = (double)(inputFrames - 1) / (OutputFrame.Length - 1);
            int validOutputFrames = 0;
            for (int i = 0; i < OutputFrame.Length; i++)
            {
                double position = i * sourceStride;
                if (position >= read) break;
                int left = (int)position;
                int right = Math.Min(left + 1, read - 1);
                float fraction = (float)(position - left);
                OutputFrame[i] = input[left] + (input[right] - input[left]) * fraction;
                validOutputFrames++;
            }
            return validOutputFrames;
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

/// <summary>Mixes only samples that were actually read from each capture source.</summary>
public static class AudioFrameMixer
{
    public static void Mix(ReadOnlySpan<float> first, int firstValidFrames,
        ReadOnlySpan<float> second, int secondValidFrames, Span<float> output)
    {
        firstValidFrames = Math.Clamp(firstValidFrames, 0, first.Length);
        secondValidFrames = Math.Clamp(secondValidFrames, 0, second.Length);
        for (int i = 0; i < output.Length; i++)
        {
            bool hasFirst = i < firstValidFrames;
            bool hasSecond = i < secondValidFrames;
            if (!hasFirst && !hasSecond) { output[i] = 0; continue; }
            float sum = (hasFirst ? first[i] : 0) + (hasSecond ? second[i] : 0);
            output[i] = Math.Clamp(sum / ((hasFirst ? 1 : 0) + (hasSecond ? 1 : 0)), -1f, 1f);
        }
    }
}

public sealed class BoundedAudioPrebuffer
{
    private readonly BufferedWaveProvider buffer;
    public double CapacitySeconds { get; }
    public double BufferedSeconds => buffer.BufferedDuration.TotalSeconds;
    public WaveFormat WaveFormat => buffer.WaveFormat;
    public ISampleProvider Samples => buffer.ToSampleProvider();
    public BoundedAudioPrebuffer(WaveFormat format, double capacitySeconds = AudioCapture.PrebufferSeconds)
    {
        CapacitySeconds = Math.Max(0.02, capacitySeconds);
        buffer = new BufferedWaveProvider(format, TimeSpan.FromSeconds(CapacitySeconds)) { ReadFully = false, DiscardOnBufferOverflow = false };
    }
    public void AddSamples(byte[] data, int offset, int count)
    {
        try { buffer.AddSamples(data, offset, count); }
        catch (InvalidOperationException error) when (error.Message == "Buffer full")
        {
            throw new AudioPrebufferOverflowException(CapacitySeconds, error);
        }
    }
    public void AddSamples(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        var rented = ArrayPool<byte>.Shared.Rent(data.Length);
        try
        {
            data.CopyTo(rented);
            AddSamples(rented, 0, data.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
    public void Clear() => buffer.ClearBuffer();
}

public sealed class AudioPrebufferOverflowException : IOException
{
    public double CapacitySeconds { get; }
    public AudioPrebufferOverflowException(double capacitySeconds, Exception innerException)
        : base($"音频预缓冲达到 {capacitySeconds:0.0} 秒上限；连接延迟过长，录音将停止以避免静默丢失音频。请检查网络后重试。", innerException)
        => CapacitySeconds = capacitySeconds;
}
