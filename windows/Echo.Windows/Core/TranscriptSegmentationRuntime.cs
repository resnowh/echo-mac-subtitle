namespace Echo_Windows.Core;

/// <summary>
/// Applies the active session's local segmentation policy to the current row and
/// finalizes it through the same token assembler used by the live session.
/// </summary>
public static class TranscriptSegmentationRuntime
{
    public static bool TryFinalizeCurrent(
        Segment? segment,
        TokenAssembler? assembler,
        TranscriptSegmentationSettings settings,
        double sessionElapsedSeconds,
        double quietSeconds,
        bool translationEnabled)
    {
        if (segment?.Entries.LastOrDefault() is not { } latest || assembler is null) return false;
        bool translationReady = !string.IsNullOrWhiteSpace(latest.Chinese);
        if (TranscriptSegmentationPolicy.Trigger(
            latest.English,
            Math.Max(0, sessionElapsedSeconds - latest.Start),
            quietSeconds,
            settings,
            translationEnabled: translationEnabled,
            translationReady: translationReady) is null)
            return false;

        return assembler.FinalizeCurrent();
    }
}
