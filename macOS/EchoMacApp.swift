import AppKit
import SwiftUI

private extension AppThemeMode {
    var colorScheme: ColorScheme? {
        switch self {
        case .light: return .light
        case .dark: return .dark
        case .system: return nil
        }
    }
}

@main
struct EchoMacApp: App {
    init() {
        // Echo is intentionally a single-window app. Prevent macOS from
        // adding an automatic tab bar/tab strip to the standard window.
        NSWindow.allowsAutomaticWindowTabbing = false
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
        .windowResizability(.automatic)
    }
}

private struct WindowAccessor: NSViewRepresentable {
    let alwaysOnTop: Bool

    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async {
            if let window = view.window { configure(window) }
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {
        DispatchQueue.main.async {
            if let window = nsView.window { configure(window) }
        }
    }

    private func configure(_ window: NSWindow) {
        window.level = alwaysOnTop ? .floating : .normal
        window.collectionBehavior = alwaysOnTop ? [.canJoinAllSpaces, .fullScreenAuxiliary] : []
    }
}

struct ContentView: View {
    @StateObject private var model = SpeechViewModel()
    @State private var showSettings = false
    @State private var editingSubtitle: SubtitleEntry?
    @State private var isSummaryExpanded = true
    @AppStorage("themeMode") private var themeModeRaw = AppThemeMode.dark.rawValue

    private var themeMode: AppThemeMode {
        AppThemeMode(rawValue: themeModeRaw) ?? .dark
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                Text("ECHO").font(.caption.weight(.bold)).foregroundStyle(.mint)
                Spacer()
                Button { showSettings = true } label: {
                    Image(systemName: "gearshape")
                }
                .buttonStyle(.borderless)
                .help("设置")
                Button {
                    cycleThemeMode()
                } label: {
                    Image(systemName: themeMode.icon)
                        .foregroundStyle(themeMode == .light ? .orange : .secondary)
                }
                .buttonStyle(.borderless)
                .help("当前：\(themeMode.title)，点击切换主题")
                Button(action: model.toggleAlwaysOnTop) {
                    Image(systemName: model.isAlwaysOnTop ? "pin.fill" : "pin.slash")
                        .foregroundStyle(model.isAlwaysOnTop ? .mint : .secondary)
                }
                .buttonStyle(.borderless)
                .help(model.isAlwaysOnTop ? "取消置顶" : "置顶窗口")
            }

            SynchronizedTranscriptView(entries: model.entries, recognitionConfig: model.recognitionConfig,
                suggestedIDs: Set(model.correctionStatuses.keys), onEdit: { editingSubtitle = $0 })
                .frame(maxWidth: .infinity, maxHeight: .infinity)

            VStack(alignment: .leading, spacing: 10) {
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: 10) {
                        recordingButtons
                        connectionStatus
                        Spacer(minLength: 0)
                    }
                    VStack(alignment: .leading, spacing: 6) {
                        recordingButtons
                        connectionStatus
                    }
                }

                HStack(spacing: 10) {
                    Menu {
                        Button("新建存档") {
                            model.chooseArchive(nil)
                        }
                        if !model.archives.isEmpty {
                            Divider()
                            ForEach(model.archives) { archive in
                                Button(archive.title) {
                                    model.chooseArchive(archive.id)
                                }
                            }
                        }
                    } label: {
                        Label(model.selectedArchiveTitle, systemImage: "archivebox")
                            .lineLimit(1)
                            .truncationMode(.middle)
                            .frame(maxWidth: 260, alignment: .leading)
                    }
                    .menuStyle(.borderedButton)
                    .disabled(model.isRecording)
                    .help(model.selectedArchiveTitle)

                    Spacer(minLength: 0)
                    Button("导出") { model.exportAllSubtitles() }
                        .buttonStyle(.bordered)
                        .fixedSize()
                        .disabled(model.entries.isEmpty || model.isRecording)

                    Menu {
                        Button("清空字幕", role: .destructive) { model.clearTranscript() }
                            .disabled(model.isRecording || model.entries.isEmpty)
                        Button("拆出本段") { model.splitCompletedSegment() }
                            .disabled(!model.canSplitCompletedSegment)
                    } label: { Text("更多") }
                    .menuStyle(.borderedButton)
                    .fixedSize()
                }
                if !model.archiveStatus.isEmpty {
                    Text(model.archiveStatus)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                }

                HStack(spacing: 12) {
                    WaveformView(samples: model.waveformSamples, active: model.isRecording)
                    HStack(spacing: 6) {
                        Circle().fill(audioStatusColor).frame(width: 8, height: 8)
                        Text(audioStatusText).font(.caption).foregroundStyle(.secondary)
                    }
                    .fixedSize(horizontal: true, vertical: false)
                }
            }

            summaryPanel
            if !model.errorMessage.isEmpty {
                HStack(alignment: .top, spacing: 10) {
                    Text(model.errorMessage)
                        .foregroundStyle(.red)
                        .font(.callout)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    Button(action: model.dismissError) {
                        Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary)
                    }
                    .buttonStyle(.borderless)
                    .help("关闭警告")
                }
            }
        }
        .padding(20)
        .frame(minWidth: 680, idealWidth: 820, minHeight: 520, idealHeight: 650, alignment: .topLeading)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(WindowAccessor(alwaysOnTop: model.isAlwaysOnTop))
        .sheet(isPresented: $showSettings) { SettingsView(model: model) }
        .sheet(item: $editingSubtitle) { entry in SubtitleCorrectionEditor(model: model, entry: entry) }
        .preferredColorScheme(themeMode.colorScheme)
    }

    private var recordingButtons: some View {
        HStack(spacing: 10) {
            Menu {
                ForEach(AudioInputMode.allCases) { mode in
                    Button {
                        model.setInputMode(mode)
                    } label: {
                        AudioInputModeLabel(mode: mode)
                    }
                }
            } label: {
                AudioInputModeLabel(mode: model.inputMode)
                    .frame(minWidth: 148, alignment: .leading)
            }
            .menuStyle(.borderedButton)
            .disabled(model.isSwitchingInput)
            .help(model.isSwitchingInput ? "正在切换输入源" : "选择输入源")

            Button(action: model.toggleRecording) {
                Label(model.isRecording ? "停止录音" : "开始录音", systemImage: model.isRecording ? "stop.fill" : "mic.fill")
                    .frame(minWidth: 126)
            }
            .buttonStyle(.borderedProminent)
            .tint(model.isRecording ? .red : .mint)
            .layoutPriority(1)
        }
        .fixedSize(horizontal: true, vertical: false)
    }

    private var connectionStatus: some View {
        HStack(spacing: 7) {
            Circle()
                .fill(statusColor)
                .frame(width: 8, height: 8)
            Text(compactStatus)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
        .fixedSize(horizontal: true, vertical: false)
    }

    @ViewBuilder private var summaryPanel: some View {
        if model.isSummaryEnabled || !model.summaryText.isEmpty || !model.summaryStatus.isEmpty {
            VStack(alignment: .leading, spacing: model.summaryText.isEmpty ? 6 : 8) {
                HStack {
                    Label("AI 总结", systemImage: "sparkles")
                        .font(.headline)
                    if model.summaryText.isEmpty, model.isRecording {
                        Text("录音中，可随时生成")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    summaryMenu
                }
                if !model.summaryStatus.isEmpty {
                    Text(model.summaryStatus)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                if !model.summaryText.isEmpty {
                    if isSummaryExpanded {
                        ScrollView(.vertical) {
                            MarkdownSummaryView(markdown: model.summaryText)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .textSelection(.enabled)
                        }
                        .frame(maxHeight: 220)
                    }
                    HStack(spacing: 12) {
                        Button(isSummaryExpanded ? "收起" : "展开全部") {
                            isSummaryExpanded.toggle()
                        }
                        .buttonStyle(.borderless)
                        Button {
                            copySummaryToPasteboard()
                        } label: {
                            Label("复制", systemImage: "doc.on.doc")
                        }
                        .buttonStyle(.borderless)
                        Spacer()
                    }
                }
            }
            .padding(12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 10))
        }
    }

    private func cycleThemeMode() {
        let modes = AppThemeMode.allCases
        guard let index = modes.firstIndex(of: themeMode) else { return }
        themeModeRaw = modes[(index + 1) % modes.count].rawValue
    }

    private func copySummaryToPasteboard() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(model.summaryText, forType: .string)
    }

    private var compactStatus: String {
        switch model.sonioxConnectionState {
        case .connecting:
            return "正在连接 Soniox…"
        case .connected:
            return model.audioLevel > 0.035 ? "Soniox 已连接 · 识别中" : "Soniox 已连接 · 等待声音"
        case .recovering:
            return "正在恢复录音…"
        case .failed:
            return "Soniox 连接失败"
        case .idle:
            return model.status
        }
    }

    private var statusColor: Color {
        switch model.sonioxConnectionState {
        case .connecting: return .orange
        case .connected: return .mint
        case .recovering: return .orange
        case .failed: return .red
        case .idle: return .secondary
        }
    }

    private var audioStatusText: String {
        guard model.isRecording else { return "未在录音" }
        switch model.audioCaptureState {
        case .starting:
            return "正在准备音频…"
        case .recovering:
            return "正在恢复音频…"
        case .failed:
            return "音频输入异常"
        case .idle:
            return "等待音频"
        case .active:
            return model.audioLevel > 0.035 ? "检测到声音" : "等待声音"
        }
    }

    private var audioStatusColor: Color {
        guard model.isRecording else { return .secondary }
        switch model.audioCaptureState {
        case .failed: return .red
        case .starting, .recovering: return .orange
        case .active: return model.audioLevel > 0.035 ? .green : .orange
        case .idle: return .secondary
        }
    }

    @ViewBuilder
    private var summaryMenu: some View {
        Menu {
            Button("总结新内容") { model.generateAISummary() }
                .disabled(model.entries.isEmpty)
            Button("重新总结当前录音段") { model.regenerateAISummary() }
                .disabled(model.entries.isEmpty)
            Button("总结整个存档") { model.summarizeSelectedArchive() }
                .disabled(model.archives.isEmpty)
        } label: {
            Label("总结", systemImage: "sparkles")
        }
        .menuStyle(.borderedButton)
    }
}

struct SettingsView: View {
    @ObservedObject var model: SpeechViewModel
    @Environment(\.dismiss) private var dismiss
    @AppStorage("themeMode") private var themeModeRaw = AppThemeMode.dark.rawValue

    var body: some View {
        ScrollView(.vertical) {
            VStack(alignment: .leading, spacing: 18) {
                HStack {
                    Text("设置").font(.title2.weight(.semibold))
                    Spacer()
                    Button("完成") {
                        model.saveAPIKey()
                        model.saveSummarySettings()
                        model.saveRecognitionSettings()
                        dismiss()
                    }
                }

                settingsSection("常规") {
                    Picker("主题", selection: $themeModeRaw) {
                        ForEach(AppThemeMode.allCases) { mode in
                            Text(mode.title).tag(mode.rawValue)
                        }
                    }
                }

                settingsSection("识别与翻译") {
                    Picker("识别模式", selection: Binding(
                        get: { model.recognitionConfig.sourceLanguageMode },
                        set: { model.recognitionConfig.sourceLanguageMode = $0 }
                    )) {
                        Text("自动识别").tag(SourceLanguageMode.automatic)
                        Text("优先语言").tag(SourceLanguageMode.specified)
                    }
                    if model.recognitionConfig.sourceLanguageMode == .specified {
                        Picker("优先语言", selection: Binding(
                            get: { model.recognitionConfig.specifiedSourceLanguage },
                            set: {
                                model.recognitionConfig.specifiedSourceLanguage = $0
                                model.recognitionConfig.languageHints = [$0]
                            }
                        )) {
                            ForEach(LanguageOption.supported) { language in
                                Text(language.title).tag(language.code)
                            }
                        }
                        Toggle("仅识别此语言", isOn: Binding(
                            get: { model.recognitionConfig.strictLanguageRestriction },
                            set: { model.recognitionConfig.strictLanguageRestriction = $0 }
                        ))
                        Text("关闭时，优先语言只作为 Soniox 的识别提示。")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Toggle("启用翻译", isOn: Binding(
                        get: { model.recognitionConfig.translationEnabled },
                        set: { model.recognitionConfig.translationEnabled = $0 }
                    ))
                    if model.recognitionConfig.translationEnabled {
                        Picker("翻译目标", selection: Binding(
                            get: { model.recognitionConfig.targetTranslationLanguage },
                            set: { model.recognitionConfig.targetTranslationLanguage = $0 }
                        )) {
                            ForEach(LanguageOption.supported) { language in
                                Text(language.title).tag(language.code)
                            }
                        }
                    }
                    Toggle("区分说话人", isOn: Binding(
                        get: { model.recognitionConfig.speakerDiarizationEnabled },
                        set: { model.recognitionConfig.speakerDiarizationEnabled = $0 }
                    ))
                    Text("在字幕中使用 Speaker 1、Speaker 2 等匿名编号。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                settingsSection("字幕分段") {
                    segmentationSlider(
                        "Soniox 最大端点延迟",
                        value: integerSliderBinding(\.sonioxMaxEndpointDelayMilliseconds),
                        range: 500...3_000,
                        step: 50,
                        valueText: "\(model.transcriptSegmentationConfig.sonioxMaxEndpointDelayMilliseconds) ms",
                        explanation: "讲话停止后等待服务端确认端点的上限。"
                    )
                    segmentationSlider(
                        "Soniox 端点灵敏度",
                        value: segmentationBinding(\.sonioxEndpointSensitivity),
                        range: -1...1,
                        step: 0.1,
                        valueText: String(format: "%.1f", model.transcriptSegmentationConfig.sonioxEndpointSensitivity),
                        explanation: "调整服务端判断语义端点的灵敏度。"
                    )
                    segmentationStepper(
                        "Soniox 延迟调整等级",
                        value: segmentationBinding(\.sonioxEndpointLatencyAdjustmentLevel),
                        range: 0...3,
                        unit: "级",
                        explanation: "Soniox v5 的端点延迟调整参数。"
                    )

                    Toggle("启用本地静音兜底", isOn: segmentationBinding(\.localSilenceFallbackEnabled))
                    segmentationSlider(
                        "静音阈值",
                        value: segmentationBinding(\.localSilenceThresholdSeconds),
                        range: 0.5...20,
                        step: 0.5,
                        valueText: String(format: "%.1f 秒", model.transcriptSegmentationConfig.localSilenceThresholdSeconds),
                        explanation: "连续无新识别内容达到此时长，且词数达标时分段。"
                    )
                    .disabled(!model.transcriptSegmentationConfig.localSilenceFallbackEnabled)
                    segmentationStepper(
                        "静音兜底最少词数",
                        value: segmentationBinding(\.localSilenceMinimumWordCount),
                        range: 1...100,
                        unit: "词",
                        explanation: "避免短语或短暂停顿造成过度分段。"
                    )
                    .disabled(!model.transcriptSegmentationConfig.localSilenceFallbackEnabled)

                    Toggle("启用超长段落兜底", isOn: segmentationBinding(\.longSegmentFallbackEnabled))
                    segmentationStepper(
                        "长段兜底词数门槛",
                        value: segmentationBinding(\.longSegmentWordThreshold),
                        range: 10...1_000,
                        unit: "词",
                        explanation: "必须同时达到词数和时长门槛才会分段。"
                    )
                    .disabled(!model.transcriptSegmentationConfig.longSegmentFallbackEnabled)
                    segmentationSlider(
                        "长段兜底时长门槛",
                        value: segmentationBinding(\.longSegmentDurationThresholdSeconds),
                        range: 5...600,
                        step: 1,
                        valueText: "\(Int(model.transcriptSegmentationConfig.longSegmentDurationThresholdSeconds.rounded())) 秒",
                        explanation: "与长段词数门槛同时满足后触发。"
                    )
                    .disabled(!model.transcriptSegmentationConfig.longSegmentFallbackEnabled)

                    Text("所有修改会立即保存在本机，并在下一次创建 Soniox 会话时生效；当前录音连接和参数保持不变。睡眠唤醒后新建的会话使用当时保存的设置。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text("Soniox 端点延迟是讲话停止后的等待上限，不是单条字幕的最大时长。本地兜底也不保证严格的最大长度；设置较激进可能让分段变碎，并影响识别准确性。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Button("恢复默认值") { model.restoreDefaultSegmentationSettings() }
                        .buttonStyle(.borderless)
                }

                settingsSection("服务") {
                    SecureField("Soniox API Key", text: $model.sonioxAPIKey)
                        .textFieldStyle(.roundedBorder)
                    Text("用于实时识别和翻译，只保存在本机。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    SecureField("DeepSeek API Key（可选）", text: $model.deepSeekAPIKey)
                        .textFieldStyle(.roundedBorder)
                    Toggle("停止录音后自动生成 AI 总结", isOn: $model.isSummaryEnabled)
                    Toggle("自动 AI 语境校对（仅生成建议）", isOn: $model.isAICorrectionEnabled)
                    Text("默认关闭。开启后将已分句文字及相邻上下文发送给 DeepSeek，可能产生费用；不发送音频。建议需人工确认，不会自动覆盖文字。")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("课程术语（每行一个，最多使用 100 个）")
                    TextEditor(text: $model.correctionTerms)
                        .font(.body).frame(height: 90)
                        .overlay(RoundedRectangle(cornerRadius: 4).stroke(.secondary.opacity(0.3)))
                    Text("术语用于 AI 校对及下一次 Soniox 建连的识别提示，不作全局替换。")
                        .font(.caption).foregroundStyle(.secondary)
                    Text("总结会把文字稿发送到云端 AI；API Key 只保存在本机。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                settingsSection("文件") {
                    Text("文字稿保存位置")
                        .font(.subheadline.weight(.semibold))
                    Text(model.transcriptFolderPath)
                        .font(.callout.monospaced())
                        .textSelection(.enabled)
                    Text("每次停止录音后自动保存 .srt 文件；默认不保存音频。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Text("输入源可在主界面切换；捕获电脑音频需要在系统设置中允许 Echo 使用“屏幕与系统音频录制”。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .padding(24)
        }
        .frame(width: 520, height: 560)
    }

    @ViewBuilder
    private func settingsSection<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title).font(.headline)
            content()
        }
    }

    private func segmentationBinding<Value>(_ keyPath: WritableKeyPath<TranscriptSegmentationConfig, Value>) -> Binding<Value> {
        Binding(
            get: { model.transcriptSegmentationConfig[keyPath: keyPath] },
            set: { newValue in
                var config = model.transcriptSegmentationConfig
                config[keyPath: keyPath] = newValue
                model.transcriptSegmentationConfig = config
            }
        )
    }

    private func integerSliderBinding(_ keyPath: WritableKeyPath<TranscriptSegmentationConfig, Int>) -> Binding<Double> {
        Binding(
            get: { Double(model.transcriptSegmentationConfig[keyPath: keyPath]) },
            set: { newValue in
                var config = model.transcriptSegmentationConfig
                config[keyPath: keyPath] = Int(newValue.rounded())
                model.transcriptSegmentationConfig = config
            }
        )
    }

    private func segmentationSlider(
        _ title: String,
        value: Binding<Double>,
        range: ClosedRange<Double>,
        step: Double,
        valueText: String,
        explanation: String
    ) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text(title)
                Spacer(minLength: 8)
                Text(valueText).monospacedDigit().foregroundStyle(.secondary)
            }
            Slider(value: value, in: range, step: step)
            Text(explanation).font(.caption).foregroundStyle(.secondary)
        }
    }

    private func segmentationStepper(
        _ title: String,
        value: Binding<Int>,
        range: ClosedRange<Int>,
        unit: String,
        explanation: String
    ) -> some View {
        HStack(alignment: .center, spacing: 8) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                Text(explanation).font(.caption).foregroundStyle(.secondary)
            }
            Spacer(minLength: 4)
            Stepper(value: value, in: range) {
                Text("\(value.wrappedValue) \(unit)").monospacedDigit()
            }
            .fixedSize()
        }
    }
}
