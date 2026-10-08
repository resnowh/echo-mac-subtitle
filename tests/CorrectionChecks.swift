import Foundation

enum CorrectionChecks {
    static func run() throws {
        try segmentationChecks()
        var entry = SubtitleEntry(start: 1, end: 3, english: "material", chinese: "材料", speaker: "Speaker 1", language: "en")
        let original = entry
        entry.edit(source: "maturity day", translation: "材料")
        precondition(entry.correction?.sourceLocked == true)
        precondition(entry.correction?.translationLocked == false)
        entry.applyRecognition(source: "material later", translation: "后续译文")
        precondition(entry.english == "maturity day" && entry.chinese == "后续译文")
        precondition(entry.correction?.rawSource == "material later")
        precondition(!entry.matchesCorrectionSnapshot(original))
        entry.edit(source: entry.english, translation: "到期日")
        let snapshot = entry
        entry.applyRecognition(source: "late callback", translation: "迟到翻译")
        precondition(entry.english == "maturity day" && entry.chinese == "到期日")
        entry.undoCorrection()
        precondition(entry.chinese == "后续译文")
        entry.edit(source: "maturity day", translation: "到期日")
        precondition(!entry.matchesCorrectionSnapshot(snapshot), "Same text after undo/edit must reject stale response")
        precondition(entry.speaker == "Speaker 1" && entry.language == "en" && entry.start == 1)
        let archived = ArchivedSubtitle(id: entry.id, start: entry.start, end: entry.end,
            recordedAt: nil, english: entry.english, chinese: entry.chinese, speaker: entry.speaker,
            language: entry.language, correction: entry.correction)
        let restored = try JSONDecoder().decode(ArchivedSubtitle.self, from: JSONEncoder().encode(archived))
        precondition(restored.correction?.sourceLocked == true && restored.correction?.translationLocked == true)
        precondition(restored.correction?.history.count == entry.correction?.history.count)
        var reloaded = SubtitleEntry(id: restored.id, start: restored.start, end: restored.end,
            english: restored.english, chinese: restored.chinese, correction: restored.correction)
        reloaded.applyRecognition(source: "stale after reload", translation: "迟到")
        precondition(reloaded.english == entry.english && reloaded.chinese == entry.chinese)
        var oldJSON = try JSONSerialization.jsonObject(with: JSONEncoder().encode(archived)) as! [String: Any]
        oldJSON.removeValue(forKey: "correction")
        oldJSON.removeValue(forKey: "speaker")
        oldJSON.removeValue(forKey: "language")
        let legacy = try JSONDecoder().decode(ArchivedSubtitle.self, from: JSONSerialization.data(withJSONObject: oldJSON))
        precondition(legacy.correction == nil && legacy.english == entry.english)
        let now = Date()
        let archive = TranscriptArchive(id: UUID(), title: "Fixture", createdAt: now, updatedAt: now,
            segments: [TranscriptSegment(id: UUID(), startedAt: now, updatedAt: now, entries: [restored])])
        let export = SRTExporter.archiveText(archive)
        precondition(export.contains("maturity day") && export.contains("到期日"))
        precondition(!export.contains("late callback") && export.contains("00:00:00,000 -->"))
        var multiple = archive
        let other = ArchivedSubtitle(id: UUID(), start: 1, end: 3, recordedAt: nil, english: "other", chinese: "其他")
        multiple.segments.insert(TranscriptSegment(id: UUID(), startedAt: now.addingTimeInterval(-60),
            updatedAt: now, entries: [other]), at: 0)
        reloaded.edit(source: "maturity date", translation: "到期日")
        precondition(multiple.updateCorrection(reloaded))
        precondition(multiple.segments[0].entries[0].english == "other")
        precondition(multiple.segments[1].entries[0].english == "maturity date")
        precondition(multiple.segments[1].entries[0].start == 1)
        let request = DeepSeekService().correctionRequest(apiKey: "", source: "maturity day", translation: "",
            context: "bond", terms: "maturity date", targetLanguage: "ja", translationOnly: true)!
        let body = try JSONSerialization.jsonObject(with: request.httpBody!) as! [String: Any]
        precondition((body["response_format"] as? [String: String])?["type"] == "json_object")
        precondition(body["stream"] as? Bool == false)
        let payload: [String: Any] = ["source": "maturity day", "translation": "到期日", "reason": "fixture", "uncertain": true]
        let content = String(data: try JSONSerialization.data(withJSONObject: payload), encoding: .utf8)!
        func envelope(_ reason: String, _ text: String) throws -> Data {
            try JSONSerialization.data(withJSONObject: ["choices": [["message": ["content": text], "finish_reason": reason]]])
        }
        let suggestion = try DeepSeekService.decodeCorrection(envelope("stop", content))
        precondition(suggestion.uncertain && suggestion.source == "maturity day")
        for data in [try envelope("length", content), try envelope("stop", "not JSON")] {
            do { _ = try DeepSeekService.decodeCorrection(data); preconditionFailure("Malformed response accepted") }
            catch { /* Expected rejection; never modify the transcript. */ }
        }
        print("PASS: manual field locks, late recognition, undo/revision guard, legacy/new archive, corrected SRT, structured AI response validation")
    }

    private static func segmentationChecks() throws {
        let defaults = TranscriptSegmentationConfig.defaults
        precondition(defaults.sonioxMaxEndpointDelayMilliseconds == 3_000)
        precondition(defaults.sonioxEndpointSensitivity == -0.3)
        precondition(defaults.sonioxEndpointLatencyAdjustmentLevel == 0)
        precondition(defaults.localSilenceFallbackEnabled && defaults.localSilenceThresholdSeconds == 4.5)
        precondition(defaults.localSilenceMinimumWordCount == 5)
        precondition(defaults.longSegmentFallbackEnabled && defaults.longSegmentWordThreshold == 80)
        precondition(defaults.longSegmentDurationThresholdSeconds == 90)

        let words5 = "one two three four five"
        precondition(TranscriptSegmentationPolicy.trigger(text: words5, elapsed: 1, quiet: 4.4, config: defaults) == nil)
        precondition(TranscriptSegmentationPolicy.trigger(text: words5, elapsed: 1, quiet: 4.5, config: defaults) == .silence)
        precondition(TranscriptSegmentationPolicy.trigger(text: "one two three four", elapsed: 1, quiet: 10, config: defaults) == nil)
        precondition(TranscriptSegmentationPolicy.trigger(text: "...", elapsed: 1, quiet: 0, config: defaults) == nil)

        let words80 = (0..<80).map { "word\($0)" }.joined(separator: " ")
        precondition(TranscriptSegmentationPolicy.trigger(text: words80, elapsed: 89.9, quiet: 0, config: defaults) == nil)
        precondition(TranscriptSegmentationPolicy.trigger(text: words80, elapsed: 90, quiet: 0, config: defaults) == .longSegment)
        let words79 = (0..<79).map { "word\($0)" }.joined(separator: " ")
        precondition(TranscriptSegmentationPolicy.trigger(text: words79, elapsed: 600, quiet: 0, config: defaults) == nil)

        var localOnlyDisabled = defaults
        localOnlyDisabled.localSilenceFallbackEnabled = false
        precondition(TranscriptSegmentationPolicy.trigger(text: words5, elapsed: 1, quiet: 20, config: localOnlyDisabled) == nil)
        var longOnlyDisabled = defaults
        longOnlyDisabled.longSegmentFallbackEnabled = false
        precondition(TranscriptSegmentationPolicy.trigger(text: words80, elapsed: 600, quiet: 0, config: longOnlyDisabled) == nil)
        precondition(TranscriptSegmentationPolicy.trigger(text: words5, elapsed: 1, quiet: 4.5, config: longOnlyDisabled) == .silence)

        // Endpoint markers remain authoritative even when local fallbacks are
        // disabled and the translation for this entry has not arrived yet.
        var bothDisabled = defaults
        bothDisabled.localSilenceFallbackEnabled = false
        bothDisabled.longSegmentFallbackEnabled = false
        precondition(TranscriptSegmentationPolicy.trigger(
            text: words5,
            elapsed: 0,
            quiet: 0,
            config: bothDisabled,
            endpointReached: true,
            translationEnabled: true,
            translationReady: false
        ) == .endpoint)
        precondition(TranscriptSegmentationPolicy.trigger(
            text: words5,
            elapsed: 1,
            quiet: 20,
            config: defaults,
            translationEnabled: true,
            translationReady: false
        ) == nil)
        precondition(TranscriptSegmentationPolicy.trigger(
            text: words5,
            elapsed: 1,
            quiet: 4.5,
            config: defaults,
            translationEnabled: true,
            translationReady: true
        ) == .silence)

        let bounds = TranscriptSegmentationConfig(
            sonioxMaxEndpointDelayMilliseconds: 500,
            sonioxEndpointSensitivity: -1,
            sonioxEndpointLatencyAdjustmentLevel: 3,
            localSilenceThresholdSeconds: 0.5,
            localSilenceMinimumWordCount: 100,
            longSegmentWordThreshold: 1_000,
            longSegmentDurationThresholdSeconds: 600
        )
        precondition(bounds.sonioxMaxEndpointDelayMilliseconds == 500 && bounds.sonioxEndpointSensitivity == -1)
        precondition(bounds.sonioxEndpointLatencyAdjustmentLevel == 3 && bounds.localSilenceThresholdSeconds == 0.5)
        precondition(bounds.localSilenceMinimumWordCount == 100 && bounds.longSegmentWordThreshold == 1_000)
        precondition(bounds.longSegmentDurationThresholdSeconds == 600)

        let invalid = TranscriptSegmentationConfig(
            sonioxMaxEndpointDelayMilliseconds: 499,
            sonioxEndpointSensitivity: .nan,
            sonioxEndpointLatencyAdjustmentLevel: 4,
            localSilenceThresholdSeconds: .infinity,
            localSilenceMinimumWordCount: 101,
            longSegmentWordThreshold: 9,
            longSegmentDurationThresholdSeconds: 601
        )
        precondition(invalid == defaults, "Invalid values must fall back safely")

        let suiteName = "EchoSegmentationChecks.\(UUID().uuidString)"
        let userDefaults = UserDefaults(suiteName: suiteName)!
        defer { userDefaults.removePersistentDomain(forName: suiteName) }
        precondition(TranscriptSegmentationConfig.load(from: userDefaults) == defaults)
        let saved = TranscriptSegmentationConfig(
            sonioxMaxEndpointDelayMilliseconds: 1_750,
            sonioxEndpointSensitivity: 0.4,
            sonioxEndpointLatencyAdjustmentLevel: 2,
            localSilenceThresholdSeconds: 6.5,
            localSilenceMinimumWordCount: 8,
            longSegmentWordThreshold: 120,
            longSegmentDurationThresholdSeconds: 120
        )
        saved.save(to: userDefaults)
        precondition(TranscriptSegmentationConfig.load(from: userDefaults) == saved)
        let outOfRangeJSON = Data("""
        {"sonioxMaxEndpointDelayMilliseconds":499,"sonioxEndpointSensitivity":0.2}
        """.utf8)
        userDefaults.set(outOfRangeJSON, forKey: TranscriptSegmentationConfig.userDefaultsKey)
        let repaired = TranscriptSegmentationConfig.load(from: userDefaults)
        precondition(repaired.sonioxMaxEndpointDelayMilliseconds == defaults.sonioxMaxEndpointDelayMilliseconds)
        precondition(repaired.sonioxEndpointSensitivity == 0.2)
        let nonFiniteJSON = Data("""
        {"sonioxEndpointSensitivity":"NaN","localSilenceThresholdSeconds":"Infinity"}
        """.utf8)
        userDefaults.set(nonFiniteJSON, forKey: TranscriptSegmentationConfig.userDefaultsKey)
        precondition(TranscriptSegmentationConfig.load(from: userDefaults) == defaults)
        userDefaults.set(Data("{invalid".utf8), forKey: TranscriptSegmentationConfig.userDefaultsKey)
        precondition(TranscriptSegmentationConfig.load(from: userDefaults) == defaults)

        let request = SonioxRequestBuilder.makeRequest(
            apiKey: "fixture-only",
            recognition: RecognitionConfig(),
            segmentation: saved
        )
        precondition(request["enable_endpoint_detection"] as? Bool == true)
        precondition(request["max_endpoint_delay_ms"] as? Int == 1_750)
        precondition(request["endpoint_sensitivity"] as? Double == 0.4)
        precondition(request["endpoint_latency_adjustment_level"] as? Int == 2)
        var conflictingBase: [String: Any] = ["max_endpoint_delay_ms": 3_000, "endpoint_sensitivity": -0.3]
        conflictingBase = SonioxRequestBuilder.applying(RecognitionConfig(), to: conflictingBase, segmentation: saved)
        precondition(conflictingBase["max_endpoint_delay_ms"] as? Int == 1_750)
        precondition(conflictingBase["endpoint_sensitivity"] as? Double == 0.4)
        print("PASS: segmentation triggers, endpoint priority, translation wait, defaults/bounds, UserDefaults roundtrip, Soniox request settings")
    }
}
