import AppKit
import SwiftUI

final class DesktopSubtitleOverlaySettingsStore: ObservableObject {
    @Published private(set) var settings: DesktopSubtitleOverlaySettings

    init() { settings = .load() }

    func update(_ change: (inout DesktopSubtitleOverlaySettings) -> Void) {
        var next = settings
        change(&next)
        next.enforceVisibleLanguage()
        next = next.validated()
        guard next != settings else { return }
        settings = next
        next.save()
    }

    func resetAppearance() {
        update { current in
            let enabled = current.enabled
            let screenID = current.screenID
            let x = current.normalizedX
            let bottom = current.normalizedBottom
            current = .defaults
            current.enabled = enabled
            current.screenID = screenID
            current.normalizedX = x
            current.normalizedBottom = bottom
        }
    }

    func resetPosition() {
        update { $0.screenID = nil; $0.normalizedX = 0.5; $0.normalizedBottom = 0.09 }
    }
}

private final class TransparentSubtitlePanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class SubtitleOverlayController: NSObject, ObservableObject, NSWindowDelegate {
    @Published private(set) var isAdjusting = false

    var hasWindow: Bool { panel != nil }
    var currentPanelFrame: NSRect? { panel?.frame }

    private var panel: TransparentSubtitlePanel?
    private var retainedSpeechModel: SpeechViewModel?
    private weak var settingsStore: DesktopSubtitleOverlaySettingsStore?
    private var applyingFrame = false
    private var gestureInProgress = false
    private var screenChangeObserver: NSObjectProtocol?
    private var lastAppliedWidthFraction: Double?
    private var measuredContentHeight: CGFloat = 140

    override init() {
        super.init()
        screenChangeObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            guard let self, self.panel != nil else { return }
            self.updateFrame(forceDefault: false)
        }
    }

    deinit {
        if let screenChangeObserver { NotificationCenter.default.removeObserver(screenChangeObserver) }
    }

    func show(model: SpeechViewModel, settings: DesktopSubtitleOverlaySettingsStore, adjusting: Bool = false) {
        guard Thread.isMainThread else {
            DispatchQueue.main.async { [weak self] in self?.show(model: model, settings: settings, adjusting: adjusting) }
            return
        }
        retainedSpeechModel = model
        settingsStore = settings
        if panel == nil { createPanel(model: model, settings: settings) }
        isAdjusting = adjusting
        updateInteraction()
        updateFrame(forceDefault: false)
        panel?.orderFrontRegardless()
    }

    func hide() {
        guard Thread.isMainThread else { DispatchQueue.main.async { [weak self] in self?.hide() }; return }
        panel?.orderOut(nil)
        panel?.delegate = nil
        panel?.contentView = nil
        panel = nil
        retainedSpeechModel = nil
        settingsStore = nil
        isAdjusting = false
        gestureInProgress = false
    }

    func setAdjusting(_ value: Bool) {
        guard let panel else { return }
        if !value && gestureInProgress { commitPlacement(resized: false) }
        isAdjusting = value
        updateInteraction()
        if value { panel.orderFrontRegardless() }
        if !value { updateFrame(forceDefault: false) }
    }

    func refresh(settings: DesktopSubtitleOverlaySettings) {
        guard let panel else { return }
        if !settings.enabled { hide(); return }
        updateInteraction()
        if !gestureInProgress &&
            (lastAppliedWidthFraction.map { abs($0 - settings.widthFraction) > 0.001 } ?? true) {
            updateFrame(forceDefault: false)
        }
        if !panel.isVisible { panel.orderFrontRegardless() }
    }

    func resetPosition() {
        settingsStore?.resetPosition()
        updateFrame(forceDefault: true)
    }

    func resetAutoHeight() {
        settingsStore?.update { $0.preferredHeightFraction = nil }
        updateFrame(forceDefault: false)
    }

    // SwiftUI gestures explicitly move the panel; background dragging was
    // unreliable because the hosting view and scroll content consume events.
    func move(from start: NSRect, translation: CGSize) {
        guard isAdjusting, let panel else { return }
        gestureInProgress = true
        let origin = NSPoint(x: start.minX + translation.width, y: start.minY + translation.height)
        applyingFrame = true
        panel.setFrameOrigin(origin)
        applyingFrame = false
    }

    func resize(from start: NSRect, translation: CGSize) {
        guard isAdjusting, let panel, let settings = settingsStore?.settings else { return }
        gestureInProgress = true
        let visible = screen(for: settings).visibleFrame
        let width = min(max(start.width + translation.width, min(280, visible.width)), visible.width)
        let height = min(max(start.height - translation.height, 90), max(90, visible.height * 0.85))
        // Bottom-right grip: keep top-left anchored as the user drags.
        let x = min(max(start.minX, visible.minX), visible.maxX - width)
        let y = min(max(start.maxY - height, visible.minY), visible.maxY - height)
        applyingFrame = true
        panel.setFrame(NSRect(x: x, y: y, width: width, height: height),
                       display: true, animate: false)
        applyingFrame = false
    }

    func commitPlacement(resized: Bool) {
        guard let panel, let store = settingsStore else { gestureInProgress = false; return }
        gestureInProgress = false
        let screen = NSScreen.screens.max {
            $0.frame.intersection(panel.frame).width * $0.frame.intersection(panel.frame).height <
            $1.frame.intersection(panel.frame).width * $1.frame.intersection(panel.frame).height
        } ?? screen(for: store.settings)
        let visible = screen.visibleFrame
        let width = min(panel.frame.width, visible.width)
        let height = min(panel.frame.height, visible.height * 0.85)
        let origin = NSPoint(
            x: min(max(panel.frame.minX, visible.minX), visible.maxX - width),
            y: min(max(panel.frame.minY, visible.minY), visible.maxY - height)
        )
        applyingFrame = true
        panel.setFrame(NSRect(origin: origin, size: NSSize(width: width, height: height)),
                       display: true, animate: false)
        applyingFrame = false
        store.update {
            $0.screenID = Self.screenID(screen)
            $0.normalizedX = min(max((origin.x + width / 2 - visible.minX) / visible.width, 0), 1)
            $0.normalizedBottom = min(max((origin.y - visible.minY) / visible.height, 0), 1)
            $0.widthFraction = min(max(width / visible.width, 0.35), 0.95)
            if resized { $0.preferredHeightFraction = min(max(height / visible.height, 0.1), 0.85) }
        }
        lastAppliedWidthFraction = store.settings.widthFraction
        if !resized { updateFrame(forceDefault: false) }
    }

    func contentHeightDidChange(_ value: CGFloat) {
        guard value.isFinite, value > 0 else { return }
        measuredContentHeight = value
        guard let panel, let settings = settingsStore?.settings,
              settings.preferredHeightFraction == nil, !gestureInProgress, !isAdjusting else { return }
        let visible = screen(for: settings).visibleFrame
        let height = min(max(value, 72), visible.height * 0.85)
        guard abs(panel.frame.height - height) > 2 else { return }
        let origin = NSPoint(
            x: panel.frame.minX,
            y: min(max(panel.frame.minY, visible.minY), visible.maxY - height)
        )
        applyingFrame = true
        panel.setFrame(NSRect(origin: origin, size: NSSize(width: panel.frame.width, height: height)),
                       display: true, animate: false)
        applyingFrame = false
    }

    private func createPanel(model: SpeechViewModel, settings: DesktopSubtitleOverlaySettingsStore) {
        let panel = TransparentSubtitlePanel(
            contentRect: NSRect(x: 0, y: 0, width: 700, height: 140),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.isReleasedWhenClosed = false
        panel.hidesOnDeactivate = false
        panel.isFloatingPanel = true
        panel.becomesKeyOnlyIfNeeded = true
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.delegate = self
        panel.contentView = NSHostingView(rootView: DesktopSubtitleOverlayView(
            feed: model.desktopSubtitleOverlayFeed,
            settingsStore: settings,
            controller: self
        ))
        self.panel = panel
    }

    private func updateInteraction() {
        guard let panel, let settings = settingsStore?.settings else { return }
        panel.ignoresMouseEvents = !isAdjusting && settings.clickThrough
        // All adjustment gestures are explicit in the SwiftUI hosting view.
        panel.isMovableByWindowBackground = false
    }

    private func updateFrame(forceDefault: Bool) {
        guard let panel, let settings = settingsStore?.settings, !gestureInProgress else { return }
        let visible = screen(for: settings).visibleFrame
        let width = min(max(visible.width * settings.widthFraction, min(280, visible.width)), visible.width)
        let height = min(max(settings.preferredHeightFraction.map { visible.height * $0 }
                             ?? measuredContentHeight, 72), visible.height * 0.85)
        let xFraction = forceDefault ? 0.5 : settings.normalizedX
        let bottomFraction = forceDefault ? 0.09 : settings.normalizedBottom
        let origin = NSPoint(
            x: min(max(visible.minX + visible.width * xFraction - width / 2, visible.minX),
                   visible.maxX - width),
            y: min(max(visible.minY + visible.height * bottomFraction, visible.minY),
                   visible.maxY - height)
        )
        applyingFrame = true
        panel.setFrame(NSRect(origin: origin, size: NSSize(width: width, height: height)),
                       display: true, animate: false)
        applyingFrame = false
        lastAppliedWidthFraction = settings.widthFraction
    }

    private func screen(for settings: DesktopSubtitleOverlaySettings) -> NSScreen {
        if let screenID = settings.screenID,
           let match = NSScreen.screens.first(where: { Self.screenID($0) == screenID }) { return match }
        return NSScreen.main ?? NSScreen.screens[0]
    }

    private static func screenID(_ screen: NSScreen) -> String? {
        (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.stringValue
    }
}

private struct DesktopSubtitleOverlayView: View {
    @ObservedObject var feed: DesktopSubtitleOverlayFeed
    @ObservedObject var settingsStore: DesktopSubtitleOverlaySettingsStore
    @ObservedObject var controller: SubtitleOverlayController
    @State private var visibleState: DesktopSubtitleOverlayState?
    @State private var expirationTask: Task<Void, Never>?
    @State private var resizeStartWidth: CGFloat?

    private var settings: DesktopSubtitleOverlaySettings { settingsStore.settings }

    var body: some View {
        VStack(spacing: 5) {
            if let state = visibleState, state.remainsVisible(at: Date(), retention: settings.retentionSeconds) {
                subtitleLines(state)
                    .transition(.opacity)
            } else if controller.isAdjusting {
                VStack(spacing: 5) {
                    if settings.showOriginal {
                        subtitleText("Original subtitle text", size: settings.originalFontSize, weight: .medium)
                    }
                    if settings.showTranslation {
                        subtitleText("实时双语字幕预览", size: settings.translationFontSize, weight: .regular)
                    }
                }
                .transition(.opacity)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(.horizontal, 18)
        .padding(.vertical, 8)
        .opacity(settings.opacity)
        .overlay {
            if controller.isAdjusting {
                ZStack(alignment: .bottomTrailing) {
                    RoundedRectangle(cornerRadius: 5).stroke(.mint.opacity(0.8), lineWidth: 1)
                    Image(systemName: "arrow.up.left.and.arrow.down.right")
                        .font(.system(size: 10, weight: .medium))
                        .foregroundStyle(.mint)
                        .padding(4)
                        .gesture(DragGesture(minimumDistance: 1)
                            .onChanged { value in
                                if resizeStartWidth == nil { resizeStartWidth = controller.currentPanelWidth }
                                controller.resize(width: (resizeStartWidth ?? 700) + value.translation.width)
                            }
                            .onEnded { _ in resizeStartWidth = nil })
                }
            }
        }
        .background(Color.clear)
        .allowsHitTesting(controller.isAdjusting)
        .onAppear { accept(feed.current) }
        .onChange(of: feed.current) { _, state in accept(state) }
        .onChange(of: settings.retentionSeconds) { _, _ in accept(feed.current) }
        .onDisappear { expirationTask?.cancel() }
    }

    @ViewBuilder private func subtitleLines(_ state: DesktopSubtitleOverlayState) -> some View {
        let showOriginal = settings.showOriginal || !state.translationEnabled || !settings.showTranslation
        if showOriginal && !state.original.isEmpty {
            subtitleText(state.original, size: settings.originalFontSize, weight: .medium)
        }
        if settings.showTranslation && state.translationEnabled && !state.translation.isEmpty {
            subtitleText(state.translation, size: settings.translationFontSize, weight: .regular)
        }
    }

    private func subtitleText(_ value: String, size: Double, weight: Font.Weight) -> some View {
        Text(value)
            .font(.system(size: size, weight: weight))
            .foregroundStyle(.white)
            .multilineTextAlignment(.center)
            .lineLimit(2)
            .truncationMode(.tail)
            .fixedSize(horizontal: false, vertical: true)
            .shadow(color: .black.opacity(settings.shadowStrength), radius: 3, x: 0, y: 1)
            .frame(maxWidth: .infinity)
            .accessibilityLabel(value)
    }

    private func accept(_ state: DesktopSubtitleOverlayState?) {
        expirationTask?.cancel()
        guard let state, state.isVisible else {
            withAnimation(.easeOut(duration: 0.12)) { visibleState = nil }
            return
        }
        withAnimation(.easeInOut(duration: 0.12)) { visibleState = state }
        guard state.isFinal, let finalizedAt = state.finalizedAt else { return }
        let remaining = max(0, settings.retentionSeconds - Date().timeIntervalSince(finalizedAt))
        let expectedEntryID = state.entryID
        let expectedRevision = state.revision
        expirationTask = Task {
            try? await Task.sleep(for: .seconds(remaining))
            guard !Task.isCancelled,
                  visibleState?.entryID == expectedEntryID,
                  visibleState?.revision == expectedRevision,
                  visibleState?.isFinal == true else { return }
            withAnimation(.easeOut(duration: 0.12)) { visibleState = nil }
        }
    }
}

struct DesktopSubtitleOverlaySettingsView: View {
    @ObservedObject var store: DesktopSubtitleOverlaySettingsStore
    @ObservedObject var controller: SubtitleOverlayController
    let model: SpeechViewModel

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                HStack {
                    Text("悬浮字幕").font(.title2.weight(.semibold))
                    Spacer()
                    Button("恢复默认") { store.resetAppearance() }
                }
                Text("视频字幕式悬浮显示，不是滚动歌词。样式立即生效，不会重新连接 Soniox。")
                    .font(.callout).foregroundStyle(.secondary)
                GroupBox("显示内容") {
                    VStack(alignment: .leading, spacing: 10) {
                        toggle("启用悬浮字幕", keyPath: \.enabled)
                        toggle("显示原文", keyPath: \.showOriginal)
                        toggle("显示译文", keyPath: \.showTranslation)
                    }.frame(maxWidth: .infinity, alignment: .leading)
                }
                GroupBox("外观") {
                    VStack(alignment: .leading, spacing: 12) {
                        slider("原文字号", value: \DesktopSubtitleOverlaySettings.originalFontSize, range: 16...48, suffix: " pt")
                        slider("译文字号", value: \DesktopSubtitleOverlaySettings.translationFontSize, range: 14...44, suffix: " pt")
                        slider("字幕透明度", value: \DesktopSubtitleOverlaySettings.opacity, range: 0.35...1, suffix: "%", multiplier: 100)
                        slider("最大宽度", value: \DesktopSubtitleOverlaySettings.widthFraction, range: 0.35...0.95, suffix: "%", multiplier: 100)
                        slider("定稿保留", value: \DesktopSubtitleOverlaySettings.retentionSeconds, range: 1...15, suffix: " 秒")
                        slider("文字阴影", value: \DesktopSubtitleOverlaySettings.shadowStrength, range: 0...1, suffix: "%", multiplier: 100)
                    }.frame(maxWidth: .infinity, alignment: .leading)
                }
                GroupBox("位置与交互") {
                    VStack(alignment: .leading, spacing: 10) {
                        toggle("点击穿透（不调整时）", keyPath: \.clickThrough)
                        Toggle("锁定位置", isOn: Binding(
                            get: { store.settings.positionLocked },
                            set: { value in
                                store.update { $0.positionLocked = value }
                                controller.setAdjusting(!value)
                            }
                        ))
                        HStack {
                            Text("调整字幕位置和大小")
                            Spacer()
                            Button("进入调整模式") {
                                store.update { $0.enabled = true }
                                controller.show(model: model, settings: store, adjusting: true)
                            }
                        }
                        Button("重置字幕位置") { controller.resetPosition() }
                    }.frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding(20)
            .frame(maxWidth: 540, alignment: .leading)
        }
        .frame(width: 540, height: 600)
    }

    private func toggle(_ title: String, keyPath: WritableKeyPath<DesktopSubtitleOverlaySettings, Bool>) -> some View {
        Toggle(title, isOn: Binding(
            get: { store.settings[keyPath: keyPath] },
            set: { value in store.update { $0[keyPath: keyPath] = value } }
        ))
    }

    private func slider(_ title: String, value keyPath: WritableKeyPath<DesktopSubtitleOverlaySettings, Double>, range: ClosedRange<Double>, suffix: String, multiplier: Double = 1) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack {
                Text(title)
                Spacer()
                Text("\(Int((store.settings[keyPath: keyPath] * multiplier).rounded()))\(suffix)")
                    .monospacedDigit().foregroundStyle(.secondary)
            }
            Slider(value: Binding(
                get: { store.settings[keyPath: keyPath] },
                set: { value in store.update { $0[keyPath: keyPath] = value } }
            ), in: range)
        }
    }
}
