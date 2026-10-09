namespace Echo_Windows.Core;

public enum TranscriptSegmentationTrigger { Endpoint, Silence, LongSegment }

public static class TranscriptSegmentationPolicy
{
    public static TranscriptSegmentationTrigger? Trigger(
        string text, double elapsedSeconds, double quietSeconds,
        TranscriptSegmentationSettings settings, bool endpointReached = false,
        bool translationEnabled = false, bool translationReady = true)
    {
        if (endpointReached) return TranscriptSegmentationTrigger.Endpoint;
        if (string.IsNullOrWhiteSpace(text) || (translationEnabled && !translationReady)) return null;
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (settings.LocalSilenceFallbackEnabled && words >= settings.LocalSilenceMinimumWordCount
            && quietSeconds >= settings.LocalSilenceThresholdSeconds)
            return TranscriptSegmentationTrigger.Silence;
        if (settings.LongSegmentFallbackEnabled && words >= settings.LongSegmentWordThreshold
            && elapsedSeconds >= settings.LongSegmentDurationThresholdSeconds)
            return TranscriptSegmentationTrigger.LongSegment;
        return null;
    }
}
