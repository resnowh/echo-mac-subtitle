#!/usr/bin/env python3
"""Extract unchanged production Soniox methods into a Mac-only parity harness."""

from pathlib import Path
import re
import sys


METHODS = (
    "handleSonioxMessage",
    "autoFinalizeIfNeeded",
    "ensureCurrentEntry",
    "updateCurrentEntry",
    "finalizeCurrentEntry",
    "elapsedSinceSessionStart",
    "refreshFullTranscript",
    "correctEconomicTerms",
    "correctEconomicTranslation",
)
DECLARATION = re.compile(r"^    private (?:static )?(?:func|var) (\w+)\b", re.MULTILINE)


def extract(source: str, name: str) -> str:
    matches = list(DECLARATION.finditer(source))
    start = next((match for match in matches if match.group(1) == name), None)
    if start is None:
        raise SystemExit(f"Production declaration not found: {name}")
    next_declaration = next((match for match in matches if match.start() > start.start()), None)
    declaration = source[start.start() : next_declaration.start() if next_declaration else len(source)].rstrip()
    declaration = re.sub(r"^(    )private ", r"\1", declaration, count=1)
    if name in {"handleSonioxMessage", "autoFinalizeIfNeeded"}:
        declaration = declaration.replace("finalizeCurrentEntry()", "recordProductionFinalization()")
    if name in {"handleSonioxMessage", "autoFinalizeIfNeeded", "elapsedSinceSessionStart"}:
        declaration = declaration.replace("Date()", "testNow")
    return declaration


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: generate_soniox_runtime_harness.py SpeechViewModel.swift output.swift")
    source = Path(sys.argv[1]).read_text(encoding="utf-8")
    output_path = Path(sys.argv[2])
    methods = "\n\n".join(extract(source, name) for name in METHODS)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(PREAMBLE + methods + POSTAMBLE, encoding="utf-8")


PREAMBLE = r'''import Foundation

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

'''


POSTAMBLE = r'''
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
'''


if __name__ == "__main__":
    main()
