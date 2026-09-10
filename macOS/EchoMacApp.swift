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

            SynchronizedTranscriptView(entries: model.entries)
                .frame(maxWidth: .infinity, maxHeight: .infinity)

            HStack(spacing: 14) {
                Button(action: model.cycleInputMode) {
                    AudioInputModeLabel(mode: model.inputMode)
                        .frame(minWidth: 132)
                }
                .buttonStyle(.bordered)
                .tint(.mint)
                .disabled(model.isSwitchingInput)
                .help(model.isSwitchingInput ? "正在切换输入源" : "点击切换输入源")
                Button(action: model.generateAISummary) {
                    Label("总结新内容", systemImage: "sparkles")
                }
                .disabled(model.entries.isEmpty)
                .help("只总结上次总结后新增或修正的文字，不会停止录音")
                Button(action: model.regenerateAISummary) {
                    Label("重新总结当前录音段", systemImage: "arrow.clockwise")
                }
                .disabled(model.entries.isEmpty)
                .help("重新总结当前录音段，不会停止录音")
                Button(action: model.summarizeSelectedArchive) {
                    Label("总结整个存档", systemImage: "archivebox")
                }
                .disabled(model.archives.isEmpty)
                .help("根据所选存档的全部文字生成总结，不会停止录音")
                Button("清空") { model.clearTranscript() }
                    .disabled(model.isRecording)
                Button("导出全部字幕") { model.exportAllSubtitles() }
                    .disabled(model.entries.isEmpty || model.isRecording)
                Spacer()
            }

            HStack(spacing: 8) {
                Button(action: model.toggleRecording) {
                    Label(model.isRecording ? "停止录音" : "开始录音", systemImage: model.isRecording ? "stop.fill" : "mic.fill")
                        .frame(width: 120)
                }
                .buttonStyle(.borderedProminent)
                .tint(model.isRecording ? .red : .mint)
                Image(systemName: "link")
                    .foregroundStyle(model.isRecording ? .mint : .secondary)
                Text(model.status)
                    .foregroundStyle(.secondary)
                Spacer()
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
                        .frame(minWidth: 180, alignment: .leading)
                }
                .menuStyle(.borderedButton)
                .disabled(model.isRecording)
                .help("选择存档；下一次录音会接续所选存档")
                if model.canSplitCompletedSegment {
                    Button("拆出本段") {
                        model.splitCompletedSegment()
                    }
                    .buttonStyle(.bordered)
                    .help("把刚完成的录音段从当前存档拆成新的存档")
                }
                Text(model.archiveStatus)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                Spacer()
            }

            HStack(spacing: 12) {
                WaveformView(samples: model.waveformSamples, active: model.isRecording)
                Circle()
                    .fill(model.isRecording
                        ? (model.audioLevel > 0.035 ? .green : (model.isReceivingAudio ? .orange : .gray))
                        : .gray)
                    .frame(width: 8, height: 8)
                Text(!model.isRecording
                    ? "等待\(model.inputMode.title)输入"
                    : (model.audioLevel > 0.035
                        ? "检测到\(model.inputMode.title)输入"
                        : (model.isReceivingAudio ? "已连接\(model.inputMode.title)，等待声音" : "等待\(model.inputMode.title)输入")))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if model.isSummaryEnabled || !model.summaryText.isEmpty || !model.summaryStatus.isEmpty {
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Label("AI 总结", systemImage: "sparkles")
                            .font(.headline)
                        Spacer()
                        Text(model.summaryStatus)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    if !model.summaryText.isEmpty {
                        ScrollView(.vertical) {
                            MarkdownSummaryView(markdown: model.summaryText)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .textSelection(.enabled)
                        }
                        .frame(maxHeight: 150)
                    } else if model.isRecording {
                        Text("停止录音后自动生成中文总结")
                            .foregroundStyle(.secondary)
                            .font(.callout)
                    }
                }
                .padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 10))
            }
            if !model.errorMessage.isEmpty {
                HStack(alignment: .top, spacing: 10) {
                    Text(model.errorMessage)
                        .foregroundStyle(.red)
                        .font(.callout)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    Button(action: model.dismissError) {
                        Image(systemName: "xmark.circle.fill")
                            .foregroundStyle(.secondary)
                    }
                    .buttonStyle(.borderless)
                    .help("关闭警告")
                }
            }
        }
        .padding(28)
        .frame(minWidth: 680, idealWidth: 820, minHeight: 520, idealHeight: 650, alignment: .topLeading)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(
            WindowAccessor(alwaysOnTop: model.isAlwaysOnTop)
        )
        .sheet(isPresented: $showSettings) {
            SettingsView(model: model)
        }
        .preferredColorScheme(themeMode.colorScheme)
    }

    private func cycleThemeMode() {
        let modes = AppThemeMode.allCases
        guard let index = modes.firstIndex(of: themeMode) else { return }
        themeModeRaw = modes[(index + 1) % modes.count].rawValue
    }
}

struct SettingsView: View {
    @ObservedObject var model: SpeechViewModel
    @Environment(\.dismiss) private var dismiss
    @AppStorage("themeMode") private var themeModeRaw = AppThemeMode.dark.rawValue

    var body: some View {
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

            Picker("界面主题", selection: $themeModeRaw) {
                ForEach(AppThemeMode.allCases) { mode in
                    Text(mode.title).tag(mode.rawValue)
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("Soniox API Key").font(.headline)
                SecureField("粘贴你的 Soniox API Key", text: $model.sonioxAPIKey)
                    .textFieldStyle(.roundedBorder)
                Text("用于 Soniox 实时识别和翻译，只保存在本机。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Text("输入源可在主界面切换；捕获电脑音频需要在系统设置中允许 Echo 使用“屏幕与系统音频录制”。")
                .font(.caption)
                .foregroundStyle(.secondary)

            Divider()

            VStack(alignment: .leading, spacing: 10) {
                Text("识别与翻译").font(.headline)
                Picker("源语言", selection: Binding(
                    get: { model.recognitionConfig.sourceLanguageMode },
                    set: { model.recognitionConfig.sourceLanguageMode = $0 }
                )) {
                    ForEach(SourceLanguageMode.allCases) { mode in
                        Text(mode.title).tag(mode)
                    }
                }
                if model.recognitionConfig.sourceLanguageMode == .specified {
                    Picker("指定语言", selection: Binding(
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
                    Toggle("严格限制为指定语言", isOn: Binding(
                        get: { model.recognitionConfig.strictLanguageRestriction },
                        set: { model.recognitionConfig.strictLanguageRestriction = $0 }
                    ))
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
                Toggle("显示 Speaker 编号", isOn: Binding(
                    get: { model.recognitionConfig.speakerDiarizationEnabled },
                    set: { model.recognitionConfig.speakerDiarizationEnabled = $0 }
                ))
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                Toggle("停止录音后自动生成 AI 总结", isOn: $model.isSummaryEnabled)
                SecureField("粘贴 DeepSeek API Key（用于 AI 总结）", text: $model.deepSeekAPIKey)
                    .textFieldStyle(.roundedBorder)
                Text("可选功能。总结会把本次英文文字稿发送到云端 AI；API Key 只保存在本机。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                Text("文字稿保存位置").font(.headline)
                Text(model.transcriptFolderPath)
                    .font(.callout.monospaced())
                    .textSelection(.enabled)
                Text("每次停止录音后自动保存 .srt 文件；默认不保存音频。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(24)
        .frame(width: 520)
    }
}
