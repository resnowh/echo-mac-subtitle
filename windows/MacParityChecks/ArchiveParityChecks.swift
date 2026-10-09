import Foundation

@main
enum ArchiveParityChecks {
    static func main() throws {
        let arguments = Array(CommandLine.arguments.dropFirst())
        guard let mode = arguments.first else {
            throw ParityError.message("Expected generate <folder> or verify <folder>.")
        }
        switch mode {
        case "generate":
            guard arguments.count == 2 else { throw ParityError.message("generate requires an output folder.") }
            try generate(at: URL(fileURLWithPath: arguments[1], isDirectory: true))
        case "verify":
            guard arguments.count == 2 else { throw ParityError.message("verify requires a fixture folder.") }
            try verify(at: URL(fileURLWithPath: arguments[1], isDirectory: true))
        default:
            throw ParityError.message("Unknown mode: \(mode)")
        }
    }

    private static func generate(at folder: URL) throws {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let storeFolder = folder.deletingLastPathComponent()
            .appendingPathComponent(folder.lastPathComponent + ".production-store", isDirectory: true)
        let archive = fixtureArchive()
        let store = TranscriptArchiveStore(folderURL: storeFolder)
        try store.save(archive)
        guard let loaded = store.load().first, loaded.id == archive.id else {
            throw ParityError.message("Mac production ArchiveStore could not reload its generated fixture.")
        }

        let productionJSON = storeFolder.appendingPathComponent("\(archive.id.uuidString).json")
        try FileManager.default.copyItem(at: productionJSON, to: folder.appendingPathComponent("mac-archive.json"))
        try Data(SRTExporter.archiveText(loaded).utf8).write(to: folder.appendingPathComponent("mac-archive.srt"), options: .atomic)
        print("PASS: Mac production ArchiveStore encoded and decoded a two-segment fixture; production SRTExporter emitted its SRT.")
    }

    private static func verify(at folder: URL) throws {
        let decoder = JSONDecoder()
        let macURL = folder.appendingPathComponent("mac-archive.json")
        let windowsURL = folder.appendingPathComponent("windows-roundtrip.json")
        let expectedSRTURL = folder.appendingPathComponent("mac-archive.srt")
        let macArchive = try decoder.decode(TranscriptArchive.self, from: Data(contentsOf: macURL))

        let windowsStoreFolder = folder.appendingPathComponent("windows-production-decode", isDirectory: true)
        try FileManager.default.createDirectory(at: windowsStoreFolder, withIntermediateDirectories: true)
        let windowsDecodeURL = windowsStoreFolder.appendingPathComponent("\(macArchive.id.uuidString).json")
        try FileManager.default.copyItem(at: windowsURL, to: windowsDecodeURL)
        let windowsArchiveStore = TranscriptArchiveStore(folderURL: windowsStoreFolder)
        guard let windowsArchive = windowsArchiveStore.load().first else {
            throw ParityError.message("Mac production ArchiveStore could not decode the Windows round-trip JSON.")
        }

        try requireEquivalent(macArchive, windowsArchive)
        let expectedSRT = try String(contentsOf: expectedSRTURL, encoding: .utf8)
        let macSRT = SRTExporter.archiveText(macArchive)
        let windowsSRT = SRTExporter.archiveText(windowsArchive)
        guard expectedSRT == macSRT, macSRT == windowsSRT else {
            throw ParityError.message("Mac production SRTExporter output differs after the Windows Archive round trip.")
        }
        print("PASS: Mac production ArchiveStore decoded the Windows two-segment round trip; IDs, dates, optional metadata, correction history, and SRT match.")
    }

    private static func fixtureArchive() -> TranscriptArchive {
        let correction = SubtitleCorrection(
            rawSource: "Recognized",
            rawTranslation: "识别译文",
            sourceLocked: true,
            translationLocked: false,
            revision: uuid("55555555-5555-5555-5555-555555555555"),
            history: [.init(source: "Earlier", translation: "之前", date: date(0))]
        )
        let firstSegment = TranscriptSegment(
            id: uuid("22222222-2222-2222-2222-222222222222"),
            startedAt: date(0),
            updatedAt: date(1),
            entries: [
                ArchivedSubtitle(id: uuid("33333333-3333-3333-3333-333333333333"), start: 0, end: 1,
                    recordedAt: date(0), english: "", chinese: "译文先到"),
                ArchivedSubtitle(id: uuid("44444444-4444-4444-4444-444444444444"), start: 2.3456, end: 3.5801,
                    recordedAt: date(2.3456), english: "Hello", chinese: "你好", speaker: "Speaker 1", language: "en", correction: correction)
            ]
        )
        let secondSegment = TranscriptSegment(
            id: uuid("66666666-6666-6666-6666-666666666666"),
            startedAt: date(100),
            updatedAt: date(201),
            entries: [
                ArchivedSubtitle(id: uuid("77777777-7777-7777-7777-777777777777"), start: 0, end: 1,
                    recordedAt: date(100), english: "第二段", chinese: "Second segment", speaker: "Speaker 2", language: "zh")
            ]
        )
        return TranscriptArchive(
            id: uuid("11111111-1111-1111-1111-111111111111"),
            title: "Mac multi-segment production fixture",
            createdAt: date(0),
            updatedAt: date(201),
            segments: [firstSegment, secondSegment]
        )
    }

    private static func requireEquivalent(_ lhs: TranscriptArchive, _ rhs: TranscriptArchive) throws {
        guard lhs.id == rhs.id, lhs.title == rhs.title,
              close(lhs.createdAt, rhs.createdAt), close(lhs.updatedAt, rhs.updatedAt),
              lhs.segments.count == rhs.segments.count else {
            throw ParityError.message("Windows round-trip changed Archive identity, dates, title, or segment count.")
        }
        for (leftSegment, rightSegment) in zip(lhs.segments, rhs.segments) {
            guard leftSegment.id == rightSegment.id,
                  close(leftSegment.startedAt, rightSegment.startedAt), close(leftSegment.updatedAt, rightSegment.updatedAt),
                  leftSegment.entries.count == rightSegment.entries.count else {
                throw ParityError.message("Windows round-trip changed a Segment identity, date, or entry count.")
            }
            for (leftEntry, rightEntry) in zip(leftSegment.entries, rightSegment.entries) {
                guard leftEntry.id == rightEntry.id, leftEntry.start == rightEntry.start, leftEntry.end == rightEntry.end,
                      sameDate(leftEntry.recordedAt, rightEntry.recordedAt), leftEntry.english == rightEntry.english,
                      leftEntry.chinese == rightEntry.chinese, leftEntry.speaker == rightEntry.speaker,
                      leftEntry.language == rightEntry.language, sameCorrection(leftEntry.correction, rightEntry.correction) else {
                    throw ParityError.message("Windows round-trip changed a subtitle field or correction history.")
                }
            }
        }
    }

    private static func sameCorrection(_ lhs: SubtitleCorrection?, _ rhs: SubtitleCorrection?) -> Bool {
        switch (lhs, rhs) {
        case (nil, nil): return true
        case let (left?, right?):
            return left.rawSource == right.rawSource && left.rawTranslation == right.rawTranslation
                && left.sourceLocked == right.sourceLocked && left.translationLocked == right.translationLocked
                && left.revision == right.revision && left.history.count == right.history.count
                && zip(left.history, right.history).allSatisfy { pair in
                    pair.0.source == pair.1.source && pair.0.translation == pair.1.translation && close(pair.0.date, pair.1.date)
                }
        default: return false
        }
    }

    private static func sameDate(_ lhs: Date?, _ rhs: Date?) -> Bool {
        switch (lhs, rhs) {
        case (nil, nil): return true
        case let (left?, right?): return close(left, right)
        default: return false
        }
    }

    private static func close(_ lhs: Date, _ rhs: Date) -> Bool {
        abs(lhs.timeIntervalSinceReferenceDate - rhs.timeIntervalSinceReferenceDate) < 0.001
    }

    private static func date(_ appleSeconds: TimeInterval) -> Date {
        Date(timeIntervalSinceReferenceDate: appleSeconds)
    }

    private static func uuid(_ value: String) -> UUID {
        guard let id = UUID(uuidString: value) else { fatalError("Invalid fixed fixture UUID: \(value)") }
        return id
    }
}

enum ParityError: Error, CustomStringConvertible {
    case message(String)
    var description: String { if case let .message(text) = self { return text }; return "Archive parity failed." }
}
