using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    private string? dateSeparatorLabel;
    [JsonIgnore] public string? DateSeparatorLabel
    {
        get => dateSeparatorLabel;
        private set
        {
            if (SetProperty(ref dateSeparatorLabel, value)) OnPropertyChanged(nameof(HasDateSeparator));
        }
    }
    [JsonIgnore] public bool HasDateSeparator => !string.IsNullOrEmpty(DateSeparatorLabel);
    public void SetDateSeparatorLabel(string? value) => DateSeparatorLabel = value;
    [ObservableProperty] public partial string English { get; set; } = "";
    [ObservableProperty] public partial string Chinese { get; set; } = "";
    [ObservableProperty] public partial string? Speaker { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLanguage))]
    public partial string? Language { get; set; }
    public SubtitleCorrection? Correction { get; set; }
    [JsonIgnore] public string CorrectionLabel => Correction is null ? "纠正" : "查看校对";
    [JsonIgnore] public bool HasCorrection => Correction is not null;
    [JsonIgnore] public bool HasLanguage => !string.IsNullOrWhiteSpace(Language);
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
    public void Edit(string? source, string? translation)
    {
        string updatedSource = source ?? English;
        string updatedTranslation = translation ?? Chinese;
        if (updatedSource == English && updatedTranslation == Chinese) return;
        Correction ??= new SubtitleCorrection { RawSource = English, RawTranslation = Chinese };
        Correction.History.Add(new SubtitleRevision { Source = English, Translation = Chinese, Date = DateTimeOffset.UtcNow });
        if (updatedSource != English) Correction.SourceLocked = true;
        if (updatedTranslation != Chinese) Correction.TranslationLocked = true;
        Correction.Revision = Guid.NewGuid(); English = updatedSource; Chinese = updatedTranslation;
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

public static class TranscriptPresentation
{
    public static string? DaySeparatorLabel(double? previousAppleSeconds, double? currentAppleSeconds, TimeZoneInfo timeZone)
    {
        if (previousAppleSeconds is not double previous || currentAppleSeconds is not double current) return null;
        var previousDate = TimeZoneInfo.ConvertTime(Archive.AppleEpoch.AddSeconds(previous), timeZone);
        var currentDate = TimeZoneInfo.ConvertTime(Archive.AppleEpoch.AddSeconds(current), timeZone);
        if (previousDate.Date == currentDate.Date) return null;
        return currentDate.ToString("yyyy年M月d日", CultureInfo.GetCultureInfo("zh-CN"));
    }
}

public static class TranscriptRecognitionCorrections
{
    private static readonly RegexOptions WordOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly (string Source, string Target)[] NormalizedVariants =
    [
        ("micro economic", "microeconomic"),
        ("macro economic", "macroeconomic"),
        ("micro-economic", "microeconomic"),
        ("macro-economic", "macroeconomic")
    ];
    private static readonly string[] MicroSignals =
    [
        "microeconomic theory", "microeconomic analysis", "microeconomic behavior",
        "microeconomic model", "microeconomic models", "microeconomic foundations",
        "microeconomic incentives", "microeconomic decision", "microeconomic decisions"
    ];
    private static readonly string[] MacroSignals =
    [
        "macroeconomic policy", "macroeconomic growth", "macroeconomic indicators",
        "macroeconomic inflation", "macroeconomic unemployment", "macroeconomic gdp",
        "macroeconomic outlook", "macroeconomic conditions", "macroeconomic performance"
    ];
    private static readonly string[] DerivativePhrases =
    [
        "first duty", "second duty", "partial duty", "duty of", "duty with respect to",
        "take the duty", "duty function", "duties of", "first duties", "second duties", "partial duties"
    ];
    private static readonly string[] HandPhrases =
    [
        "on the other han", "on the one han", "one han", "other han", "right han",
        "left han", "han side", "at han"
    ];

    public static string CorrectEnglish(string text)
    {
        string result = text;
        foreach (var (source, target) in NormalizedVariants)
            result = Regex.Replace(result, $@"\b{Regex.Escape(source)}\b", target, WordOptions);

        string lower = result.ToLowerInvariant();
        bool stronglyMicroeconomic = MicroSignals.Any(lower.Contains);
        bool stronglyMacroeconomic = MacroSignals.Any(lower.Contains);
        if (stronglyMicroeconomic && !stronglyMacroeconomic)
            result = Regex.Replace(result, @"\bmacroeconomics?\b", "microeconomic", WordOptions);
        else if (stronglyMacroeconomic && !stronglyMicroeconomic)
            result = Regex.Replace(result, @"\bmicroeconomics?\b", "macroeconomic", WordOptions);

        if (DerivativePhrases.Any(phrase => result.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = Regex.Replace(result, @"\bfirst duties?\b", "first derivative", WordOptions);
            result = Regex.Replace(result, @"\bsecond duties?\b", "second derivative", WordOptions);
            result = Regex.Replace(result, @"\bpartial duties?\b", "partial derivative", WordOptions);
            result = Regex.Replace(result, @"\bduties\b", "derivatives", WordOptions);
            result = Regex.Replace(result, @"\bduty\b", "derivative", WordOptions);
        }

        if (HandPhrases.Any(phrase => result.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            result = Regex.Replace(result, @"\bhan\b", "hand", WordOptions);
        return result;
    }

    public static string CorrectChineseTranslation(string text, string correctedEnglish)
    {
        string lower = correctedEnglish.ToLowerInvariant();
        if (!lower.Contains("derivative", StringComparison.Ordinal)
            && !lower.Contains("differentiate", StringComparison.Ordinal)
            && !lower.Contains("with respect to", StringComparison.Ordinal)) return text;
        return text.Replace("关税", "导数", StringComparison.Ordinal);
    }
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

public static class TranscriptTextChunks
{
    public static IReadOnlyList<string> Create(IEnumerable<Subtitle> entries, int maxCharacters = 18000)
    {
        if (maxCharacters < 128) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        var chunks = new List<string>(); var current = new StringBuilder();
        void Flush()
        {
            if (current.Length == 0) return;
            chunks.Add(current.ToString()); current.Clear();
        }
        foreach (var entry in entries)
        {
            string prefix = $"[{entry.TimeLabel}] {entry.Speaker} ";
            if (prefix.Length + entry.English.Length + 1 <= maxCharacters)
            {
                if (current.Length + prefix.Length + entry.English.Length + 1 > maxCharacters) Flush();
                current.Append(prefix).AppendLine(entry.English);
                continue;
            }
            Flush();
            int bodyLimit = Math.Max(1, maxCharacters - prefix.Length - 1);
            var part = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(entry.English);
            while (elements.MoveNext())
            {
                string element = (string)elements.Current!;
                if (part.Length + element.Length > bodyLimit)
                {
                    chunks.Add(prefix + part + "\n"); part.Clear();
                }
                part.Append(element);
            }
            if (part.Length > 0) chunks.Add(prefix + part + "\n");
        }
        Flush();
        return chunks;
    }
}

public static class TranscriptSummarySelection
{
    public static List<Subtitle> Select(Archive archive, int scope)
    {
        IEnumerable<Subtitle> entries = scope switch
        {
            0 or 2 => archive.Segments.SelectMany(s => s.Entries),
            1 => archive.Segments.LastOrDefault()?.Entries ?? [],
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        entries = entries.Where(e => !string.IsNullOrWhiteSpace(e.English));
        if (scope == 0)
            entries = entries.Where(e => archive.SummarizedEntries.GetValueOrDefault(e.Id) != TranscriptFiles.SummarySignature(e.English));
        return entries.ToList();
    }
}

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
    public string? Summary { get; set; }
    public Dictionary<Guid, string> SummarizedEntries { get; set; } = [];
    public List<Segment> Segments { get; set; } = [];
    public override string ToString() => Title;
}

public sealed record ArchiveSegmentSplit(Archive ExtractedArchive, Segment Segment, int OriginalIndex,
    Dictionary<Guid, string> RemovedSummarySignatures);

public static class ArchiveOperations
{
    public static ArchiveSegmentSplit? SplitSegment(Archive source, Guid segmentId)
    {
        int index = source.Segments.FindIndex(s => s.Id == segmentId);
        if (index < 0) return null;
        var segment = source.Segments[index];
        source.Segments.RemoveAt(index);
        var signatures = new Dictionary<Guid, string>();
        foreach (var entry in segment.Entries)
            if (source.SummarizedEntries.Remove(entry.Id, out var signature)) signatures[entry.Id] = signature;
        var extracted = new Archive { Title = $"{source.Title} - 本段", CreatedAt = segment.StartedAt, Segments = [segment] };
        return new ArchiveSegmentSplit(extracted, segment, index, signatures);
    }

    public static void RestoreSplit(Archive source, ArchiveSegmentSplit split)
    {
        source.Segments.Insert(Math.Clamp(split.OriginalIndex, 0, source.Segments.Count), split.Segment);
        foreach (var signature in split.RemovedSummarySignatures) source.SummarizedEntries[signature.Key] = signature.Value;
    }
}

public static class TranscriptFiles
{
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
        options.Converters.Add(new AppleDateTimeOffsetJsonConverter());
        return options;
    }
    public static string SummarySignature(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static Archive Snapshot(Archive source) => new()
    {
        Id = source.Id, Title = source.Title, CreatedAt = source.CreatedAt, UpdatedAt = source.UpdatedAt,
        Summary = source.Summary, SummarizedEntries = new(source.SummarizedEntries),
        Segments = source.Segments.Select(segment => new Segment
        {
            Id = segment.Id, StartedAt = segment.StartedAt, UpdatedAt = segment.UpdatedAt,
            Entries = segment.Entries.Select(entry => new Subtitle
            {
                Id = entry.Id, Start = entry.Start, End = entry.End, RecordedAt = entry.RecordedAt,
                English = entry.English, Chinese = entry.Chinese, Speaker = entry.Speaker, Language = entry.Language,
                Correction = entry.Correction is { } correction ? new SubtitleCorrection
                {
                    RawSource = correction.RawSource, RawTranslation = correction.RawTranslation,
                    SourceLocked = correction.SourceLocked, TranslationLocked = correction.TranslationLocked,
                    Revision = correction.Revision,
                    History = correction.History.Select(version => new SubtitleRevision
                    { Source = version.Source, Translation = version.Translation, Date = version.Date }).ToList()
                } : null
            }).ToList()
        }).ToList()
    };
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoWindows");
    public static string Folder => Path.Combine(Root, "Archives");
    public static string DeletedFolder => Path.Combine(Root, "Deleted");
    public static void AtomicWrite(string path, string text)
        => AtomicWrite(path, text, afterFlushBeforeReplace: null);

    internal static void AtomicWrite(string path, string text, Action? afterFlushBeforeReplace)
        => AtomicWrite(path, text, static (stream, bytes) => stream.Write(bytes), afterFlushBeforeReplace);

    internal static void AtomicWrite(string path, string text, Action<Stream, byte[]> writeToTemp,
        Action? afterFlushBeforeReplace = null)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        CleanupAbandonedWrites(directory, Path.GetFileName(path));
        string temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                writeToTemp(stream, bytes);
                stream.Flush(flushToDisk: true);
            }
            afterFlushBeforeReplace?.Invoke();
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* A failed cleanup must not hide the original write/replace error. */ }
        }
    }
    public static string SaveFailureMessage(Exception error)
    {
        if (error is IOException && (error.HResult & 0xFFFF) is 39 or 112)
            return "磁盘空间不足，存档未保存。请释放磁盘空间后重试。";
        return "存档尚未保存，请导出备份：" + error.Message;
    }
    private static void CleanupAbandonedWrites(string directory, string fileName)
    {
        string prefix = $".{fileName}.";
        foreach (string candidate in Directory.EnumerateFiles(directory, $"{prefix}*.tmp", SearchOption.TopDirectoryOnly))
        {
            string suffix = Path.GetFileName(candidate)[prefix.Length..];
            string processIdText = suffix.Split('.', 2)[0];
            if (!int.TryParse(processIdText, out int processId) || IsProcessRunning(processId)) continue;
            try { File.Delete(candidate); }
            catch { /* Keep an orphan if access is denied; the current checkpoint may still proceed. */ }
        }
    }
    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch { return true; } // If liveness is uncertain, preserve the candidate file.
    }
    public static void Save(Archive archive)
    {
        archive.UpdatedAt = Archive.Now;
        AtomicWrite(Path.Combine(Folder, $"{archive.Id}.json"), JsonSerializer.Serialize(archive, Json));
    }
    public static string MoveToDeleted(Archive archive) => MoveToDeleted(archive, Folder, DeletedFolder);
    public static string MoveToDeleted(Archive archive, string archivesFolder, string deletedFolder)
    {
        string source = Path.Combine(archivesFolder, $"{archive.Id}.json");
        if (!File.Exists(source)) throw new FileNotFoundException("找不到待移入回收区的存档文件。", source);
        Directory.CreateDirectory(deletedFolder);
        string destination = Path.Combine(deletedFolder, $"{archive.Id}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
        string backup = source + ".bak", destinationBackup = destination + ".bak";
        File.Move(source, destination);
        try { if (File.Exists(backup)) File.Move(backup, destinationBackup); }
        catch { File.Move(destination, source); throw; }
        return destination;
    }
    public static Archive Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out _) || !root.TryGetProperty("segments", out _) || !root.TryGetProperty("createdAt", out _)) throw new InvalidDataException("不是 Echo 存档：缺少 id、segments 或 createdAt。");
        var a = JsonSerializer.Deserialize<Archive>(text, Json) ?? throw new InvalidDataException("存档为空。");
        if (a.Id == Guid.Empty || a.Segments is null || a.Segments.Any(s => s is null || s.Entries is null)) throw new InvalidDataException("存档结构无效。");
        a.SummarizedEntries ??= [];
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
        var all = archive.Segments.OrderBy(s => s.StartedAt).SelectMany(s => s.Entries.Select(e => (e, at: e.RecordedAt ?? s.StartedAt + e.Start))).ToList();
        if (all.Count == 0) return "";
        double origin = all.Min(x => x.at), last = 0;
        var exportEntries = all.Where(x => !string.IsNullOrWhiteSpace(x.e.English)).ToList();
        if (exportEntries.Count == 0) return "";
        var result = new StringBuilder(); int index = 1;
        foreach (var (e, at) in exportEntries)
        {
            double start = Math.Max(last, at - origin), end = start + Math.Max(.5, e.End - e.Start);
            if (index > 1) result.AppendLine();
            result.AppendLine((index++).ToString(CultureInfo.InvariantCulture));
            result.AppendLine($"{Stamp(start)} --> {Stamp(end)}");
            if (!string.IsNullOrWhiteSpace(e.Speaker)) result.AppendLine($"[{e.Speaker}]");
            result.AppendLine(e.English.Trim());
            if (!string.IsNullOrWhiteSpace(e.Chinese)) result.AppendLine(e.Chinese.Trim());
            last = end;
        }
        return result.ToString();
    }
    private static string Stamp(double seconds)
    {
        var t = TimeSpan.FromMilliseconds(Math.Truncate(seconds * 1000));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }
}

public sealed class AppleDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return Archive.AppleEpoch.AddSeconds(reader.GetDouble());
        if (reader.TokenType == JsonTokenType.String)
            return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        throw new JsonException("Date must be an Apple epoch number or an ISO-8601 string.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue((value - Archive.AppleEpoch).TotalSeconds);
}

// Final tokens append exactly once; provisional tokens are replaced on each response.
// Source and translation have independent endpoint cursors, so delayed translations do not target the next source row.
public sealed class TokenAssembler(Segment segment, Action<Subtitle> added, Action<Subtitle>? finalized = null)
{
    private int sourceCursor, translationCursor;
    private readonly Dictionary<int, string> sourceFinal = [], translationFinal = [];
    private readonly Dictionary<int, string> sourceProvisional = [], translationProvisional = [];
    public bool FinalizeCurrent()
    {
        int index = Math.Max(sourceCursor, translationCursor);
        if (index >= segment.Entries.Count) return false;
        finalized?.Invoke(segment.Entries[index]);
        sourceCursor = translationCursor = index + 1;
        ForgetRow(index);
        sourceProvisional.Clear(); translationProvisional.Clear();
        return true;
    }
    private void ForgetRow(int index)
    {
        sourceFinal.Remove(index); translationFinal.Remove(index);
        sourceProvisional.Remove(index); translationProvisional.Remove(index);
    }
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
    private void ApplyRecognition(int index)
    {
        string source = TranscriptRecognitionCorrections.CorrectEnglish(
            sourceFinal.GetValueOrDefault(index, "") + sourceProvisional.GetValueOrDefault(index, ""));
        string translation = TranscriptRecognitionCorrections.CorrectChineseTranslation(
            translationFinal.GetValueOrDefault(index, "") + translationProvisional.GetValueOrDefault(index, ""), source);
        segment.Entries[index].ApplyRecognition(source, translation);
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
        foreach (int i in affectedRows) ApplyRecognition(i);
        int provisionalSource = sourceCursor, provisionalTranslation = translationCursor;
        foreach (var token in tokens.EnumerateArray())
        {
            string text = token.TryGetProperty("text", out var tv) ? tv.GetString() ?? "" : "";
            bool translation = token.TryGetProperty("translation_status", out var tr) && tr.GetString() == "translation";
            bool final = token.TryGetProperty("is_final", out var f) && f.GetBoolean();
            if (text is "<end>" or "<fin>")
            {
                // Mac treats either marker as the boundary for the active bilingual entry.
                // Soniox emits <end>/<fin> as final markers after the whole stream segment,
                // so the marker is not owned by the original or translation lane.
                int completed = Math.Max(sourceCursor, translationCursor);
                if (completed >= segment.Entries.Count) continue;
                finalized?.Invoke(segment.Entries[completed]);
                sourceCursor = translationCursor = completed + 1;
                provisionalSource = provisionalTranslation = completed + 1;
                ForgetRow(completed);
                continue;
            }
            if (text.Length == 0 || text.StartsWith('<')) continue;
            if (!translation && final && token.TryGetProperty("speaker", out var finalSpeaker)
                && finalSpeaker.ValueKind == JsonValueKind.String)
            {
                string label = "Speaker " + finalSpeaker.GetString();
                if (sourceCursor < segment.Entries.Count && segment.Entries[sourceCursor] is { } current
                    && !string.IsNullOrWhiteSpace(current.English) && current.Speaker is not null && current.Speaker != label)
                {
                    finalized?.Invoke(current);
                    ForgetRow(sourceCursor);
                    sourceCursor++;
                    translationCursor = Math.Max(translationCursor, sourceCursor);
                    provisionalSource = sourceCursor;
                    provisionalTranslation = Math.Max(provisionalTranslation, translationCursor);
                }
            }
            int index = translation ? (final ? translationCursor : provisionalTranslation) : (final ? sourceCursor : provisionalSource);
            var row = Row(index);
            if (!translation)
            {
                if (token.TryGetProperty("start_ms", out var start) && row.English.Length == 0) { row.Start = start.GetDouble() / 1000; row.RecordedAt = segment.StartedAt + row.Start; }
                if (token.TryGetProperty("end_ms", out var end)) row.End = Math.Max(row.Start, end.GetDouble() / 1000);
                if (token.TryGetProperty("speaker", out var speaker)
                    && speaker.ValueKind == JsonValueKind.String && (final || row.Speaker is null))
                    row.Speaker = "Speaker " + speaker.GetString();
                if (token.TryGetProperty("language", out var lang)
                    && lang.ValueKind == JsonValueKind.String && (final || row.Language is null))
                    row.Language = lang.GetString();
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
            ApplyRecognition(index);
        }
        segment.UpdatedAt = Archive.Now;
    }
}
