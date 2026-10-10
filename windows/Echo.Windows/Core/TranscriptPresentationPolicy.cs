namespace Echo_Windows.Core;

/// <summary>Resolves transcript metadata presentation against the active session or saved preferences.</summary>
public static class TranscriptPresentationPolicy
{
    public static bool SpeakerMetadataVisible(bool isRecording, bool activeSessionSetting, bool savedSetting) =>
        isRecording ? activeSessionSetting : savedSetting;
}
