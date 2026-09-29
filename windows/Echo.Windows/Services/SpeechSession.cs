using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Json;
using Echo_Windows.Core;
using NAudio.CoreAudioApi;

namespace Echo_Windows.Services;

public sealed class SpeechSession : IAsyncDisposable
{
    private readonly Uri endpoint;
    private readonly bool captureEnabled;
    public SpeechSession() : this(new Uri("wss://stt-rt.soniox.com/transcribe-websocket"), captureEnabled: true) { }
    internal SpeechSession(Uri endpoint, bool captureEnabled = false) { this.endpoint = endpoint; this.captureEnabled = captureEnabled; }
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationTokenSource audioStop = new();
    private readonly AudioCapture capture = new();
    private readonly TaskCompletionSource<Exception> captureFailureSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim audioGate = new(1, 1);
    private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task receiver = Task.CompletedTask, sender = Task.CompletedTask;
    private int mode;
    private int defaultFlowsPending, defaultSwitchRunning;
    private string? outputId, inputId;
    private Exception? captureFailure;
    public event Action<JsonElement>? Message;
    public event Action<double>? Level;
    public event Action<Exception>? Failure;
    public event Action<string>? Status;
    private bool stopping;
    private bool audioTransportReady;
    public double BufferedAudioSeconds => capture.BufferedSeconds;
    public async Task StartAsync(Preferences config, string key, int mode, string? outputId, string? inputId, CancellationToken cancellationToken = default)
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(20));
        this.mode = mode; this.outputId = outputId; this.inputId = inputId;
        capture.DefaultDeviceChanged += QueueDefaultDeviceChange;
        capture.Failed += error =>
        {
            var reported = new AudioCaptureFailureException(error is AudioPrebufferOverflowException ? error.Message : "音频中断：" + error.Message, error);
            if (Interlocked.CompareExchange(ref captureFailure, reported, null) is null) captureFailureSignal.TrySetResult(reported);
            if (!stopping) Failure?.Invoke(reported);
        };
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (captureEnabled) capture.Start(mode, outputId, inputId);
            Task connection = socket.ConnectAsync(endpoint, connectTimeout.Token);
            Task completed = await Task.WhenAny(connection, captureFailureSignal.Task);
            if (completed == captureFailureSignal.Task)
            {
                connectTimeout.Cancel();
                try { await connection; } catch { }
                throw await captureFailureSignal.Task;
            }
            try { await connection; }
            catch (WebSocketException e)
            {
                int? status = ReadHandshakeStatus(e);
                if (status is null) throw;
                throw new SpeechServiceException($"转写服务拒绝连接（HTTP {status}）。", e, status is 408 or >= 500 and <= 599);
            }
            catch (OperationCanceledException e) when (!lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (Volatile.Read(ref captureFailure) is { } error) throw error;
                throw new TimeoutException("连接转写服务超时。", e);
            }
            var request = new Dictionary<string, object> {
                ["api_key"] = key, ["model"] = config.SonioxModel, ["audio_format"] = "pcm_s16le", ["sample_rate"] = 16000, ["num_channels"] = 1,
                ["enable_endpoint_detection"] = true, ["max_endpoint_delay_ms"] = 900, ["enable_language_identification"] = true, ["enable_speaker_diarization"] = config.Speakers
            };
            if (!string.IsNullOrWhiteSpace(config.SourceLanguage)) { request["language_hints"] = new[] { config.SourceLanguage }; request["language_hints_strict"] = config.Strict; }
            if (config.Translate) request["translation"] = new { type = "one_way", target_language = config.TargetLanguage };
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(request).AsMemory(), WebSocketMessageType.Text, true, connectTimeout.Token);
            receiver = ReceiveAsync();
            if (Volatile.Read(ref captureFailure) is { } captureError) throw captureError;
            sender = SendAudioAsync();
            Volatile.Write(ref audioTransportReady, true);
            if (Volatile.Read(ref defaultFlowsPending) != 0 && Interlocked.CompareExchange(ref defaultSwitchRunning, 1, 0) == 0)
                _ = Task.Run(FollowDefaultDeviceAsync);
        }
        catch
        {
            Volatile.Write(ref audioTransportReady, false);
            capture.StopInputs(); socket.Abort();
            throw;
        }
    }
    private int? ReadHandshakeStatus(WebSocketException error)
    {
        int responseStatus = (int)socket.HttpStatusCode;
        if (responseStatus is >= 400 and <= 599) return responseStatus;
        var match = Regex.Match(error.Message, @"status code\s+'(?<status>[1-5]\d{2})'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["status"].Value, out int messageStatus) ? messageStatus : null;
    }
    public async Task SwitchDevicesAsync(string? newOutputId, string? newInputId, bool forceRestart = false)
    {
        if (stopping) throw new InvalidOperationException("录音正在停止，暂时不能切换设备。");
        if (!forceRestart && newOutputId == outputId && newInputId == inputId) return;
        string? oldOutputId = capture.ActiveOutputId ?? outputId, oldInputId = capture.ActiveInputId ?? inputId;
        await audioGate.WaitAsync(audioStop.Token);
        try
        {
            capture.StopInputs();
            try { await SendCaptureTailAsync(); }
            catch (Exception e) { throw new AudioDeviceSwitchException("切换期间音频传输失败；正在保存已收到的文字并停止录音。", false, e); }
            try
            {
                await Task.Run(() => capture.Restart(mode, newOutputId, newInputId));
                outputId = newOutputId; inputId = newInputId;
            }
            catch (Exception switchError)
            {
                try { await Task.Run(() => capture.Start(mode, oldOutputId, oldInputId)); }
                catch (Exception restoreError)
                {
                    throw new AudioDeviceSwitchException("新设备启动失败，原设备也无法恢复；正在保存已收到的文字并停止录音。", false,
                        new AggregateException(switchError, restoreError));
                }
                throw new AudioDeviceSwitchException("新设备启动失败，已恢复原设备；录音继续。", true, switchError);
            }
        }
        finally { audioGate.Release(); }
    }
    private void QueueDefaultDeviceChange(DataFlow flow)
    {
        Interlocked.Or(ref defaultFlowsPending, flow == DataFlow.Render ? 1 : 2);
        if (!Volatile.Read(ref audioTransportReady)) return;
        if (Interlocked.CompareExchange(ref defaultSwitchRunning, 1, 0) == 0) _ = Task.Run(FollowDefaultDeviceAsync);
    }
    private async Task FollowDefaultDeviceAsync()
    {
        try
        {
            while (true)
            {
                int pending = Interlocked.Exchange(ref defaultFlowsPending, 0);
                if (pending == 0 || stopping) return;
                bool shouldRefresh = (pending & 1) != 0 && capture.IsFollowingDefault(DataFlow.Render)
                    || (pending & 2) != 0 && capture.IsFollowingDefault(DataFlow.Capture);
                if (!shouldRefresh) continue;
                Status?.Invoke("系统默认音频设备已变化，正在切换采集设备…");
                try
                {
                    await SwitchDevicesAsync(outputId, inputId, forceRestart: true);
                    Status?.Invoke("已跟随系统默认音频设备切换；转写会话保持连接。");
                }
                catch (AudioDeviceSwitchException e) when (e.CaptureRestored)
                {
                    Status?.Invoke(e.Message + "当前仍使用原设备；可刷新设备列表后手动切换。");
                }
                catch (Exception e)
                {
                    if (!stopping) Failure?.Invoke(new AudioCaptureFailureException("跟随系统默认设备失败：" + e.Message, e));
                    return;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref defaultSwitchRunning, 0);
            if (Volatile.Read(ref defaultFlowsPending) != 0 && !stopping && Volatile.Read(ref audioTransportReady)
                && Interlocked.CompareExchange(ref defaultSwitchRunning, 1, 0) == 0)
                _ = Task.Run(FollowDefaultDeviceAsync);
        }
    }
    private async Task SendCaptureTailAsync()
    {
        if (socket.State != WebSocketState.Open) throw new IOException("转写服务连接已断开。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(audioStop.Token, lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        int tail = capture.TailFrames;
        for (int i = 0; i < tail; i++)
            await socket.SendAsync(capture.ReadFrame(out _).AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
    }
    private async Task SendAudioAsync()
    {
        try
        {
            // Allow a small capture lead. Only bounded memory buffers are used; no audio file is written.
            await Task.Delay(100, audioStop.Token);
            int frames = 0;
            // Catch up the bounded pre-connect audio burst before switching to real-time pacing.
            while (capture.BufferedSeconds > 0.08)
                await SendCaptureFrameAsync(++frames);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
            while (await timer.WaitForNextTickAsync(audioStop.Token))
                await SendCaptureFrameAsync(++frames);
        }
        catch (OperationCanceledException) when (audioStop.IsCancellationRequested || lifetime.IsCancellationRequested) { }
        catch (Exception e) { if (!stopping) Failure?.Invoke(e); }
    }
    private async Task SendCaptureFrameAsync(int frameNumber)
    {
        await audioGate.WaitAsync(audioStop.Token);
        try
        {
            var bytes = capture.ReadFrame(out var level);
            if (frameNumber % 5 == 0) Level?.Invoke(level);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(audioStop.Token, lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
        }
        finally { audioGate.Release(); }
    }
    private async Task ReceiveAsync()
    {
        try
        {
            var bytes = new byte[16384];
            while (!lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), lifetime.Token);
                    if (part.MessageType == WebSocketMessageType.Close) { if (!finished.Task.IsCompleted) throw new IOException("服务连接已关闭，最后结果可能不完整。"); return; }
                    message.Write(bytes, 0, part.Count);
                    if (message.Length > 4 * 1024 * 1024) throw new IOException("服务响应超过限制。");
                } while (!part.EndOfMessage);
                using var json = JsonDocument.Parse(message.ToArray());
                var root = json.RootElement;
                if (root.TryGetProperty("error_code", out var error)) throw new SpeechServiceException($"服务错误 {error}：{(root.TryGetProperty("error_message", out var reason) ? reason.ToString() : "请求失败")}");
                Message?.Invoke(root.Clone());
                if (root.TryGetProperty("finished", out var done) && done.GetBoolean()) { finished.TrySetResult(); return; }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { finished.TrySetException(e); if (!stopping) Failure?.Invoke(e); }
    }
    public async Task StopAsync()
    {
        Volatile.Write(ref audioTransportReady, false);
        stopping = true; audioStop.Cancel(); await sender;
        capture.StopInputs();
        // The capture lead is drained before the end marker, preserving the final spoken frame.
        if (socket.State == WebSocketState.Open)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            int tail = capture.TailFrames;
            for (int i = 0; i < tail; i++) await socket.SendAsync(capture.ReadFrame(out _).AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
            capture.Dispose();
            await socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Binary, true, timeout.Token);
            await finished.Task.WaitAsync(timeout.Token);
        }
        else capture.Dispose();
    }
    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref audioTransportReady, false);
        stopping = true; audioStop.Cancel(); lifetime.Cancel(); socket.Abort();
        try { await Task.WhenAll(sender, receiver); } catch { }
        capture.Dispose(); socket.Dispose(); audioGate.Dispose(); audioStop.Dispose(); lifetime.Dispose();
    }
}

public sealed class AudioDeviceSwitchException(string message, bool captureRestored, Exception innerException) : IOException(message, innerException)
{
    public bool CaptureRestored { get; } = captureRestored;
}

public sealed class SpeechServiceException : IOException
{
    public SpeechServiceException(string message, Exception? innerException = null, bool retryable = false) : base(message, innerException) => Retryable = retryable;
    public bool Retryable { get; }
}
public sealed class AudioCaptureFailureException(string message, Exception innerException) : IOException(message, innerException) { }

public static class SpeechRetryPolicy
{
    public const int MaxRetries = 2;
    public static TimeSpan Delay(int retryNumber) => retryNumber switch
    {
        1 => TimeSpan.FromSeconds(1),
        2 => TimeSpan.FromSeconds(3),
        _ => throw new ArgumentOutOfRangeException(nameof(retryNumber))
    };
    public static bool IsTransient(Exception error) => error is WebSocketException or SocketException or TimeoutException or OperationCanceledException
        || error is SpeechServiceException { Retryable: true }
        || error is IOException and not SpeechServiceException and not AudioCaptureFailureException and not AudioDeviceSwitchException;
}
