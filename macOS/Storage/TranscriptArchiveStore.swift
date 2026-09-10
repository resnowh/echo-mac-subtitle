import Foundation

final class TranscriptArchiveStore {
    private let folderURL: URL

    init(folderURL: URL? = nil) {
        if let folderURL {
            self.folderURL = folderURL
        } else {
            let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first ?? FileManager.default.temporaryDirectory
            self.folderURL = support.appendingPathComponent("Echo/Archives", isDirectory: true)
        }
    }

    func load() -> [TranscriptArchive] {
        guard let urls = try? FileManager.default.contentsOfDirectory(at: folderURL, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles]) else { return [] }
        let decoder = JSONDecoder()
        return urls.filter { $0.pathExtension == "json" }.compactMap { url in
            guard let data = try? Data(contentsOf: url) else { return nil }
            return try? decoder.decode(TranscriptArchive.self, from: data)
        }.sorted { $0.updatedAt > $1.updatedAt }
    }

    func save(_ archive: TranscriptArchive) throws {
        try FileManager.default.createDirectory(at: folderURL, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(archive)
        try data.write(to: folderURL.appendingPathComponent("\(archive.id.uuidString).json"), options: .atomic)
    }
}
