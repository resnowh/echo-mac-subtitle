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
var archive = new Archive { CreatedAt = 800000000, Segments = [segment, new Segment { StartedAt = 800000010, Entries = [new Subtitle { Start = 0, End = 1, English = "Again" }] }] };
string srt = TranscriptFiles.Srt(archive);
Check(srt.Contains("00:00:10,000 --> 00:00:11,000") && srt.Contains("你好。"), "SRT preserves segment wall-clock gap and translation");
string json = JsonSerializer.Serialize(archive, TranscriptFiles.Json);
var loaded = TranscriptFiles.Parse(json);
Check(loaded.Id == archive.Id && loaded.Segments[0].StartedAt == 800000000 && json.Contains("\"english\""), "legacy JSON field names, UUID and Apple reference date round trip");
var correctionLoaded = TranscriptFiles.Parse(correctionSnapshot).Segments[0].Entries[0].Correction;
Check(correctionLoaded?.RawSource == "Recognized text" && correctionLoaded.SourceLocked && correctionLoaded.History.Count == 1 && correctionSnapshot.Contains("\"rawSource\""), "correction source, lock and undo history survive Mac-compatible archive round trip");
Check(Archive.AppleEpoch.AddSeconds(0).Year == 2001, "Apple date reference is not Unix time");
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
        await Task.Delay(450); await session.StopAsync();
    }
    await server; listener.Stop();
    Check(validConfig && receivedAudio > 0 && receivedFinal, "local WebSocket: config, binary PCM, end marker, final result before stop completes");
}
Console.WriteLine($"Completed {passed} checks. No cloud calls; no audio was saved.");
