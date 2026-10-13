import AppKit
import SwiftUI

// Isolated feed-only model: no capture devices, credentials, or Echo lifecycle.
final class SpeechViewModel {
    let desktopSubtitleOverlayFeed = DesktopSubtitleOverlayFeed()
}

@main
struct OverlayViewChecks {
    @MainActor static func settle(_ window: NSWindow) {
        window.displayIfNeeded()
        RunLoop.main.run(until: Date().addingTimeInterval(0.25))
        window.contentView?.layoutSubtreeIfNeeded()
    }

    @MainActor static func descendants<T: NSView>(_ root: NSView, of type: T.Type) -> [T] {
        (root as? T).map { [$0] } ?? root.subviews.flatMap { descendants($0, of: type) }
    }

    @MainActor static func main() throws {
        NSApplication.shared.setActivationPolicy(.prohibited)
        let suite = "EchoOverlayViewChecks.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let store = DesktopSubtitleOverlaySettingsStore(defaults: defaults)
        store.update { $0.screenID = "disconnected"; $0.normalizedX = 0.9; $0.normalizedBottom = 0.8 }
        store.resetPosition()
        precondition(DesktopSubtitleOverlaySettingsStore(defaults: defaults).settings.normalizedX == 0.5)
        store.update { $0.clickThrough = false; $0.positionLocked = false }
        store.resetAppearance()
        precondition(!store.settings.clickThrough && !store.settings.positionLocked,
                     "Reset appearance must preserve the interaction choice")
        store.update { $0.enabled = true; $0.clickThrough = true; $0.positionLocked = true }
        let model = SpeechViewModel()
        let controller = SubtitleOverlayController(presentsWindows: false)
        controller.show(model: model, settings: store)
        guard let window = NSApplication.shared.windows.first(where: { $0 is NSPanel }), let host = window.contentView else {
            fatalError("Expected the hidden production subtitle panel")
        }
        precondition(!window.isVisible && window.ignoresMouseEvents)
        controller.setAdjusting(true)
        precondition(controller.isAdjusting && !window.ignoresMouseEvents)
        let before = window.frame
        controller.move(from: before, mouseStart: .zero, mouseNow: NSPoint(x: 25, y: 30))
        precondition(abs(window.frame.minX - before.minX - 25) < 1 && abs(window.frame.minY - before.minY - 30) < 1)
        controller.commitPlacement(resized: false)
        let resizeStart = window.frame
        controller.resize(from: resizeStart, mouseStart: .zero, mouseNow: NSPoint(x: -80, y: -60))
        controller.commitPlacement(resized: true)
        precondition(store.settings.preferredHeightFraction != nil && window.frame.width < resizeStart.width)
        controller.finishAdjusting()
        precondition(!controller.isAdjusting && window.ignoresMouseEvents && store.settings.positionLocked)
        precondition(!window.isVisible, "Tests must never present a real overlay")

        let output = CommandLine.arguments.count > 1 ? URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true) : nil
        if let output { try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true) }
        let entryID = UUID()
        for (name, font, translationFont, adjusting) in [
            ("large-bilingual", 48.0, 44.0, false),
            ("adjusting", 26.0, 24.0, true)
        ] {
            store.update {
                $0.originalFontSize = font; $0.translationFontSize = translationFont
                $0.widthFraction = 0.45; $0.preferredHeightFraction = 0.25
                $0.backgroundOpacity = 0.65
            }
            controller.refresh(settings: store.settings)
            controller.setAdjusting(adjusting)
            model.desktopSubtitleOverlayFeed.update(SubtitleEntry(id: entryID, start: 0, end: 1,
                english: String(repeating: "Earlier spoken words. ", count: 20) + "These are the latest words.",
                chinese: String(repeating: "此前说出的内容。", count: 20) + "这里是最新字幕。"), translationEnabled: true)
            settle(window)
            if !adjusting {
                precondition(descendants(host, of: NSScrollView.self).isEmpty,
                             "Watching mode must keep both language tails visible without a shared scroll viewport")
                let font = NSFont.systemFont(ofSize: font)
                let tail = DesktopSubtitleOverlayText.latestLines("Earlier words. " + String(repeating: "Old words. ", count: 40) + "LATEST", font: font, width: 300, count: 1)
                precondition(tail.hasSuffix("LATEST") && !tail.hasPrefix("Earlier"))
            }
            if let output, let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) {
                host.cacheDisplay(in: host.bounds, to: bitmap)
                try bitmap.representation(using: .png, properties: [:])!.write(to: output.appendingPathComponent("\(name).png"))
            }
        }
        controller.finishAdjusting()
        settle(window)
        precondition(descendants(host, of: NSScrollView.self).isEmpty,
                     "Finishing adjustment must restore the latest paired subtitle presentation")
        controller.hide()
        precondition(!controller.hasWindow)
        print("PASS: hidden production panel movement, two-axis resize, click-through, persistence, and latest-content following")
    }
}
