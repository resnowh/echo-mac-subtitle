import Foundation

enum SonioxRequestBuilder {
    static func applying(_ recognition: RecognitionConfig, to base: [String: Any]) -> [String: Any] {
        var request = base
        // Prefer Soniox's semantic endpoint over punctuation/short gaps.
        request["max_endpoint_delay_ms"] = 3_000
        request["endpoint_sensitivity"] = -0.3
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
        context: [String: Any] = [:]
    ) -> [String: Any] {
        var request: [String: Any] = [
            "api_key": apiKey,
            "model": model,
            "audio_format": audioFormat,
            "sample_rate": sampleRate,
            "num_channels": channels,
            "enable_endpoint_detection": true,
            "max_endpoint_delay_ms": 3_000,
            "endpoint_sensitivity": -0.3,
            "enable_speaker_diarization": recognition.speakerDiarizationEnabled,
            "enable_language_identification": true
        ]

        if recognition.sourceLanguageMode == .specified {
            request["language_hints"] = [recognition.specifiedSourceLanguage]
            request["language_hints_strict"] = recognition.strictLanguageRestriction
        } else if !recognition.languageHints.isEmpty {
            request["language_hints"] = recognition.languageHints
        }

        if recognition.translationEnabled {
            request["translation"] = [
                "type": "one_way",
                "target_language": recognition.targetTranslationLanguage
            ]
        }
        if !context.isEmpty { request["context"] = context }
        return request
    }
}
