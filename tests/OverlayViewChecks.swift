import AppKit
import SwiftUI

// The overlay only consumes this feed. No audio, credentials, or app lifecycle.
final class SpeechViewModel {
    let desktopSubtitleOverlayFeed = DesktopSubtitleOverlayFeed()
}

@main
struct OverlayViewChecks {
    @MainActor static func main() throws {
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)
        let suite = "EchoOverlayViewChecks.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let store = DesktopSubtitleOverlaySettingsStore(defaults: defaults)
        store.update {
            $0.screenID = "disconnected-display"
            $0.normalizedX = 0.9
            $0.normalizedBottom = 0.8
        }
        store.resetPosition()
        let restored = DesktopSubtitleOverlaySettingsStore(defaults: defaults)
        precondition(restored.settings.screenID == nil && restored.settings.normalizedX == 0.5,
                     "Reset while disabled must persist without an overlay window")
        let controller = SubtitleOverlayController()
        controller.setAdjusting(true)
        controller.finishAdjusting()
        precondition(!controller.isAdjusting, "Finishing adjustment exits even without a window")

        let longText = "Old opening words. " + String(repeating: "More spoken words. ", count: 20) + "Latest words."
        let tail = DesktopSubtitleOverlayText.latestLines(longText, font: .systemFont(ofSize: 26), width: 320)
        precondition(longText.hasSuffix(tail) && tail.hasSuffix("Latest words.") && !tail.contains("Old opening"),
                     "Long captions must show a contiguous suffix containing the newest words")
        precondition(DesktopSubtitleOverlayText.latestLines("Short caption", font: .systemFont(ofSize: 26), width: 700) == "Short caption")

        guard CommandLine.arguments.count > 1 else {
            print("PASS: isolated overlay settings reset and adjustment exit")
            return
        }
        let output = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        for (name, width, font, translationFont, adjusting) in [
            ("large-bilingual", 700.0, 48.0, 44.0, false),
            ("narrow-adjusting", 360.0, 26.0, 24.0, true)
        ] {
            store.update {
                $0.originalFontSize = font
                $0.translationFontSize = translationFont
            }
            controller.setAdjusting(adjusting)
            let feed = DesktopSubtitleOverlayFeed()
            feed.update(SubtitleEntry(start: 0, end: 1,
                english: "This is a long live caption that keeps growing while the speaker explains the topic. These are the latest words.",
                chinese: "这是一段持续更新的较长字幕，用于检查双语大字号显示和窄窗口中的换行。这里是最新说出的内容。"), translationEnabled: true)
            let metrics = NSLayoutManager()
            let height = ceil(metrics.defaultLineHeight(for: .systemFont(ofSize: font, weight: .medium)) * 2
                + metrics.defaultLineHeight(for: .systemFont(ofSize: translationFont)) * 2 + 57)
            let host = NSHostingView(rootView: DesktopSubtitleOverlayView(
                feed: feed, settingsStore: store, controller: controller))
            let window = NSWindow(contentRect: NSRect(x: -10000, y: -10000, width: width, height: height),
                                  styleMask: [.borderless], backing: .buffered, defer: false)
            window.contentView = host
            host.frame = NSRect(x: 0, y: 0, width: width, height: height)
            window.displayIfNeeded()
            RunLoop.main.run(until: Date().addingTimeInterval(0.2))
            host.layoutSubtreeIfNeeded()
            guard let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else {
                fatalError("Could not allocate overlay snapshot")
            }
            host.cacheDisplay(in: host.bounds, to: bitmap)
            try bitmap.representation(using: .png, properties: [:])!.write(to: output.appendingPathComponent("\(name).png"))
            window.contentView = nil
        }
        print("PASS: isolated overlay settings and offscreen rendering (no visible windows or audio)")
    }
}
