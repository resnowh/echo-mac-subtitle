import Foundation

enum CorrectionChecks {
    static func run() throws {
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
}
