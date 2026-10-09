namespace Echo_Windows.Core;

public static class SonioxRequestBuilder
{
    public static Dictionary<string, object> Build(Preferences config, TranscriptSegmentationSettings segmentation)
    {
        segmentation = segmentation.Copy().Validate();
        var request = new Dictionary<string, object>
        {
            ["model"] = config.SonioxModel,
            ["audio_format"] = "pcm_s16le",
            ["sample_rate"] = 16000,
            ["num_channels"] = 1,
            ["enable_endpoint_detection"] = true,
            ["max_endpoint_delay_ms"] = segmentation.SonioxMaxEndpointDelayMilliseconds,
            ["endpoint_sensitivity"] = segmentation.SonioxEndpointSensitivity,
            ["endpoint_latency_adjustment_level"] = segmentation.SonioxEndpointLatencyAdjustmentLevel,
            ["enable_language_identification"] = true,
            ["enable_speaker_diarization"] = config.Speakers
        };
        if (!string.IsNullOrWhiteSpace(config.SourceLanguage))
        {
            request["language_hints"] = new[] { config.SourceLanguage };
            request["language_hints_strict"] = config.Strict;
        }
        if (config.Translate) request["translation"] = new { type = "one_way", target_language = config.TargetLanguage };
        return request;
    }
}
