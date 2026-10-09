using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Echo_Windows.Core;

public static class EchoServiceModels
{
    public const string SonioxRealtime = "stt-rt-v5";
    public const string DeepSeek = "deepseek-v4-flash";
}

public sealed class Preferences
{
    // Retained for existing settings-file compatibility; requests use the Mac-defined fixed models.
    public string SonioxModel { get; set; } = EchoServiceModels.SonioxRealtime;
    public string DeepSeekModel { get; set; } = EchoServiceModels.DeepSeek;
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "zh";
    public bool Translate { get; set; } = true;
    public bool Strict { get; set; }
    public bool Speakers { get; set; } = true;
    public bool AutoCorrectionEnabled { get; set; }
    public bool AutoSummaryEnabled { get; set; }
    public string SonioxSecret { get; set; } = "";
    public string DeepSeekSecret { get; set; } = "";
    public string CorrectionTerms { get; set; } = "";
    public string Theme { get; set; } = "Default";
    public DesktopSubtitleOverlaySettings SubtitleOverlay { get; set; } = new();
    public TranscriptSegmentationSettings Segmentation { get; set; } = new();
    public static string Protect(string value) => value.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    public static string Unprotect(string value) => value.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
    public static Preferences Load()
    {
        string file = Path.Combine(TranscriptFiles.Root, "settings.json");
        var result = File.Exists(file) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file), TranscriptFiles.Json) ?? new() : new();
        result.SubtitleOverlay ??= new();
        result.SubtitleOverlay.Validate();
        result.Segmentation ??= new();
        result.Segmentation.Validate();
        return result;
    }
    public void Save()
    {
        SubtitleOverlay ??= new();
        SubtitleOverlay.Validate();
        Segmentation ??= new();
        Segmentation.Validate();
        TranscriptFiles.AtomicWrite(Path.Combine(TranscriptFiles.Root, "settings.json"), JsonSerializer.Serialize(this, TranscriptFiles.Json));
    }
}

public sealed class TranscriptSegmentationSettings
{
    public int SonioxMaxEndpointDelayMilliseconds { get; set; } = 3000;
    public double SonioxEndpointSensitivity { get; set; } = -0.3;
    public int SonioxEndpointLatencyAdjustmentLevel { get; set; }
    public bool LocalSilenceFallbackEnabled { get; set; } = true;
    public double LocalSilenceThresholdSeconds { get; set; } = 4.5;
    public int LocalSilenceMinimumWordCount { get; set; } = 5;
    public bool LongSegmentFallbackEnabled { get; set; } = true;
    public int LongSegmentWordThreshold { get; set; } = 80;
    public double LongSegmentDurationThresholdSeconds { get; set; } = 90;

    public TranscriptSegmentationSettings Validate()
    {
        SonioxMaxEndpointDelayMilliseconds = Clamp(SonioxMaxEndpointDelayMilliseconds, 500, 3000, 3000);
        SonioxEndpointSensitivity = Clamp(SonioxEndpointSensitivity, -1, 1, -0.3);
        SonioxEndpointLatencyAdjustmentLevel = Clamp(SonioxEndpointLatencyAdjustmentLevel, 0, 3, 0);
        LocalSilenceThresholdSeconds = Clamp(LocalSilenceThresholdSeconds, .5, 20, 4.5);
        LocalSilenceMinimumWordCount = Clamp(LocalSilenceMinimumWordCount, 1, 100, 5);
        LongSegmentWordThreshold = Clamp(LongSegmentWordThreshold, 10, 1000, 80);
        LongSegmentDurationThresholdSeconds = Clamp(LongSegmentDurationThresholdSeconds, 5, 600, 90);
        return this;
    }

    public TranscriptSegmentationSettings Copy() => (TranscriptSegmentationSettings)MemberwiseClone();
    private static int Clamp(int value, int min, int max, int fallback) => value >= min && value <= max ? value : fallback;
    private static double Clamp(double value, double min, double max, double fallback) => double.IsFinite(value) && value >= min && value <= max ? value : fallback;
}

public sealed class DesktopSubtitleOverlaySettings
{
    public bool Enabled { get; set; }
    public bool ShowOriginal { get; set; } = true;
    public bool ShowTranslation { get; set; } = true;
    public double OriginalFontSize { get; set; } = 26;
    public double TranslationFontSize { get; set; } = 24;
    public double Opacity { get; set; } = 1;
    public double WidthFraction { get; set; } = .75;
    public double RetentionSeconds { get; set; } = 5;
    public double ShadowStrength { get; set; } = .35;
    public bool ClickThrough { get; set; } = true;
    public bool PositionLocked { get; set; } = true;
    public double NormalizedX { get; set; } = .5;
    public double NormalizedBottom { get; set; } = .09;

    public DesktopSubtitleOverlaySettings Validate()
    {
        ShowOriginal |= !ShowTranslation;
        OriginalFontSize = Clamp(OriginalFontSize, 16, 48, 26);
        TranslationFontSize = Clamp(TranslationFontSize, 14, 44, 24);
        Opacity = Clamp(Opacity, .35, 1, 1);
        WidthFraction = Clamp(WidthFraction, .35, .95, .75);
        RetentionSeconds = Clamp(RetentionSeconds, 1, 15, 5);
        ShadowStrength = Clamp(ShadowStrength, 0, 1, .35);
        NormalizedX = Clamp(NormalizedX, 0, 1, .5);
        NormalizedBottom = Clamp(NormalizedBottom, 0, 1, .09);
        return this;
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
