namespace Echo_Windows.Core;

/// <summary>
/// Captures the recognition options that affect an AI correction request. A response
/// created for an older language configuration must not be offered as a current suggestion.
/// </summary>
public sealed record CorrectionRecognitionSnapshot(
    string SourceLanguage,
    string TargetLanguage,
    bool Translate,
    bool Strict,
    bool Speakers)
{
    public static CorrectionRecognitionSnapshot Capture(Preferences preferences) => new(
        preferences.SourceLanguage,
        preferences.TargetLanguage,
        preferences.Translate,
        preferences.Strict,
        preferences.Speakers);

    public bool Matches(Preferences preferences) => this == Capture(preferences);
}
