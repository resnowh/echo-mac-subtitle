import Foundation

@main
struct ArchiveRoundTripChecks {
    static func main() throws {
        guard CommandLine.arguments.count == 2 else {
            fatalError("Usage: ArchiveRoundTripChecks <windows-roundtrip.json>")
        }
        let data = try Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1]))
        let archive = try TranscriptArchiveStore.decode(data)
        precondition(archive.id == UUID(uuidString: "11111111-1111-1111-1111-111111111111"))
        precondition(archive.createdAt.timeIntervalSinceReferenceDate == 0)
        precondition(archive.updatedAt.timeIntervalSinceReferenceDate == 1)
        precondition(archive.segments.count == 1 && archive.segments[0].entries.count == 2)

        let first = archive.segments[0].entries[0]
        let entry = archive.segments[0].entries[1]
        precondition(first.english.isEmpty && first.chinese == "译文先到")
        precondition(entry.recordedAt?.timeIntervalSinceReferenceDate == 2.3456)
        precondition(entry.english == "Hello" && entry.chinese == "你好")
        precondition(entry.speaker == "Speaker 1" && entry.language == "en")
        precondition(entry.correction?.rawSource == "Recognized")
        precondition(entry.correction?.sourceLocked == true && entry.correction?.translationLocked == false)
        precondition(entry.correction?.history.first?.date.timeIntervalSinceReferenceDate == 0)

        print("PASS: production Mac decoder reads Windows round-trip JSON and preserves dates, optional fields, speaker/language, and correction history")
    }
}
