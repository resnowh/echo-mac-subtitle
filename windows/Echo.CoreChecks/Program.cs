using System.Text.Json;
using Echo_Windows.Core;
using Echo_Windows.Services;
using NAudio.CoreAudioApi;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var segment = new Segment { StartedAt = 800000000 };
var assembly = new TokenAssembler(segment, _ => { });
void Apply(string text) { using var doc = JsonDocument.Parse(text); assembly.Apply(doc.RootElement); }
Apply("""{"tokens":[{"text":"Hel","is_final":false,"start_ms":0,"end_ms":200}]}""");
Apply("""{"tokens":[{"text":"Hello","is_final":true,"start_ms":0,"end_ms":400},{"text":" world","is_final":false,"start_ms":400,"end_ms":700}]}""");
Check(segment.Entries[0].English == "Hello world", "provisional replacement without duplicated prefix");
Apply("""{"tokens":[{"text":" there.","is_final":true,"start_ms":400,"end_ms":800},{"text":"<end>","is_final":true,"translation_status":"original"},{"text":"Next.","is_final":true,"start_ms":1500,"end_ms":2000}]}""");
Apply("""{"tokens":[{"text":"你好。","is_final":true,"translation_status":"translation"},{"text":"<end>","is_final":true,"translation_status":"translation"}]}""");
Check(segment.Entries[0].English == "Hello there." && segment.Entries[0].Chinese == "你好。" && segment.Entries[1].Chinese == "", "delayed translation remains with previous source endpoint");
var editedSegment = new Segment(); var editedAssembler = new TokenAssembler(editedSegment, _ => { });
void ApplyEdited(string text) { using var doc = JsonDocument.Parse(text); editedAssembler.Apply(doc.RootElement); }
ApplyEdited("""{"tokens":[{"text":"Recognized","is_final":false}]}""");
var edited = editedSegment.Entries[0]; edited.Edit("人工纠正", "");
ApplyEdited("""{"tokens":[{"text":"Recognized text","is_final":false}]}""");
ApplyEdited("""{"tokens":[{"text":"识别译文","is_final":false,"translation_status":"translation"}]}""");
Check(edited.English == "人工纠正" && edited.Chinese == "识别译文" && edited.Correction?.RawSource == "Recognized text", "manual source lock preserves correction while live recognition updates raw text and unlocked translation");
var correctionSnapshot = JsonSerializer.Serialize(new Archive { Segments = [editedSegment] }, TranscriptFiles.Json);
Check(edited.UndoCorrection() && edited.English == "Recognized" && edited.Correction?.SourceLocked == true, "undo restores prior subtitle and locks the restored human choice");
ApplyEdited("""{"tokens":[{"text":"Late recognition","is_final":false}]}""");
Check(edited.English == "Recognized" && edited.Correction?.RawSource == "Late recognition", "late recognition cannot overwrite text after undo");
var bilingual = new Segment(); var bilingualAssembler = new TokenAssembler(bilingual, _ => { });
using (var turn = JsonDocument.Parse("""{"tokens":[{"text":"First","is_final":true},{"text":"一","is_final":true,"translation_status":"translation"},{"text":"<end>","is_final":true},{"text":"Second","is_final":true},{"text":"二","is_final":true,"translation_status":"translation"}]}""")) bilingualAssembler.Apply(turn.RootElement);
Check(bilingual.Entries.Count == 2 && bilingual.Entries[1].Chinese == "二", "untagged endpoint advances both transcript and translation");
int finalizedCount = 0; var finalizedSegment = new Segment(); var finalizedAssembler = new TokenAssembler(finalizedSegment, _ => { }, _ => finalizedCount++);
using (var finalTurn = JsonDocument.Parse("""{"tokens":[{"text":"finished sentence","is_final":true},{"text":"<end>","is_final":true}]}""")) finalizedAssembler.Apply(finalTurn.RootElement);
Check(finalizedCount == 1, "transcript final boundary triggers a single opt-in correction job");
int speakerFinalized = 0; var speakerSegment = new Segment(); var speakerAssembler = new TokenAssembler(speakerSegment, _ => { }, _ => speakerFinalized++);
using (var speakerTurn = JsonDocument.Parse("""{"tokens":[{"text":"first speaker","is_final":true,"speaker":1,"start_ms":0,"end_ms":500},{"text":"second speaker","is_final":true,"speaker":2,"start_ms":600,"end_ms":1000},{"text":"<end>","is_final":true}]}""")) speakerAssembler.Apply(speakerTurn.RootElement);
Check(speakerSegment.Entries.Count == 2 && speakerSegment.Entries[0].English == "first speaker" && speakerSegment.Entries[0].Speaker == "Speaker 1" && speakerSegment.Entries[1].English == "second speaker" && speakerSegment.Entries[1].Speaker == "Speaker 2" && speakerFinalized == 2, "final speaker change splits rows and finalizes each speaker turn once");
var archive = new Archive { CreatedAt = 800000000, Summary = "已保存总结", SummarizedEntries = { [segment.Entries[0].Id] = TranscriptFiles.SummarySignature(segment.Entries[0].English) }, Segments = [segment, new Segment { StartedAt = 800000010, Entries = [new Subtitle { Start = 0, End = 1, English = "Again" }] }] };
var snapshot = TranscriptFiles.Snapshot(archive);
archive.Segments[0].Entries[0].English = "后续编辑";
archive.Segments[0].Entries[0].Correction = new SubtitleCorrection { RawSource = "后来补充" };
Check(snapshot.Segments[0].Entries[0].English == "Hello there." && snapshot.Segments[0].Entries[0].Correction is null && snapshot.SummarizedEntries.Count == 1, "background persistence snapshot is isolated from later edits and keeps archive metadata");
string srt = TranscriptFiles.Srt(archive);
Check(srt.Contains("00:00:10,000 --> 00:00:11,000") && srt.Contains("你好。"), "SRT preserves segment wall-clock gap and translation");
string json = JsonSerializer.Serialize(archive, TranscriptFiles.Json);
var loaded = TranscriptFiles.Parse(json);
Check(loaded.Id == archive.Id && loaded.Segments[0].StartedAt == 800000000 && loaded.Summary == "已保存总结" && loaded.SummarizedEntries.GetValueOrDefault(segment.Entries[0].Id) == TranscriptFiles.SummarySignature("Hello there.") && json.Contains("\"english\""), "archive summary, incremental signatures, legacy JSON fields and Apple reference date round trip");
var splitSource = new Archive { Title = "课程", Segments = [new Segment(), new Segment { StartedAt = 800000123, Entries = [new Subtitle { English = "拆分字幕" }] }] };
splitSource.SummarizedEntries[splitSource.Segments[1].Entries[0].Id] = TranscriptFiles.SummarySignature("拆分字幕");
var splitResult = ArchiveOperations.SplitSegment(splitSource, splitSource.Segments[1].Id)!;
Check(splitSource.Segments.Count == 1 && splitResult.ExtractedArchive.Title == "课程 - 本段" && splitResult.ExtractedArchive.Segments[0].Entries[0].English == "拆分字幕" && splitSource.SummarizedEntries.Count == 0, "splitting moves a completed segment and preserves its text in a separate archive");
ArchiveOperations.RestoreSplit(splitSource, splitResult);
Check(splitSource.Segments.Count == 2 && splitSource.Segments[1].Entries[0].English == "拆分字幕" && splitSource.SummarizedEntries.Count == 1, "split rollback restores the original segment order and incremental summary state");
string trashTestRoot = Path.Combine(Path.GetTempPath(), "Echo-CoreChecks-" + Guid.NewGuid().ToString("N"));
string trashArchives = Path.Combine(trashTestRoot, "Archives"), trashDeleted = Path.Combine(trashTestRoot, "Deleted");
Directory.CreateDirectory(trashArchives);
var trashArchive = new Archive(); string trashSource = Path.Combine(trashArchives, $"{trashArchive.Id}.json");
File.WriteAllText(trashSource, "archive contents"); File.WriteAllText(trashSource + ".bak", "previous archive");
try
{
    string movedArchive = TranscriptFiles.MoveToDeleted(trashArchive, trashArchives, trashDeleted);
    Check(!File.Exists(trashSource) && File.ReadAllText(movedArchive) == "archive contents" && File.ReadAllText(movedArchive + ".bak") == "previous archive", "moving an archive to recycle area preserves both JSON and backup contents");
}
finally { if (Directory.Exists(trashTestRoot)) Directory.Delete(trashTestRoot, recursive: true); }
var correctionLoaded = TranscriptFiles.Parse(correctionSnapshot).Segments[0].Entries[0].Correction;
Check(correctionLoaded?.RawSource == "Recognized text" && correctionLoaded.SourceLocked && correctionLoaded.History.Count == 1 && correctionSnapshot.Contains("\"rawSource\""), "correction source, lock and undo history survive Mac-compatible archive round trip");
var chunkInput = new Subtitle { English = string.Concat(Enumerable.Repeat("汉", 300)) };
var chunks = TranscriptTextChunks.Create([chunkInput], 128);
Check(chunks.Count > 1 && chunks.All(c => c.Length <= 128) && chunks.Sum(c => c.Count(ch => ch == '汉')) == 300, "long transcript chunks stay bounded without dropping Unicode text");
Check(Archive.AppleEpoch.AddSeconds(0).Year == 2001, "Apple date reference is not Unix time");
Check(SpeechRetryPolicy.MaxRetries == 2 && SpeechRetryPolicy.Delay(1) == TimeSpan.FromSeconds(1) && SpeechRetryPolicy.Delay(2) == TimeSpan.FromSeconds(3)
    && SpeechRetryPolicy.IsTransient(new System.Net.WebSockets.WebSocketException())
    && SpeechRetryPolicy.IsTransient(new SpeechServiceException("temporary 503", retryable: true))
    && !SpeechRetryPolicy.IsTransient(new SpeechServiceException("401 or quota"))
    && !SpeechRetryPolicy.IsTransient(new AudioCaptureFailureException("device failed", new IOException())), "network retry policy is bounded and excludes service/device errors");
string secret = "synthetic-local-test-not-an-api-key";
Check(Preferences.Unprotect(Preferences.Protect(secret)) == secret, "current-user DPAPI credential round trip");
bool rejected = false; try { TranscriptFiles.Parse("{}"); } catch { rejected = true; }
Check(rejected, "malformed archive is rejected rather than silently imported");
rejected = false;
try { TranscriptFiles.Parse(json.Replace("800000000", "1e100")); } catch { rejected = true; }
Check(rejected, "out-of-range archive dates are rejected before UI formatting");
var duplicate = new Archive { Segments = [new Segment { Entries = [segment.Entries[0], segment.Entries[0]] }] };
rejected = false; try { TranscriptFiles.Parse(JsonSerializer.Serialize(duplicate, TranscriptFiles.Json)); } catch { rejected = true; }
Check(rejected, "duplicate subtitle IDs are rejected before summary processing");
async Task<bool> CheckRejectedHandshakeAsync(int statusCode)
{
    var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
    int statusPort = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    using var rejectedListener = new HttpListener(); rejectedListener.Prefixes.Add($"http://127.0.0.1:{statusPort}/"); rejectedListener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var reject = Task.Run(async () =>
    {
        var context = await rejectedListener.GetContextAsync().WaitAsync(timeout.Token);
        context.Response.StatusCode = statusCode; context.Response.Close();
    }, timeout.Token);
    await using var rejectedSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"));
    try { await rejectedSession.StartAsync(new Preferences(), "synthetic", 0, null, null); }
    catch (SpeechServiceException error)
    {
        await reject;
        return error.Retryable == (statusCode >= 500);
    }
    catch { await reject; return false; }
    return false;
}
Check(await CheckRejectedHandshakeAsync(401), "HTTP 401 handshake failure is not retried");
Check(await CheckRejectedHandshakeAsync(503), "HTTP 503 handshake failure uses the bounded retry policy");
async Task<bool> CheckConnectCancellationAsync()
{
    var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
    int statusPort = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    using var slowListener = new HttpListener(); slowListener.Prefixes.Add($"http://127.0.0.1:{statusPort}/"); slowListener.Start();
    using var serverTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var rejectLater = Task.Run(async () =>
    {
        var context = await slowListener.GetContextAsync().WaitAsync(serverTimeout.Token);
        await Task.Delay(300, serverTimeout.Token);
        context.Response.StatusCode = 503; context.Response.Close();
    }, serverTimeout.Token);
    using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
    await using var canceledSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"));
    try { await canceledSession.StartAsync(new Preferences(), "synthetic", 0, null, null, stop.Token); }
    catch (OperationCanceledException) { await rejectLater; return true; }
    catch { await rejectLater; return false; }
    await rejectLater; return false;
}
Check(await CheckConnectCancellationAsync(), "user cancellation during reconnect is not mistaken for a connection timeout");
if (args.Contains("--audio"))
{
    Console.WriteLine($"Devices: render={AudioCapture.Devices(DataFlow.Render).Count}, capture={AudioCapture.Devices(DataFlow.Capture).Count}");
    foreach (int mode in new[] { 0, 1, 2 })
    {
        using var audio = new AudioCapture(); string? failure = null; audio.Failed += e => failure = e.Message;
        audio.Start(mode, null, null); await Task.Delay(120);
        int bytes = 0;
        for (int i = 0; i < 50; i++) { bytes += audio.ReadFrame(out _).Length; await Task.Delay(20); }
        Check(failure is null && bytes == 32000, $"audio mode {mode}: one second PCM frame contract, no capture failure");
        string? outputId = mode is 0 or 2 ? AudioCapture.Devices(DataFlow.Render).First().Id : null;
        string? inputId = mode is 1 or 2 ? AudioCapture.Devices(DataFlow.Capture).First().Id : null;
        audio.Restart(mode, outputId, inputId); await Task.Delay(120); int switchedBytes = 0;
        for (int i = 0; i < 5; i++) { switchedBytes += audio.ReadFrame(out _).Length; await Task.Delay(20); }
        Check(failure is null && switchedBytes == 3200, $"audio mode {mode}: hot device reinitialization keeps PCM capture active");
    }
    var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start();
    int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
    using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    int receivedAudio = 0; bool validConfig = false, receivedFinal = false;
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
        using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
        validConfig = configResult.MessageType == WebSocketMessageType.Text && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000 && config.RootElement.GetProperty("api_key").GetString() == "synthetic";
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
            if (result.Count == 0) break;
            receivedAudio += result.Count;
        }
        byte[] final = Encoding.UTF8.GetBytes("""{"tokens":[{"text":"Final tail.","is_final":true,"start_ms":0,"end_ms":500}],"finished":true}""");
        await socket.SendAsync(final.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }, timeout.Token);
    await using (var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/")))
    {
        session.Message += m => receivedFinal = m.TryGetProperty("finished", out var f) && f.GetBoolean();
        await session.StartAsync(new Preferences(), "synthetic", 0, null, null);
        bool restoredInput = false;
        try { await session.SwitchDevicesAsync("missing-device-id", null); }
        catch (AudioDeviceSwitchException e) { restoredInput = e.CaptureRestored; }
        Check(restoredInput, "invalid live device switch restores capture and keeps the recognition session open");
        await Task.Delay(450); await session.StopAsync();
    }
    await server; listener.Stop();
    Check(validConfig && receivedAudio > 0 && receivedFinal, "local WebSocket: config, binary PCM, end marker, final result before stop completes");
}
Console.WriteLine($"Completed {passed} checks. No cloud calls; no audio was saved.");
