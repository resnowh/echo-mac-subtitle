import Foundation

enum SonioxRequestBuilder {
    static func applying(
        _ recognition: RecognitionConfig,
        to base: [String: Any],
        segmentation: TranscriptSegmentationConfig = .defaults
    ) -> [String: Any] {
        var request = base
        let segmentation = segmentation.validated()
        request["enable_endpoint_detection"] = true
        request["max_endpoint_delay_ms"] = segmentation.sonioxMaxEndpointDelayMilliseconds
        request["endpoint_sensitivity"] = segmentation.sonioxEndpointSensitivity
        request["endpoint_latency_adjustment_level"] = segmentation.sonioxEndpointLatencyAdjustmentLevel
        request["enable_speaker_diarization"] = recognition.speakerDiarizationEnabled
        request["enable_language_identification"] = true
        if recognition.sourceLanguageMode == .specified {
            request["language_hints"] = [recognition.specifiedSourceLanguage]
            request["language_hints_strict"] = recognition.strictLanguageRestriction
        } else if recognition.languageHints.isEmpty {
            request.removeValue(forKey: "language_hints")
            request.removeValue(forKey: "language_hints_strict")
        } else {
            request["language_hints"] = recognition.languageHints
            request.removeValue(forKey: "language_hints_strict")
        }
        if recognition.translationEnabled {
            request["translation"] = ["type": "one_way", "target_language": recognition.targetTranslationLanguage]
        } else {
            request.removeValue(forKey: "translation")
        }
        return request
    }

    static func makeRequest(
        apiKey: String,
        model: String = "stt-rt-v5",
        audioFormat: String = "pcm_s16le",
        sampleRate: Int = 16_000,
        channels: Int = 1,
        recognition: RecognitionConfig,
        segmentation: TranscriptSegmentationConfig = .defaults,
        context: [String: Any] = [:]
    ) -> [String: Any] {
        var request: [String: Any] = [
            "api_key": apiKey,
            "model": model,
            "audio_format": audioFormat,
            "sample_rate": sampleRate,
            "num_channels": channels
        ]
        if !context.isEmpty { request["context"] = context }
        return applying(recognition, to: request, segmentation: segmentation)
    }
}
