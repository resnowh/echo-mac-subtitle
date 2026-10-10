import Foundation

@main
enum SegmentationPolicyChecks {
    static func main() throws {
        guard CommandLine.arguments.count == 3 else {
            fputs("usage: segmentation-policy-checks input.json output.json\n", stderr)
            exit(2)
        }

        let inputURL = URL(fileURLWithPath: CommandLine.arguments[1])
        let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
        let inputData = try Data(contentsOf: inputURL)
        let fixture = try JSONSerialization.jsonObject(with: inputData) as! [String: Any]
        let cases = fixture["cases"] as! [[String: Any]]
        let outputs = cases.map { item -> [String: String] in
            let settings = TranscriptSegmentationConfig(
                localSilenceFallbackEnabled: item["localSilenceFallbackEnabled"] as? Bool ?? true,
                localSilenceThresholdSeconds: item["localSilenceThresholdSeconds"] as? Double ?? 4.5,
                localSilenceMinimumWordCount: item["localSilenceMinimumWordCount"] as? Int ?? 5,
                longSegmentFallbackEnabled: item["longSegmentFallbackEnabled"] as? Bool ?? true,
                longSegmentWordThreshold: item["longSegmentWordThreshold"] as? Int ?? 80,
                longSegmentDurationThresholdSeconds: item["longSegmentDurationThresholdSeconds"] as? Double ?? 90
            )
            let text: String
            if let literal = item["text"] as? String {
                text = literal
            } else {
                let word = item["repeatWord"] as! String
                let count = item["repeatCount"] as! Int
                text = Array(repeating: word, count: count).joined(separator: " ")
            }
            let trigger = TranscriptSegmentationPolicy.trigger(
                text: text,
                elapsed: item["elapsedSeconds"] as? Double ?? 0,
                quiet: item["quietSeconds"] as? Double ?? 0,
                config: settings,
                endpointReached: item["endpointReached"] as? Bool ?? false,
                translationEnabled: item["translationEnabled"] as? Bool ?? false,
                translationReady: item["translationReady"] as? Bool ?? true
            )
            let name: String
            switch trigger {
            case .endpoint: name = "endpoint"
            case .silence: name = "silence"
            case .longSegment: name = "longSegment"
            case nil: name = "none"
            }
            return ["id": item["id"] as! String, "trigger": name]
        }

        let output = [
            "macSourceCommit": ProcessInfo.processInfo.environment["ECHO_MAC_SOURCE_COMMIT"] ?? "unknown",
            "transcriptModelsSha256": ProcessInfo.processInfo.environment["ECHO_MAC_TRANSCRIPT_MODELS_SHA256"] ?? "unknown",
            "cases": outputs
        ] as [String: Any]
        let outputData = try JSONSerialization.data(withJSONObject: output, options: [.prettyPrinted, .sortedKeys])
        try outputData.write(to: outputURL, options: .atomic)
        print("PASS: Mac production TranscriptSegmentationPolicy evaluated \(outputs.count) fixed parity cases.")
    }
}
