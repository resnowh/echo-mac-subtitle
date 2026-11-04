import Foundation

enum SonioxConnectionState { case idle }

final class SonioxClientStub {
    func cancel() {}
}

final class OverlayFeedStub {
    func update(_ entry: SubtitleEntry, translationEnabled: Bool) {}
    func finalize(_ entry: SubtitleEntry, translationEnabled: Bool) {}
    func clear() {}
}

@MainActor
final class ProductionSonioxHarness {
    var entries: [SubtitleEntry] = []
    var currentEntryID: UUID?
    var sessionStartedAt: Date? = Date(timeIntervalSince1970: 1767225600)
    var testNow = Date(timeIntervalSince1970: 1767225600)
    var currentSpeaker: String?
    var currentLanguage: String?
    var finalEnglish = ""
    var partialEnglish = ""
    var finalChinese = ""
    var partialChinese = ""
    var currentSourceStart: TimeInterval?
    var currentSourceEnd: TimeInterval?
    var english = ""
    var chinese = ""
    var lastTokenReceivedAt: Date?
    var currentSessionFinished = false
    var isRecording = true
    var isAICorrectionEnabled = false
    var correctionGeneration = UUID()
    var correctionSuggestions: [UUID: CorrectionSuggestion] = [:]
    var correctionStatuses: [UUID: String] = [:]
    var activeRecognitionConfig = RecognitionConfig()
    var activeSegmentationConfig = TranscriptSegmentationConfig.defaults
    var desktopSubtitleOverlayFeed = OverlayFeedStub()
    var sonioxClient = SonioxClientStub()
    var sonioxConnectionState = SonioxConnectionState.idle
    var socketReady = true
    var status = ""
    var errorMessage = ""
    var finalizationCount = 0

    func errorMessageReceived(_ message: String) { errorMessage = message }
    func saveCurrentSessionFile(force: Bool = false) {}
    func requestCorrection(_ id: UUID, automatic: Bool) {}
    func recordProductionFinalization() {
        guard currentEntryID != nil else { return }
        finalizationCount += 1
        finalizeCurrentEntry()
    }

    func handleSonioxMessage(_ text: String) {
        guard let data = text.data(using: .utf8),
              let response = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        if let errorMessage = response["error_message"] as? String {
            let requestID = response["request_id"] as? String
            errorMessageReceived("\(errorMessage)\(requestID.map { "（request_id: \($0)）" } ?? "")")
            return
        }
        if response["finished"] as? Bool == true {
            recordProductionFinalization()
            saveCurrentSessionFile(force: true)
            status = "已停止"
            sonioxClient.cancel()
            socketReady = false
            sonioxConnectionState = .idle
            return
        }

        #if DEBUG
        if firstTokenSessionID != activeSessionID {
            firstTokenSessionID = activeSessionID
            MicrophoneLifecycleLog.mark("first Soniox token received")
        }
        #endif

        partialEnglish = ""
        partialChinese = ""
        lastTokenReceivedAt = testNow
        var reachedEndpoint = false
        if let tokens = response["tokens"] as? [[String: Any]] {
            for token in tokens {
                guard let tokenText = token["text"] as? String, !tokenText.isEmpty else { continue }
                if tokenText == "<end>" || tokenText == "<fin>" {
                    reachedEndpoint = true
                    continue
                }
                let translationStatus = token["translation_status"] as? String ?? "none"
                let isTranslation = translationStatus == "translation"
                let isFinal = token["is_final"] as? Bool ?? false
                if !isTranslation {
                    let tokenSpeaker = token["speaker"] as? String
                    let tokenLanguage = token["language"] as? String
                    if isFinal {
                        if let tokenSpeaker,
                           let currentSpeaker,
                           tokenSpeaker != currentSpeaker,
                           currentEntryID != nil,
                           !finalEnglish.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                            recordProductionFinalization()
                        }
                        if let tokenSpeaker { currentSpeaker = tokenSpeaker }
                        if let tokenLanguage { currentLanguage = tokenLanguage }
                    } else {
                        // Provisional speaker/language labels can change while
                        // Soniox revises the same token. Do not split a row or
                        // overwrite a settled label until the token is final.
                        if currentSpeaker == nil, let tokenSpeaker { currentSpeaker = tokenSpeaker }
                        if currentLanguage == nil, let tokenLanguage { currentLanguage = tokenLanguage }
                    }
                }
                if isTranslation {
                    isFinal ? (finalChinese += tokenText) : (partialChinese += tokenText)
                } else {
                    isFinal ? (finalEnglish += tokenText) : (partialEnglish += tokenText)
                    if let start = token["start_ms"] as? Double { currentSourceStart = min(currentSourceStart ?? start / 1000, start / 1000) }
                    if let end = token["end_ms"] as? Double { currentSourceEnd = max(currentSourceEnd ?? 0, end / 1000) }
                }
            }
        }
        if !finalEnglish.isEmpty || !partialEnglish.isEmpty || !finalChinese.isEmpty || !partialChinese.isEmpty {
            ensureCurrentEntry()
            updateCurrentEntry()
        }
        let endpointTrigger = TranscriptSegmentationPolicy.trigger(
            text: finalEnglish + partialEnglish,
            elapsed: 0,
            quiet: 0,
            config: activeSegmentationConfig,
            endpointReached: reachedEndpoint,
            translationEnabled: activeRecognitionConfig.translationEnabled,
            translationReady: !(finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        )
        if endpointTrigger == .endpoint {
            recordProductionFinalization()
            saveCurrentSessionFile()
        }
    }

    func autoFinalizeIfNeeded() {
        guard isRecording, currentEntryID != nil else { return }
        let text = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        let elapsed = elapsedSinceSessionStart - (currentSourceStart ?? elapsedSinceSessionStart)
        let quiet = lastTokenReceivedAt.map { testNow.timeIntervalSince($0) } ?? 0
        // Prefer Soniox's semantic <end>. This is only a safety fallback when
        // endpoint tokens are absent: don't split at punctuation or short pauses.
        let translationReady = !(finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        guard TranscriptSegmentationPolicy.trigger(
            text: text,
            elapsed: elapsed,
            quiet: quiet,
            config: activeSegmentationConfig,
            translationEnabled: activeRecognitionConfig.translationEnabled,
            translationReady: translationReady
        ) != nil else { return }
        recordProductionFinalization()
        saveCurrentSessionFile()
    }

    func ensureCurrentEntry() {
        guard currentEntryID == nil else { return }
        let start = currentSourceStart ?? elapsedSinceSessionStart
        let recordedAt = sessionStartedAt?.addingTimeInterval(start)
        let entry = SubtitleEntry(
            start: start,
            end: start,
            recordedAt: recordedAt,
            english: "",
            chinese: "",
            speaker: currentSpeaker.map { "Speaker \($0)" },
            language: currentLanguage
        )
        entries.append(entry)
        currentEntryID = entry.id
    }

    func updateCurrentEntry() {
        guard let currentEntryID,
              let index = entries.firstIndex(where: { $0.id == currentEntryID }) else { return }
        let previous = entries[index]
        let rawEnglish = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        let correctedEnglish = Self.correctEconomicTerms(in: rawEnglish)
        let rawChinese = (finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines)
        entries[index].applyRecognition(source: correctedEnglish,
            translation: Self.correctEconomicTranslation(rawChinese, for: correctedEnglish))
        if !entries[index].matchesCorrectionSnapshot(previous), correctionSuggestions[currentEntryID] != nil {
            correctionSuggestions[currentEntryID] = nil
            correctionStatuses[currentEntryID] = "识别稿已更新，旧建议已失效"
        }
        entries[index].speaker = currentSpeaker.map { "Speaker \($0)" }
        entries[index].language = currentLanguage
        entries[index].start = currentSourceStart ?? entries[index].start
        entries[index].end = max(entries[index].start + 0.1, currentSourceEnd ?? elapsedSinceSessionStart)
        if isRecording {
            desktopSubtitleOverlayFeed.update(entries[index], translationEnabled: activeRecognitionConfig.translationEnabled)
        }
        refreshFullTranscript()
        if currentSessionFinished { saveCurrentSessionFile() }
    }

    func finalizeCurrentEntry() {
        updateCurrentEntry()
        guard let currentEntryID,
              let index = entries.firstIndex(where: { $0.id == currentEntryID }) else { return }
        if entries[index].english.isEmpty && entries[index].chinese.isEmpty {
            entries.remove(at: index)
            desktopSubtitleOverlayFeed.clear()
        } else if isRecording {
            desktopSubtitleOverlayFeed.finalize(entries[index], translationEnabled: activeRecognitionConfig.translationEnabled)
        }
        self.currentEntryID = nil
        if isAICorrectionEnabled {
            let finishedID = currentEntryID
            let generation = correctionGeneration
            DispatchQueue.main.asyncAfter(deadline: .now() + 3) { [weak self] in
                guard let self, self.isAICorrectionEnabled, self.correctionGeneration == generation else { return }
                self.requestCorrection(finishedID, automatic: true)
            }
        }
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        currentSpeaker = nil
        currentLanguage = nil
        refreshFullTranscript()
    }

    var elapsedSinceSessionStart: TimeInterval {
        guard let sessionStartedAt else { return 0 }
        return max(0, testNow.timeIntervalSince(sessionStartedAt))
    }

    func refreshFullTranscript() {
        english = entries.map(\.english).filter { !$0.isEmpty }.joined(separator: "\n")
        chinese = entries.map(\.chinese).filter { !$0.isEmpty }.joined(separator: "\n")
    }

    static func correctEconomicTerms(in text: String) -> String {
        var result = text
        let normalizedVariants = [
            ("micro economic", "microeconomic"),
            ("macro economic", "macroeconomic"),
            ("micro-economic", "microeconomic"),
            ("macro-economic", "macroeconomic")
        ]
        for (source, target) in normalizedVariants {
            result = result.replacingOccurrences(
                of: "\\b\(source)\\b",
                with: target,
                options: [.regularExpression, .caseInsensitive]
            )
        }

        let lowercased = result.lowercased()
        let microSignals = [
            "microeconomic theory", "microeconomic analysis", "microeconomic behavior",
            "microeconomic model", "microeconomic models", "microeconomic foundations",
            "microeconomic incentives", "microeconomic decision", "microeconomic decisions"
        ]
        let macroSignals = [
            "macroeconomic policy", "macroeconomic growth", "macroeconomic indicators",
            "macroeconomic inflation", "macroeconomic unemployment", "macroeconomic gdp",
            "macroeconomic outlook", "macroeconomic conditions", "macroeconomic performance"
        ]
        let stronglyMicroeconomic = microSignals.contains { lowercased.contains($0) }
        let stronglyMacroeconomic = macroSignals.contains { lowercased.contains($0) }

        if stronglyMicroeconomic && !stronglyMacroeconomic {
            result = result.replacingOccurrences(
                of: "\\bmacroeconomics?\\b",
                with: "microeconomic",
                options: [.regularExpression, .caseInsensitive]
            )
        } else if stronglyMacroeconomic && !stronglyMicroeconomic {
            result = result.replacingOccurrences(
                of: "\\bmicroeconomics?\\b",
                with: "macroeconomic",
                options: [.regularExpression, .caseInsensitive]
            )
        }

        // In an economics/calculus lecture, Soniox can hear “derivative” as
        // “duty”. Correct only strong calculus phrases so genuine tax or
        // obligation uses of “duty” remain unchanged.
        let derivativePhrases = [
            "first duty", "second duty", "partial duty",
            "duty of", "duty with respect to", "take the duty", "duty function",
            "duties of", "first duties", "second duties", "partial duties"
        ]
        if derivativePhrases.contains(where: { lowercased.contains($0) }) {
            result = result.replacingOccurrences(
                of: "\\bfirst duties?\\b",
                with: "first derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bsecond duties?\\b",
                with: "second derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bpartial duties?\\b",
                with: "partial derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bduties\\b",
                with: "derivatives",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bduty\\b",
                with: "derivative",
                options: [.regularExpression, .caseInsensitive]
            )
        }

        // “hand” may arrive as the truncated token “han”. Limit this fix to
        // recognizable phrases so a name such as “Han” is left untouched.
        let handPhrases = [
            "on the other han", "on the one han", "one han", "other han",
            "right han", "left han", "han side", "at han"
        ]
        if handPhrases.contains(where: { result.lowercased().contains($0) }) {
            result = result.replacingOccurrences(
                of: "\\bhan\\b",
                with: "hand",
                options: [.regularExpression, .caseInsensitive]
            )
        }
        return result
    }

    static func correctEconomicTranslation(_ text: String, for english: String) -> String {
        let lowercasedEnglish = english.lowercased()
        let isDerivativeContext = lowercasedEnglish.contains("derivative")
            || lowercasedEnglish.contains("differentiate")
            || lowercasedEnglish.contains("with respect to")
        guard isDerivativeContext else { return text }
        return text.replacingOccurrences(of: "关税", with: "导数")
    }
}

@main
struct SonioxRuntimeFixtureRunner {
    @MainActor
    static func main() throws {
        guard CommandLine.arguments.count == 3 else {
            fputs("usage: soniox-runtime-checks input.json output.json\n", stderr)
            exit(2)
        }
        let inputURL = URL(fileURLWithPath: CommandLine.arguments[1])
        let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
        let inputData = try Data(contentsOf: inputURL)
        let fixture = try JSONSerialization.jsonObject(with: inputData) as! [String: Any]
        let model = ProductionSonioxHarness()
        var outputs: [[String: Any]] = []

        if fixture["mode"] as? String == "two-hour-stress" {
            func send(_ tokens: [[String: Any]]) throws {
                let messageData = try JSONSerialization.data(withJSONObject: ["tokens": tokens], options: [.sortedKeys])
                model.handleSonioxMessage(String(data: messageData, encoding: .utf8)!)
            }
            for index in 0..<7200 {
                let start = Double(index)
                try send([["text": "Partial \(index)", "is_final": false,
                           "start_ms": start * 1000, "end_ms": (start + 1) * 1000]])
                try send([
                    ["text": "Lecture point \(index).", "is_final": true,
                     "start_ms": start * 1000, "end_ms": (start + 1) * 1000,
                     "speaker": "1", "language": "en"],
                    ["text": "课程点 \(index)。", "is_final": true, "translation_status": "translation"],
                    ["text": "<end>", "is_final": true]
                ])
                if (index + 1) % 1200 == 0 { print("Mac production stress: \(index + 1)/7200 rows") }
            }
            let expected = model.entries.map { entry -> [String: Any] in
                var value: [String: Any] = [
                    "english": entry.english,
                    "chinese": entry.chinese,
                    "start": entry.start,
                    "end": entry.end
                ]
                if let speaker = entry.speaker { value["speaker"] = speaker }
                if let language = entry.language { value["language"] = language }
                return value
            }
            outputs.append([
                "expectedFinalizations": model.finalizationCount,
                "expected": expected
            ])
        } else if fixture["mode"] as? String == "segmentation-session" {
            let baseUnix = fixture["sessionStartedAtUnixSeconds"] as! Double
            let baseDate = Date(timeIntervalSince1970: baseUnix)
            model.sessionStartedAt = baseDate
            model.testNow = baseDate
            let config = fixture["config"] as! [String: Any]
            model.activeSegmentationConfig = TranscriptSegmentationConfig(
                localSilenceFallbackEnabled: config["localSilenceFallbackEnabled"] as? Bool ?? true,
                localSilenceThresholdSeconds: config["localSilenceThresholdSeconds"] as? Double ?? 4.5,
                localSilenceMinimumWordCount: config["localSilenceMinimumWordCount"] as? Int ?? 5,
                longSegmentFallbackEnabled: config["longSegmentFallbackEnabled"] as? Bool ?? true,
                longSegmentWordThreshold: config["longSegmentWordThreshold"] as? Int ?? 80,
                longSegmentDurationThresholdSeconds: config["longSegmentDurationThresholdSeconds"] as? Double ?? 90
            )
            var recognition = RecognitionConfig()
            recognition.translationEnabled = config["translationEnabled"] as? Bool ?? true
            model.activeRecognitionConfig = recognition
            let events = fixture["events"] as! [[String: Any]]
            for (index, event) in events.enumerated() {
                let elapsed = event["elapsedSeconds"] as? Double ?? 0
                model.testNow = baseDate.addingTimeInterval(elapsed)
                if event["kind"] as? String == "response" {
                    let message = event["message"] as! [String: Any]
                    let data = try JSONSerialization.data(withJSONObject: message, options: [.sortedKeys])
                    model.handleSonioxMessage(String(data: data, encoding: .utf8)!)
                } else {
                    let quiet = event["quietSeconds"] as? Double ?? 0
                    model.lastTokenReceivedAt = model.testNow.addingTimeInterval(-quiet)
                    model.autoFinalizeIfNeeded()
                }
                let expected = model.entries.map { entry -> [String: Any] in
                    var value: [String: Any] = [
                        "english": entry.english,
                        "chinese": entry.chinese,
                        "start": entry.start,
                        "end": entry.end
                    ]
                    if let recordedAt = entry.recordedAt {
                        value["recordedAtUnixSeconds"] = recordedAt.timeIntervalSince1970
                    }
                    if let speaker = entry.speaker { value["speaker"] = speaker }
                    if let language = entry.language { value["language"] = language }
                    return value
                }
                outputs.append([
                    "kind": event["kind"] as? String ?? "tick",
                    "finalizationCount": model.finalizationCount,
                    "expected": expected
                ])
                print("Mac production segmentation event \(index + 1): entries=\(expected.count), finalizations=\(model.finalizationCount)")
            }
        } else {
            let events = fixture["events"] as! [[String: Any]]
            for (index, event) in events.enumerated() {
                let message = event["message"] as! [String: Any]
                let messageData = try JSONSerialization.data(withJSONObject: message, options: [.sortedKeys])
                model.handleSonioxMessage(String(data: messageData, encoding: .utf8)!)
                let expected = model.entries.map { entry -> [String: Any] in
                    var value: [String: Any] = [
                        "english": entry.english,
                        "chinese": entry.chinese,
                        "start": entry.start,
                        "end": entry.end
                    ]
                    if let speaker = entry.speaker { value["speaker"] = speaker }
                    if let language = entry.language { value["language"] = language }
                    return value
                }
                outputs.append([
                    "message": message,
                    "expectedFinalizations": model.finalizationCount,
                    "expected": expected
                ])
                print("Mac production Soniox handler response \(index + 1): entries=\(expected.count), finalizations=\(model.finalizationCount)")
            }
        }

        let result: [String: Any] = [
            "source": "macOS/ViewModels/SpeechViewModel.swift production method extraction",
            "sourceCommit": ProcessInfo.processInfo.environment["ECHO_SOURCE_SHA"] ?? "unknown",
            "speechViewModelSha256": ProcessInfo.processInfo.environment["ECHO_SPEECH_VIEW_MODEL_SHA256"] ?? "unknown",
            "mode": fixture["mode"] as? String ?? "sequence",
            "events": outputs
        ]
        let outputData = try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys])
        try FileManager.default.createDirectory(at: outputURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try outputData.write(to: outputURL, options: .atomic)
        print("Mac production Soniox fixture: \(outputData.count) bytes")
    }
}
