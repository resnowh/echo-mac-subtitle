using System.Text.Json;
using Echo_Windows.Core;
using Echo_Windows.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
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
using (var speakerTurn = JsonDocument.Parse("""{"tokens":[{"text":"first speaker","is_final":true,"speaker":1,"language":"en","start_ms":0,"end_ms":500},{"text":"second speaker","is_final":true,"speaker":2,"language":"ja","start_ms":600,"end_ms":1000},{"text":"<end>","is_final":true}]}""")) speakerAssembler.Apply(speakerTurn.RootElement);
Check(speakerSegment.Entries.Count == 2 && speakerSegment.Entries[0].English == "first speaker" && speakerSegment.Entries[0].Speaker == "Speaker 1" && speakerSegment.Entries[0].Language == "en" && speakerSegment.Entries[1].English == "second speaker" && speakerSegment.Entries[1].Speaker == "Speaker 2" && speakerSegment.Entries[1].Language == "ja" && speakerFinalized == 2, "final speaker change splits rows, retains detected language and finalizes each anonymous speaker turn once");
var archive = new Archive { CreatedAt = 800000000, Summary = "已保存总结", SummarizedEntries = { [segment.Entries[0].Id] = TranscriptFiles.SummarySignature(segment.Entries[0].English) }, Segments = [segment, new Segment { StartedAt = 800000010, Entries = [new Subtitle { Start = 0, End = 1, English = "Again" }] }] };
var snapshot = TranscriptFiles.Snapshot(archive);
archive.Segments[0].Entries[0].English = "后续编辑";
archive.Segments[0].Entries[0].Correction = new SubtitleCorrection { RawSource = "后来补充" };
Check(snapshot.Segments[0].Entries[0].English == "Hello there." && snapshot.Segments[0].Entries[0].Correction is null && snapshot.SummarizedEntries.Count == 1, "background persistence snapshot is isolated from later edits and keeps archive metadata");
string srt = TranscriptFiles.Srt(archive);
Check(srt.Contains("00:00:10,000 --> 00:00:11,000") && srt.Contains("你好。"), "SRT preserves segment wall-clock gap and translation");
var overlapExport = new Archive { Segments =
[
    new Segment { StartedAt = 800000002, Entries = [new Subtitle { Start = 0, End = 3, English = "later segment" }] },
    new Segment { StartedAt = 800000000, Entries = [new Subtitle { Start = 0, End = 5, English = "earlier segment" }] }
] };
string overlapSrt = TranscriptFiles.Srt(overlapExport);
Check(overlapSrt.IndexOf("earlier segment", StringComparison.Ordinal) < overlapSrt.IndexOf("later segment", StringComparison.Ordinal)
    && overlapSrt.Contains("00:00:00,000 --> 00:00:05,000") && overlapSrt.Contains("00:00:05,000 --> 00:00:08,000"),
    "archive SRT export sorts unordered segments by wall clock and clamps overlaps so cue times never move backward");
var metadataArchive = new Archive { Segments = [new Segment { StartedAt = 800000000, Entries = [new Subtitle { Start = 0, End = 1, English = "bonjour", Chinese = "你好", Speaker = "Speaker 2", Language = "fr" }] }] };
string metadataJson = JsonSerializer.Serialize(metadataArchive, TranscriptFiles.Json);
var metadataLoaded = TranscriptFiles.Parse(metadataJson);
string metadataSrt = TranscriptFiles.Srt(metadataLoaded);
Check(metadataLoaded.Segments[0].Entries[0].Speaker == "Speaker 2" && metadataLoaded.Segments[0].Entries[0].Language == "fr"
    && metadataSrt.Contains("[Speaker 2] [fr] bonjour") && metadataArchive.Segments[0].Entries[0].HasLanguage,
    "anonymous speaker and detected language survive archive round trip, remain available to the subtitle view, and export in SRT");
string json = JsonSerializer.Serialize(archive, TranscriptFiles.Json);
var loaded = TranscriptFiles.Parse(json);
Check(loaded.Id == archive.Id && loaded.Segments[0].StartedAt == 800000000 && loaded.Summary == "已保存总结" && loaded.SummarizedEntries.GetValueOrDefault(segment.Entries[0].Id) == TranscriptFiles.SummarySignature("Hello there.") && json.Contains("\"english\""), "archive summary, incremental signatures, legacy JSON fields and Apple reference date round trip");
var summaryUnchanged = new Subtitle { English = "unchanged" };
var summaryEdited = new Subtitle { English = "edited" };
var summaryNew = new Subtitle { English = "new" };
var summaryLast = new Subtitle { English = "last segment" };
var summaryArchive = new Archive { Segments = [
    new Segment { Entries = [summaryUnchanged, summaryEdited, summaryNew] },
    new Segment { Entries = [summaryLast] }
] };
summaryArchive.SummarizedEntries[summaryUnchanged.Id] = TranscriptFiles.SummarySignature(summaryUnchanged.English);
summaryArchive.SummarizedEntries[summaryEdited.Id] = TranscriptFiles.SummarySignature("before edit");
summaryArchive.SummarizedEntries[summaryLast.Id] = TranscriptFiles.SummarySignature(summaryLast.English);
var incrementalSummary = TranscriptSummarySelection.Select(summaryArchive, 0);
var currentSegmentSummary = TranscriptSummarySelection.Select(summaryArchive, 1);
var completeSummary = TranscriptSummarySelection.Select(summaryArchive, 2);
Check(incrementalSummary.Select(e => e.English).SequenceEqual(["edited", "new"])
    && currentSegmentSummary.Select(e => e.English).SequenceEqual(["last segment"])
    && completeSummary.Count == 4,
    "summary selection excludes unchanged entries incrementally while current-segment and whole-archive scopes stay distinct");
var legacyId = Guid.NewGuid(); var legacySegmentId = Guid.NewGuid(); var legacyEntryId = Guid.NewGuid();
double legacyCreated = (DateTimeOffset.Parse("2024-01-01T00:00:00Z") - Archive.AppleEpoch).TotalSeconds;
double legacySegmentStart = (DateTimeOffset.Parse("2024-01-01T23:59:59Z") - Archive.AppleEpoch).TotalSeconds;
string legacyJson = JsonSerializer.Serialize(new
{
    id = legacyId, title = "旧格式跨日存档", createdAt = legacyCreated, updatedAt = legacyCreated,
    segments = new[] { new { id = legacySegmentId, startedAt = legacySegmentStart, updatedAt = legacySegmentStart,
        entries = new[] { new { id = legacyEntryId, start = 2d, end = 3d, english = "跨日旧档", chinese = "跨日字幕" } } } }
}, TranscriptFiles.Json);
var legacyArchive = TranscriptFiles.Parse(legacyJson); var legacySubtitle = legacyArchive.Segments[0].Entries[0];
string legacySrt = TranscriptFiles.Srt(legacyArchive);
Check(legacySubtitle.Language is null && legacySubtitle.Speaker is null && legacyArchive.Segments[0].StartedAt == legacySegmentStart
    && legacyArchive.Segments[0].StartedAt + legacySubtitle.Start == (DateTimeOffset.Parse("2024-01-02T00:00:01Z") - Archive.AppleEpoch).TotalSeconds
    && legacySrt.Contains("00:00:00,000 --> 00:00:01,000") && !legacySrt.Contains("[en]"),
    "legacy archive without language or speaker loads without inferred metadata and retains its UTC cross-midnight timestamp through SRT export");
string atomicTestRoot = Path.Combine(Path.GetTempPath(), "Echo-AtomicWrite-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(atomicTestRoot);
try
{
    string atomicPath = Path.Combine(atomicTestRoot, "archive.json");
    TranscriptFiles.AtomicWrite(atomicPath, "旧存档");
    TranscriptFiles.AtomicWrite(atomicPath, "新存档");
    bool replacedSafely = File.ReadAllText(atomicPath) == "新存档" && File.ReadAllText(atomicPath + ".bak") == "旧存档"
        && !Directory.EnumerateFiles(atomicTestRoot, "*.tmp", SearchOption.TopDirectoryOnly).Any();
    string blockedPath = Path.Combine(atomicTestRoot, "blocked.json"); Directory.CreateDirectory(blockedPath);
    bool failedSafely = false;
    try { TranscriptFiles.AtomicWrite(blockedPath, "不应覆盖目录"); }
    catch { failedSafely = Directory.Exists(blockedPath) && !Directory.EnumerateFiles(atomicTestRoot, ".blocked.json.*.tmp").Any(); }
    string protectedPath = Path.Combine(atomicTestRoot, "protected.json");
    TranscriptFiles.AtomicWrite(protectedPath, "必须保留的旧存档");
    Directory.CreateDirectory(protectedPath + ".bak");
    bool replacementFailureSafe = false;
    try { TranscriptFiles.AtomicWrite(protectedPath, "不能替换的新存档"); }
    catch
    {
        replacementFailureSafe = File.ReadAllText(protectedPath) == "必须保留的旧存档"
            && Directory.Exists(protectedPath + ".bak")
            && !Directory.EnumerateFiles(atomicTestRoot, ".protected.json.*.tmp").Any();
    }
    Check(replacedSafely && failedSafely && replacementFailureSafe, "atomic archive replacement flushes data, preserves the last good file and backup on replace failure, and cleans temporary writes");
}
finally { if (Directory.Exists(atomicTestRoot)) Directory.Delete(atomicTestRoot, recursive: true); }
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
    await using var rejectedSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"), captureEnabled: false);
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
    await using var canceledSession = new SpeechSession(new Uri($"ws://127.0.0.1:{statusPort}/"), captureEnabled: false);
    try { await canceledSession.StartAsync(new Preferences(), "synthetic", 0, null, null, stop.Token); }
    catch (OperationCanceledException) { await rejectLater; return true; }
    catch { await rejectLater; return false; }
    await rejectLater; return false;
}
Check(await CheckConnectCancellationAsync(), "user cancellation during reconnect is not mistaken for a connection timeout");
var disconnectProbe = new TcpListener(IPAddress.Loopback, 0); disconnectProbe.Start();
int disconnectPort = ((IPEndPoint)disconnectProbe.LocalEndpoint).Port; disconnectProbe.Stop();
using var disconnectListener = new HttpListener(); disconnectListener.Prefixes.Add($"http://127.0.0.1:{disconnectPort}/"); disconnectListener.Start();
using var disconnectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var unexpectedClose = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
var disconnectServer = Task.Run(async () =>
{
    var context = await disconnectListener.GetContextAsync().WaitAsync(disconnectTimeout.Token);
    using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
    var packet = new byte[65536];
    var config = await socket.ReceiveAsync(new ArraySegment<byte>(packet), disconnectTimeout.Token);
    using var configJson = JsonDocument.Parse(packet.AsMemory(0, config.Count));
    bool validConfig = config.MessageType == WebSocketMessageType.Text
        && configJson.RootElement.GetProperty("sample_rate").GetInt32() == 16000;
    WebSocketReceiveResult audio;
    do { audio = await socket.ReceiveAsync(new ArraySegment<byte>(packet), disconnectTimeout.Token); }
    while (audio.Count == 0 && !disconnectTimeout.IsCancellationRequested);
    if (!validConfig || audio.MessageType != WebSocketMessageType.Binary || audio.Count == 0)
        throw new InvalidDataException("synthetic session did not send configuration and audio");
    await socket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "simulated network interruption", disconnectTimeout.Token);
}, disconnectTimeout.Token);
await using (var disconnectSession = new SpeechSession(new Uri($"ws://127.0.0.1:{disconnectPort}/"), captureEnabled: false))
{
    disconnectSession.Failure += error => unexpectedClose.TrySetResult(error);
    await disconnectSession.StartAsync(new Preferences(), "synthetic", 0, null, null);
    var disconnectError = await unexpectedClose.Task.WaitAsync(disconnectTimeout.Token);
    await disconnectServer; disconnectListener.Stop();
    Check(disconnectError is IOException && SpeechRetryPolicy.IsTransient(disconnectError),
        "an unexpected close after audio starts is reported as a transient failure eligible for bounded reconnection");
}
var drift = new ClockDriftController();
int neutralFrames = drift.InputFramesFor(320, ClockDriftController.TargetBufferSeconds);
int highBufferFrames = drift.InputFramesFor(320, 0.30);
var longDrift = new ClockDriftController(); int adjustedFrameTotal = 0;
for (int i = 0; i < 10000; i++) adjustedFrameTotal += longDrift.InputFramesFor(320, 0.30);
var lowBuffer = new ClockDriftController(); int lowBufferFrames = lowBuffer.InputFramesFor(320, 0.01);
Check(neutralFrames == 320 && highBufferFrames > 320 && lowBufferFrames < 320
    && longDrift.LastRatio <= 1.005 && longDrift.LastRatio >= 0.995
    && Math.Abs(adjustedFrameTotal - 320 * 10000 * longDrift.LastRatio) < 4,
    "dual-input drift correction responds to buffer direction, stays bounded and preserves fractional frame adjustments");
List<float> ReadNormalized(ISampleProvider input)
{
    ISampleProvider normalized = AudioCapture.ToMono16k(input);
    var samples = new List<float>(); var frame = new float[1024]; int read;
    while ((read = normalized.Read(frame.AsSpan())) > 0) samples.AddRange(frame.AsSpan(0, read).ToArray());
    return samples;
}
var normalized441 = ReadNormalized(new FiniteToneSampleProvider(44100, 2, 1.0, 0));
var normalized480 = ReadNormalized(new FiniteToneSampleProvider(48000, 1, 1.0, 0));
Console.WriteLine($"A03 synthetic resample samples: 44.1 kHz stereo={normalized441.Count}, 48 kHz mono={normalized480.Count}");
Check(normalized441.Count is >= 15900 and <= 16100 && normalized480.Count is >= 15900 and <= 16100,
    "44.1 kHz stereo and 48 kHz mono sources resample to one second of 16 kHz mono within 0.01 seconds");
var delayed480 = ReadNormalized(new FiniteToneSampleProvider(48000, 2, 1.0, 0.15));
int firstDelayedTone = delayed480.FindIndex(sample => sample > 0.1f);
Console.WriteLine($"A03 delayed source: expected onset=2400 samples, measured={firstDelayedTone}");
Check(firstDelayedTone is >= 1600 and <= 3200,
    "a 150 ms later-starting 48 kHz source retains its offset within the 50 ms timeline tolerance");
var boundedPrebuffer = new BoundedAudioPrebuffer(new WaveFormat(16000, 16, 1));
int fullPrebufferBytes = (int)(boundedPrebuffer.CapacitySeconds * boundedPrebuffer.WaveFormat.AverageBytesPerSecond);
boundedPrebuffer.AddSamples(new byte[fullPrebufferBytes], 0, fullPrebufferBytes);
bool overflowReported = false;
try { boundedPrebuffer.AddSamples(new byte[640], 0, 640); }
catch (AudioPrebufferOverflowException e) { overflowReported = e.CapacitySeconds == AudioCapture.PrebufferSeconds && e.Message.Contains("避免静默丢失音频"); }
Check(overflowReported && boundedPrebuffer.BufferedSeconds <= AudioCapture.PrebufferSeconds,
    "pre-connect audio storage is capped at 2.5 seconds and overrun becomes an explicit failure");
(double Min, double Max) SimulateHour(double clockPpm)
{
    var controller = new ClockDriftController(); double bufferedFrames = 16000 * ClockDriftController.TargetBufferSeconds;
    double minimum = bufferedFrames, maximum = bufferedFrames;
    for (int i = 0; i < 180000; i++)
    {
        bufferedFrames += 320 * (1 + clockPpm / 1_000_000);
        bufferedFrames -= controller.InputFramesFor(320, bufferedFrames / 16000.0);
        minimum = Math.Min(minimum, bufferedFrames); maximum = Math.Max(maximum, bufferedFrames);
    }
    return (minimum, maximum);
}
var fastClock = SimulateHour(500); var slowClock = SimulateHour(-500);
Check(fastClock.Min > 0 && fastClock.Max < 16000 && slowClock.Min > 0 && slowClock.Max < 16000,
    "one-hour simulated independent capture clocks at plus/minus 500 ppm keep queues inside the one-second safety bound");
var fixedRoute = new AudioDeviceRoute(DataFlow.Render, "fixed-speaker", "fixed-speaker", "USB Speaker");
var defaultRoute = new AudioDeviceRoute(DataFlow.Capture, null, "old-default-mic", "Built-in Microphone");
Check(AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "fixed-speaker", DeviceState.Unplugged)?.Name == "USB Speaker"
    && AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "fixed-speaker", DeviceState.Active) is null
    && !AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(fixedRoute)
    && AudioEndpointChangePolicy.ShouldRefreshDefaultAfterUnavailable(defaultRoute)
    && !AudioEndpointChangePolicy.IsFollowingDefault(fixedRoute, DataFlow.Render, Role.Multimedia, "new-default-speaker")
    && AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Multimedia, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Communications, "new-default-mic"),
    "audio endpoint policy follows multimedia defaults and reports loss of the explicitly selected endpoint");
Check(AudioEndpointChangePolicy.FindUnavailableRoute([defaultRoute], "old-default-mic", DeviceState.Unplugged) == defaultRoute
    && AudioEndpointChangePolicy.FindUnavailableRoute([fixedRoute], "another-device", DeviceState.Unplugged) is null
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Render, Role.Multimedia, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Console, "new-default-mic")
    && !AudioEndpointChangePolicy.IsFollowingDefault(defaultRoute, DataFlow.Capture, Role.Multimedia, "old-default-mic"),
    "endpoint notifications ignore active/unknown devices, unrelated flows and roles, and unchanged default IDs");
var sleepState = new SleepRecoveryState();
bool endForSleep = sleepState.BeginSleep(recordingIntended: true);
bool scheduleWake = sleepState.BeginWake(); bool beginWakeRecovery = sleepState.BeginRecovery(); sleepState.FinishRecovery();
var stoppedBeforeSleep = new SleepRecoveryState();
stoppedBeforeSleep.BeginSleep(recordingIntended: false); stoppedBeforeSleep.CancelByUser();
Check(endForSleep && scheduleWake && beginWakeRecovery && !sleepState.IsRecovering
    && !stoppedBeforeSleep.BeginWake(),
    "sleep recovery restores only a recording that was intended before sleep and can be cancelled by the user");
int recoveredCycles = 0;
for (int cycle = 0; cycle < 10; cycle++)
{
    var repeatedSleep = new SleepRecoveryState();
    bool ended = repeatedSleep.BeginSleep(recordingIntended: true);
    bool queued = repeatedSleep.BeginWake();
    bool recovering = repeatedSleep.BeginRecovery();
    repeatedSleep.FinishRecovery();
    if (ended && queued && recovering && !repeatedSleep.IsRecovering && !repeatedSleep.HasPendingWakeRecovery) recoveredCycles++;
}
var userStoppedAfterWake = new SleepRecoveryState();
userStoppedAfterWake.BeginSleep(recordingIntended: true); userStoppedAfterWake.BeginWake(); userStoppedAfterWake.CancelByUser();
Check(recoveredCycles == 10 && !userStoppedAfterWake.BeginRecovery(),
    "ten simulated sleep/wake cycles recover an intended session, while a user stop after wake cancels pending recovery");
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
    var provisionalApplied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var switchedSegment = new Segment(); var switchedAssembler = new TokenAssembler(switchedSegment, _ => { });
    var server = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        var configResult = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
        using var config = JsonDocument.Parse(packet.AsMemory(0, configResult.Count));
        validConfig = configResult.MessageType == WebSocketMessageType.Text && config.RootElement.GetProperty("sample_rate").GetInt32() == 16000 && config.RootElement.GetProperty("api_key").GetString() == "synthetic";
        byte[] provisional = Encoding.UTF8.GetBytes("""{"tokens":[{"text":"Before","is_final":false,"start_ms":0,"end_ms":250}]}""");
        await socket.SendAsync(provisional.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), timeout.Token);
            if (result.Count == 0) break;
            receivedAudio += result.Count;
        }
        byte[] final = Encoding.UTF8.GetBytes("""{"tokens":[{"text":"Before and after.","is_final":true,"start_ms":0,"end_ms":500}],"finished":true}""");
        await socket.SendAsync(final.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
    }, timeout.Token);
    await using (var session = new SpeechSession(new Uri($"ws://127.0.0.1:{port}/"), captureEnabled: true))
    {
        session.Message += m =>
        {
            switchedAssembler.Apply(m);
            if (m.TryGetProperty("tokens", out var tokens) && tokens.EnumerateArray().Any(t => t.TryGetProperty("is_final", out var finalToken) && !finalToken.GetBoolean())) provisionalApplied.TrySetResult();
            receivedFinal = m.TryGetProperty("finished", out var f) && f.GetBoolean();
        };
        await session.StartAsync(new Preferences(), "synthetic", 1, null, null);
        double bufferedAtReady = session.BufferedAudioSeconds;
        Check(bufferedAtReady >= 0.7, $"capture buffers audio while the WebSocket connects ({bufferedAtReady:0.00} seconds available)");
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        double bufferedAfterCatchUp = session.BufferedAudioSeconds;
        Console.WriteLine($"A05 catch-up buffer: at connection={bufferedAtReady:0.00}s, after 200 ms={bufferedAfterCatchUp:0.00}s");
        Check(bufferedAfterCatchUp < bufferedAtReady - 0.25, "the sender catches up pre-connect audio instead of preserving a permanent subtitle delay");
        bool restoredInput = false;
        try { await session.SwitchDevicesAsync(null, "missing-device-id"); }
        catch (AudioDeviceSwitchException e) { restoredInput = e.CaptureRestored; }
        Check(restoredInput, "invalid live device switch restores capture and keeps the recognition session open");
        await provisionalApplied.Task.WaitAsync(timeout.Token);
        string[] inputIds = AudioCapture.Devices(DataFlow.Capture).Select(d => d.Id).ToArray();
        int successfulSwitches = 0;
        for (int i = 0; i < 20; i++)
        {
            await session.SwitchDevicesAsync(null, inputIds[i % inputIds.Length]);
            successfulSwitches++;
        }
        Check(successfulSwitches == 20, "live microphone capture switches among active inputs 20 times in one recognition session");
        await Task.Delay(450); await session.StopAsync();
    }
    await server; listener.Stop();
    Check(validConfig && receivedAudio > 0 && receivedFinal && switchedSegment.Entries.Count == 1 && switchedSegment.Entries[0].English == "Before and after.", "local WebSocket: 20 input switches preserve the session and subtitle while final PCM and end marker complete");

    var noFinalPortProbe = new TcpListener(IPAddress.Loopback, 0); noFinalPortProbe.Start();
    int noFinalPort = ((IPEndPoint)noFinalPortProbe.LocalEndpoint).Port; noFinalPortProbe.Stop();
    using var noFinalListener = new HttpListener(); noFinalListener.Prefixes.Add($"http://127.0.0.1:{noFinalPort}/"); noFinalListener.Start();
    using var noFinalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var endMarkerReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var noFinalServer = Task.Run(async () =>
    {
        var context = await noFinalListener.GetContextAsync().WaitAsync(noFinalTimeout.Token);
        using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var packet = new byte[65536];
        await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token); // config
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token);
            if (result.Count == 0) { endMarkerReceived.TrySetResult(); break; }
        }
        try { await socket.ReceiveAsync(new ArraySegment<byte>(packet), noFinalTimeout.Token); } catch (WebSocketException) { }
    }, noFinalTimeout.Token);
    Exception? finalTimeout = null;
    await using (var noFinalSession = new SpeechSession(new Uri($"ws://127.0.0.1:{noFinalPort}/")))
    {
        await noFinalSession.StartAsync(new Preferences(), "synthetic", 0, null, null);
        try { await noFinalSession.StopAsync(); } catch (Exception e) { finalTimeout = e; }
    }
    await endMarkerReceived.Task.WaitAsync(noFinalTimeout.Token);
    noFinalListener.Stop(); await noFinalServer;
    Check(finalTimeout is TimeoutException && finalTimeout.Message.Contains("最后识别结果超时") && endMarkerReceived.Task.IsCompleted,
        "stop reports an explicit incomplete-result timeout when the service receives the end marker but sends no final response");

    var delayedPortProbe = new TcpListener(IPAddress.Loopback, 0); delayedPortProbe.Start();
    int delayedPort = ((IPEndPoint)delayedPortProbe.LocalEndpoint).Port; delayedPortProbe.Stop();
    using var delayedListener = new HttpListener(); delayedListener.Prefixes.Add($"http://127.0.0.1:{delayedPort}/"); delayedListener.Start();
    using var delayedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
    var delayedServer = Task.Run(async () =>
    {
        var context = await delayedListener.GetContextAsync().WaitAsync(delayedTimeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(3.2), delayedTimeout.Token);
        try { context.Response.StatusCode = 503; context.Response.Close(); }
        catch (HttpListenerException) { }
    }, delayedTimeout.Token);
    Exception? overflowFailure = null;
    await using (var slowSession = new SpeechSession(new Uri($"ws://127.0.0.1:{delayedPort}/"), captureEnabled: true))
    {
        try { await slowSession.StartAsync(new Preferences(), "synthetic", 1, null, null); }
        catch (Exception e) { overflowFailure = e; }
    }
    await delayedServer; delayedListener.Stop();
    Check(overflowFailure is AudioCaptureFailureException && overflowFailure.Message.Contains("2.5 秒上限"),
        "a WebSocket handshake slower than the 2.5-second prebuffer fails visibly instead of silently dropping captured audio");
}
Console.WriteLine($"Completed {passed} checks. No cloud calls; no audio was saved.");

sealed class FiniteToneSampleProvider(int sampleRate, int channels, double durationSeconds, double leadingSilenceSeconds) : ISampleProvider
{
    private readonly int totalSamples = (int)Math.Round(sampleRate * channels * durationSeconds);
    private int position;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public int Read(Span<float> buffer)
    {
        int available = Math.Min(buffer.Length, totalSamples - position);
        for (int i = 0; i < available; i++)
        {
            int sourceFrame = (position + i) / channels;
            buffer[i] = sourceFrame / (double)sampleRate < leadingSilenceSeconds ? 0 : 0.25f;
        }
        position += available;
        return available;
    }
}
