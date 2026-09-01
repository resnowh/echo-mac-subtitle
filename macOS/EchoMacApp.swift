import AVFoundation
import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct SubtitleEntry: Identifiable {
    let id = UUID()
    var start: TimeInterval
    var end: TimeInterval
    var english: String
    var chinese: String
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

final class SpeechViewModel: NSObject, ObservableObject, @unchecked Sendable {
    @Published var isRecording = false
    @Published var english = ""
    @Published var chinese = ""
    @Published var status = "准备就绪"
    @Published var errorMessage = ""
    @Published var audioLevel = 0.0
    @Published var waveformSamples = Array(repeating: 0.0, count: 48)
    @Published var entries: [SubtitleEntry] = []
    @Published var fileStatus = ""
    @Published var sonioxAPIKey: String
    @Published var isAlwaysOnTop: Bool

    private let audioEngine = AVAudioEngine()
    private let audioQueue = DispatchQueue(label: "local.echo.soniox-audio")
    private let sonioxURL = URL(string: "wss://stt-rt.soniox.com/transcribe-websocket")!
    private var socket: URLSessionWebSocketTask?
    private var urlSession: URLSession?
    private var converter: AVAudioConverter?
    private var targetAudioFormat: AVAudioFormat?
    private var sessionStartedAt: Date?
    private var currentEntryID: UUID?
    private var currentSessionFileURL: URL?
    private var sessionEntriesStartIndex = 0
    private var currentSessionFinished = false
    private var activeSessionID = UUID()
    private var isClosingSocket = false

    // Soniox sends finalized tokens once and provisional tokens repeatedly.
    // Keep these separately so a provisional update never erases the transcript.
    private var finalEnglish = ""
    private var partialEnglish = ""
    private var finalChinese = ""
    private var partialChinese = ""
    private var currentSourceStart: TimeInterval?
    private var currentSourceEnd: TimeInterval?
    private var segmentationTimer: Timer?
    private var lastTokenReceivedAt: Date?

    override init() {
        let environmentKey = ProcessInfo.processInfo.environment["SONIOX_API_KEY"] ?? ""
        let savedKey = UserDefaults.standard.string(forKey: "sonioxAPIKey") ?? ""
        sonioxAPIKey = savedKey.isEmpty ? environmentKey : savedKey
        isAlwaysOnTop = UserDefaults.standard.object(forKey: "alwaysOnTop") as? Bool ?? true
        super.init()
    }

    func toggleRecording() {
        isRecording ? stop() : start()
    }

    private func start() {
        errorMessage = ""
        fileStatus = ""
        let key = sonioxAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else {
            errorMessage = "请先填写 Soniox API Key。可从 console.soniox.com 创建。"
            status = "等待 Soniox API Key"
            return
        }
        UserDefaults.standard.set(key, forKey: "sonioxAPIKey")

        let permission = AVCaptureDevice.authorizationStatus(for: .audio)
        if permission == .authorized {
            do { try beginCapture(apiKey: key) }
            catch { errorMessage = error.localizedDescription; status = "启动失败" }
            return
        }
        guard permission == .notDetermined else {
            errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo。"
            return
        }
        status = "等待麦克风授权"
        AVCaptureDevice.requestAccess(for: .audio) { [weak self] granted in
            DispatchQueue.main.async {
                guard let self else { return }
                guard granted else {
                    self.errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo。"
                    return
                }
                do { try self.beginCapture(apiKey: key) }
                catch { self.errorMessage = error.localizedDescription; self.status = "启动失败" }
            }
        }
    }

    private func beginCapture(apiKey: String) throws {
        let input = audioEngine.inputNode
        let inputFormat = input.outputFormat(forBus: 0)
        guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0 else {
            throw NSError(domain: "Echo", code: 3, userInfo: [NSLocalizedDescriptionKey: "没有检测到可用的麦克风输入，请检查 Mac 的输入设备设置。"])
        }
        guard let targetFormat = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true),
              let converter = AVAudioConverter(from: inputFormat, to: targetFormat) else {
            throw NSError(domain: "Echo", code: 4, userInfo: [NSLocalizedDescriptionKey: "无法准备麦克风音频格式。"])
        }

        input.removeTap(onBus: 0)
        self.converter = converter
        self.targetAudioFormat = targetFormat
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        currentEntryID = nil
        currentSessionFileURL = nil
        currentSessionFinished = false
        isClosingSocket = false
        sessionStartedAt = Date()
        sessionEntriesStartIndex = entries.count
        activeSessionID = UUID()
        let sessionID = activeSessionID

        openSonioxSocket(apiKey: apiKey, sessionID: sessionID)
        input.installTap(onBus: 0, bufferSize: 1024, format: inputFormat) { [weak self] buffer, _ in
            let level = Self.rmsLevel(buffer)
            DispatchQueue.main.async { [weak self] in self?.recordAudioLevel(level) }
            self?.audioQueue.async { [weak self] in
                self?.sendAudio(buffer, sessionID: sessionID)
            }
        }
        audioEngine.prepare()
        do {
            try audioEngine.start()
        } catch {
            input.removeTap(onBus: 0)
            socket?.cancel(with: .goingAway, reason: nil)
            socket = nil
            throw error
        }
        isRecording = true
        status = "正在通过 Soniox 实时识别与翻译"
        segmentationTimer?.invalidate()
        segmentationTimer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            self?.autoFinalizeIfNeeded()
        }
    }

    private func openSonioxSocket(apiKey: String, sessionID: UUID) {
        let session = URLSession(configuration: .default)
        let task = session.webSocketTask(with: sonioxURL)
        urlSession = session
        socket = task
        task.resume()

        let config: [String: Any] = [
            "api_key": apiKey,
            "model": "stt-rt-v5",
            "audio_format": "pcm_s16le",
            "sample_rate": 16_000,
            "num_channels": 1,
            "language_hints": ["en"],
            "enable_language_identification": true,
            "enable_endpoint_detection": true,
            "max_endpoint_delay_ms": 900,
            "translation": [
                "type": "one_way",
                "target_language": "zh"
            ]
        ]
        do {
            let data = try JSONSerialization.data(withJSONObject: config)
            let json = String(decoding: data, as: UTF8.self)
            task.send(.string(json)) { [weak self] error in
                if let error {
                    DispatchQueue.main.async {
                        guard let self, self.activeSessionID == sessionID else { return }
                        self.errorMessage = "Soniox 连接失败：\(error.localizedDescription)"
                        self.status = "连接失败"
                    }
                }
            }
            receiveSonioxMessages(from: task, sessionID: sessionID)
        } catch {
            errorMessage = "Soniox 配置失败：\(error.localizedDescription)"
        }
    }

    private func receiveSonioxMessages(from task: URLSessionWebSocketTask, sessionID: UUID) {
        task.receive { [weak self] result in
            guard let self else { return }
            switch result {
            case .success(let message):
                let text: String?
                switch message {
                case .string(let value): text = value
                case .data(let data): text = String(data: data, encoding: .utf8)
                @unknown default: text = nil
                }
                if let text {
                    DispatchQueue.main.async { [weak self] in
                        guard let self, self.activeSessionID == sessionID else { return }
                        self.handleSonioxMessage(text)
                    }
                }
                self.receiveSonioxMessages(from: task, sessionID: sessionID)
            case .failure(let error):
                DispatchQueue.main.async { [weak self] in
                    guard let self, self.activeSessionID == sessionID else { return }
                    if self.isRecording && !self.isClosingSocket {
                        self.errorMessage = "Soniox 连接中断：\(error.localizedDescription)"
                        self.status = "连接中断"
                    }
                }
            }
        }
    }

    private func sendAudio(_ buffer: AVAudioPCMBuffer, sessionID: UUID) {
        guard activeSessionID == sessionID, let socket, let converter, let targetFormat = targetAudioFormat else { return }
        let ratio = targetFormat.sampleRate / max(buffer.format.sampleRate, 1)
        let capacity = AVAudioFrameCount(max(1, Int(Double(buffer.frameLength) * ratio) + 1024))
        guard let converted = AVAudioPCMBuffer(pcmFormat: targetFormat, frameCapacity: capacity) else { return }
        var consumed = false
        var conversionError: NSError?
        let conversionStatus = converter.convert(to: converted, error: &conversionError) { _, status in
            if consumed {
                status.pointee = .noDataNow
                return nil
            }
            consumed = true
            status.pointee = .haveData
            return buffer
        }
        guard conversionStatus != .error, converted.frameLength > 0,
              let dataPointer = converted.audioBufferList.pointee.mBuffers.mData else { return }
        let dataSize = Int(converted.audioBufferList.pointee.mBuffers.mDataByteSize)
        let data = Data(bytes: dataPointer, count: dataSize)
        socket.send(.data(data)) { [weak self] error in
            if let error {
                DispatchQueue.main.async {
                    guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
                    self.errorMessage = "音频发送失败：\(error.localizedDescription)"
                }
            }
        }
    }

    func stop() {
        guard isRecording || socket != nil else { return }
        isRecording = false
        isClosingSocket = true
        audioEngine.stop()
        audioEngine.inputNode.removeTap(onBus: 0)
        audioLevel = 0
        waveformSamples = Array(repeating: 0.0, count: waveformSamples.count)
        segmentationTimer?.invalidate()
        segmentationTimer = nil
        currentSessionFinished = true
        updateCurrentEntry()
        finalizeCurrentEntry()
        saveCurrentSessionFile()
        status = "正在完成最后一句"

        if let socket {
            socket.send(.data(Data())) { [weak self] _ in
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
                    self?.socket?.cancel(with: .goingAway, reason: nil)
                    self?.socket = nil
                    self?.urlSession?.invalidateAndCancel()
                    self?.urlSession = nil
                    self?.status = "已停止"
                }
            }
        } else {
            status = "已停止"
        }
    }

    private func handleSonioxMessage(_ text: String) {
        guard let data = text.data(using: .utf8),
              let response = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
        if let errorMessage = response["error_message"] as? String {
            let requestID = response["request_id"] as? String
            errorMessageReceived("\(errorMessage)\(requestID.map { "（request_id: \($0)）" } ?? "")")
            return
        }
        if response["finished"] as? Bool == true {
            finalizeCurrentEntry()
            status = "已停止"
            socket = nil
            return
        }

        partialEnglish = ""
        partialChinese = ""
        lastTokenReceivedAt = Date()
        var reachedEndpoint = false
        if let tokens = response["tokens"] as? [[String: Any]] {
            for token in tokens {
                guard let tokenText = token["text"] as? String, !tokenText.isEmpty else { continue }
                if tokenText == "<end>" || tokenText == "<fin>" {
                    reachedEndpoint = true
                    continue
                }
                let translationStatus = token["translation_status"] as? String ?? "none"
                let isTranslation = translationStatus == "translation"
                let isFinal = token["is_final"] as? Bool ?? false
                if isTranslation {
                    isFinal ? (finalChinese += tokenText) : (partialChinese += tokenText)
                } else {
                    isFinal ? (finalEnglish += tokenText) : (partialEnglish += tokenText)
                    if let start = token["start_ms"] as? Double { currentSourceStart = min(currentSourceStart ?? start / 1000, start / 1000) }
                    if let end = token["end_ms"] as? Double { currentSourceEnd = max(currentSourceEnd ?? 0, end / 1000) }
                }
            }
        }
        if !finalEnglish.isEmpty || !partialEnglish.isEmpty || !finalChinese.isEmpty || !partialChinese.isEmpty {
            ensureCurrentEntry()
            updateCurrentEntry()
        }
        if reachedEndpoint {
            finalizeCurrentEntry()
            saveCurrentSessionFile()
        }
    }

    private func autoFinalizeIfNeeded() {
        guard isRecording, currentEntryID != nil else { return }
        let text = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        let translation = (finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty, !translation.isEmpty else { return }

        let words = text.split(whereSeparator: { $0 == " " || $0 == "\n" }).count
        let elapsed = elapsedSinceSessionStart - (currentSourceStart ?? elapsedSinceSessionStart)
        let endsSentence = text.range(of: "[.!?。！？][\\\"’”)]*$", options: .regularExpression) != nil
        let longEnough = words >= 16 || (text.count >= 90 && elapsed >= 3)
        let quietLongEnough = lastTokenReceivedAt.map { Date().timeIntervalSince($0) >= 1.8 } ?? false

        // Soniox normally emits <end>. These fallbacks handle devices/streams
        // where endpoint tokens are omitted, so one row cannot grow forever.
        guard (endsSentence && words >= 3) || longEnough || (quietLongEnough && words >= 5) else { return }
        finalizeCurrentEntry()
        saveCurrentSessionFile()
    }

    private func ensureCurrentEntry() {
        guard currentEntryID == nil else { return }
        let start = currentSourceStart ?? elapsedSinceSessionStart
        let entry = SubtitleEntry(start: start, end: start, english: "", chinese: "")
        entries.append(entry)
        currentEntryID = entry.id
    }

    private func updateCurrentEntry() {
        guard let currentEntryID,
              let index = entries.firstIndex(where: { $0.id == currentEntryID }) else { return }
        entries[index].english = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        entries[index].chinese = (finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines)
        entries[index].start = currentSourceStart ?? entries[index].start
        entries[index].end = max(entries[index].start + 0.1, currentSourceEnd ?? elapsedSinceSessionStart)
        refreshFullTranscript()
        if currentSessionFinished { saveCurrentSessionFile() }
    }

    private func finalizeCurrentEntry() {
        updateCurrentEntry()
        guard let currentEntryID,
              let index = entries.firstIndex(where: { $0.id == currentEntryID }) else { return }
        if entries[index].english.isEmpty && entries[index].chinese.isEmpty {
            entries.remove(at: index)
        }
        self.currentEntryID = nil
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        refreshFullTranscript()
    }

    private var elapsedSinceSessionStart: TimeInterval {
        guard let sessionStartedAt else { return 0 }
        return max(0, Date().timeIntervalSince(sessionStartedAt))
    }

    private func refreshFullTranscript() {
        english = entries.map(\.english).filter { !$0.isEmpty }.joined(separator: "\n")
        chinese = entries.map(\.chinese).filter { !$0.isEmpty }.joined(separator: "\n")
    }

    private static func rmsLevel(_ buffer: AVAudioPCMBuffer) -> Double {
        guard let samples = buffer.floatChannelData?.pointee else { return 0 }
        let count = Int(buffer.frameLength)
        guard count > 0 else { return 0 }
        var sum = 0.0
        for index in 0..<count {
            let sample = Double(samples[index])
            sum += sample * sample
        }
        return min(1, max(0, sqrt(sum / Double(count)) * 7.5))
    }

    private func recordAudioLevel(_ level: Double) {
        // Attack quickly when speech starts, then decay more slowly. This
        // keeps the history readable without inventing movement when silent.
        let previous = audioLevel
        let smoothed = level >= previous
            ? previous * 0.25 + level * 0.75
            : previous * 0.82 + level * 0.18
        audioLevel = smoothed
        waveformSamples.append(smoothed)
        if waveformSamples.count > 48 { waveformSamples.removeFirst() }
    }

    private func errorMessageReceived(_ message: String) {
        errorMessage = "Soniox 翻译失败：\(message)"
        status = "翻译失败"
    }

    var transcriptFolderPath: String {
        FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first?.path ?? FileManager.default.temporaryDirectory.path
    }

    func saveAPIKey() {
        let key = sonioxAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        if key.isEmpty {
            UserDefaults.standard.removeObject(forKey: "sonioxAPIKey")
        } else {
            UserDefaults.standard.set(key, forKey: "sonioxAPIKey")
        }
        sonioxAPIKey = key
    }

    func toggleAlwaysOnTop() {
        isAlwaysOnTop.toggle()
        UserDefaults.standard.set(isAlwaysOnTop, forKey: "alwaysOnTop")
    }

    func clearTranscript() {
        activeSessionID = UUID()
        socket?.cancel(with: .goingAway, reason: nil)
        socket = nil
        urlSession?.invalidateAndCancel()
        urlSession = nil
        entries = []
        english = ""
        chinese = ""
        audioLevel = 0
        waveformSamples = Array(repeating: 0.0, count: waveformSamples.count)
        currentEntryID = nil
        currentSessionFileURL = nil
        currentSessionFinished = false
        sessionStartedAt = nil
        sessionEntriesStartIndex = 0
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        errorMessage = ""
        fileStatus = ""
        status = "准备就绪"
    }

    func exportAllSubtitles() {
        guard !entries.isEmpty else { return }
        let panel = NSSavePanel()
        panel.title = "导出全部字幕"
        panel.nameFieldStringValue = "echo-subtitles.srt"
        panel.allowedContentTypes = [UTType(filenameExtension: "srt") ?? .plainText]
        panel.begin { [weak self] response in
            guard response == .OK, let url = panel.url, let self else { return }
            do {
                let source = self.entries.filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
                try self.srtText(for: source, base: source.first?.start ?? 0).write(to: url, atomically: true, encoding: .utf8)
                self.fileStatus = "已导出：\(url.path)"
            } catch { self.fileStatus = "导出失败：\(error.localizedDescription)" }
        }
    }

    private func saveCurrentSessionFile() {
        let source = Array(entries.dropFirst(sessionEntriesStartIndex))
            .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        guard !source.isEmpty else { return }
        let url: URL
        if let currentSessionFileURL { url = currentSessionFileURL }
        else {
            let downloads = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first ?? FileManager.default.temporaryDirectory
            let formatter = DateFormatter()
            formatter.dateFormat = "yyyyMMdd-HHmmss"
            url = downloads.appendingPathComponent("Echo-\(formatter.string(from: Date())).srt")
            currentSessionFileURL = url
        }
        do {
            try srtText(for: source, base: source.first?.start ?? 0).write(to: url, atomically: true, encoding: .utf8)
            fileStatus = "已保存文字稿：\(url.path)"
        } catch { fileStatus = "字幕保存失败：\(error.localizedDescription)" }
    }

    private func srtText(for source: [SubtitleEntry], base: TimeInterval) -> String {
        source.enumerated().map { number, entry in
            let start = max(0, entry.start - base)
            let end = max(start + 0.5, entry.end - base)
            let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
            let translated = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
            let body = translated.isEmpty ? original : "\(original)\n\(translated)"
            return "\(number + 1)\n\(srtTime(start)) --> \(srtTime(end))\n\(body)\n"
        }.joined(separator: "\n")
    }

    private func srtTime(_ seconds: TimeInterval) -> String {
        let milliseconds = Int(seconds * 1000)
        return String(format: "%02d:%02d:%02d,%03d", milliseconds / 3_600_000, (milliseconds / 60_000) % 60, (milliseconds / 1000) % 60, milliseconds % 1000)
    }
}

struct ContentView: View {
    @StateObject private var model = SpeechViewModel()
    @State private var showSettings = false

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
                Button(action: model.toggleRecording) {
                    Label(model.isRecording ? "停止录音" : "开始录音", systemImage: model.isRecording ? "stop.fill" : "mic.fill")
                        .frame(width: 120)
                }
                .buttonStyle(.borderedProminent)
                .tint(model.isRecording ? .red : .mint)
                Text(model.status).foregroundStyle(.secondary)
                Spacer()
                Button("清空") { model.clearTranscript() }
                    .disabled(model.isRecording)
                Button("导出全部字幕") { model.exportAllSubtitles() }
                    .disabled(model.entries.isEmpty || model.isRecording)
            }
            HStack(spacing: 12) {
                WaveformView(samples: model.waveformSamples, active: model.isRecording)
                Circle()
                    .fill(model.isRecording && model.audioLevel > 0.035 ? .green : .gray)
                    .frame(width: 8, height: 8)
                Text(model.isRecording && model.audioLevel > 0.035 ? "检测到声音输入" : "等待麦克风声音")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if !model.errorMessage.isEmpty {
                Text(model.errorMessage).foregroundStyle(.red).font(.callout)
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
    }
}

struct SettingsView: View {
    @ObservedObject var model: SpeechViewModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                Text("设置").font(.title2.weight(.semibold))
                Spacer()
                Button("完成") {
                    model.saveAPIKey()
                    dismiss()
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                Text("Soniox API Key").font(.headline)
                SecureField("粘贴你的 Soniox API Key", text: $model.sonioxAPIKey)
                    .textFieldStyle(.roundedBorder)
                Text("用于 Soniox 实时识别和中文翻译，只保存在本机。")
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

private struct TranscriptBottomPreferenceKey: PreferenceKey {
    static let defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = nextValue()
    }
}

struct SynchronizedTranscriptView: View {
    let entries: [SubtitleEntry]
    @State private var isAtBottom = true
    @State private var hasNewContent = false

    var body: some View {
        GeometryReader { container in
            ScrollViewReader { proxy in
                ZStack(alignment: .bottomTrailing) {
                    ScrollView(.vertical) {
                        VStack(spacing: 0) {
                            HStack(alignment: .top, spacing: 14) {
                                Text("英文原文").font(.headline).frame(maxWidth: .infinity, alignment: .leading)
                                Text("中文翻译").font(.headline).frame(maxWidth: .infinity, alignment: .leading)
                            }
                            .padding(.bottom, 10)
                            if entries.isEmpty {
                                HStack(alignment: .top, spacing: 14) {
                                    Text("开始说英文后，每条识别结果会保留在这里").foregroundStyle(.secondary)
                                    Text("Soniox 翻译会逐条追加到这里").foregroundStyle(.secondary)
                                }
                                .frame(maxWidth: .infinity, alignment: .leading)
                            } else {
                                ForEach(entries) { entry in
                                    HStack(alignment: .top, spacing: 14) {
                                        Text(entry.english.isEmpty ? "…" : entry.english)
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                        Text(entry.chinese.isEmpty ? "翻译中…" : entry.chinese)
                                            .foregroundStyle(entry.chinese.isEmpty ? .secondary : .primary)
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                    }
                                    .padding(.vertical, 8)
                                    Divider()
                                }
                            }
                            GeometryReader { bottom in
                                Color.clear
                                    .preference(key: TranscriptBottomPreferenceKey.self,
                                                value: bottom.frame(in: .named("transcriptScroll")).maxY)
                            }
                            .frame(height: 1)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .id("subtitle-content")
                    }
                    .coordinateSpace(name: "transcriptScroll")
                    .onPreferenceChange(TranscriptBottomPreferenceKey.self) { bottomMaxY in
                        let nowAtBottom = bottomMaxY <= container.size.height + 24
                        isAtBottom = nowAtBottom
                        if nowAtBottom { hasNewContent = false }
                    }

                    if hasNewContent {
                        Button {
                            scrollToBottom(proxy)
                            isAtBottom = true
                            hasNewContent = false
                        } label: {
                            Label("有新内容", systemImage: "arrow.down")
                                .font(.caption.weight(.semibold))
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(.mint)
                        .padding(10)
                    }
                }
                .onChange(of: entries.count) { _, _ in contentDidChange(proxy) }
                .onChange(of: entries.map(\.english).joined()) { _, _ in contentDidChange(proxy) }
                .onChange(of: entries.map(\.chinese).joined()) { _, _ in contentDidChange(proxy) }
            }
        }
        .frame(minHeight: 220, maxHeight: .infinity)
    }

    private func contentDidChange(_ proxy: ScrollViewProxy) {
        guard isAtBottom else {
            hasNewContent = true
            return
        }
        hasNewContent = false
        DispatchQueue.main.async {
            scrollToBottom(proxy)
        }
    }

    private func scrollToBottom(_ proxy: ScrollViewProxy) {
        withAnimation(.easeOut(duration: 0.18)) {
            proxy.scrollTo("subtitle-content", anchor: .bottom)
        }
    }
}

struct WaveformView: View {
    let samples: [Double]
    let active: Bool

    var body: some View {
        HStack(alignment: .center, spacing: 3) {
            ForEach(Array(samples.enumerated()), id: \.offset) { _, sample in
                RoundedRectangle(cornerRadius: 2)
                    .fill(active ? Color.mint.opacity(0.85) : Color.secondary.opacity(0.35))
                    .frame(width: 3, height: active ? max(2, min(46, 3 + sample * 44)) : 2)
            }
        }
        .frame(maxWidth: .infinity)
        .frame(height: 52, alignment: .center)
        .padding(.horizontal, 12)
        .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 10))
        .animation(.easeOut(duration: 0.06), value: samples)
    }
}
