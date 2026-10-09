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

public sealed record CorrectionSuggestion(
    [property: JsonRequired] string Source,
    [property: JsonRequired] string Translation,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] bool Uncertain);

public static class TranscriptTextChunks
{
    public static IReadOnlyList<string> Create(IEnumerable<Subtitle> entries, int maxCharacters = 18000, Archive? archive = null)
    {
        if (maxCharacters < 128) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        var chunks = new List<string>(); var current = new StringBuilder();
        var archiveTimes = archive?.Segments
            .SelectMany(segment => segment.Entries.Select(entry => (entry.Id, Timestamp: segment.StartedAt + entry.Start)))
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First().Timestamp)
            ?? new Dictionary<Guid, double>();
        void Flush()
        {
            if (current.Length == 0) return;
            chunks.Add(current.ToString()); current.Clear();
        }
        void AddBoundedRow(string row)
        {
            string separator = current.Length == 0 ? "" : "\n\n";
            if (row.Length + separator.Length <= maxCharacters && current.Length + row.Length + separator.Length <= maxCharacters)
            {
                current.Append(separator).Append(row);
                return;
            }
            Flush();
            var part = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(row);
            while (elements.MoveNext())
            {
                string element = (string)elements.Current!;
                if (part.Length + element.Length > maxCharacters)
                {
                    if (part.Length > 0) chunks.Add(part.ToString());
                    part.Clear();
                }
                part.Append(element);
            }
            if (part.Length > 0) chunks.Add(part.ToString());
        }
        DateTime? previousLocalDay = null;
        int rowNumber = 0;
        foreach (var entry in entries)
        {
            double appleSeconds = entry.RecordedAt ?? archiveTimes.GetValueOrDefault(entry.Id, entry.Start);
            DateTimeOffset localStart = Archive.AppleEpoch.AddSeconds(appleSeconds).ToLocalTime();
            string time = rowNumber == 0 || previousLocalDay != localStart.Date
                ? localStart.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : localStart.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            previousLocalDay = localStart.Date;
            string row = $"{rowNumber + 1}. [{time}]\n英文：{entry.English.Trim()}\n中文：{entry.Chinese.Trim()}";
            AddBoundedRow(row);
            rowNumber++;
        }
        Flush();
        return chunks;
    }
}

public static class TranscriptSummaryPrompt
{
    public static string Build(int scope, string transcript) => scope switch
    {
        0 => $$"""
            下面是一次已经进行中的实时文字稿中，刚刚新增或被修正的部分。请只总结这部分新内容，不要重新总结整场内容，也不要重复之前已经讲过的内容。
            请按自然主题合并新增内容，不要逐句复述，也不要一句话一个段落。只有主题发生变化时才换段；内容较少时只保留一个段落，不要为了凑数量拆分。每条控制在 1-2 句，合并连续表达同一概念的内容。
            对新增内容中的复杂概念，在对应要点中补充一句简短解释，说明它是什么、为什么重要或与上下文的关系；把解释和原要点放在同一条中。
            请直接使用以下格式：
            ### 新增内容
            - [开始时间] 新内容要点；必要时补充简短解释。
            - [开始时间] 相关因果关系、例子或结论。
            仅在这部分明确出现行动项时追加“### 待办”，没有行动项时省略。
            合并重复信息，每个主题保留 1-3 条要点；新增内容通常控制在 1-4 条要点，没有新的实质信息时不要编造内容。
            每条只能标注一个开始时间点。时间是现实世界的本地开始时间：第一条和跨天后的第一条使用 MM-dd HH:mm:ss，同一天其他条目只使用 HH:mm:ss。
            严禁输出结束时间、时间范围、时间区间、-->、至、到或起止时间之间的短横线。
            这是一份实时语音识别稿，可能存在听错、漏词、重复词、断句错误，以及机器翻译不准确的问题。
            英文原文是主要依据，中文翻译只作为辅助参考；如果两者不一致，优先依据英文上下文判断。
            对明显的同音误识别、专业术语误识别和中文误译进行合理纠正，但不要凭空补充原文没有的信息。
            只返回增量总结正文，不要解释过程，也不要提及你看到了文字稿。

            新增文字稿：
            {{transcript}}
            """,
        1 or 2 => $$"""
            请根据下面的英文实时文字稿，生成简洁、准确的简体中文总结。
            每条文字稿前的方括号是现实世界的本地开始时间，请保留这些时间信息，并在相关要点和待办后尽量标注对应时间。第一条和跨天后的第一条显示 MM-dd HH:mm:ss，同一天的其他条目只显示 HH:mm:ss。时间只表示开始时刻，不要补充结束时间。
            输出时间时只能引用一个开始时间点，例如 [09-03 11:24:18] 或 [11:25:02]。严禁输出任何结束时间、时间范围、时间区间，严禁使用“-->”“至”“到”或起止时间之间的短横线。
            这是一份实时语音识别稿，可能存在听错、漏词、重复词、断句错误，以及机器翻译不准确的问题。
            请结合上下文理解原意：英文原文是主要依据，中文翻译只作为辅助参考；如果两者不一致，优先依据英文上下文判断。
            对明显的同音误识别、专业术语误识别和中文误译进行合理纠正，但不要凭空补充原文没有的信息。
            对无法确定的内容使用保守表述，不要把猜测写成事实。
            请按自然主题组织内容，不要逐句复述，也不要把每句话拆成一个段落。全文通常分成 2-4 个主题段落；只有主题确实发生变化时才换段。每条控制在 1-2 句，合并连续表达同一概念的内容。
            对复杂概念，在对应要点中补充一句简短解释，说明它是什么、为什么重要或与前后内容的关系；必要时给出原文中出现的例子，但不要写成教科书式长篇扩展。
            每个主题段落列 1-3 个要点，合并重复信息；全文要点通常控制在 4-8 条。每条尽量以单个 [开始时间] 开头，相关解释和因果关系放在同一条中。
            请使用以下格式：
            ## 主题
            一句话概括全文主旨。

            ### 核心概念或主题一
            - [开始时间] 关键内容；复杂概念后补充简短解释。
            - [开始时间] 相关因果关系、例子或结论。

            ### 主题二
            - [开始时间] 关键内容与必要解释。
            主题标题和段落不要过度拆分；内容不足时合并主题，不要为了凑数量添加空泛要点。
            仅在存在明确行动项时输出“待办”一栏，没有行动项时省略该栏；有待办时也请标注单个 [开始时间]。
            只返回总结正文，不要解释过程，也不要提及你看到了文字稿。

            文字稿：
            {{transcript}}
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}

public static class TranscriptSummarySelection
{
    public static List<Subtitle> Select(Archive archive, int scope, int newContentStartSegmentIndex = 0)
    {
        IEnumerable<Subtitle> entries = scope switch
        {
            0 => archive.Segments.Skip(Math.Clamp(newContentStartSegmentIndex, 0, archive.Segments.Count)).SelectMany(s => s.Entries),
            2 => archive.Segments.SelectMany(s => s.Entries),
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
    public static string NewTitle(DateTime localTime) => $"课程 {localTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)}";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = NewTitle(DateTime.Now);
    public double CreatedAt { get; set; } = Now;
    public double UpdatedAt { get; set; } = Now;
    public string? Summary { get; set; }
    public Dictionary<Guid, string> SummarizedEntries { get; set; } = [];
    public List<Segment> Segments { get; set; } = [];
    public override string ToString() => Title;
}

public static class ArchiveOrdering
{
    public static List<Archive> NewestFirst(IEnumerable<Archive> archives) =>
        archives.OrderByDescending(archive => archive.UpdatedAt).ToList();
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
    private readonly HashSet<int> sourceStartSet = [], sourceEndSet = [];
    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool ReadBoolean(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && (value.ValueKind is JsonValueKind.True or JsonValueKind.False) && value.GetBoolean();
    private static bool TryReadNumber(JsonElement element, string property, out double value)
    {
        value = 0;
        return element.TryGetProperty(property, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetDouble(out value);
    }
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
        sourceStartSet.Remove(index); sourceEndSet.Remove(index);
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
        // Mac resets both provisional buffers for every WebSocket response. Treat the
        // response as the latest provisional snapshot across both lanes.
        sourceProvisional.Clear(); translationProvisional.Clear();
        if (!message.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array)
        {
            int activeRowWithoutTokenArray = Math.Max(sourceCursor, translationCursor);
            if (activeRowWithoutTokenArray < segment.Entries.Count
                && (!string.IsNullOrEmpty(sourceFinal.GetValueOrDefault(activeRowWithoutTokenArray))
                    || !string.IsNullOrEmpty(translationFinal.GetValueOrDefault(activeRowWithoutTokenArray))))
                ApplyRecognition(activeRowWithoutTokenArray);
            return;
        }
        if (tokens.EnumerateArray().Any(token => token.ValueKind != JsonValueKind.Object))
        {
            int activeRowWithInvalidTokenShape = Math.Max(sourceCursor, translationCursor);
            if (activeRowWithInvalidTokenShape < segment.Entries.Count
                && (!string.IsNullOrEmpty(sourceFinal.GetValueOrDefault(activeRowWithInvalidTokenShape))
                    || !string.IsNullOrEmpty(translationFinal.GetValueOrDefault(activeRowWithInvalidTokenShape))))
                ApplyRecognition(activeRowWithInvalidTokenShape);
            return;
        }
        int provisionalSource = sourceCursor, provisionalTranslation = translationCursor;
        bool reachedEndpoint = false;
        // Mac can split an already-open row when a final speaker change arrives,
        // but it does not create/update the current row until after this response's
        // token loop. Therefore speaker changes within a newly-created row in the
        // same response are folded into that row, using the last final metadata.
        bool canSplitSpeakerChange = sourceCursor < segment.Entries.Count
            && segment.Entries[sourceCursor].Speaker is not null
            && !string.IsNullOrWhiteSpace(sourceFinal.GetValueOrDefault(sourceCursor));
        var changedRows = new HashSet<int>();
        foreach (var token in tokens.EnumerateArray())
        {
            string? tokenText = ReadString(token, "text");
            if (string.IsNullOrEmpty(tokenText)) continue;
            string text = tokenText;
            bool translation = ReadString(token, "translation_status") == "translation";
            bool final = ReadBoolean(token, "is_final");
            if (text is "<end>" or "<fin>")
            {
                // Mac records endpoint presence for the whole WebSocket response
                // and finalizes once after consuming every token in that response.
                // Keep the flag response-scoped instead of advancing per marker.
                reachedEndpoint = true;
                continue;
            }
            if (text.Length == 0 || text.StartsWith('<')) continue;
            if (!translation && final && token.TryGetProperty("speaker", out var finalSpeaker)
                && finalSpeaker.ValueKind == JsonValueKind.String)
            {
                string label = "Speaker " + finalSpeaker.GetString();
                if (canSplitSpeakerChange && sourceCursor < segment.Entries.Count && segment.Entries[sourceCursor] is { } current
                    && !string.IsNullOrWhiteSpace(sourceFinal.GetValueOrDefault(sourceCursor))
                    && current.Speaker is not null && current.Speaker != label)
                {
                    ApplyRecognition(sourceCursor);
                    changedRows.Remove(sourceCursor);
                    finalized?.Invoke(current);
                    ForgetRow(sourceCursor);
                    sourceCursor++;
                    translationCursor = Math.Max(translationCursor, sourceCursor);
                    provisionalSource = sourceCursor;
                    provisionalTranslation = Math.Max(provisionalTranslation, translationCursor);
                    canSplitSpeakerChange = false;
                }
            }
            int index = translation ? (final ? translationCursor : provisionalTranslation) : (final ? sourceCursor : provisionalSource);
            var row = Row(index);
            if (!translation)
            {
                if (TryReadNumber(token, "start_ms", out double tokenStartMs))
                {
                    double tokenStart = tokenStartMs / 1000;
                    if (sourceStartSet.Add(index)) row.Start = tokenStart;
                    else row.Start = Math.Min(row.Start, tokenStart);
                    row.RecordedAt = segment.StartedAt + row.Start;
                }
                if (TryReadNumber(token, "end_ms", out double tokenEndMs))
                {
                    double tokenEnd = tokenEndMs / 1000;
                    row.End = sourceEndSet.Add(index) ? Math.Max(row.Start, tokenEnd) : Math.Max(row.End, tokenEnd);
                }
                string? speaker = ReadString(token, "speaker");
                if (speaker is not null && (final || row.Speaker is null)) row.Speaker = "Speaker " + speaker;
                string? language = ReadString(token, "language");
                if (language is not null && (final || row.Language is null)) row.Language = language;
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
            changedRows.Add(index);
        }
        int activeRow = Math.Max(sourceCursor, translationCursor);
        if (activeRow < segment.Entries.Count
            && (!string.IsNullOrEmpty(sourceFinal.GetValueOrDefault(activeRow))
                || !string.IsNullOrEmpty(translationFinal.GetValueOrDefault(activeRow))))
            changedRows.Add(activeRow);
        foreach (int index in changedRows.OrderBy(i => i)) ApplyRecognition(index);
        if (reachedEndpoint) FinalizeCurrent();
        segment.UpdatedAt = Archive.Now;
    }
}
