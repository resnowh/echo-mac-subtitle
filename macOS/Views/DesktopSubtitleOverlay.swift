import AppKit
import SwiftUI

final class DesktopSubtitleOverlaySettingsStore: ObservableObject {
    @Published private(set) var settings: DesktopSubtitleOverlaySettings

    private let defaults: UserDefaults
    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        settings = .load(from: defaults)
    }

    func update(_ change: (inout DesktopSubtitleOverlaySettings) -> Void) {
        var next = settings
        change(&next)
        next.enforceVisibleLanguage()
        next = next.validated()
        guard next != settings else { return }
        settings = next
        next.save(to: defaults)
    }

    func resetAppearance() {
        update {
            $0.showOriginal = true
            $0.showTranslation = true
            $0.originalFontSize = 26
            $0.translationFontSize = 24
            $0.opacity = 1
            $0.backgroundOpacity = 0
            $0.originalColorHex = "#FFFFFF"
            $0.translationColorHex = "#FFFFFF"
            $0.shadowStrength = 0.35
            $0.widthFraction = 0.75
            $0.preferredHeightFraction = nil
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
    private var lastAppliedSettings: DesktopSubtitleOverlaySettings?
    private let presentsWindows: Bool
    private var measuredContentHeight: CGFloat = 140

    init(presentsWindows: Bool = true) {
        self.presentsWindows = presentsWindows
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
        isAdjusting = adjusting || !settings.settings.positionLocked
        updateInteraction()
        updateFrame(forceDefault: false)
        if presentsWindows { panel?.orderFrontRegardless() }
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
        if value && presentsWindows { panel.orderFrontRegardless() }
        updateFrame(forceDefault: false)
    }

    func refresh(settings: DesktopSubtitleOverlaySettings) {
        guard let panel else { return }
        if !settings.enabled { hide(); return }
        if let previous = lastAppliedSettings, previous.positionLocked != settings.positionLocked {
            isAdjusting = !settings.positionLocked
        }
        updateInteraction()
        if !gestureInProgress && lastAppliedSettings != settings { updateFrame(forceDefault: false) }
        if presentsWindows && !panel.isVisible { panel.orderFrontRegardless() }
    }

    func finishAdjusting() {
        if gestureInProgress { commitPlacement(resized: false) }
        settingsStore?.update { $0.positionLocked = true }
        setAdjusting(false)
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
    func move(from start: NSRect, mouseStart: NSPoint, mouseNow: NSPoint) {
        guard isAdjusting, let panel else { return }
        gestureInProgress = true
        let origin = NSPoint(x: start.minX + mouseNow.x - mouseStart.x,
                             y: start.minY + mouseNow.y - mouseStart.y)
        applyingFrame = true
        panel.setFrameOrigin(origin)
        applyingFrame = false
    }

    func resize(from start: NSRect, mouseStart: NSPoint, mouseNow: NSPoint) {
        guard isAdjusting, let panel, let settings = settingsStore?.settings else { return }
        gestureInProgress = true
        guard let screen = screen(for: settings) else { return }
        let visible = screen.visibleFrame
        // Screen-space cursor deltas remain stable as the window moves/resizes.
        let width = min(max(start.width + mouseNow.x - mouseStart.x,
                            min(visible.width * 0.95, max(280, visible.width * 0.35))), visible.width * 0.95)
        let minimumHeight = DesktopSubtitleOverlayText.lineHeight(settings.originalFontSize) + (settings.showTranslation ? DesktopSubtitleOverlayText.lineHeight(settings.translationFontSize) : 0) + 67
        let height = min(max(start.height + mouseStart.y - mouseNow.y, minimumHeight), visible.height * 0.85)
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
        let screens = NSScreen.screens
        guard let index = DesktopSubtitleOverlayLayout.screenIndex(for: panel.frame, screens: screens.map(\.frame)),
              screens.indices.contains(index) else {
            updateFrame(forceDefault: false)
            return
        }
        let screen = screens[index]
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
        var placement = store.settings
        placement.screenID = Self.screenID(screen)
        placement.normalizedX = min(max((origin.x + width / 2 - visible.minX) / visible.width, 0), 1)
        placement.normalizedBottom = min(max((origin.y - visible.minY) / visible.height, 0), 1)
        placement.widthFraction = min(max(width / visible.width, 0.35), 0.95)
        if resized { placement.preferredHeightFraction = min(max(height / visible.height, 0.1), 0.85) }
        lastAppliedSettings = placement
        store.update { $0 = placement }
        updateFrame(forceDefault: false)
    }

    func contentHeightDidChange(_ value: CGFloat) {
        guard value.isFinite, value > 0, abs(measuredContentHeight - value) > 1 else { return }
        measuredContentHeight = value
        guard settingsStore?.settings.preferredHeightFraction == nil, !gestureInProgress else { return }
        updateFrame(forceDefault: false)
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
        guard let screen = screen(for: settings) else { return }
        let visible = screen.visibleFrame
        let width = min(max(visible.width * settings.widthFraction, min(280, visible.width)), visible.width)
        let minimumReadingHeight = DesktopSubtitleOverlayText.lineHeight(settings.originalFontSize) + (settings.showTranslation ? DesktopSubtitleOverlayText.lineHeight(settings.translationFontSize) : 0) + 29
        let height = min(max(settings.preferredHeightFraction.map { visible.height * $0 }
                             ?? (measuredContentHeight + (isAdjusting ? 38 : 0)), minimumReadingHeight + (isAdjusting ? 38 : 0)), visible.height * 0.85)
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
        lastAppliedSettings = settings
    }

    private func screen(for settings: DesktopSubtitleOverlaySettings) -> NSScreen? {
        if let screenID = settings.screenID,
           let match = NSScreen.screens.first(where: { Self.screenID($0) == screenID }) { return match }
        return NSScreen.main ?? NSScreen.screens.first
    }

    private static func screenID(_ screen: NSScreen) -> String? {
        (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.stringValue
    }
}

private struct SubtitleContentHeightPreference: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

struct DesktopSubtitleOverlayView: View {
    @ObservedObject var feed: DesktopSubtitleOverlayFeed
    @ObservedObject var settingsStore: DesktopSubtitleOverlaySettingsStore
    @ObservedObject var controller: SubtitleOverlayController

    @State private var visibleState: DesktopSubtitleOverlayState?
    @State private var recentState: DesktopSubtitleOverlayPrevious?
    @State private var recentExpirationTask: Task<Void, Never>?
    @State private var expirationTask: Task<Void, Never>?
    @State private var moveStartFrame: NSRect?
    @State private var moveStartMouse: NSPoint?
    @State private var resizeStartFrame: NSRect?
    @State private var resizeStartMouse: NSPoint?

    private var settings: DesktopSubtitleOverlaySettings { settingsStore.settings }

    var body: some View {
        ZStack(alignment: .bottomTrailing) {
            VStack(spacing: 0) {
                if controller.isAdjusting {
                    HStack(spacing: 8) {
                        HStack {
                            Image(systemName: "hand.draw")
                            Text("拖动此处移动字幕")
                            Spacer()
                        }
                        .contentShape(Rectangle())
                        .gesture(moveGesture)
                        Button("完成") { controller.finishAdjusting() }
                            .buttonStyle(.borderedProminent)
                            .tint(.white.opacity(0.2))
                            .help("保存位置并返回实时字幕")
                    }
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 12)
                    .frame(height: 34)
                    .background(.mint.opacity(0.85), in: RoundedRectangle(cornerRadius: 8))
                    .padding(.bottom, 4)
                }

                if controller.isAdjusting {
                    ScrollView(.vertical) {
                        VStack(spacing: 12) {
                            if let previous = recentState,
                               previous.remainsVisible(at: Date(), retention: settings.retentionSeconds) {
                                VStack(spacing: 5) {
                                    subtitleLines(previous.state)
                                }
                                .opacity(0.8)
                            }
                            if let state = visibleState,
                               state.remainsVisible(at: Date(), retention: settings.retentionSeconds) {
                                VStack(spacing: 5) { subtitleLines(state) }
                            } else if controller.isAdjusting && recentState == nil {
                                if settings.showOriginal {
                                    subtitleText("Original subtitle text",
                                                 size: settings.originalFontSize, weight: .medium,
                                                 colorHex: settings.originalColorHex)
                                }
                                if settings.showTranslation {
                                    subtitleText("实时双语字幕预览",
                                                 size: settings.translationFontSize, weight: .regular,
                                                 colorHex: settings.translationColorHex)
                                }
                            }
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.horizontal, 18)
                        .padding(.vertical, 12)
                        .background {
                            RoundedRectangle(cornerRadius: 10)
                                .fill(Color.black.opacity(settings.backgroundOpacity))
                        }
                        // Intrinsic height is measured inside the scroll view,
                        // not truncated by the window's current viewport height.
                        .background {
                            GeometryReader { proxy in
                                Color.clear.preference(key: SubtitleContentHeightPreference.self,
                                                       value: proxy.size.height)
                            }
                        }
                    }
                    .scrollIndicators(.automatic)
                } else {
                    watchingContent
                }
            }

            if controller.isAdjusting {
                RoundedRectangle(cornerRadius: 10)
                    .strokeBorder(.mint.opacity(0.9), lineWidth: 1.5)
                    .allowsHitTesting(false)
                Image(systemName: "arrow.up.left.and.arrow.down.right")
                    .font(.system(size: 15, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 36, height: 36)
                    .background(.mint, in: RoundedRectangle(cornerRadius: 8))
                    .contentShape(Rectangle())
                    .help("拖动以调整字幕宽度和高度")
                    .accessibilityLabel("拖动以调整字幕宽度和高度")
                    .highPriorityGesture(resizeGesture)
            }
        }
        .background(Color.clear)
        .onPreferenceChange(SubtitleContentHeightPreference.self) { height in
            controller.contentHeightDidChange(height)
        }
        .onAppear {
            accept(feed.current)
            acceptRecent(feed.previous)
        }
        .onChange(of: feed.current) { _, state in accept(state) }
        .onChange(of: feed.previous) { _, state in acceptRecent(state) }
        .onChange(of: settings.retentionSeconds) { _, _ in
            accept(feed.current)
            acceptRecent(feed.previous)
        }
        .onDisappear {
            expirationTask?.cancel()
            recentExpirationTask?.cancel()
        }
    }

    // Each language keeps its own contiguous tail, so a long translation can
    // never push the latest original out of a short, click-through window.
    private var watchingContent: some View {
        // Prefer one complete pair; an older translation must not accompany a new original.
        let states = [visibleState ?? recentState?.state].compactMap { $0 }
        let original = states.map(\.original).filter { !$0.isEmpty }.joined(separator: "\n")
        let translation = states.filter(\.translationEnabled).map(\.translation)
            .filter { !$0.isEmpty }.joined(separator: "\n")
        let showOriginal = !original.isEmpty && (settings.showOriginal || !settings.showTranslation || translation.isEmpty)
        let showTranslation = settings.showTranslation && !translation.isEmpty
        let lineHeight = (showOriginal ? DesktopSubtitleOverlayText.lineHeight(settings.originalFontSize) : 0)
            + (showTranslation ? DesktopSubtitleOverlayText.lineHeight(settings.translationFontSize) : 0)
        return GeometryReader { geometry in
            let lines = max(1, min(2, Int(max(0, geometry.size.height - 29) / max(1, lineHeight))))
            VStack(spacing: 5) {
                if showOriginal {
                    watchingText(original, size: settings.originalFontSize, weight: .medium,
                                 colorHex: settings.originalColorHex, width: geometry.size.width - 36, lines: lines)
                }
                if showTranslation {
                    watchingText(translation, size: settings.translationFontSize, weight: .regular,
                                 colorHex: settings.translationColorHex, width: geometry.size.width - 36, lines: lines)
                }
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 12)
            .frame(width: geometry.size.width, height: geometry.size.height, alignment: .bottom)
            .background {
                if showOriginal || showTranslation {
                    RoundedRectangle(cornerRadius: 10).fill(Color.black.opacity(settings.backgroundOpacity))
                }
            }
            .preference(key: SubtitleContentHeightPreference.self, value: lineHeight * 2 + 29)
        }
    }

    private func watchingText(_ value: String, size: Double, weight: Font.Weight,
                              colorHex: String, width: CGFloat, lines: Int) -> some View {
        let font = NSFont.systemFont(ofSize: size, weight: weight == .medium ? .medium : .regular)
        return subtitleText(DesktopSubtitleOverlayText.latestLines(value, font: font, width: width, count: lines),
                            size: size, weight: weight, colorHex: colorHex)
            .accessibilityLabel(value)
    }

    private var moveGesture: some Gesture {
        DragGesture(minimumDistance: 2)
            .onChanged { value in
                if moveStartFrame == nil {
                    moveStartFrame = controller.currentPanelFrame
                    moveStartMouse = NSEvent.mouseLocation
                }
                if let start = moveStartFrame, let mouseStart = moveStartMouse {
                    controller.move(from: start, mouseStart: mouseStart, mouseNow: NSEvent.mouseLocation)
                }
            }
            .onEnded { _ in
                controller.commitPlacement(resized: false)
                moveStartFrame = nil
                moveStartMouse = nil
            }
    }

    private var resizeGesture: some Gesture {
        DragGesture(minimumDistance: 2)
            .onChanged { value in
                if resizeStartFrame == nil {
                    resizeStartFrame = controller.currentPanelFrame
                    resizeStartMouse = NSEvent.mouseLocation
                }
                if let start = resizeStartFrame, let mouseStart = resizeStartMouse {
                    controller.resize(from: start, mouseStart: mouseStart, mouseNow: NSEvent.mouseLocation)
                }
            }
            .onEnded { _ in
                controller.commitPlacement(resized: true)
                resizeStartFrame = nil
                resizeStartMouse = nil
            }
    }

    @ViewBuilder private func subtitleLines(_ state: DesktopSubtitleOverlayState) -> some View {
        let showOriginal = settings.showOriginal || !state.translationEnabled || !settings.showTranslation || state.translation.isEmpty
        if showOriginal && !state.original.isEmpty {
            subtitleText(state.original, size: settings.originalFontSize, weight: .medium,
                         colorHex: settings.originalColorHex)
        }
        if settings.showTranslation && state.translationEnabled && !state.translation.isEmpty {
            subtitleText(state.translation, size: settings.translationFontSize, weight: .regular,
                         colorHex: settings.translationColorHex)
        }
    }

    private func subtitleText(_ value: String, size: Double, weight: Font.Weight, colorHex: String) -> some View {
        Text(value)
            .font(.system(size: size, weight: weight))
            .foregroundStyle(Self.color(from: colorHex))
            .multilineTextAlignment(.center)
            .fixedSize(horizontal: false, vertical: true)
            .shadow(color: .black.opacity(settings.shadowStrength), radius: 3, x: 0, y: 1)
            .opacity(settings.opacity)
            .frame(maxWidth: .infinity)
            .accessibilityLabel(value)
    }

    static func color(from hex: String) -> Color {
        let text = DesktopSubtitleOverlaySettings.colorHex(hex)
        let value = Int(text.dropFirst(), radix: 16) ?? 0xFFFFFF
        return Color(.sRGB, red: Double((value >> 16) & 255) / 255,
                     green: Double((value >> 8) & 255) / 255,
                     blue: Double(value & 255) / 255, opacity: 1)
    }

    private func acceptRecent(_ state: DesktopSubtitleOverlayPrevious?) {
        recentExpirationTask?.cancel()
        guard let state, state.remainsVisible(at: Date(), retention: settings.retentionSeconds) else {
            recentState = nil
            return
        }
        recentState = state
        let remaining = max(0, settings.retentionSeconds -
                            Date().timeIntervalSince(state.replacedAt))
        let expectedEntryID = state.state.entryID
        let expectedReplaceTime = state.replacedAt
        recentExpirationTask = Task {
            try? await Task.sleep(for: .seconds(remaining))
            guard !Task.isCancelled,
                  recentState?.state.entryID == expectedEntryID,
                  recentState?.replacedAt == expectedReplaceTime else { return }
            recentState = nil
        }
    }

    private func accept(_ state: DesktopSubtitleOverlayState?) {
        expirationTask?.cancel()
        guard let state, state.isVisible else {
            visibleState = nil
            return
        }
        visibleState = state
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
            visibleState = nil
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
                    Button("恢复外观默认值") { store.resetAppearance() }
                }

                Toggle("启用悬浮字幕", isOn: boolBinding(\.enabled))
                HStack(spacing: 22) {
                    Toggle("原文", isOn: boolBinding(\.showOriginal))
                    Toggle("译文", isOn: boolBinding(\.showTranslation))
                }

                Divider()
                Text("外观").font(.headline)
                slider("字体大小", value: \DesktopSubtitleOverlaySettings.originalFontSize,
                       range: 16...48, suffix: " pt", fontPair: true)
                HStack(spacing: 22) {
                    ColorPicker("原文颜色", selection: colorBinding(\.originalColorHex),
                                supportsOpacity: false)
                    ColorPicker("译文颜色", selection: colorBinding(\.translationColorHex),
                                supportsOpacity: false)
                }
                slider("背景不透明度", value: \DesktopSubtitleOverlaySettings.backgroundOpacity,
                       range: 0...1, suffix: "%", multiplier: 100)

                Divider()
                Text("位置与大小").font(.headline)
                HStack {
                    Button(controller.isAdjusting ? "完成调整" : "调整位置与大小") {
                        if controller.isAdjusting {
                            controller.finishAdjusting()
                        } else {
                            store.update { $0.enabled = true }
                            controller.show(model: model, settings: store, adjusting: true)
                        }
                    }
                    Button("重置位置") { store.resetPosition(); controller.resetPosition() }
                    Button("高度自适应") {
                        store.update { $0.preferredHeightFraction = nil }
                        controller.resetAutoHeight()
                    }
                }
                Text("拖动顶部移动字幕，右下角调整大小。观看时自动跟随最新内容；调整时可滚动回看，点击“完成”后返回最新字幕。")
                    .font(.caption).foregroundStyle(.secondary)

                DisclosureGroup("高级选项") {
                    VStack(alignment: .leading, spacing: 10) {
                        Toggle("非调整模式点击穿透", isOn: boolBinding(\.clickThrough))
                        slider("定稿保留", value: \DesktopSubtitleOverlaySettings.retentionSeconds,
                               range: 1...15, suffix: " 秒")
                        slider("文字不透明度", value: \DesktopSubtitleOverlaySettings.opacity,
                               range: 0.35...1, suffix: "%", multiplier: 100)
                        slider("文字阴影", value: \DesktopSubtitleOverlaySettings.shadowStrength,
                               range: 0...1, suffix: "%", multiplier: 100)
                    }
                    .padding(.top, 10)
                }
            }
            .padding(20)
            .frame(maxWidth: 510, alignment: .leading)
        }
        .frame(width: 530, height: 510)
    }

    private func boolBinding(_ path: WritableKeyPath<DesktopSubtitleOverlaySettings, Bool>) -> Binding<Bool> {
        Binding(
            get: { store.settings[keyPath: path] },
            set: { newValue in store.update { $0[keyPath: path] = newValue } }
        )
    }

    private func colorBinding(_ path: WritableKeyPath<DesktopSubtitleOverlaySettings, String>) -> Binding<Color> {
        Binding(
            get: { DesktopSubtitleOverlayView.color(from: store.settings[keyPath: path]) },
            set: { value in
                guard let rgba = NSColor(value).usingColorSpace(.deviceRGB) else { return }
                let red = Int((rgba.redComponent * 255).rounded())
                let green = Int((rgba.greenComponent * 255).rounded())
                let blue = Int((rgba.blueComponent * 255).rounded())
                let hex = String(format: "#%02X%02X%02X", red, green, blue)
                store.update { $0[keyPath: path] = hex }
            }
        )
    }

    private func slider(_ title: String,
                        value keyPath: WritableKeyPath<DesktopSubtitleOverlaySettings, Double>,
                        range: ClosedRange<Double>,
                        suffix: String, multiplier: Double = 1, fontPair: Bool = false) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack {
                Text(title)
                Spacer()
                Text("\(Int((store.settings[keyPath: keyPath] * multiplier).rounded()))\(suffix)")
                    .monospacedDigit().foregroundStyle(.secondary)
            }
            Slider(value: Binding(
                get: { store.settings[keyPath: keyPath] },
                set: { value in
                    store.update {
                        $0[keyPath: keyPath] = value
                        if fontPair { $0.translationFontSize = min(44, max(14, value - 2)) }
                    }
                }
            ), in: range)
        }
    }
}

/// Keep a contiguous live reading window, rather than truncating the middle
/// between SwiftUI's first line and its head-truncated last line.
enum DesktopSubtitleOverlayText {
    static func lineHeight(_ size: CGFloat) -> CGFloat {
        NSLayoutManager().defaultLineHeight(for: .systemFont(ofSize: size))
    }
    static func latestLines(_ value: String, font: NSFont, width: CGFloat, count: Int = 2) -> String {
        guard !value.isEmpty, width > 0 else { return value }
        let storage = NSTextStorage(string: value, attributes: [.font: font])
        let layout = NSLayoutManager()
        let container = NSTextContainer(size: NSSize(width: width, height: .greatestFiniteMagnitude))
        container.lineFragmentPadding = 0
        layout.addTextContainer(container)
        storage.addLayoutManager(layout)
        let glyphs = layout.glyphRange(for: container)
        var lines: [NSRange] = []
        layout.enumerateLineFragments(forGlyphRange: glyphs) { _, _, _, range, _ in
            lines.append(range)
        }
        guard lines.count > max(1, count) else { return value }
        let characters = layout.characterRange(forGlyphRange: lines[lines.count - max(1, count)], actualGlyphRange: nil)
        return (value as NSString).substring(from: characters.location)
    }
}
