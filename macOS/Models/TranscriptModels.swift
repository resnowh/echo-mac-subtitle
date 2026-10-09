import Foundation

enum TranscriptSegmentationPolicy {
    enum Trigger: Equatable {
        case endpoint
        case silence
        case longSegment
    }

    static func trigger(
        text: String,
        elapsed: TimeInterval,
        quiet: TimeInterval,
        config: TranscriptSegmentationConfig,
        endpointReached: Bool = false,
        translationEnabled: Bool = false,
        translationReady: Bool = true
    ) -> Trigger? {
        // A semantic endpoint is authoritative and is never blocked by local
        // fallback settings or a not-yet-arrived translation.
        if endpointReached { return .endpoint }
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !translationEnabled || translationReady else { return nil }
        let words = text.split(whereSeparator: \.isWhitespace).count
        if config.localSilenceFallbackEnabled,
           words >= config.localSilenceMinimumWordCount,
           quiet >= config.localSilenceThresholdSeconds {
            return .silence
        }
        if config.longSegmentFallbackEnabled,
           words >= config.longSegmentWordThreshold,
           elapsed >= config.longSegmentDurationThresholdSeconds {
            return .longSegment
        }
        return nil
    }
}

struct TranscriptSegmentationConfig: Codable, Equatable {
    static let userDefaultsKey = "transcriptSegmentationConfig.v1"

    var sonioxMaxEndpointDelayMilliseconds: Int
    var sonioxEndpointSensitivity: Double
    var sonioxEndpointLatencyAdjustmentLevel: Int
    var localSilenceFallbackEnabled: Bool
    var localSilenceThresholdSeconds: Double
    var localSilenceMinimumWordCount: Int
    var longSegmentFallbackEnabled: Bool
    var longSegmentWordThreshold: Int
    var longSegmentDurationThresholdSeconds: Double

    static let defaults = TranscriptSegmentationConfig(
        sonioxMaxEndpointDelayMilliseconds: 3_000,
        sonioxEndpointSensitivity: -0.3,
        sonioxEndpointLatencyAdjustmentLevel: 0,
        localSilenceFallbackEnabled: true,
        localSilenceThresholdSeconds: 4.5,
        localSilenceMinimumWordCount: 5,
        longSegmentFallbackEnabled: true,
        longSegmentWordThreshold: 80,
        longSegmentDurationThresholdSeconds: 90
    )

    init(
        sonioxMaxEndpointDelayMilliseconds: Int = 3_000,
        sonioxEndpointSensitivity: Double = -0.3,
        sonioxEndpointLatencyAdjustmentLevel: Int = 0,
        localSilenceFallbackEnabled: Bool = true,
        localSilenceThresholdSeconds: Double = 4.5,
        localSilenceMinimumWordCount: Int = 5,
        longSegmentFallbackEnabled: Bool = true,
        longSegmentWordThreshold: Int = 80,
        longSegmentDurationThresholdSeconds: Double = 90
    ) {
        self.sonioxMaxEndpointDelayMilliseconds = Self.validated(sonioxMaxEndpointDelayMilliseconds, range: 500...3_000, fallback: 3_000)
        self.sonioxEndpointSensitivity = Self.validated(sonioxEndpointSensitivity, range: -1...1, fallback: -0.3)
        self.sonioxEndpointLatencyAdjustmentLevel = Self.validated(sonioxEndpointLatencyAdjustmentLevel, range: 0...3, fallback: 0)
        self.localSilenceFallbackEnabled = localSilenceFallbackEnabled
        self.localSilenceThresholdSeconds = Self.validated(localSilenceThresholdSeconds, range: 0.5...20, fallback: 4.5)
        self.localSilenceMinimumWordCount = Self.validated(localSilenceMinimumWordCount, range: 1...100, fallback: 5)
        self.longSegmentFallbackEnabled = longSegmentFallbackEnabled
        self.longSegmentWordThreshold = Self.validated(longSegmentWordThreshold, range: 10...1_000, fallback: 80)
        self.longSegmentDurationThresholdSeconds = Self.validated(longSegmentDurationThresholdSeconds, range: 5...600, fallback: 90)
    }

    private enum CodingKeys: String, CodingKey {
        case sonioxMaxEndpointDelayMilliseconds
        case sonioxEndpointSensitivity
        case sonioxEndpointLatencyAdjustmentLevel
        case localSilenceFallbackEnabled
        case localSilenceThresholdSeconds
        case localSilenceMinimumWordCount
        case longSegmentFallbackEnabled
        case longSegmentWordThreshold
        case longSegmentDurationThresholdSeconds
    }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            sonioxMaxEndpointDelayMilliseconds: try values.decodeIfPresent(Int.self, forKey: .sonioxMaxEndpointDelayMilliseconds) ?? 3_000,
            sonioxEndpointSensitivity: try values.decodeIfPresent(Double.self, forKey: .sonioxEndpointSensitivity) ?? -0.3,
            sonioxEndpointLatencyAdjustmentLevel: try values.decodeIfPresent(Int.self, forKey: .sonioxEndpointLatencyAdjustmentLevel) ?? 0,
            localSilenceFallbackEnabled: try values.decodeIfPresent(Bool.self, forKey: .localSilenceFallbackEnabled) ?? true,
            localSilenceThresholdSeconds: try values.decodeIfPresent(Double.self, forKey: .localSilenceThresholdSeconds) ?? 4.5,
            localSilenceMinimumWordCount: try values.decodeIfPresent(Int.self, forKey: .localSilenceMinimumWordCount) ?? 5,
            longSegmentFallbackEnabled: try values.decodeIfPresent(Bool.self, forKey: .longSegmentFallbackEnabled) ?? true,
            longSegmentWordThreshold: try values.decodeIfPresent(Int.self, forKey: .longSegmentWordThreshold) ?? 80,
            longSegmentDurationThresholdSeconds: try values.decodeIfPresent(Double.self, forKey: .longSegmentDurationThresholdSeconds) ?? 90
        )
    }

    func validated() -> TranscriptSegmentationConfig {
        TranscriptSegmentationConfig(
            sonioxMaxEndpointDelayMilliseconds: sonioxMaxEndpointDelayMilliseconds,
            sonioxEndpointSensitivity: sonioxEndpointSensitivity,
            sonioxEndpointLatencyAdjustmentLevel: sonioxEndpointLatencyAdjustmentLevel,
            localSilenceFallbackEnabled: localSilenceFallbackEnabled,
            localSilenceThresholdSeconds: localSilenceThresholdSeconds,
            localSilenceMinimumWordCount: localSilenceMinimumWordCount,
            longSegmentFallbackEnabled: longSegmentFallbackEnabled,
            longSegmentWordThreshold: longSegmentWordThreshold,
            longSegmentDurationThresholdSeconds: longSegmentDurationThresholdSeconds
        )
    }

    func save(to userDefaults: UserDefaults = .standard) {
        guard let data = try? JSONEncoder().encode(validated()) else { return }
        userDefaults.set(data, forKey: Self.userDefaultsKey)
    }

    static func load(from userDefaults: UserDefaults = .standard) -> TranscriptSegmentationConfig {
        guard let data = userDefaults.data(forKey: userDefaultsKey),
              let config = try? JSONDecoder().decode(Self.self, from: data) else {
            return .defaults
        }
        return config.validated()
    }

    private static func validated<T: Comparable>(_ value: T, range: ClosedRange<T>, fallback: T) -> T {
        range.contains(value) ? value : fallback
    }

    private static func validated(_ value: Double, range: ClosedRange<Double>, fallback: Double) -> Double {
        value.isFinite && range.contains(value) ? value : fallback
    }
}

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

    static func load(from defaults: UserDefaults = .standard) -> RecognitionConfig {
        let languageMode = SourceLanguageMode(
            rawValue: defaults.string(forKey: "sourceLanguageMode") ?? SourceLanguageMode.specified.rawValue
        ) ?? .specified
        let specifiedLanguage = defaults.string(forKey: "specifiedSourceLanguage") ?? "en"
        return RecognitionConfig(
            sourceLanguageMode: languageMode,
            specifiedSourceLanguage: specifiedLanguage,
            languageHints: languageMode == .specified ? [specifiedLanguage] : [],
            strictLanguageRestriction: defaults.bool(forKey: "strictLanguageRestriction"),
            translationEnabled: defaults.object(forKey: "translationEnabled") as? Bool ?? true,
            targetTranslationLanguage: defaults.string(forKey: "targetTranslationLanguage") ?? "zh",
            speakerDiarizationEnabled: defaults.object(forKey: "speakerDiarizationEnabled") as? Bool ?? true
        )
    }

    func save(to defaults: UserDefaults = .standard) {
        defaults.set(sourceLanguageMode.rawValue, forKey: "sourceLanguageMode")
        defaults.set(specifiedSourceLanguage, forKey: "specifiedSourceLanguage")
        defaults.set(strictLanguageRestriction, forKey: "strictLanguageRestriction")
        defaults.set(translationEnabled, forKey: "translationEnabled")
        defaults.set(targetTranslationLanguage, forKey: "targetTranslationLanguage")
        defaults.set(speakerDiarizationEnabled, forKey: "speakerDiarizationEnabled")
    }

    mutating func selectSourceLanguage(_ languageCode: String?) {
        guard let languageCode else {
            sourceLanguageMode = .automatic
            languageHints = []
            return
        }
        sourceLanguageMode = .specified
        specifiedSourceLanguage = languageCode
        languageHints = [languageCode]
    }

    mutating func selectTranslationLanguage(_ languageCode: String?) {
        guard let languageCode else {
            translationEnabled = false
            return
        }
        targetTranslationLanguage = languageCode
        translationEnabled = true
    }
}
