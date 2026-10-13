import CoreGraphics
import Foundation

struct DesktopSubtitleOverlaySettings: Codable, Equatable {
    static let userDefaultsKey = "desktopSubtitleOverlaySettings.v1"

    var enabled = false
    var showOriginal = true
    var showTranslation = true
    var originalFontSize = 26.0
    var translationFontSize = 24.0
    var opacity = 1.0
    var backgroundOpacity = 0.0
    var originalColorHex = "#FFFFFF"
    var translationColorHex = "#FFFFFF"
    // nil means fit content height dynamically; a value records a user resize.
    var preferredHeightFraction: Double?
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
        case backgroundOpacity, originalColorHex, translationColorHex, preferredHeightFraction
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
        backgroundOpacity = Self.validated(try values.decodeIfPresent(Double.self, forKey: .backgroundOpacity), range: 0...1, fallback: 0)
        originalColorHex = Self.colorHex(try values.decodeIfPresent(String.self, forKey: .originalColorHex))
        translationColorHex = Self.colorHex(try values.decodeIfPresent(String.self, forKey: .translationColorHex))
        if let height = try values.decodeIfPresent(Double.self, forKey: .preferredHeightFraction) {
            preferredHeightFraction = height.isFinite ? min(max(height, 0.1), 0.85) : nil
        }
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
        result.backgroundOpacity = Self.validated(backgroundOpacity, range: 0...1, fallback: 0)
        result.originalColorHex = Self.colorHex(originalColorHex)
        result.translationColorHex = Self.colorHex(translationColorHex)
        if let height = preferredHeightFraction {
            result.preferredHeightFraction = height.isFinite ? min(max(height, 0.1), 0.85) : nil
        }
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

    static func colorHex(_ value: String?) -> String {
        guard let value, value.count == 7, value.first == "#",
              value.dropFirst().utf8.allSatisfy({
                  (48...57).contains($0) || (65...70).contains($0) || (97...102).contains($0)
              }) else { return "#FFFFFF" }
        return value.uppercased()
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

/// The immediately preceding sentence is retained independently of the
/// current provisional sentence. A replacement must not erase its text.
struct DesktopSubtitleOverlayPrevious: Equatable {
    var state: DesktopSubtitleOverlayState
    var replacedAt: Date

    func remainsVisible(at date: Date, retention: TimeInterval) -> Bool {
        // Do not resurrect a sentence that had already expired before
        // its successor finally arrived after a long pause.
        if state.isFinal, let finalizedAt = state.finalizedAt,
           replacedAt.timeIntervalSince(finalizedAt) >= retention { return false }
        return date.timeIntervalSince(replacedAt) < retention
    }
}

struct DesktopSubtitleOverlayReducer {
    private(set) var current: DesktopSubtitleOverlayState?
    private(set) var previous: DesktopSubtitleOverlayPrevious?

    mutating func update(_ entry: SubtitleEntry, translationEnabled: Bool, at date: Date = Date()) {
        let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
        let translation = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !original.isEmpty || (translationEnabled && !translation.isEmpty) else {
            if current?.entryID == entry.id { current = nil }
            if previous?.state.entryID == entry.id { previous = nil }
            return
        }

        // Translation may arrive after a new source sentence has started.
        // Update the retained sentence by ID rather than switching the live
        // line back to the old sentence or pairing text with the new one.
        if current?.entryID != entry.id, previous?.state.entryID == entry.id {
            guard let old = previous else { return }
            let updated = Self.updatedState(old.state, entry: entry,
                                            translationEnabled: translationEnabled, at: date)
            if updated.original != old.state.original ||
               updated.translation != old.state.translation ||
               updated.translationEnabled != old.state.translationEnabled {
                previous = DesktopSubtitleOverlayPrevious(state: updated, replacedAt: date)
            }
            return
        }

        if current?.entryID != entry.id {
            if let outgoing = current {
                previous = DesktopSubtitleOverlayPrevious(state: outgoing, replacedAt: date)
            }
            current = nil
        }
        current = Self.updatedState(current, entry: entry,
                                    translationEnabled: translationEnabled, at: date)
    }

    mutating func finalize(_ entry: SubtitleEntry, translationEnabled: Bool, at date: Date = Date()) {
        update(entry, translationEnabled: translationEnabled, at: date)
        if previous?.state.entryID == entry.id, current?.entryID != entry.id {
            guard previous?.state.isFinal == false else { return }
            previous?.state.isFinal = true
            previous?.state.revision = UUID()
            previous?.state.finalizedAt = date
            return
        }
        guard current?.entryID == entry.id, current?.isFinal == false else { return }
        current?.isFinal = true
        current?.finalizedAt = date
        current?.revision = UUID()
    }

    mutating func clear() {
        current = nil
        previous = nil
    }

    private static func updatedState(_ prior: DesktopSubtitleOverlayState?,
                                     entry: SubtitleEntry, translationEnabled: Bool,
                                     at date: Date) -> DesktopSubtitleOverlayState {
        let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
        let translation = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
        let sameEntry = prior?.entryID == entry.id
        let textChanged = sameEntry &&
            (prior?.original != original || prior?.translation != translation ||
             prior?.translationEnabled != translationEnabled)
        if sameEntry && !textChanged, let prior { return prior }
        let wasFinal = sameEntry && (prior?.isFinal ?? false)
        return DesktopSubtitleOverlayState(
            entryID: entry.id,
            original: original,
            translation: translation,
            translationEnabled: translationEnabled,
            isFinal: wasFinal,
            revision: UUID(),
            finalizedAt: wasFinal && textChanged ? date : (sameEntry ? prior?.finalizedAt : nil)
        )
    }
}

/// Pure placement policy, shared with isolated multi-display regression checks.
enum DesktopSubtitleOverlayLayout {
    static func screenIndex(for frame: CGRect, screens: [CGRect]) -> Int? {
        screens.indices.filter { screens[$0].intersects(frame) }.max { lhs, rhs in
            let left = screens[lhs].intersection(frame)
            let right = screens[rhs].intersection(frame)
            return left.width * left.height < right.width * right.height
        }
    }

    static func frame(in visible: CGRect, settings: DesktopSubtitleOverlaySettings, height: CGFloat) -> CGRect {
        let height = min(height, visible.height)
        let width = min(max(visible.width * settings.widthFraction, 280), visible.width)
        return CGRect(
            x: min(max(visible.minX + visible.width * settings.normalizedX - width / 2, visible.minX), visible.maxX - width),
            y: min(max(visible.minY + visible.height * settings.normalizedBottom, visible.minY), visible.maxY - height),
            width: width, height: height)
    }
}

struct DesktopSubtitleOverlaySnapshot: Equatable {
    var current: DesktopSubtitleOverlayState?
    var previous: DesktopSubtitleOverlayPrevious?
}
