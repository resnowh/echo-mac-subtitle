using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Echo_Windows.Core;

public partial class Subtitle : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double Start { get; set; }
    public double End { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeLabel))]
    public partial double? RecordedAt { get; set; }
    [ObservableProperty] public partial string English { get; set; } = "";
    [ObservableProperty] public partial string Chinese { get; set; } = "";
    [ObservableProperty] public partial string? Speaker { get; set; }
    public string? Language { get; set; }
    public SubtitleCorrection? Correction { get; set; }
    [JsonIgnore] public string CorrectionLabel => Correction is null ? "纠正" : "查看校对";
    [JsonIgnore] public bool HasCorrection => Correction is not null;
    [JsonIgnore] public bool CanUndoCorrection => Correction?.History.Count > 0;
    public void ApplyRecognition(string source, string translation)
    {
        if (Correction is { } c)
        {
            c.RawSource = source; c.RawTranslation = translation;
            if (!c.SourceLocked) English = source;
            if (!c.TranslationLocked) Chinese = translation;
        }
        else { English = source; Chinese = translation; }
    }
    public void Edit(string source, string translation)
    {
        if (source == English && translation == Chinese) return;
        Correction ??= new SubtitleCorrection { RawSource = English, RawTranslation = Chinese };
        Correction.History.Add(new SubtitleRevision { Source = English, Translation = Chinese, Date = DateTimeOffset.UtcNow });
        if (source != English) Correction.SourceLocked = true;
        if (translation != Chinese) Correction.TranslationLocked = true;
        Correction.Revision = Guid.NewGuid(); English = source; Chinese = translation;
        OnPropertyChanged(nameof(CorrectionLabel)); OnPropertyChanged(nameof(HasCorrection)); OnPropertyChanged(nameof(CanUndoCorrection));
    }
    public bool UndoCorrection()
    {
        if (Correction is not { History.Count: > 0 } c) return false;
        var previous = c.History[^1]; c.History.RemoveAt(c.History.Count - 1);
        English = previous.Source; Chinese = previous.Translation;
        c.SourceLocked = true; c.TranslationLocked = true; c.Revision = Guid.NewGuid();
        OnPropertyChanged(nameof(CanUndoCorrection)); return true;
    }
    [JsonIgnore] public string TimeLabel => RecordedAt is double t
        ? Archive.AppleEpoch.AddSeconds(t).ToLocalTime().ToString("HH:mm:ss") : TimeSpan.FromSeconds(Start).ToString(@"hh\:mm\:ss");
}

public sealed class SubtitleCorrection
{
    public string RawSource { get; set; } = "";
    public string RawTranslation { get; set; } = "";
    public bool SourceLocked { get; set; }
    public bool TranslationLocked { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public List<SubtitleRevision> History { get; set; } = [];
}

public sealed class SubtitleRevision
{
    public string Source { get; set; } = "";
    public string Translation { get; set; } = "";
    public DateTimeOffset Date { get; set; }
}

public sealed record CorrectionSuggestion(string Source, string Translation, string Reason, bool Uncertain);

public sealed class Segment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double StartedAt { get; set; } = Archive.Now;
    public double UpdatedAt { get; set; } = Archive.Now;
    public List<Subtitle> Entries { get; set; } = [];
}

public sealed class Archive
{
    public static readonly DateTimeOffset AppleEpoch = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static double Now => (DateTimeOffset.UtcNow - AppleEpoch).TotalSeconds;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = $"录音 {DateTime.Now:MM-dd HH:mm}";
    public double CreatedAt { get; set; } = Now;
    public double UpdatedAt { get; set; } = Now;
    public List<Segment> Segments { get; set; } = [];
    public override string ToString() => Title;
}

public static class TranscriptFiles
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoWindows");
    public static string Folder => Path.Combine(Root, "Archives");
    public static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
    public static void Save(Archive archive)
    {
        archive.UpdatedAt = Archive.Now;
        AtomicWrite(Path.Combine(Folder, $"{archive.Id}.json"), JsonSerializer.Serialize(archive, Json));
    }
    public static Archive Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out _) || !root.TryGetProperty("segments", out _) || !root.TryGetProperty("createdAt", out _)) throw new InvalidDataException("不是 Echo 存档：缺少 id、segments 或 createdAt。");
        var a = JsonSerializer.Deserialize<Archive>(text, Json) ?? throw new InvalidDataException("存档为空。");
        if (a.Id == Guid.Empty || a.Segments is null || a.Segments.Any(s => s is null || s.Entries is null)) throw new InvalidDataException("存档结构无效。");
        bool ValidDate(double time) => double.IsFinite(time) && time >= -63082281600 && time <= 252423993599;
        if (!ValidDate(a.CreatedAt) || !ValidDate(a.UpdatedAt)) throw new InvalidDataException("存档日期无效。");
        var ids = new HashSet<Guid>();
        foreach (var s in a.Segments)
        {
            if (!ValidDate(s.StartedAt) || !ValidDate(s.UpdatedAt)) throw new InvalidDataException("录音段日期无效。");
            foreach (var e in s.Entries)
            {
                if (e is null || !ids.Add(e.Id) || e.Id == Guid.Empty) throw new InvalidDataException("字幕编号为空或重复。");
                if (!double.IsFinite(e.Start) || !double.IsFinite(e.End) || e.Start < 0 || e.End < e.Start || !ValidDate(s.StartedAt + e.End) || (e.RecordedAt is double at && !ValidDate(at))) throw new InvalidDataException("字幕时间无效。");
            }
        }
        return a;
    }
    public static string Srt(Archive archive)
    {
        var all = archive.Segments.OrderBy(s => s.StartedAt).SelectMany(s => s.Entries.Select(e => (e, at: e.RecordedAt ?? s.StartedAt + e.Start)))
            .Where(x => !string.IsNullOrWhiteSpace(x.e.English + x.e.Chinese)).ToList();
        if (all.Count == 0) return "";
        double origin = all.Min(x => x.at), last = 0;
        var result = new StringBuilder(); int index = 1;
        foreach (var (e, at) in all)
        {
            double start = Math.Max(last, at - origin), end = start + Math.Max(.5, e.End - e.Start);
            result.AppendLine((index++).ToString(CultureInfo.InvariantCulture));
            result.AppendLine($"{Stamp(start)} --> {Stamp(end)}");
            if (!string.IsNullOrWhiteSpace(e.Speaker)) result.Append($"[{e.Speaker}] ");
            if (!string.IsNullOrWhiteSpace(e.English)) result.AppendLine(e.English.Trim());
            if (!string.IsNullOrWhiteSpace(e.Chinese)) result.AppendLine(e.Chinese.Trim());
            result.AppendLine(); last = end;
        }
        return result.ToString();
    }
    private static string Stamp(double seconds)
    {
        var t = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }
}

// Final tokens append exactly once; provisional tokens are replaced on each response.
// Source and translation have independent endpoint cursors, so delayed translations do not target the next source row.
public sealed class TokenAssembler(Segment segment, Action<Subtitle> added, Action<Subtitle>? finalized = null)
{
    private int sourceCursor, translationCursor;
    private readonly Dictionary<int, string> sourceFinal = [], translationFinal = [];
    private readonly Dictionary<int, string> sourceProvisional = [], translationProvisional = [];
    private Subtitle Row(int index)
    {
        while (segment.Entries.Count <= index)
        {
            var previous = segment.Entries.LastOrDefault();
            var e = new Subtitle { Start = previous?.End ?? 0, End = previous?.End ?? 0, RecordedAt = segment.StartedAt + (previous?.End ?? 0) };
            segment.Entries.Add(e); added(e);
        }
        return segment.Entries[index];
    }
    public void Apply(JsonElement message)
    {
        if (!message.TryGetProperty("tokens", out var tokens)) return;
        bool hasSourceUpdate = false, hasTranslationUpdate = false;
        foreach (var token in tokens.EnumerateArray())
        {
            if (!token.TryGetProperty("text", out var value)) continue;
            var text = value.GetString() ?? "";
            if (text.Length == 0 || text.StartsWith('<')) continue;
            bool isTranslation = token.TryGetProperty("translation_status", out var lane) && lane.GetString() == "translation";
            if (isTranslation) hasTranslationUpdate = true; else hasSourceUpdate = true;
        }
        // Some services send source and translation provisional updates in separate messages.
        // Replace only the lane present in this response and preserve the other lane's latest snapshot.
        var affectedRows = new HashSet<int>();
        if (hasSourceUpdate) { affectedRows.UnionWith(sourceProvisional.Keys); sourceProvisional.Clear(); }
        if (hasTranslationUpdate) { affectedRows.UnionWith(translationProvisional.Keys); translationProvisional.Clear(); }
        foreach (int i in affectedRows)
            segment.Entries[i].ApplyRecognition(sourceFinal.GetValueOrDefault(i, "") + sourceProvisional.GetValueOrDefault(i, ""),
                translationFinal.GetValueOrDefault(i, "") + translationProvisional.GetValueOrDefault(i, ""));
        int provisionalSource = sourceCursor, provisionalTranslation = translationCursor;
        foreach (var token in tokens.EnumerateArray())
        {
            string text = token.TryGetProperty("text", out var tv) ? tv.GetString() ?? "" : "";
            bool translation = token.TryGetProperty("translation_status", out var tr) && tr.GetString() == "translation";
            bool final = token.TryGetProperty("is_final", out var f) && f.GetBoolean();
            if (text is "<end>" or "<fin>")
            {
                string markerLane = token.TryGetProperty("translation_status", out var marker) ? marker.GetString() ?? "" : "";
                if (markerLane is "" or "none")
                {
                    // The normal untagged endpoint finalizes the complete bilingual utterance.
                    if (final)
                    {
                        int completed = Math.Max(sourceCursor, translationCursor);
                        if (completed < segment.Entries.Count) finalized?.Invoke(segment.Entries[completed]);
                        sourceCursor = translationCursor = completed + 1;
                    }
                    provisionalSource = provisionalTranslation = Math.Max(provisionalSource, provisionalTranslation) + 1;
                }
                else
                {
                    if (final)
                    {
                        if (translation) translationCursor++;
                        else
                        {
                            if (sourceCursor < segment.Entries.Count) finalized?.Invoke(segment.Entries[sourceCursor]);
                            sourceCursor++;
                        }
                    }
                    if (translation) provisionalTranslation++; else provisionalSource++;
                }
                continue;
            }
            if (text.Length == 0 || text.StartsWith('<')) continue;
            int index = translation ? (final ? translationCursor : provisionalTranslation) : (final ? sourceCursor : provisionalSource);
            var row = Row(index);
            if (!translation)
            {
                if (token.TryGetProperty("start_ms", out var start) && row.English.Length == 0) { row.Start = start.GetDouble() / 1000; row.RecordedAt = segment.StartedAt + row.Start; }
                if (token.TryGetProperty("end_ms", out var end)) row.End = Math.Max(row.Start, end.GetDouble() / 1000);
                if (token.TryGetProperty("speaker", out var speaker)) row.Speaker = "Speaker " + speaker.ToString();
                if (token.TryGetProperty("language", out var lang)) row.Language = lang.GetString();
            }
            if (final)
            {
                var dict = translation ? translationFinal : sourceFinal;
                dict[index] = dict.GetValueOrDefault(index, "") + text;
            }
            else
            {
                var dict = translation ? translationProvisional : sourceProvisional;
                dict[index] = dict.GetValueOrDefault(index, "") + text;
            }
            row.ApplyRecognition(sourceFinal.GetValueOrDefault(index, "") + sourceProvisional.GetValueOrDefault(index, ""),
                translationFinal.GetValueOrDefault(index, "") + translationProvisional.GetValueOrDefault(index, ""));
        }
        segment.UpdatedAt = Archive.Now;
    }
}
