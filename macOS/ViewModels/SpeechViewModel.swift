import AVFoundation
import AppKit
import CoreMedia
import ScreenCaptureKit
import SwiftUI
import UniformTypeIdentifiers

final class SpeechViewModel: NSObject, ObservableObject, @unchecked Sendable {
    @Published var isRecording = false
    @Published var english = ""
    @Published var chinese = ""
    @Published var status = "准备就绪"
    @Published var errorMessage = ""
    @Published var audioLevel = 0.0
    @Published var isReceivingAudio = false
    @Published var waveformSamples = Array(repeating: 0.0, count: 48)
    @Published var entries: [SubtitleEntry] = []
    @Published var fileStatus = ""
    @Published var sonioxAPIKey: String
    @Published var isAlwaysOnTop: Bool
    @Published var inputMode: AudioInputMode
    @Published var isSwitchingInput = false
    @Published var isSummaryEnabled: Bool
    @Published var deepSeekAPIKey: String
    @Published var recognitionConfig: RecognitionConfig
    @Published var summaryText = ""
    @Published var summaryStatus = ""
    @Published private(set) var archives: [TranscriptArchive] = []
    @Published var selectedArchiveID: UUID?
    @Published var archiveStatus = ""

    private let microphoneCapture = MacMicrophoneCapture()
    private let audioQueue = DispatchQueue(label: "local.echo.soniox-audio")
    private let pcmMixer = PCM16TimelineMixer()
    private let pcmPrebuffer = PCM16Prebuffer(seconds: 2.5, sampleRate: 16_000)
    private let sonioxClient = SonioxWebSocketClient()
    private let lifecycleObserver = MacLifecycleObserver()
    private var lifecycleState = LifecycleRecoveryState()
    private var wakeRecoveryWorkItem: DispatchWorkItem?
    private var captureRecoveryWorkItem: DispatchWorkItem?
    private var lastAudioCaptureRecoveryAt: Date?
    // Kept in one place so the wake recovery policy is easy to tune.
    private let lifecycleRecoveryDelay: TimeInterval = 0.75
    private var isRecoveringAudioCapture = false
    private var summaryTask: URLSessionDataTask?
    private var summaryRequestID = UUID()
    private var converter: AVAudioConverter?
    private var targetAudioFormat: AVAudioFormat?
    private var systemAudioCapture: SystemAudioCaptureSource?
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
    private var lastSummarizedEntrySignatures: [UUID: String] = [:]
    private var currentArchiveID: UUID?
    private var currentSegmentID: UUID?
    private var completedArchiveID: UUID?
    private var completedSegmentID: UUID?
    private var currentSessionFinished = false
    private var activeSessionID = UUID()
    private var isClosingSocket = false
    private var socketReady = false
    private var audioFrameCursors: [PCM16Source: Int64] = [:]
    private var currentSpeaker: String?
    private var currentLanguage: String?

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
        let languageMode = SourceLanguageMode(rawValue: UserDefaults.standard.string(forKey: "sourceLanguageMode") ?? "specified") ?? .specified
        let specifiedLanguage = UserDefaults.standard.string(forKey: "specifiedSourceLanguage") ?? "en"
        let targetLanguage = UserDefaults.standard.string(forKey: "targetTranslationLanguage") ?? "zh"
        recognitionConfig = RecognitionConfig(
            sourceLanguageMode: languageMode,
            specifiedSourceLanguage: specifiedLanguage,
            languageHints: languageMode == .specified ? [specifiedLanguage] : [],
            strictLanguageRestriction: UserDefaults.standard.bool(forKey: "strictLanguageRestriction"),
            translationEnabled: UserDefaults.standard.object(forKey: "translationEnabled") as? Bool ?? true,
            targetTranslationLanguage: targetLanguage,
            speakerDiarizationEnabled: UserDefaults.standard.object(forKey: "speakerDiarizationEnabled") as? Bool ?? true
        )
        archives = Self.loadArchives()
        selectedArchiveID = nil
        super.init()
        lifecycleObserver.onWillSleep = { [weak self] in self?.handleSystemWillSleep() }
        lifecycleObserver.onDidWake = { [weak self] in self?.handleSystemDidWake() }
        lifecycleObserver.onAudioConfigurationChange = { [weak self] in self?.handleAudioConfigurationChange() }
        lifecycleObserver.start()
    }

    deinit {
        lifecycleObserver.stop()
        wakeRecoveryWorkItem?.cancel()
        captureRecoveryWorkItem?.cancel()
    }

    private var isSystemSleeping: Bool { lifecycleState.isSystemSleeping }

    private func handleSystemWillSleep() {
        let action = lifecycleState.handle(.willSleep, recordingIntended: isRecording)
        guard action == .endRecordingForSleep else {
            return
        }

        wakeRecoveryWorkItem?.cancel()
        wakeRecoveryWorkItem = nil
        captureRecoveryWorkItem?.cancel()
        captureRecoveryWorkItem = nil
        isRecoveringAudioCapture = false
        status = "系统即将睡眠，正在暂停录音"
        finishRecordingForSleep()
    }

    private func handleSystemDidWake() {
        guard lifecycleState.handle(.didWake) == .scheduleWakeRecovery else { return }
        wakeRecoveryWorkItem?.cancel()
        status = "系统已唤醒，正在恢复录音"

        let workItem = DispatchWorkItem { [weak self] in
            guard let self else { return }
            guard self.lifecycleState.handle(.recoveryStarted) == .beginWakeRecovery else { return }
            guard !self.isSystemSleeping else { return }
            self.start(preserveSummary: true)
        }
        wakeRecoveryWorkItem = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + lifecycleRecoveryDelay, execute: workItem)
    }

    private func handleAudioConfigurationChange() {
        guard isRecording, !isSystemSleeping, inputMode.requiresMicrophone else { return }
        guard !isRecoveringAudioCapture else { return }
        if let lastAudioCaptureRecoveryAt,
           Date().timeIntervalSince(lastAudioCaptureRecoveryAt) < lifecycleRecoveryDelay * 2 {
            return
        }

        isRecoveringAudioCapture = true
        lastAudioCaptureRecoveryAt = Date()
        status = "音频输入已中断，正在恢复录音"
        stopMicrophoneCapture()
        let sessionID = activeSessionID
        let workItem = DispatchWorkItem { [weak self] in
            guard let self else { return }
            guard self.activeSessionID == sessionID,
                  self.isRecording,
                  !self.isSystemSleeping else {
                self.isRecoveringAudioCapture = false
                return
            }
            do {
                try self.startMicrophoneCapture(sessionID: sessionID)
                self.isRecoveringAudioCapture = false
                self.finishWakeRecoveryIfReady()
                if !self.lifecycleState.isRecovering {
                    self.status = "正在通过 Soniox 实时识别与翻译（\(self.inputMode.title)）"
                }
            } catch {
                self.isRecoveringAudioCapture = false
                self.failActiveRecording(message: "音频输入恢复失败：\(error.localizedDescription)")
            }
        }
        captureRecoveryWorkItem?.cancel()
        captureRecoveryWorkItem = workItem
        DispatchQueue.main.asyncAfter(deadline: .now() + lifecycleRecoveryDelay, execute: workItem)
    }

    private func finishWakeRecoveryIfReady() {
        guard lifecycleState.isRecovering, isRecording, socketReady else { return }
        guard (!inputMode.requiresMicrophone || (microphoneTapInstalled && microphoneCapture.isEngineActuallyRunning)),
              (!inputMode.includesComputerAudio || systemAudioReady) else { return }
        _ = lifecycleState.handle(.recoverySucceeded)
        status = "正在通过 Soniox 实时识别与翻译（\(inputMode.title)）"
    }

    private func failActiveRecording(message: String) {
        let wasRecovery = lifecycleState.isRecovering
        stopCurrentRecording(scheduleSummary: false)
        sonioxClient.cancel()
        socketReady = false
        activeSessionID = UUID()
        if wasRecovery {
            _ = lifecycleState.handle(.recoveryFailed)
        } else {
            _ = lifecycleState.handle(.userStop)
        }
        errorMessage = message
        status = wasRecovery ? "录音恢复失败" : "音频输入已中断"
    }

    private func finishRecordingForSleep() {
        guard isRecording || sonioxClient.isActive else { return }
        stopCurrentRecording(scheduleSummary: false)
        // Invalidate every callback created before sleep. The archived segment
        // remains intact, while wake starts a fresh session/segment.
        activeSessionID = UUID()
        sonioxClient.cancel()
        socketReady = false
        status = "已暂停，等待系统唤醒"
    }

    func toggleRecording() {
        if isRecording {
            stop()
        } else {
            // A manual start supersedes a delayed wake recovery. This keeps a
            // user action from racing the scheduled recovery task.
            _ = lifecycleState.handle(.userStop)
            wakeRecoveryWorkItem?.cancel()
            wakeRecoveryWorkItem = nil
            captureRecoveryWorkItem?.cancel()
            captureRecoveryWorkItem = nil
            isRecoveringAudioCapture = false
            // A failed WebSocket can outlive the recording session. Do not
            // permanently block the start button on that stale connection.
            if sonioxClient.isActive { sonioxClient.cancel() }
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
        let permission = AVAudioApplication.shared.recordPermission
        if permission == .granted {
            finishMicrophoneInputSwitch(to: mode, from: previousMode)
            return
        }
        guard permission == .undetermined else {
            isSwitchingInput = false
            errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo，然后再切换到\(mode.title)。"
            status = "输入源未切换"
            return
        }
        status = "等待麦克风授权"
        AVAudioApplication.requestRecordPermission { [weak self] granted in
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

    private func start(preserveSummary: Bool = false) {
        errorMessage = ""
        fileStatus = ""
        if !preserveSummary {
            summaryTask?.cancel()
            summaryText = ""
            summaryStatus = ""
        }
        isReceivingAudio = false
        let key = sonioxAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else {
            errorMessage = "请先填写 Soniox API Key。可从 console.soniox.com 创建。"
            if lifecycleState.isRecovering {
                _ = lifecycleState.handle(.recoveryFailed)
                status = "录音恢复失败"
            } else {
                status = "等待 Soniox API Key"
            }
            return
        }
        UserDefaults.standard.set(key, forKey: "sonioxAPIKey")

        let permission = AVAudioApplication.shared.recordPermission
        guard inputMode.requiresMicrophone else {
            beginCapture(apiKey: key)
            return
        }
        if permission == .granted {
            beginCapture(apiKey: key)
            return
        }
        guard permission == .undetermined else {
            errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo。"
            if lifecycleState.isRecovering {
                _ = lifecycleState.handle(.recoveryFailed)
                status = "录音恢复失败"
            }
            return
        }
        status = "等待麦克风授权"
        AVAudioApplication.requestRecordPermission { [weak self] granted in
            DispatchQueue.main.async {
                guard let self else { return }
                guard granted else {
                    self.errorMessage = "请在“系统设置 → 隐私与安全性 → 麦克风”中允许 Echo。"
                    if self.lifecycleState.isRecovering {
                        _ = self.lifecycleState.handle(.recoveryFailed)
                        self.status = "录音恢复失败"
                    }
                    return
                }
                self.beginCapture(apiKey: key)
            }
        }
    }

    private func beginCapture(apiKey: String) {
        do {
            prepareArchiveForRecording()
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
                finishWakeRecoveryIfReady()
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
        pcmPrebuffer.clear()
        pcmMixer.reset()
        audioFrameCursors.removeAll(keepingCapacity: true)
        currentSpeaker = nil
        currentLanguage = nil
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
        currentSegmentID = UUID()
        completedArchiveID = nil
        completedSegmentID = nil
        sessionStartedAt = Date()
        sessionEntriesStartIndex = entries.count
        lastSummarizedEntrySignatures = [:]
        activeSessionID = sessionID
        microphoneTapInstalled = false
        systemAudioCapture = nil
        systemAudioReady = false
        systemConverter = nil
        systemInputSampleRate = 0
        systemInputChannelCount = 0
    }

    private func startMicrophoneCapture(sessionID: UUID) throws {
        let inputFormat = microphoneCapture.inputFormat
        guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0 else {
            throw NSError(domain: "Echo", code: 3, userInfo: [NSLocalizedDescriptionKey: "没有检测到可用的麦克风输入，请检查 Mac 的输入设备设置。"])
        }
        guard let targetFormat = targetAudioFormat,
              let converter = AVAudioConverter(from: inputFormat, to: targetFormat) else {
            throw NSError(domain: "Echo", code: 4, userInfo: [NSLocalizedDescriptionKey: "无法准备麦克风音频格式。"])
        }

        self.converter = converter
        try microphoneCapture.start { [weak self] buffer in
            let level = Self.rmsLevel(buffer)
            DispatchQueue.main.async { [weak self] in self?.recordAudioLevel(level) }
            self?.audioQueue.async { [weak self] in
                self?.sendAudio(buffer, sessionID: sessionID)
            }
        }
        microphoneTapInstalled = true
    }

    private func startSystemAudioCapture(sessionID: UUID, targetMode: AudioInputMode, previousMode: AudioInputMode?) {
        let capture = MacSystemAudioCapture(
            onAudio: { [weak self] sampleBuffer in
                self?.handleSystemAudio(sampleBuffer, sessionID: sessionID)
            },
            onError: { [weak self] error in
                DispatchQueue.main.async {
                    guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
                    if self.lifecycleState.isRecovering {
                        self.failActiveRecording(message: "电脑音频恢复失败：\(error.localizedDescription)")
                        return
                    }
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
                    self.finishWakeRecoveryIfReady()
                    if !self.lifecycleState.isRecovering {
                        self.status = "正在通过 Soniox 实时识别与翻译（\(self.inputMode.title)）"
                    }
                }
            }
        }
    }

    private func abortCaptureStart(with message: String) {
        let wasRecovery = lifecycleState.isRecovering
        isRecording = false
        isClosingSocket = true
        isSwitchingInput = false
        segmentationTimer?.invalidate()
        segmentationTimer = nil
        stopMicrophoneCapture()
        stopSystemAudioCapture()
        sonioxClient.cancel()
        pcmPrebuffer.clear()
        pcmMixer.reset()
        audioFrameCursors.removeAll(keepingCapacity: true)
        socketReady = false
        activeSessionID = UUID()
        if wasRecovery {
            _ = lifecycleState.handle(.recoveryFailed)
        }
        errorMessage = message
        status = wasRecovery ? "录音恢复失败" : "启动失败"
    }

    private func stopMicrophoneCapture() {
        guard microphoneTapInstalled else { return }
        microphoneCapture.stop()
        microphoneTapInstalled = false
        converter = nil
    }

    private func stopSystemAudioCapture() {
        systemAudioCapture?.stop(completion: nil)
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
            self?.enqueueAudio(data, source: .computer, sessionID: sessionID)
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
        socketReady = false

        var config: [String: Any] = [
            "api_key": apiKey,
            "model": "stt-rt-v5",
            "audio_format": "pcm_s16le",
            "sample_rate": 16_000,
            "num_channels": 1,
            "language_hints": recognitionConfig.sourceLanguageMode == .specified
                ? [recognitionConfig.specifiedSourceLanguage]
                : recognitionConfig.languageHints,
            "language_hints_strict": recognitionConfig.strictLanguageRestriction,
            "enable_language_identification": true,
            "enable_speaker_diarization": recognitionConfig.speakerDiarizationEnabled,
            "enable_endpoint_detection": true,
            "max_endpoint_delay_ms": 900,
            "context": [
                "general": [
                    ["key": "domain", "value": "economics and finance"],
                    ["key": "topic", "value": "economic research, markets, and financial analysis"]
                ],
                "text": "This recording may be an economics lecture. Carefully distinguish microeconomic and microeconomics, which refer to individual consumers, firms, markets, and incentives, from macroeconomic and macroeconomics, which refer to economy-wide growth, inflation, unemployment, GDP, and monetary or fiscal policy. In calculus and economics, derivative, partial derivative, first derivative, second derivative, and derivative of a function refer to calculus concepts and must not be confused with duty or duties, which mean a tax or obligation. Never substitute one term for the other.",
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
                    "incremental cost",
                    "derivative",
                    "derivatives",
                    "partial derivative",
                    "first derivative",
                    "second derivative",
                    "derivative of",
                    "take the derivative",
                    "with respect to",
                    "differentiate",
                    "hand",
                    "hands",
                    "on the other hand",
                    "on the one hand",
                    "one hand",
                    "other hand",
                    "right hand",
                    "left hand",
                    "hand side"
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
                    ["source": "incremental cost", "target": "增量成本"],
                    ["source": "derivative", "target": "导数"],
                    ["source": "derivatives", "target": "导数"],
                    ["source": "partial derivative", "target": "偏导数"],
                    ["source": "first derivative", "target": "一阶导数"],
                    ["source": "second derivative", "target": "二阶导数"],
                    ["source": "hand", "target": "手"],
                    ["source": "hands", "target": "手"]
                ]
            ],
            "translation": [
                "type": "one_way",
                "target_language": recognitionConfig.targetTranslationLanguage
            ]
        ]
        if recognitionConfig.sourceLanguageMode == .automatic && recognitionConfig.languageHints.isEmpty {
            config.removeValue(forKey: "language_hints")
        }
        if !recognitionConfig.translationEnabled {
            config.removeValue(forKey: "translation")
        }
        config = SonioxRequestBuilder.applying(recognitionConfig, to: config)
        do {
            let data = try JSONSerialization.data(withJSONObject: config)
            let json = String(decoding: data, as: UTF8.self)
            sonioxClient.connect(
                configuration: json,
                onReady: { [weak self] in
                    self?.audioQueue.async {
                        guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
                        self.socketReady = true
                        for chunk in self.pcmPrebuffer.drain() {
                            self.sendPCMData(chunk.data, sessionID: sessionID)
                        }
                        self.finishWakeRecoveryIfReady()
                    }
                },
                onMessage: { [weak self] message in
                    DispatchQueue.main.async {
                        guard let self, self.activeSessionID == sessionID else { return }
                        self.handleSonioxMessage(message)
                    }
                },
                onFailure: { [weak self] error in
                    DispatchQueue.main.async {
                        guard let self, self.activeSessionID == sessionID, self.isRecording, !self.isClosingSocket else { return }
                        self.handleSocketFailure(error, sessionID: sessionID)
                    }
                }
            )
        } catch {
            errorMessage = "Soniox 配置失败：\(error.localizedDescription)"
        }
    }

    private func handleSocketFailure(_ error: Error, sessionID: UUID) {
        guard activeSessionID == sessionID else { return }
        failActiveRecording(message: "Soniox 连接失败：\(error.localizedDescription)")
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
        enqueueAudio(data, source: .microphone, sessionID: sessionID)
    }

    private func enqueueAudio(_ data: Data, source: PCM16Source, sessionID: UUID) {
        guard activeSessionID == sessionID, isRecording else { return }
        let frameCount = Int64(data.count / 2)
        guard frameCount > 0 else { return }
        let startFrame = audioFrameCursors[source, default: 0]
        audioFrameCursors[source] = startFrame + frameCount

        let chunks: [PCM16Chunk]
        if inputMode == .computerAndMicrophone {
            chunks = pcmMixer.append(data, source: source, startFrame: startFrame)
        } else {
            chunks = [PCM16Chunk(data: data, startFrame: startFrame)]
        }
        for chunk in chunks {
            if socketReady {
                sendPCMData(chunk.data, sessionID: sessionID)
            } else {
                pcmPrebuffer.append(chunk)
            }
        }
    }

    private func sendPCMData(_ data: Data, sessionID: UUID) {
        guard activeSessionID == sessionID, socketReady else { return }
        sonioxClient.sendAudio(data) { [weak self] error in
            if let error {
                DispatchQueue.main.async {
                    guard let self, self.activeSessionID == sessionID, self.isRecording else { return }
                    self.errorMessage = "音频发送失败：\(error.localizedDescription)"
                }
            }
        }
    }

    func stop() {
        _ = lifecycleState.handle(.userStop)
        wakeRecoveryWorkItem?.cancel()
        wakeRecoveryWorkItem = nil
        captureRecoveryWorkItem?.cancel()
        captureRecoveryWorkItem = nil
        isRecoveringAudioCapture = false
        guard isRecording || sonioxClient.isActive else {
            status = "已停止"
            return
        }
        stopCurrentRecording(scheduleSummary: true)
    }

    private func stopCurrentRecording(scheduleSummary: Bool) {
        guard isRecording || sonioxClient.isActive else { return }
        isRecording = false
        isClosingSocket = true
        isSwitchingInput = false
        stopMicrophoneCapture()
        stopSystemAudioCapture()
        audioQueue.sync {
            // Preserve the final partial mixer block when the user stops.
            // The queue is also the ownership boundary for mixer/pre-buffer
            // state, so no late callback can leak into a future session.
            for chunk in pcmMixer.flush() {
                if socketReady {
                    sendPCMData(chunk.data, sessionID: activeSessionID)
                } else {
                    pcmPrebuffer.append(chunk)
                }
            }
            pcmPrebuffer.clear()
            pcmMixer.reset()
            audioFrameCursors.removeAll(keepingCapacity: true)
        }
        audioLevel = 0
        isReceivingAudio = false
        waveformSamples = Array(repeating: 0.0, count: waveformSamples.count)
        segmentationTimer?.invalidate()
        segmentationTimer = nil
        currentSessionFinished = true
        updateCurrentEntry()
        finalizeCurrentEntry()
        saveCurrentSessionFile(force: true)
        completedArchiveID = currentArchiveID
        completedSegmentID = currentSegmentID
        if scheduleSummary {
            let stoppedSessionID = activeSessionID
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.7) { [weak self] in
                guard let self, self.activeSessionID == stoppedSessionID, !self.isRecording else { return }
                self.requestAISummary(scope: .session)
            }
        }
        status = "正在完成最后一句"

        if sonioxClient.isActive {
            let stoppedSessionID = activeSessionID
            sonioxClient.finish { [weak self] _ in
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
                    guard let self, self.activeSessionID == stoppedSessionID else { return }
                    self.sonioxClient.cancel()
                    self.socketReady = false
                    self.status = "已停止"
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
            sonioxClient.cancel()
            socketReady = false
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
                if !isTranslation {
                    let tokenSpeaker = token["speaker"] as? String
                    let tokenLanguage = token["language"] as? String
                    if isFinal {
                        if let tokenSpeaker,
                           let currentSpeaker,
                           tokenSpeaker != currentSpeaker,
                           currentEntryID != nil,
                           !finalEnglish.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                            finalizeCurrentEntry()
                        }
                        if let tokenSpeaker { currentSpeaker = tokenSpeaker }
                        if let tokenLanguage { currentLanguage = tokenLanguage }
                    } else {
                        // Provisional speaker/language labels can change while
                        // Soniox revises the same token. Do not split a row or
                        // overwrite a settled label until the token is final.
                        if currentSpeaker == nil, let tokenSpeaker { currentSpeaker = tokenSpeaker }
                        if currentLanguage == nil, let tokenLanguage { currentLanguage = tokenLanguage }
                    }
                }
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
        guard !text.isEmpty else { return }
        if recognitionConfig.translationEnabled {
            let translation = (finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !translation.isEmpty else { return }
        }

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
        let recordedAt = sessionStartedAt?.addingTimeInterval(start)
        let entry = SubtitleEntry(
            start: start,
            end: start,
            recordedAt: recordedAt,
            english: "",
            chinese: "",
            speaker: currentSpeaker.map { "Speaker \($0)" },
            language: currentLanguage
        )
        entries.append(entry)
        currentEntryID = entry.id
    }

    private func updateCurrentEntry() {
        guard let currentEntryID,
              let index = entries.firstIndex(where: { $0.id == currentEntryID }) else { return }
        let rawEnglish = (finalEnglish + partialEnglish).trimmingCharacters(in: .whitespacesAndNewlines)
        let correctedEnglish = Self.correctEconomicTerms(in: rawEnglish)
        entries[index].english = correctedEnglish
        let rawChinese = (finalChinese + partialChinese).trimmingCharacters(in: .whitespacesAndNewlines)
        entries[index].chinese = Self.correctEconomicTranslation(rawChinese, for: correctedEnglish)
        entries[index].speaker = currentSpeaker.map { "Speaker \($0)" }
        entries[index].language = currentLanguage
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
        currentSpeaker = nil
        currentLanguage = nil
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

        // In an economics/calculus lecture, Soniox can hear “derivative” as
        // “duty”. Correct only strong calculus phrases so genuine tax or
        // obligation uses of “duty” remain unchanged.
        let derivativePhrases = [
            "first duty", "second duty", "partial duty",
            "duty of", "duty with respect to", "take the duty", "duty function",
            "duties of", "first duties", "second duties", "partial duties"
        ]
        if derivativePhrases.contains(where: { lowercased.contains($0) }) {
            result = result.replacingOccurrences(
                of: "\\bfirst duties?\\b",
                with: "first derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bsecond duties?\\b",
                with: "second derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bpartial duties?\\b",
                with: "partial derivative",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bduties\\b",
                with: "derivatives",
                options: [.regularExpression, .caseInsensitive]
            )
            result = result.replacingOccurrences(
                of: "\\bduty\\b",
                with: "derivative",
                options: [.regularExpression, .caseInsensitive]
            )
        }

        // “hand” may arrive as the truncated token “han”. Limit this fix to
        // recognizable phrases so a name such as “Han” is left untouched.
        let handPhrases = [
            "on the other han", "on the one han", "one han", "other han",
            "right han", "left han", "han side", "at han"
        ]
        if handPhrases.contains(where: { result.lowercased().contains($0) }) {
            result = result.replacingOccurrences(
                of: "\\bhan\\b",
                with: "hand",
                options: [.regularExpression, .caseInsensitive]
            )
        }
        return result
    }

    private static func correctEconomicTranslation(_ text: String, for english: String) -> String {
        let lowercasedEnglish = english.lowercased()
        let isDerivativeContext = lowercasedEnglish.contains("derivative")
            || lowercasedEnglish.contains("differentiate")
            || lowercasedEnglish.contains("with respect to")
        guard isDerivativeContext else { return text }
        return text.replacingOccurrences(of: "关税", with: "导数")
    }

    private func recordAudioLevel(_ level: Double) {
        // Attack quickly when speech starts, then decay more slowly. This
        // keeps the history readable without inventing movement when silent.
        isReceivingAudio = true
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

    var selectedArchiveTitle: String {
        guard let selectedArchiveID,
              let archive = archives.first(where: { $0.id == selectedArchiveID }) else {
            return "新建存档"
        }
        return archive.title
    }

    var canSplitCompletedSegment: Bool {
        !isRecording && completedArchiveID != nil && completedSegmentID != nil
    }

    func chooseArchive(_ id: UUID?) {
        guard !isRecording else { return }
        selectedArchiveID = id
        if let id, let archive = archives.first(where: { $0.id == id }) {
            archiveStatus = "下一段将接续：\(archive.title)"
        } else {
            archiveStatus = "下一段将新建存档"
        }
    }

    private func prepareArchiveForRecording() {
        let now = Date()
        if let selectedArchiveID,
           let archive = archives.first(where: { $0.id == selectedArchiveID }) {
            let restoredEntries = archive.segments.flatMap(\.entries).map { archived in
                SubtitleEntry(
                    start: archived.start,
                    end: archived.end,
                    recordedAt: archived.recordedAt,
                    english: archived.english,
                    chinese: archived.chinese,
                    speaker: archived.speaker,
                    language: archived.language
                )
            }
            entries = restoredEntries
            refreshFullTranscript()
            currentArchiveID = archive.id
            archiveStatus = "正在接续：\(archive.title)"
        } else {
            let id = UUID()
            let formatter = DateFormatter()
            formatter.locale = Locale(identifier: "zh_CN")
            formatter.dateFormat = "MM-dd HH:mm"
            let archive = TranscriptArchive(
                id: id,
                title: "课程 \(formatter.string(from: now))",
                createdAt: now,
                updatedAt: now,
                segments: []
            )
            archives.append(archive)
            selectedArchiveID = id
            currentArchiveID = id
            archiveStatus = "已新建：\(archive.title)"
            saveArchive(archive)
        }
    }

    private static func loadArchives() -> [TranscriptArchive] {
        TranscriptArchiveStore().load()
    }

    private func saveArchive(_ archive: TranscriptArchive) {
        do {
            try TranscriptArchiveStore().save(archive)
        } catch {
            archiveStatus = "存档保存失败：\(error.localizedDescription)"
        }
    }

    private func saveArchiveProgress() {
        guard let archiveID = currentArchiveID,
              let segmentID = currentSegmentID,
              let archiveIndex = archives.firstIndex(where: { $0.id == archiveID }) else { return }
        let source = Array(entries.dropFirst(sessionEntriesStartIndex))
            .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        let archivedEntries = source.map {
            ArchivedSubtitle(
                id: $0.id,
                start: $0.start,
                end: $0.end,
                recordedAt: $0.recordedAt,
                english: $0.english,
                chinese: $0.chinese,
                speaker: $0.speaker,
                language: $0.language
            )
        }
        let now = Date()
        var archive = archives[archiveIndex]
        if let segmentIndex = archive.segments.firstIndex(where: { $0.id == segmentID }) {
            archive.segments[segmentIndex].entries = archivedEntries
            archive.segments[segmentIndex].updatedAt = now
        } else {
            archive.segments.append(TranscriptSegment(id: segmentID, startedAt: sessionStartedAt ?? now, updatedAt: now, entries: archivedEntries))
        }
        archive.updatedAt = now
        archives[archiveIndex] = archive
        archives.sort { $0.updatedAt > $1.updatedAt }
        saveArchive(archive)
    }

    func splitCompletedSegment() {
        guard canSplitCompletedSegment,
              let archiveID = completedArchiveID,
              let segmentID = completedSegmentID,
              let archiveIndex = archives.firstIndex(where: { $0.id == archiveID }),
              let segmentIndex = archives[archiveIndex].segments.firstIndex(where: { $0.id == segmentID }) else {
            return
        }
        var archive = archives[archiveIndex]
        let segment = archive.segments.remove(at: segmentIndex)
        archive.updatedAt = Date()
        archives[archiveIndex] = archive
        saveArchive(archive)

        let newArchiveID = UUID()
        let newArchive = TranscriptArchive(
            id: newArchiveID,
            title: "\(archive.title) - 本段",
            createdAt: segment.startedAt,
            updatedAt: Date(),
            segments: [segment]
        )
        archives.append(newArchive)
        archives.sort { $0.updatedAt > $1.updatedAt }
        saveArchive(newArchive)
        selectedArchiveID = newArchiveID
        completedArchiveID = nil
        completedSegmentID = nil
        archiveStatus = "已将本段拆出为：\(newArchive.title)"
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

    func saveRecognitionSettings() {
        UserDefaults.standard.set(recognitionConfig.sourceLanguageMode.rawValue, forKey: "sourceLanguageMode")
        UserDefaults.standard.set(recognitionConfig.specifiedSourceLanguage, forKey: "specifiedSourceLanguage")
        UserDefaults.standard.set(recognitionConfig.strictLanguageRestriction, forKey: "strictLanguageRestriction")
        UserDefaults.standard.set(recognitionConfig.translationEnabled, forKey: "translationEnabled")
        UserDefaults.standard.set(recognitionConfig.targetTranslationLanguage, forKey: "targetTranslationLanguage")
        UserDefaults.standard.set(recognitionConfig.speakerDiarizationEnabled, forKey: "speakerDiarizationEnabled")
    }

    func generateAISummary() {
        requestAISummary(scope: .newContent, manual: true)
    }

    func regenerateAISummary() {
        requestAISummary(scope: .session, force: true, manual: true)
    }

    func summarizeSelectedArchive() {
        requestAISummary(scope: .archive, force: true, manual: true)
    }

    private enum SummaryScope {
        case newContent
        case session
        case archive
    }

    private func requestAISummary(scope: SummaryScope = .session, force: Bool = false, manual: Bool = false) {
        guard force || manual || isSummaryEnabled else { return }
        let key = deepSeekAPIKey.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else {
            summaryStatus = "请先在设置中填写 DeepSeek API Key。"
            return
        }

        let source: [SubtitleEntry]
        switch scope {
        case .archive:
            guard let archive = selectedArchiveID.flatMap({ id in archives.first(where: { $0.id == id }) }) else {
                summaryStatus = "请先选择一个存档。"
                return
            }
            source = archiveEntries(for: archive)
        case .newContent, .session:
            source = Array(entries.dropFirst(sessionEntriesStartIndex))
                .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
        }
        guard !source.isEmpty else {
            summaryStatus = "本次没有可总结的文字。"
            return
        }

        let sourceSignatures = Dictionary(uniqueKeysWithValues: source.map { ($0.id, Self.summarySignature(for: $0)) })
        let selectedSource: [SubtitleEntry]
        if force || scope == .session {
            selectedSource = source
        } else {
            selectedSource = source.filter { entry in
                lastSummarizedEntrySignatures[entry.id] != sourceSignatures[entry.id]
            }
            guard !selectedSource.isEmpty else {
                summaryStatus = "没有新的文字可以总结。"
                return
            }
        }
        let selectedSignatures = Dictionary(uniqueKeysWithValues: selectedSource.map { ($0.id, sourceSignatures[$0.id] ?? "") })

        summaryTask?.cancel()
        let requestID = UUID()
        summaryRequestID = requestID
        summaryStatus = scope == .archive ? "正在总结整个存档…" : (force ? "正在重新总结当前录音段…" : "正在总结新增内容…")

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
        let transcript = selectedSource.enumerated().map { index, entry in
            let original = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
            let translated = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
            let startDate = entry.recordedAt ?? recordingStart.addingTimeInterval(entry.start)
            let startDay = calendar.startOfDay(for: startDate)
            let timestamp: String
            if isFirstTimestamp || startDay != previousDay {
                timestamp = dateTimeFormatter.string(from: startDate)
                previousDay = startDay
                isFirstTimestamp = false
            } else {
                timestamp = timeFormatter.string(from: startDate)
            }
            return "\(index + 1). [\(timestamp)]\n英文：\(original)\n中文：\(translated)"
        }.joined(separator: "\n\n")
        let prompt: String
        if force || scope == .session {
            prompt = """
            请根据下面的英文实时文字稿，生成简洁、准确的简体中文总结。
            每条文字稿前的方括号是现实世界的本地开始时间，请保留这些时间信息，并在相关要点和待办后尽量标注对应时间。第一条和跨天后的第一条显示 MM-dd HH:mm:ss，同一天的其他条目只显示 HH:mm:ss。时间只表示开始时刻，不要补充结束时间。
            输出时间时只能引用一个开始时间点，例如 [09-03 11:24:18] 或 [11:25:02]。严禁输出任何结束时间、时间范围、时间区间，严禁使用“-->”“至”“到”或起止时间之间的短横线。
            这是一份实时语音识别稿，可能存在听错、漏词、重复词、断句错误，以及机器翻译不准确的问题。
            请结合上下文理解原意：英文原文是主要依据，中文翻译只作为辅助参考；如果两者不一致，优先依据英文上下文判断。
            对明显的同音误识别、专业术语误识别和中文误译进行合理纠正，但不要凭空补充原文没有的信息。
            对无法确定的内容使用保守表述，不要把猜测写成事实。
            请按自然主题组织内容，不要逐句复述，也不要把每句话拆成一个段落。全文通常分成 2-4 个主题段落；只有主题确实发生变化时才换段。
            对复杂概念，在对应要点中补充一句简短解释，说明它是什么、为什么重要或与前后内容的关系；必要时给出原文中出现的例子，但不要写成教科书式长篇扩展。
            每个主题段落列 1-3 个要点，合并重复信息；要点总数通常控制在 3-8 条。每条尽量以单个 [开始时间] 开头，相关解释和因果关系放在同一条中。
            请使用以下格式：
            ## 主题
            一句话概括全文主旨。

            ### 核心概念或主题一
            - [开始时间] 关键内容；复杂概念后补充简短解释。
            - [开始时间] 相关因果关系、例子或结论。

            ### 主题二
            - [开始时间] 关键内容与必要解释。
            主题标题和段落不要过度拆分；内容不足时合并主题，不要为了凑数量添加空泛要点。
            仅在存在明确行动项时输出“待办”一栏，没有行动项时省略该栏；有待办时也请标注单个 [开始时间]。
            只返回总结正文，不要解释过程，也不要提及你看到了文字稿。

            文字稿：
            \(transcript)
            """
        } else {
            prompt = """
            下面是一次已经进行中的实时文字稿中，刚刚新增或被修正的部分。请只总结这部分新内容，不要重新总结整场内容，也不要重复之前已经讲过的内容。
            请按自然主题合并新增内容，不要逐句复述，也不要一句话一个段落。只有主题发生变化时才换段；内容较少时只保留一个段落，不要为了凑数量拆分。
            对新增内容中的复杂概念，在对应要点中补充一句简短解释，说明它是什么、为什么重要或与上下文的关系；把解释和原要点放在同一条中。
            请直接使用以下格式：
            ### 新增内容
            - [开始时间] 新内容要点；必要时补充简短解释。
            - [开始时间] 相关因果关系、例子或结论。
            仅在这部分明确出现行动项时追加“### 待办”，没有行动项时省略。
            合并重复信息，每个主题保留 1-3 条要点；没有新的实质信息时不要编造内容。
            每条只能标注一个开始时间点。时间是现实世界的本地开始时间：第一条和跨天后的第一条使用 MM-dd HH:mm:ss，同一天其他条目只使用 HH:mm:ss。
            严禁输出结束时间、时间范围、时间区间、-->、至、到或起止时间之间的短横线。
            这是一份实时语音识别稿，可能存在听错、漏词、重复词、断句错误，以及机器翻译不准确的问题。
            英文原文是主要依据，中文翻译只作为辅助参考；如果两者不一致，优先依据英文上下文判断。
            对明显的同音误识别、专业术语误识别和中文误译进行合理纠正，但不要凭空补充原文没有的信息。
            只返回增量总结正文，不要解释过程，也不要提及你看到了文字稿。

            新增文字稿：
            \(transcript)
            """
        }
        guard let request = DeepSeekService().request(apiKey: key, prompt: prompt) else {
            summaryStatus = "AI 总结失败：请求格式错误。"
            return
        }
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
                guard let rawSummary = Self.responseText(from: data), !rawSummary.isEmpty else {
                    self.summaryStatus = "AI 总结失败：响应中没有总结内容。"
                    return
                }
                let cleanedSummary = Self.removeSummaryEndTimes(from: rawSummary)
                if force || self.summaryText.isEmpty {
                    self.summaryText = cleanedSummary
                } else {
                    self.summaryText += "\n\n" + cleanedSummary
                }
                if force || scope == .archive {
                    self.lastSummarizedEntrySignatures = sourceSignatures
                } else {
                    self.lastSummarizedEntrySignatures.merge(selectedSignatures) { _, new in new }
                }
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

    private static func removeSummaryEndTimes(from text: String) -> String {
        let pattern = "\\[?((?:(?:\\d{4}-\\d{2}-\\d{2}|\\d{2}-\\d{2})\\s+)?\\d{2}:\\d{2}:\\d{2})\\s*(?:-|–|—|~|～|至|到)\\s*(?:(?:\\d{4}-\\d{2}-\\d{2}|\\d{2}-\\d{2})\\s+)?\\d{2}:\\d{2}:\\d{2}\\]?"
        return text.replacingOccurrences(of: pattern, with: "[$1]", options: [.regularExpression])
    }

    private static func summarySignature(for entry: SubtitleEntry) -> String {
        let english = entry.english.trimmingCharacters(in: .whitespacesAndNewlines)
        let chinese = entry.chinese.trimmingCharacters(in: .whitespacesAndNewlines)
        return "\(english)\u{1F}" + chinese
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
        _ = lifecycleState.handle(.userStop)
        wakeRecoveryWorkItem?.cancel()
        wakeRecoveryWorkItem = nil
        captureRecoveryWorkItem?.cancel()
        captureRecoveryWorkItem = nil
        isRecoveringAudioCapture = false
        activeSessionID = UUID()
        summaryTask?.cancel()
        summaryRequestID = UUID()
        summaryText = ""
        summaryStatus = ""
        lastSummarizedEntrySignatures = [:]
        stopMicrophoneCapture()
        stopSystemAudioCapture()
        sonioxClient.cancel()
        socketReady = false
        audioQueue.sync {
            pcmPrebuffer.clear()
            pcmMixer.reset()
            audioFrameCursors.removeAll(keepingCapacity: true)
        }
        entries = []
        english = ""
        chinese = ""
        audioLevel = 0
        isReceivingAudio = false
        waveformSamples = Array(repeating: 0.0, count: waveformSamples.count)
        currentEntryID = nil
        currentSessionFileURL = nil
        lastSessionFileSaveAt = nil
        currentSessionFinished = false
        sessionStartedAt = nil
        sessionEntriesStartIndex = 0
        currentArchiveID = nil
        currentSegmentID = nil
        completedArchiveID = nil
        completedSegmentID = nil
        selectedArchiveID = nil
        archiveStatus = ""
        finalEnglish = ""
        partialEnglish = ""
        finalChinese = ""
        partialChinese = ""
        currentSourceStart = nil
        currentSourceEnd = nil
        lastTokenReceivedAt = nil
        currentSpeaker = nil
        currentLanguage = nil
        isSwitchingInput = false
        errorMessage = ""
        fileStatus = ""
        status = "准备就绪"
    }

    func exportAllSubtitles() {
        let archiveEntries = selectedArchiveID.flatMap { id in archives.first(where: { $0.id == id }) }
        guard !(archiveEntries?.segments.isEmpty ?? entries.isEmpty) else { return }
        let panel = NSSavePanel()
        panel.title = "导出全部字幕"
        panel.nameFieldStringValue = "echo-subtitles.srt"
        panel.allowedContentTypes = [UTType(filenameExtension: "srt") ?? .plainText]
        panel.begin { [weak self] response in
            guard response == .OK, let url = panel.url, let self else { return }
            do {
                let text: String
                if let archive = self.selectedArchiveID.flatMap({ id in self.archives.first(where: { $0.id == id }) }) {
                    text = self.archiveSRTText(archive)
                } else {
                    let source = self.entries.filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
                    text = self.srtText(for: source, base: source.first?.start ?? 0)
                }
                try text.write(to: url, atomically: true, encoding: .utf8)
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
            saveArchiveProgress()
        } catch { fileStatus = "字幕保存失败：\(error.localizedDescription)" }
    }

    private func srtText(for source: [SubtitleEntry], base: TimeInterval) -> String {
        return SRTExporter.sessionText(entries: source, base: base)
    }

    private func archiveSRTText(_ archive: TranscriptArchive) -> String {
        return SRTExporter.archiveText(archive)
    }

    private func archiveEntries(for archive: TranscriptArchive) -> [SubtitleEntry] {
        archive.segments
            .sorted { $0.startedAt < $1.startedAt }
            .flatMap { segment in
                segment.entries.map { entry in
                    SubtitleEntry(
                        id: entry.id,
                        start: entry.start,
                        end: entry.end,
                        recordedAt: entry.recordedAt ?? segment.startedAt.addingTimeInterval(entry.start),
                        english: entry.english,
                        chinese: entry.chinese,
                        speaker: entry.speaker,
                        language: entry.language
                    )
                }
            }
            .filter { !$0.english.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    }

}
