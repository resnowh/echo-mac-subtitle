using System.Net.WebSockets;
using System.Text.Json;
using Echo_Windows.Core;
using NAudio.CoreAudioApi;

namespace Echo_Windows.Services;

public sealed class SpeechSession : IAsyncDisposable
{
    private readonly Uri endpoint;
    public SpeechSession() : this(new Uri("wss://stt-rt.soniox.com/transcribe-websocket")) { }
    internal SpeechSession(Uri endpoint) { this.endpoint = endpoint; }
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationTokenSource audioStop = new();
    private readonly AudioCapture capture = new();
    private readonly SemaphoreSlim audioGate = new(1, 1);
    private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task receiver = Task.CompletedTask, sender = Task.CompletedTask;
    private int mode;
    private string? outputId, inputId;
    public event Action<JsonElement>? Message;
    public event Action<double>? Level;
    public event Action<string>? Failure;
    private bool stopping;
    public async Task StartAsync(Preferences config, string key, int mode, string? outputId, string? inputId)
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(endpoint, connectTimeout.Token);
        var request = new Dictionary<string, object> {
            ["api_key"] = key, ["model"] = config.SonioxModel, ["audio_format"] = "pcm_s16le", ["sample_rate"] = 16000, ["num_channels"] = 1,
            ["enable_endpoint_detection"] = true, ["max_endpoint_delay_ms"] = 900, ["enable_language_identification"] = true, ["enable_speaker_diarization"] = config.Speakers
        };
        if (!string.IsNullOrWhiteSpace(config.SourceLanguage)) { request["language_hints"] = new[] { config.SourceLanguage }; request["language_hints_strict"] = config.Strict; }
        if (config.Translate) request["translation"] = new { type = "one_way", target_language = config.TargetLanguage };
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(request).AsMemory(), WebSocketMessageType.Text, true, connectTimeout.Token);
        receiver = ReceiveAsync();
        capture.Failed += error => { if (!stopping) Failure?.Invoke("音频中断：" + error.Message); };
        this.mode = mode; this.outputId = outputId; this.inputId = inputId;
        capture.Start(mode, outputId, inputId);
        sender = SendAudioAsync();
    }
    public async Task SwitchDevicesAsync(string? newOutputId, string? newInputId)
    {
        if (stopping) throw new InvalidOperationException("录音正在停止，暂时不能切换设备。");
        if (newOutputId == outputId && newInputId == inputId) return;
        string? oldOutputId = outputId, oldInputId = inputId;
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
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
            int frames = 0;
            while (await timer.WaitForNextTickAsync(audioStop.Token))
            {
                await audioGate.WaitAsync(audioStop.Token);
                try
                {
                    var bytes = capture.ReadFrame(out var level);
                    if (++frames % 5 == 0) Level?.Invoke(level);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(audioStop.Token, lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
                }
                finally { audioGate.Release(); }
            }
        }
        catch (OperationCanceledException) when (audioStop.IsCancellationRequested || lifetime.IsCancellationRequested) { }
        catch (Exception e) { if (!stopping) Failure?.Invoke("发送中断：" + e.Message); }
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
                if (root.TryGetProperty("error_code", out var error)) throw new IOException($"服务错误 {error}：{(root.TryGetProperty("error_message", out var reason) ? reason.ToString() : "请求失败")}");
                Message?.Invoke(root.Clone());
                if (root.TryGetProperty("finished", out var done) && done.GetBoolean()) { finished.TrySetResult(); return; }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { finished.TrySetException(e); if (!stopping) Failure?.Invoke(e.Message); }
    }
    public async Task StopAsync()
    {
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
        stopping = true; audioStop.Cancel(); lifetime.Cancel(); socket.Abort();
        try { await Task.WhenAll(sender, receiver); } catch { }
        capture.Dispose(); socket.Dispose(); audioGate.Dispose(); audioStop.Dispose(); lifetime.Dispose();
    }
}

public sealed class AudioDeviceSwitchException(string message, bool captureRestored, Exception innerException) : IOException(message, innerException)
{
    public bool CaptureRestored { get; } = captureRestored;
}
