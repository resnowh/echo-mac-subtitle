import AVFoundation
import AppKit
import CoreMedia
import ScreenCaptureKit
import SwiftUI
import UniformTypeIdentifiers

struct SubtitleEntry: Identifiable {
    let id = UUID()
    var start: TimeInterval
    var end: TimeInterval
    var english: String
    var chinese: String
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

    var requiresMicrophone: Bool {
        self == .microphone || self == .computerAndMicrophone
    }

    var includesComputerAudio: Bool {
        self == .computer || self == .computerAndMicrophone
    }
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

private final class SystemAudioCapture: NSObject, SCStreamOutput, SCStreamDelegate {
    private let outputQueue = DispatchQueue(label: "local.echo.system-audio-capture")
    private let onAudio: (CMSampleBuffer) -> Void
    private let onError: (Error) -> Void
    private var stream: SCStream?

    init(onAudio: @escaping (CMSampleBuffer) -> Void, onError: @escaping (Error) -> Void) {
        self.onAudio = onAudio
        self.onError = onError
        super.init()
    }

    func start(completion: @escaping (Error?) -> Void) {
        SCShareableContent.getExcludingDesktopWindows(
            false,
            onScreenWindowsOnly: false
        ) { [weak self] content, error in
            guard let self else { return }
            if let error {
                DispatchQueue.main.async { completion(error) }
                return
            }
            guard let display = content?.displays.first else {
                let error = NSError(
                    domain: "Echo",
                    code: 20,
                    userInfo: [NSLocalizedDescriptionKey: "没有找到可用于捕获电脑音频的显示器。"]
                )
                DispatchQueue.main.async { completion(error) }
                return
            }

            let filter = SCContentFilter(display: display, excludingWindows: [])
            let configuration = SCStreamConfiguration()
            configuration.width = 2
            configuration.height = 2
            configuration.minimumFrameInterval = CMTime(value: 1, timescale: 1)
            configuration.queueDepth = 1
            configuration.capturesAudio = true
            configuration.sampleRate = 16_000
            configuration.channelCount = 1
            configuration.excludesCurrentProcessAudio = true

            let stream = SCStream(filter: filter, configuration: configuration, delegate: self)
            do {
                try stream.addStreamOutput(
                    self,
                    type: .audio,
                    sampleHandlerQueue: self.outputQueue
                )
            } catch {
                DispatchQueue.main.async { completion(error) }
                return
            }

            self.stream = stream
            stream.startCapture { error in
                DispatchQueue.main.async {
                    completion(error)
                }
            }
        }
    }

    func stop(completion: (() -> Void)? = nil) {
        guard let stream else {
            completion?()
            return
        }
        self.stream = nil
        stream.stopCapture { _ in
            DispatchQueue.main.async {
                completion?()
            }
        }
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .audio, CMSampleBufferIsValid(sampleBuffer) else { return }
        onAudio(sampleBuffer)
    }

    func stream(_ stream: SCStream, didStopWithError error: Error) {
        onError(error)
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
    @Published var inputMode: AudioInputMode
    @Published var isSwitchingInput = false
    @Published var isSummaryEnabled: Bool
    @Published var deepSeekAPIKey: String
    @Published var summaryText = ""
    @Published var summaryStatus = ""

    private let audioEngine = AVAudioEngine()
    private let audioQueue = DispatchQueue(label: "local.echo.soniox-audio")
    private let sonioxURL = URL(string: "wss://stt-rt.soniox.com/transcribe-websocket")!
    private let summaryURL = URL(string: "https://api.deepseek.com/chat/completions")!
    private var socket: URLSessionWebSocketTask?
    private var urlSession: URLSession?
    private var summaryTask: URLSessionDataTask?
    private var summaryRequestID = UUID()
    private var converter: AVAudioConverter?
    private var targetAudioFormat: AVAudioFormat?
    private var systemAudioCapture: SystemAudioCapture?
    private var systemConverter: AVAudioConverter?
    private var systemInputSampleRate = 0.0
    private var systemInputChannelCount = 0
    private var systemAudioReady = false
    private var microphoneTapInstalled = false
    private var sessionStartedAt: Date?
    private var currentEntryID: UUID?
    private var currentSessionFileURL: URL?
    private var lastSessionFileSaveAt: Date?
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
        let savedInputMode = UserDefaults.standard.string(forKey: "audioInputMode") ?? AudioInputMode.microphone.rawValue
        inputMode = AudioInputMode(rawValue: savedInputMode) ?? .microphone
        isSummaryEnabled = UserDefaults.standard.bool(forKey: "aiSummaryEnabled")
        deepSeekAPIKey = UserDefaults.standard.string(forKey: "deepSeekAPIKey") ?? ""
        super.init()
    }

    func toggleRecording() {
        if isRecording {
            stop()
        } else if socket != nil {
            status = "正在完成上一段录音"
        } else {
            start()
        }
    }

    func setInputMode(_ mode: AudioInputMode) {
        guard mode != inputMode, !isSwitchingInput else { return }
        guard isRecording else {
            activateInputMode(mode)
            return
        }

        let previousMode = inputMode
        status = "正在切换到\(mode.title)"

        if mode.requiresMicrophone && !microphoneTapInstalled {
            isSwitchingInput = true
            requestMicrophoneForInputSwitch(to: mode, from: previousMode)
        } else if mode.includesComputerAudio && !systemAudioReady {
            isSwitchingInput = true
            startSystemAudioCapture(sessionID: activeSessionID, targetMode: mode, previousMode: previousMode)
        } else {
            activateInputMode(mode)
            removeSourcesNotNeeded(from: previousMode, to: mode)
        }
    }

    func cycleInputMode() {
        guard !isSwitchingInput,
              let currentIndex = AudioInputMode.allCases.firstIndex(of: inputMode) else { return }
        let nextIndex = (currentIndex + 1) % AudioInputMode.allCases.count
        setInputMode(AudioInputMode.allCases[nextIndex])
    }

    private func activateInputMode(_ mode: AudioInputMode) {
        inputMode = mode
        UserDefaults.standard.set(mode.rawValue, forKey: "audioInputMode")
        status = isRecording
            ? "正在通过 Soniox 实时识别与翻译（\(mode.title)）"
            : "已选择\(mode.title)"
    }

    private func requestMicrophoneForInputSwitch(to mode: AudioInputMode, from previousMode: AudioInputMode) {
        let permission = AVCaptureDevice.authorizationStatus(for: .audio)
        if permission == .authorized {
            finishMicrophoneInputSwitch(to: mode, from: previousMode)
            return
        }
        guard permission == .notDetermined else {
            isSwitchingInput = false
            errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo，然后再切换到\(mode.title)。"
            status = "输入源未切换"
            return
        }
        status = "等待麦克风授权"
        AVCaptureDevice.requestAccess(for: .audio) { [weak self] granted in
            DispatchQueue.main.async {
                guard let self else { return }
                guard granted else {
                    self.isSwitchingInput = false
                    self.errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo。"
                    self.status = "输入源未切换"
                    return
                }
                self.finishMicrophoneInputSwitch(to: mode, from: previousMode)
            }
        }
    }

    private func finishMicrophoneInputSwitch(to mode: AudioInputMode, from previousMode: AudioInputMode) {
        do {
            try startMicrophoneCapture(sessionID: activeSessionID)
            activateInputMode(mode)
            removeSourcesNotNeeded(from: previousMode, to: mode)
            isSwitchingInput = false
        } catch {
            isSwitchingInput = false
            errorMessage = "话筒启动失败：\(error.localizedDescription)"
            status = "输入源未切换"
        }
    }

    private func removeSourcesNotNeeded(from previousMode: AudioInputMode, to mode: AudioInputMode) {
        if previousMode.requiresMicrophone && !mode.requiresMicrophone {
            stopMicrophoneCapture()
        }
        if previousMode.includesComputerAudio && !mode.includesComputerAudio {
            stopSystemAudioCapture()
        }
    }

    private func start() {
        errorMessage = ""
        fileStatus = ""
        summaryTask?.cancel()
        summaryText = ""
        summaryStatus = ""
        let key = sonioxAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else {
            errorMessage = "请先填写 Soniox API Key。可从 console.soniox.com 创建。"
            status = "等待 Soniox API Key"
            return
        }
        UserDefaults.standard.set(key, forKey: "sonioxAPIKey")

        let permission = AVCaptureDevice.authorizationStatus(for: .audio)
        guard inputMode.requiresMicrophone else {
            beginCapture(apiKey: key)
            return
        }
        if permission == .authorized {
            beginCapture(apiKey: key)
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
                self.beginCapture(apiKey: key)
            }
        }
    }

    private func beginCapture(apiKey: String) {
        do {
            guard let targetFormat = AVAudioFormat(commonFormat: .pcmFormatInt16, sampleRate: 16_000, channels: 1, interleaved: true) else {
                throw NSError(domain: "Echo", code: 4, userInfo: [NSLocalizedDescriptionKey: "无法准备 Soniox 音频格式。"])
            }
            targetAudioFormat = targetFormat
            converter = nil
            resetSessionState()
            openSonioxSocket(apiKey: apiKey, sessionID: activeSessionID)
            isRecording = true
            status = "正在通过 Soniox 实时识别与翻译（\(inputMode.title)）"
            segmentationTimer?.invalidate()
            segmentationTimer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
                self?.autoFinalizeIfNeeded()
            }

            if inputMode.requiresMicrophone {
                try startMicrophoneCapture(sessionID: activeSessionID)
            }
            if inputMode.includesComputerAudio {
                isSwitchingInput = true
                startSystemAudioCapture(sessionID: activeSessionID, targetMode: inputMode, previousMode: nil)
            }
        } catch {
            abortCaptureStart(with: error.localizedDescription)
        }
    }

    private func resetSessionState() {
        let sessionID = UUID()
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        currentEntryID = nil
        currentSessionFileURL = nil
        lastSessionFileSaveAt = nil
        currentSessionFinished = false
        isClosingSocket = false
        sessionStartedAt = Date()
        sessionEntriesStartIndex = entries.count
        activeSessionID = sessionID
        microphoneTapInstalled = false
        systemAudioCapture = nil
        systemAudioReady = false
        systemConverter = nil
        systemInputSampleRate = 0
        systemInputChannelCount = 0
    }

    private func startMicrophoneCapture(sessionID: UUID) throws {
        let input = audioEngine.inputNode
        let inputFormat = input.outputFormat(forBus: 0)
        guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0 else {
            throw NSError(domain: "Echo", code: 3, userInfo: [NSLocalizedDescriptionKey: "没有检测到可用的麦克风输入，请检查 Mac 的输入设备设置。"])
        }
        guard let targetFormat = targetAudioFormat,
              let converter = AVAudioConverter(from: inputFormat, to: targetFormat) else {
            throw NSError(domain: "Echo", code: 4, userInfo: [NSLocalizedDescriptionKey: "无法准备麦克风音频格式。"])
        }

        input.removeTap(onBus: 0)
        self.converter = converter
        input.installTap(onBus: 0, bufferSize: 1024, format: inputFormat) { [weak self] buffer, _ in
            let level = Self.rmsLevel(buffer)
            DispatchQueue.main.async { [weak self] in self?.recordAudioLevel(level) }
            self?.audioQueue.async { [weak self] in
                self?.sendAudio(buffer, sessionID: sessionID)
            }
        }
        microphoneTapInstalled = true
        audioEngine.prepare()
        do {
            try audioEngine.start()
        } catch {
            input.removeTap(onBus: 0)
            microphoneTapInstalled = false
            throw error
        }
    }

    private func startSystemAudioCapture(sessionID: UUID, targetMode: AudioInputMode, previousMode: AudioInputMode?) {
        let capture = SystemAudioCapture(
            onAudio: { [weak self] sampleBuffer in
                self?.handleSystemAudio(sampleBuffer, sessionID: sessionID)
            },
            onError: { [weak self] error in
                DispatchQueue.main.async {
                    guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
                    self.systemAudioReady = false
                    self.errorMessage = "电脑音频捕获中断：\(error.localizedDescription)"
                    self.status = "电脑音频捕获中断"
                }
            }
        )
        systemAudioCapture = capture
        capture.start { [weak self] error in
            guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
            if let error {
                self.systemAudioReady = false
                self.systemAudioCapture = nil
                if let previousMode {
                    self.isSwitchingInput = false
                    self.errorMessage = "无法切换到\(targetMode.title)：\(error.localizedDescription)。请在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”中允许 Echo。"
                    self.status = "继续使用\(previousMode.title)"
                } else {
                    self.abortCaptureStart(with: "无法捕获电脑音频：\(error.localizedDescription)。请在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”中允许 Echo。")
                }
            } else {
                self.systemAudioReady = true
                if let previousMode {
                    self.activateInputMode(targetMode)
                    self.removeSourcesNotNeeded(from: previousMode, to: targetMode)
                    self.isSwitchingInput = false
                } else {
                    self.isSwitchingInput = false
                    self.status = "正在通过 Soniox 实时识别与翻译（\(self.inputMode.title)）"
                }
            }
        }
    }

    private func abortCaptureStart(with message: String) {
        isRecording = false
        isClosingSocket = true
        isSwitchingInput = false
        segmentationTimer?.invalidate()
        segmentationTimer = nil
        stopMicrophoneCapture()
        stopSystemAudioCapture()
        socket?.cancel(with: .goingAway, reason: nil)
        socket = nil
        urlSession?.invalidateAndCancel()
        urlSession = nil
        errorMessage = message
        status = "启动失败"
    }

    private func stopMicrophoneCapture() {
        guard microphoneTapInstalled else { return }
        audioEngine.stop()
        audioEngine.inputNode.removeTap(onBus: 0)
        microphoneTapInstalled = false
        converter = nil
    }

    private func stopSystemAudioCapture() {
        systemAudioCapture?.stop()
        systemAudioCapture = nil
        systemAudioReady = false
        systemConverter = nil
        systemInputSampleRate = 0
        systemInputChannelCount = 0
    }

    private func handleSystemAudio(_ sampleBuffer: CMSampleBuffer, sessionID: UUID) {
        guard isRecording, activeSessionID == sessionID,
              let inputBuffer = Self.pcmBuffer(from: sampleBuffer),
              let targetFormat = targetAudioFormat else { return }

        if systemConverter == nil
            || systemInputSampleRate != inputBuffer.format.sampleRate
            || systemInputChannelCount != inputBuffer.format.channelCount {
            systemConverter = AVAudioConverter(from: inputBuffer.format, to: targetFormat)
            systemInputSampleRate = inputBuffer.format.sampleRate
            systemInputChannelCount = Int(inputBuffer.format.channelCount)
        }
        guard let converter = systemConverter else { return }

        let ratio = targetFormat.sampleRate / max(inputBuffer.format.sampleRate, 1)
        let capacity = AVAudioFrameCount(max(1, Int(Double(inputBuffer.frameLength) * ratio) + 1024))
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
            return inputBuffer
        }
        guard conversionStatus != .error, converted.frameLength > 0,
              let dataPointer = converted.audioBufferList.pointee.mBuffers.mData else { return }

        let dataSize = Int(converted.audioBufferList.pointee.mBuffers.mDataByteSize)
        let data = Data(bytes: dataPointer, count: dataSize)
        let level = Self.rmsLevel(converted)
        DispatchQueue.main.async { [weak self] in self?.recordAudioLevel(level) }
        audioQueue.async { [weak self] in
            self?.sendPCMData(data, sessionID: sessionID)
        }
    }

    private static func pcmBuffer(from sampleBuffer: CMSampleBuffer) -> AVAudioPCMBuffer? {
        guard let formatDescription = CMSampleBufferGetFormatDescription(sampleBuffer),
              let streamDescription = CMAudioFormatDescriptionGetStreamBasicDescription(formatDescription),
              let format = AVAudioFormat(streamDescription: streamDescription) else { return nil }
        let frameLength = CMSampleBufferGetNumSamples(sampleBuffer)
        guard frameLength > 0,
              let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AVAudioFrameCount(frameLength)) else { return nil }
        buffer.frameLength = AVAudioFrameCount(frameLength)

        var bufferListSize = 0
        let sizeStatus = CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer(
            sampleBuffer,
            bufferListSizeNeededOut: &bufferListSize,
            bufferListOut: nil,
            bufferListSize: 0,
            blockBufferAllocator: nil,
            blockBufferMemoryAllocator: nil,
            flags: 0,
            blockBufferOut: nil
        )
        guard sizeStatus == noErr, bufferListSize > 0 else { return nil }

        let rawBufferList = UnsafeMutableRawPointer.allocate(
            byteCount: bufferListSize,
            alignment: MemoryLayout<AudioBufferList>.alignment
        )
        defer { rawBufferList.deallocate() }
        let sourceBufferList = rawBufferList.bindMemory(to: AudioBufferList.self, capacity: 1)
        var blockBuffer: CMBlockBuffer?
        let copyStatus = CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer(
            sampleBuffer,
            bufferListSizeNeededOut: nil,
            bufferListOut: sourceBufferList,
            bufferListSize: bufferListSize,
            blockBufferAllocator: nil,
            blockBufferMemoryAllocator: nil,
            flags: 0,
            blockBufferOut: &blockBuffer
        )
        guard copyStatus == noErr else { return nil }

        let source = UnsafeMutableAudioBufferListPointer(sourceBufferList)
        let destination = UnsafeMutableAudioBufferListPointer(buffer.mutableAudioBufferList)
        for index in 0..<min(source.count, destination.count) {
            guard let sourceData = source[index].mData, let destinationData = destination[index].mData else { continue }
            let byteCount = min(Int(source[index].mDataByteSize), Int(destination[index].mDataByteSize))
            memcpy(destinationData, sourceData, byteCount)
        }
        return buffer
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
            "context": [
                "general": [
                    ["key": "domain", "value": "economics and finance"],
                    ["key": "topic", "value": "economic research, markets, and financial analysis"]
                ],
                "text": "This recording may be an economics lecture. Carefully distinguish microeconomic and microeconomics, which refer to individual consumers, firms, markets, and incentives, from macroeconomic and macroeconomics, which refer to economy-wide growth, inflation, unemployment, GDP, and monetary or fiscal policy. Never substitute one term for the other.",
                "terms": [
                    "microeconomic",
                    "macroeconomic",
                    "microeconomics",
                    "macroeconomics",
                    "micro economic",
                    "macro economic",
                    "micro-economic",
                    "macro-economic",
                    "microeconomic policy",
                    "macroeconomic policy",
                    "explicit cost",
                    "implicit cost",
                    "explicit costs",
                    "implicit costs",
                    "margin",
                    "marginal",
                    "marginal benefit",
                    "marginal cost",
                    "marginal benefits",
                    "marginal costs",
                    "incremental cost"
                ],
                "translation_terms": [
                    ["source": "microeconomic", "target": "微观经济学"],
                    ["source": "macroeconomic", "target": "宏观经济学"],
                    ["source": "microeconomics", "target": "微观经济学"],
                    ["source": "macroeconomics", "target": "宏观经济学"],
                    ["source": "micro economic", "target": "微观经济学"],
                    ["source": "macro economic", "target": "宏观经济学"],
                    ["source": "micro-economic", "target": "微观经济学"],
                    ["source": "macro-economic", "target": "宏观经济学"],
                    ["source": "explicit cost", "target": "显性成本"],
                    ["source": "implicit cost", "target": "隐性成本"],
                    ["source": "explicit costs", "target": "显性成本"],
                    ["source": "implicit costs", "target": "隐性成本"],
                    ["source": "margin", "target": "边际"],
                    ["source": "marginal", "target": "边际"],
                    ["source": "marginal benefit", "target": "边际收益"],
                    ["source": "marginal cost", "target": "边际成本"],
                    ["source": "marginal benefits", "target": "边际收益"],
                    ["source": "marginal costs", "target": "边际成本"],
                    ["source": "incremental cost", "target": "增量成本"]
                ]
            ],
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
        guard activeSessionID == sessionID, let converter, let targetFormat = targetAudioFormat else { return }
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
        sendPCMData(data, sessionID: sessionID)
    }

    private func sendPCMData(_ data: Data, sessionID: UUID) {
        guard activeSessionID == sessionID, let socket else { return }
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
        isSwitchingInput = false
        stopMicrophoneCapture()
        stopSystemAudioCapture()
        audioLevel = 0
        waveformSamples = Array(repeating: 0.0, count: waveformSamples.count)
        segmentationTimer?.invalidate()
        segmentationTimer = nil
        currentSessionFinished = true
        updateCurrentEntry()
        finalizeCurrentEntry()
        saveCurrentSessionFile(force: true)
        let stoppedSessionID = activeSessionID
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.7) { [weak self] in
            guard let self, self.activeSessionID == stoppedSessionID, !self.isRecording else { return }
            self.requestAISummaryForCurrentSession()
        }
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
            saveCurrentSessionFile(force: true)
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
        let rawEnglish = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        entries[index].english = Self.correctEconomicTerms(in: rawEnglish)
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
        let count = Int(buffer.frameLength)
        guard count > 0 else { return 0 }
        var sum = 0.0
        if let samples = buffer.floatChannelData?.pointee {
            for index in 0..<count {
                let sample = Double(samples[index])
                sum += sample * sample
            }
        } else if let samples = buffer.int16ChannelData?.pointee {
            for index in 0..<count {
                let sample = Double(samples[index]) / 32_768.0
                sum += sample * sample
            }
        } else {
            return 0
        }
        return min(1, max(0, sqrt(sum / Double(count)) * 7.5))
    }

    private static func correctEconomicTerms(in text: String) -> String {
        var result = text
        let normalizedVariants = [
            ("micro economic", "microeconomic"),
            ("macro economic", "macroeconomic"),
            ("micro-economic", "microeconomic"),
            ("macro-economic", "macroeconomic")
        ]
        for (source, target) in normalizedVariants {
            result = result.replacingOccurrences(
                of: "\\b\(source)\\b",
                with: target,
                options: [.regularExpression, .caseInsensitive]
            )
        }

        let lowercased = result.lowercased()
        let microSignals = [
            "microeconomic theory", "microeconomic analysis", "microeconomic behavior",
            "microeconomic model", "microeconomic models", "microeconomic foundations",
            "microeconomic incentives", "microeconomic decision", "microeconomic decisions"
        ]
        let macroSignals = [
            "macroeconomic policy", "macroeconomic growth", "macroeconomic indicators",
            "macroeconomic inflation", "macroeconomic unemployment", "macroeconomic gdp",
            "macroeconomic outlook", "macroeconomic conditions", "macroeconomic performance"
        ]
        let stronglyMicroeconomic = microSignals.contains { lowercased.contains($0) }
        let stronglyMacroeconomic = macroSignals.contains { lowercased.contains($0) }

        if stronglyMicroeconomic && !stronglyMacroeconomic {
            result = result.replacingOccurrences(
                of: "\\bmacroeconomics?\\b",
                with: "microeconomic",
                options: [.regularExpression, .caseInsensitive]
            )
        } else if stronglyMacroeconomic && !stronglyMicroeconomic {
            result = result.replacingOccurrences(
                of: "\\bmicroeconomics?\\b",
                with: "macroeconomic",
                options: [.regularExpression, .caseInsensitive]
            )
        }
        return result
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

    func dismissError() {
        errorMessage = ""
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

    func saveSummarySettings() {
        let key = deepSeekAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        if key.isEmpty {
            UserDefaults.standard.removeObject(forKey: "deepSeekAPIKey")
        } else {
            UserDefaults.standard.set(key, forKey: "deepSeekAPIKey")
        }
        UserDefaults.standard.set(isSummaryEnabled, forKey: "aiSummaryEnabled")
        deepSeekAPIKey = key
    }

    func generateAISummary() {
        requestAISummaryForCurrentSession(force: true)
    }

    private func requestAISummaryForCurrentSession(force: Bool = false) {
        guard force || isSummaryEnabled else { return }
        let key = deepSeekAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else {
            summaryStatus = "请先在设置中填写 DeepSeek API Key。"
            return
        }

        let source = Array(entries.dropFirst(sessionEntriesStartIndex))
            .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        guard !source.isEmpty else {
            summaryStatus = "本次没有可总结的文字。"
            return
        }

        summaryTask?.cancel()
        let requestID = UUID()
        summaryRequestID = requestID
        summaryText = ""
        summaryStatus = "正在生成 AI 总结…"

        let recordingStart = sessionStartedAt ?? Date()
        let calendar = Calendar.current
        let timeFormatter = DateFormatter()
        timeFormatter.locale = Locale(identifier: "en_US_POSIX")
        timeFormatter.timeZone = .current
        timeFormatter.dateFormat = "HH:mm:ss"
        let dateTimeFormatter = DateFormatter()
        dateTimeFormatter.locale = Locale(identifier: "en_US_POSIX")
        dateTimeFormatter.timeZone = .current
        dateTimeFormatter.dateFormat = "MM-dd HH:mm:ss"
        var previousDay = calendar.startOfDay(for: recordingStart)
        var isFirstTimestamp = true
        let transcript = source.enumerated().map { index, entry in
            let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
            let translated = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
            let startDate = recordingStart.addingTimeInterval(entry.start)
            let startDay = calendar.startOfDay(for: startDate)
            let timestamp: String
            if isFirstTimestamp || startDay != previousDay {
                timestamp = dateTimeFormatter.string(from: startDate)
                previousDay = startDay
                isFirstTimestamp = false
            } else {
                timestamp = timeFormatter.string(from: startDate)
            }
            return "\(index + 1). \(timestamp)\n英文：\(original)\n中文：\(translated)"
        }.joined(separator: "\n\n")
        let prompt = """
        请根据下面的英文实时文字稿，生成简洁、准确的简体中文总结。
        每条文字稿前的方括号是现实世界的本地开始时间，请保留这些时间信息，并在相关要点和待办后尽量标注对应时间。第一条和跨天后的第一条显示 MM-dd HH:mm:ss，同一天的其他条目只显示 HH:mm:ss。时间只表示开始时刻，不要补充结束时间。
        这是一份实时语音识别稿，可能存在听错、漏词、重复词、断句错误，以及机器翻译不准确的问题。
        请结合上下文理解原意：英文原文是主要依据，中文翻译只作为辅助参考；如果两者不一致，优先依据英文上下文判断。
        对明显的同音误识别、专业术语误识别和中文误译进行合理纠正，但不要凭空补充原文没有的信息。
        对无法确定的内容使用保守表述，不要把猜测写成事实。
        请使用以下格式：
        主题：一句话概括
        要点：用 3-8 条列出关键信息，每条尽量以 [时间] 开头
        仅在存在明确行动项时输出“待办”一栏，没有行动项时省略该栏；有待办时也请标注 [时间]。
        只返回总结正文，不要解释过程，也不要提及你看到了文字稿。

        文字稿：
        \(transcript)
        """
        let body: [String: Any] = [
            "model": "deepseek-v4-flash",
            "messages": [
                [
                    "role": "system",
                    "content": "你是一个专业的会议和演讲总结助手。请用简体中文回答。"
                ],
                [
                    "role": "user",
                    "content": prompt
                ]
            ],
            "thinking": ["type": "disabled"],
            "stream": false,
            "max_tokens": 1200
        ]
        guard let bodyData = try? JSONSerialization.data(withJSONObject: body) else {
            summaryStatus = "AI 总结失败：请求格式错误。"
            return
        }

        var request = URLRequest(url: summaryURL)
        request.httpMethod = "POST"
        request.timeoutInterval = 90
        request.setValue("Bearer \(key)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.httpBody = bodyData
        summaryTask = URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            DispatchQueue.main.async {
                guard let self, self.summaryRequestID == requestID else { return }
                self.summaryTask = nil
                if let error {
                    if (error as NSError).code == NSURLErrorCancelled { return }
                    self.summaryStatus = "AI 总结失败：\(error.localizedDescription)"
                    return
                }
                guard let httpResponse = response as? HTTPURLResponse,
                      let data else {
                    self.summaryStatus = "AI 总结失败：没有收到有效响应。"
                    return
                }
                guard (200..<300).contains(httpResponse.statusCode) else {
                    let message = Self.apiErrorMessage(from: data) ?? "HTTP \(httpResponse.statusCode)"
                    self.summaryStatus = "AI 总结失败：\(message)"
                    return
                }
                guard let summary = Self.responseText(from: data), !summary.isEmpty else {
                    self.summaryStatus = "AI 总结失败：响应中没有总结内容。"
                    return
                }
                self.summaryText = summary
                self.summaryStatus = "已生成"
            }
        }
        summaryTask?.resume()
    }

    private static func responseText(from data: Data) -> String? {
        guard let response = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return nil }
        if let choices = response["choices"] as? [[String: Any]],
           let message = choices.first?["message"] as? [String: Any],
           let content = message["content"] as? String {
            return content.trimmingCharacters(in: .whitespacesAndNewlines)
        }
        if let outputText = response["output_text"] as? String {
            return outputText.trimmingCharacters(in: .whitespacesAndNewlines)
        }
        guard let output = response["output"] as? [[String: Any]] else { return nil }
        let parts = output.flatMap { $0["content"] as? [[String: Any]] ?? [] }
        let text = parts.compactMap { part -> String? in
            guard part["type"] as? String == "output_text" else { return nil }
            return part["text"] as? String
        }.joined()
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func apiErrorMessage(from data: Data) -> String? {
        if let response = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
            if let error = response["error"] as? [String: Any],
               let message = error["message"] as? String {
                return message
            }
            if let error = response["error"] as? String {
                return error
            }
            if let message = response["message"] as? String {
                return message
            }
        }
        guard let raw = String(data: data, encoding: .utf8)?
            .trimmingCharacters(in: .whitespacesAndNewlines),
              !raw.isEmpty else { return nil }
        return String(raw.prefix(240))
    }

    func toggleAlwaysOnTop() {
        isAlwaysOnTop.toggle()
        UserDefaults.standard.set(isAlwaysOnTop, forKey: "alwaysOnTop")
    }

    func clearTranscript() {
        activeSessionID = UUID()
        summaryTask?.cancel()
        summaryRequestID = UUID()
        summaryText = ""
        summaryStatus = ""
        stopMicrophoneCapture()
        stopSystemAudioCapture()
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
        lastSessionFileSaveAt = nil
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
        isSwitchingInput = false
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

    private func saveCurrentSessionFile(force: Bool = false) {
        let source = Array(entries.dropFirst(sessionEntriesStartIndex))
            .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        guard !source.isEmpty else { return }
        if !force,
           let lastSessionFileSaveAt,
           Date().timeIntervalSince(lastSessionFileSaveAt) < 3 {
            return
        }
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
            lastSessionFileSaveAt = Date()
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

            HStack(spacing: 8) {
                Text("输入源：")
                    .foregroundStyle(.secondary)
                Button(action: model.cycleInputMode) {
                    AudioInputModeLabel(mode: model.inputMode)
                        .frame(minWidth: 132)
                }
                .buttonStyle(.bordered)
                .tint(.mint)
                .disabled(model.isSwitchingInput)
                .help(model.isSwitchingInput ? "正在切换输入源" : "点击切换输入源")
                Spacer()
            }

            HStack(spacing: 14) {
                Button(action: model.toggleRecording) {
                    Label(model.isRecording ? "停止录音" : "开始录音", systemImage: model.isRecording ? "stop.fill" : "mic.fill")
                        .frame(width: 120)
                }
                .buttonStyle(.borderedProminent)
                .tint(model.isRecording ? .red : .mint)
                Text(model.status).foregroundStyle(.secondary)
                Spacer()
                Button(action: model.generateAISummary) {
                    Label("生成总结", systemImage: "sparkles")
                }
                .disabled(model.entries.isEmpty)
                .help("根据当前已识别文字生成 AI 总结，不会停止录音")
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
                Text(model.isRecording && model.audioLevel > 0.035 ? "检测到\(model.inputMode.title)输入" : "等待\(model.inputMode.title)输入")
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
                            Text(model.summaryText)
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

private struct AudioInputModeLabel: View {
    let mode: AudioInputMode

    var body: some View {
        HStack(spacing: 6) {
            if mode == .computerAndMicrophone {
                Image(systemName: "speaker.wave.2.fill")
                Image(systemName: "mic.fill")
            } else {
                Image(systemName: mode.icon)
            }
            Text(mode.title)
        }
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
                Text("用于 Soniox 实时识别和中文翻译，只保存在本机。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Text("输入源可在主界面切换；捕获电脑音频需要在系统设置中允许 Echo 使用“屏幕与系统音频录制”。")
                .font(.caption)
                .foregroundStyle(.secondary)

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
