import Foundation

enum SRTExporter {
    static func sessionText(entries: [SubtitleEntry], base: TimeInterval) -> String {
        entries.enumerated().map { index, entry in
            let start = max(0, entry.start - base)
            let end = max(start + 0.5, entry.end - base)
            return block(number: index + 1, start: start, end: end, entry: entry)
        }.joined(separator: "\n")
    }

    static func archiveText(_ archive: TranscriptArchive) -> String {
        let segments = archive.segments.sorted { $0.startedAt < $1.startedAt }
        let reference = segments.flatMap { segment in
            segment.entries.map { $0.recordedAt ?? segment.startedAt.addingTimeInterval($0.start) }
        }.min() ?? archive.createdAt
        var lastEnd = 0.0
        var number = 1
        var blocks: [String] = []

        for segment in segments {
            for entry in segment.entries where !entry.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                let startDate = entry.recordedAt ?? segment.startedAt.addingTimeInterval(entry.start)
                let start = max(lastEnd, startDate.timeIntervalSince(reference))
                let end = max(start + 0.5, start + max(0.5, entry.end - entry.start))
                blocks.append(block(number: number, start: start, end: end, speaker: entry.speaker, english: entry.english, chinese: entry.chinese))
                number += 1
                lastEnd = end
            }
        }
        return blocks.joined(separator: "\n")
    }

    private static func block(number: Int, start: TimeInterval, end: TimeInterval, entry: SubtitleEntry) -> String {
        return block(number: number, start: start, end: end, speaker: entry.speaker, english: entry.english, chinese: entry.chinese)
    }

    private static func block(number: Int, start: TimeInterval, end: TimeInterval, speaker: String?, english: String, chinese: String) -> String {
        let original = english.trimmingCharacters(in: .whitespacesAndNewlines)
        let translated = chinese.trimmingCharacters(in: .whitespacesAndNewlines)
        let speakerPrefix = speaker.map { "[\($0)]\n" } ?? ""
        let body = translated.isEmpty ? "\(speakerPrefix)\(original)" : "\(speakerPrefix)\(original)\n\(translated)"
        return "\(number)\n\(timestamp(start)) --> \(timestamp(end))\n\(body)\n"
    }

    private static func timestamp(_ seconds: TimeInterval) -> String {
        let milliseconds = max(0, Int(seconds * 1000))
        return String(format: "%02d:%02d:%02d,%03d", milliseconds / 3_600_000, (milliseconds / 60_000) % 60, (milliseconds / 1000) % 60, milliseconds % 1000)
    }
}
