import Foundation

/// Optional on older subtitles. Original recognition and undo history never
/// replace the effective text consumed by SRT and summary.
struct SubtitleCorrection: Codable {
    var rawSource: String
    var rawTranslation: String
    var sourceLocked = false
    var translationLocked = false
    var revision = UUID()
    var history: [Revision] = []

    struct Revision: Codable {
        var source: String
        var translation: String
        var date: Date
    }
}

struct CorrectionSuggestion: Decodable {
    let source: String
    let translation: String
    let reason: String
    let uncertain: Bool
}

struct SubtitleEntry: Identifiable {
    let id: UUID
    var start: TimeInterval
    var end: TimeInterval
    var recordedAt: Date?
    var english: String
    var chinese: String
    var speaker: String?
    var language: String?
    var correction: SubtitleCorrection?

    init(
        id: UUID = UUID(),
        start: TimeInterval,
        end: TimeInterval,
        recordedAt: Date? = nil,
        english: String,
        chinese: String,
        speaker: String? = nil,
        language: String? = nil,
        correction: SubtitleCorrection? = nil
    ) {
        self.id = id
        self.start = start
        self.end = end
        self.recordedAt = recordedAt
        self.english = english
        self.chinese = chinese
        self.speaker = speaker
        self.language = language
        self.correction = correction
    }

    mutating func edit(source: String, translation: String) {
        guard source != english || translation != chinese else { return }
        var state = correction ?? SubtitleCorrection(rawSource: english, rawTranslation: chinese)
        state.history.append(.init(source: english, translation: chinese, date: Date()))
        if source != english { state.sourceLocked = true }
        if translation != chinese { state.translationLocked = true }
        state.revision = UUID()
        english = source
        chinese = translation
        correction = state
    }

    mutating func undoCorrection() {
        guard var state = correction, let previous = state.history.popLast() else { return }
        english = previous.source
        chinese = previous.translation
        // Undo is an explicit human choice too; late tokens must not undo it.
        state.sourceLocked = true
        state.translationLocked = true
        state.revision = UUID()
        correction = state
    }

    mutating func applyRecognition(source: String, translation: String) {
        if correction != nil {
            correction?.rawSource = source
            correction?.rawTranslation = translation
        }
        if correction?.sourceLocked != true { english = source }
        if correction?.translationLocked != true { chinese = translation }
    }

    func matchesCorrectionSnapshot(_ snapshot: SubtitleEntry) -> Bool {
        id == snapshot.id && english == snapshot.english && chinese == snapshot.chinese
            && correction?.revision == snapshot.correction?.revision
    }
}

struct ArchivedSubtitle: Codable, Identifiable {
    let id: UUID
    var start: TimeInterval
    var end: TimeInterval
    var recordedAt: Date?
    var english: String
    var chinese: String
    var speaker: String?
    var language: String?
    var correction: SubtitleCorrection?

    init(
        id: UUID,
        start: TimeInterval,
        end: TimeInterval,
        recordedAt: Date?,
        english: String,
        chinese: String,
        speaker: String? = nil,
        language: String? = nil,
        correction: SubtitleCorrection? = nil
    ) {
        self.id = id
        self.start = start
        self.end = end
        self.recordedAt = recordedAt
        self.english = english
        self.chinese = chinese
        self.speaker = speaker
        self.language = language
        self.correction = correction
    }
}

struct TranscriptSegment: Codable, Identifiable {
    let id: UUID
    var startedAt: Date
    var updatedAt: Date
    var entries: [ArchivedSubtitle]
}

struct TranscriptArchive: Codable, Identifiable {
    let id: UUID
    var title: String
    let createdAt: Date
    var updatedAt: Date
    var segments: [TranscriptSegment]

    mutating func updateCorrection(_ entry: SubtitleEntry) -> Bool {
        for segmentIndex in segments.indices {
            if let index = segments[segmentIndex].entries.firstIndex(where: { $0.id == entry.id }) {
                segments[segmentIndex].entries[index].english = entry.english
                segments[segmentIndex].entries[index].chinese = entry.chinese
                segments[segmentIndex].entries[index].correction = entry.correction
                segments[segmentIndex].updatedAt = Date()
                updatedAt = Date()
                return true
            }
        }
        return false
    }
}

enum AudioInputMode: String, CaseIterable, Identifiable {
    case computer = "computer"
    case microphone = "microphone"
    case computerAndMicrophone = "computerAndMicrophone"

    var id: String { rawValue }

    var title: String {
        switch self {
        case .computer: return "电脑音频"
        case .microphone: return "话筒"
        case .computerAndMicrophone: return "电脑音频和话筒"
        }
    }

    var icon: String {
        switch self {
        case .computer: return "speaker.wave.2.fill"
        case .microphone: return "mic.fill"
        case .computerAndMicrophone: return "speaker.and.mic.fill"
        }
    }

    var requiresMicrophone: Bool { self == .microphone || self == .computerAndMicrophone }
    var includesComputerAudio: Bool { self == .computer || self == .computerAndMicrophone }
}

enum AppThemeMode: String, CaseIterable, Identifiable {
    case light
    case dark
    case system

    var id: String { rawValue }
    var title: String {
        switch self {
        case .light: return "浅色"
        case .dark: return "深色"
        case .system: return "跟随系统"
        }
    }
    var icon: String {
        switch self {
        case .light: return "sun.max.fill"
        case .dark: return "moon.fill"
        case .system: return "circle.lefthalf.filled"
        }
    }
}

struct LanguageOption: Identifiable, Hashable {
    let code: String
    let title: String

    var id: String { code }

    static let supported: [LanguageOption] = [
        LanguageOption(code: "en", title: "English"),
        LanguageOption(code: "zh", title: "简体中文"),
        LanguageOption(code: "ja", title: "日本語"),
        LanguageOption(code: "ko", title: "한국어"),
        LanguageOption(code: "es", title: "Español"),
        LanguageOption(code: "fr", title: "Français"),
        LanguageOption(code: "de", title: "Deutsch"),
        LanguageOption(code: "it", title: "Italiano"),
        LanguageOption(code: "pt", title: "Português"),
        LanguageOption(code: "ru", title: "Русский"),
        LanguageOption(code: "ar", title: "العربية"),
        LanguageOption(code: "hi", title: "हिन्दी")
    ]
}

enum SourceLanguageMode: String, CaseIterable, Identifiable {
    case automatic
    case specified
    var id: String { rawValue }
    var title: String { self == .automatic ? "自动识别" : "指定语言" }
}

struct RecognitionConfig: Equatable {
    var sourceLanguageMode: SourceLanguageMode = .specified
    var specifiedSourceLanguage = "en"
    var languageHints = ["en"]
    var strictLanguageRestriction = false
    var translationEnabled = true
    var targetTranslationLanguage = "zh"
    var speakerDiarizationEnabled = true
}
