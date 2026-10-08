import Foundation

struct DesktopSubtitleOverlaySettings: Codable, Equatable {
    static let userDefaultsKey = "desktopSubtitleOverlaySettings.v1"

    var enabled = false
    var showOriginal = true
    var showTranslation = true
    var originalFontSize = 26.0
    var translationFontSize = 24.0
    var opacity = 1.0
    var widthFraction = 0.75
    var retentionSeconds = 5.0
    var shadowStrength = 0.35
    var clickThrough = true
    var positionLocked = true
    var screenID: String?
    var normalizedX = 0.5
    var normalizedBottom = 0.09

    static let defaults = DesktopSubtitleOverlaySettings()

    private enum CodingKeys: String, CodingKey {
        case enabled, showOriginal, showTranslation, originalFontSize, translationFontSize
        case opacity, widthFraction, retentionSeconds, shadowStrength, clickThrough
        case positionLocked, screenID, normalizedX, normalizedBottom
    }

    init() {}

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        self.init()
        enabled = try values.decodeIfPresent(Bool.self, forKey: .enabled) ?? false
        showOriginal = try values.decodeIfPresent(Bool.self, forKey: .showOriginal) ?? true
        showTranslation = try values.decodeIfPresent(Bool.self, forKey: .showTranslation) ?? true
        originalFontSize = Self.validated(try values.decodeIfPresent(Double.self, forKey: .originalFontSize), range: 16...48, fallback: 26)
        translationFontSize = Self.validated(try values.decodeIfPresent(Double.self, forKey: .translationFontSize), range: 14...44, fallback: 24)
        opacity = Self.validated(try values.decodeIfPresent(Double.self, forKey: .opacity), range: 0.35...1, fallback: 1)
        widthFraction = Self.validated(try values.decodeIfPresent(Double.self, forKey: .widthFraction), range: 0.35...0.95, fallback: 0.75)
        retentionSeconds = Self.validated(try values.decodeIfPresent(Double.self, forKey: .retentionSeconds), range: 1...15, fallback: 5)
        shadowStrength = Self.validated(try values.decodeIfPresent(Double.self, forKey: .shadowStrength), range: 0...1, fallback: 0.35)
        clickThrough = try values.decodeIfPresent(Bool.self, forKey: .clickThrough) ?? true
        positionLocked = try values.decodeIfPresent(Bool.self, forKey: .positionLocked) ?? true
        screenID = try values.decodeIfPresent(String.self, forKey: .screenID)
        normalizedX = Self.validated(try values.decodeIfPresent(Double.self, forKey: .normalizedX), range: 0...1, fallback: 0.5)
        normalizedBottom = Self.validated(try values.decodeIfPresent(Double.self, forKey: .normalizedBottom), range: 0...1, fallback: 0.09)
        enforceVisibleLanguage()
    }

    mutating func enforceVisibleLanguage() {
        if !showOriginal && !showTranslation { showOriginal = true }
    }

    func validated() -> Self {
        var result = self
        result.originalFontSize = Self.validated(originalFontSize, range: 16...48, fallback: 26)
        result.translationFontSize = Self.validated(translationFontSize, range: 14...44, fallback: 24)
        result.opacity = Self.validated(opacity, range: 0.35...1, fallback: 1)
        result.widthFraction = Self.validated(widthFraction, range: 0.35...0.95, fallback: 0.75)
        result.retentionSeconds = Self.validated(retentionSeconds, range: 1...15, fallback: 5)
        result.shadowStrength = Self.validated(shadowStrength, range: 0...1, fallback: 0.35)
        result.normalizedX = Self.validated(normalizedX, range: 0...1, fallback: 0.5)
        result.normalizedBottom = Self.validated(normalizedBottom, range: 0...1, fallback: 0.09)
        result.enforceVisibleLanguage()
        return result
    }

    func save(to defaults: UserDefaults = .standard) {
        guard let data = try? JSONEncoder().encode(validated()) else { return }
        defaults.set(data, forKey: Self.userDefaultsKey)
    }

    static func load(from defaults: UserDefaults = .standard) -> Self {
        guard let data = defaults.data(forKey: userDefaultsKey),
              let value = try? JSONDecoder().decode(Self.self, from: data) else { return .defaults }
        return value.validated()
    }

    private static func validated(_ value: Double?, range: ClosedRange<Double>, fallback: Double) -> Double {
        guard let value, value.isFinite, range.contains(value) else { return fallback }
        return value
    }

    private static func validated(_ value: Double, range: ClosedRange<Double>, fallback: Double) -> Double {
        validated(Optional(value), range: range, fallback: fallback)
    }
}

struct DesktopSubtitleOverlayState: Equatable {
    var entryID: UUID
    var original: String
    var translation: String
    var translationEnabled: Bool
    var isFinal: Bool
    var revision: UUID
    var finalizedAt: Date?

    var isVisible: Bool { !original.isEmpty || (translationEnabled && !translation.isEmpty) }

    func remainsVisible(at date: Date, retention: TimeInterval) -> Bool {
        guard isFinal, let finalizedAt else { return true }
        return date.timeIntervalSince(finalizedAt) < retention
    }
}

struct DesktopSubtitleOverlayReducer {
    private(set) var current: DesktopSubtitleOverlayState?

    mutating func update(_ entry: SubtitleEntry, translationEnabled: Bool) {
        let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
        let translation = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !original.isEmpty || (translationEnabled && !translation.isEmpty) else { return }
        let sameEntry = current?.entryID == entry.id
        current = DesktopSubtitleOverlayState(
            entryID: entry.id,
            original: original,
            translation: translation,
            translationEnabled: translationEnabled,
            isFinal: sameEntry ? (current?.isFinal ?? false) : false,
            revision: UUID(),
            finalizedAt: sameEntry ? current?.finalizedAt : nil
        )
    }

    mutating func finalize(_ entry: SubtitleEntry, translationEnabled: Bool, at date: Date = Date()) {
        update(entry, translationEnabled: translationEnabled)
        guard current?.entryID == entry.id else { return }
        current?.isFinal = true
        current?.finalizedAt = date
        current?.revision = UUID()
    }

    mutating func clear() { current = nil }
}
