import AVFoundation
import Foundation

protocol AudioCaptureSource: AnyObject {
    func start(
        targetFormat: AVAudioFormat,
        onRawInput: @escaping () -> Void,
        onAudio: @escaping (AVAudioPCMBuffer) -> Void,
        completion: @escaping (Result<AVAudioFormat, Error>) -> Void
    )
    func stop()
}

protocol SystemAudioCaptureSource: AnyObject {
    func start(completion: @escaping (Error?) -> Void)
    func stop(completion: (() -> Void)?)
}

#if DEBUG
enum MicrophoneLifecycleLog {
    static func mark(_ message: String) {
        let uptime = ProcessInfo.processInfo.systemUptime
        print(String(format: "[Echo][mic][%.3f] %@ main=%@", uptime, message, Thread.isMainThread ? "true" : "false"))
    }
}
#endif

/// Owns every AVAudioEngine lifecycle operation on one serial queue.
///
/// The capture callback is converted to the requested PCM format before it is
/// handed to the view model. This keeps the input format used by the tap and
/// converter identical for a startup attempt and prevents CoreAudio setup from
/// blocking SwiftUI's main thread.
final class MacMicrophoneCapture: AudioCaptureSource {
    private let controlQueue = DispatchQueue(label: "local.echo.microphone-control")
    private let processingQueue = DispatchQueue(label: "local.echo.microphone-processing")
    private let stateLock = NSLock()
    private var engine: AVAudioEngine?
    private var configurationObserver: NSObjectProtocol?
    private var operationID: UInt64 = 0
    private var running = false
    private var lastRawCallbackUptime: TimeInterval?
    var onConfigurationChange: (() -> Void)?

    var isRunning: Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        return running
    }

    func hasRecentRawCallback(within interval: TimeInterval) -> Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        guard let lastRawCallbackUptime else { return false }
        return ProcessInfo.processInfo.systemUptime - lastRawCallbackUptime <= interval
    }

    func start(
        targetFormat: AVAudioFormat,
        onRawInput: @escaping () -> Void,
        onAudio: @escaping (AVAudioPCMBuffer) -> Void,
        completion: @escaping (Result<AVAudioFormat, Error>) -> Void
    ) {
        controlQueue.async { [weak self] in
            guard let self else { return }
            self.operationID &+= 1
            let operationID = self.operationID
            self.teardownOnControlQueue()

            #if DEBUG
            MicrophoneLifecycleLog.mark("mic startup control begin")
            MicrophoneLifecycleLog.mark("refresh input format begin")
            #endif
            let engine = AVAudioEngine()
            let input = engine.inputNode
            let inputFormat = input.outputFormat(forBus: 0)
            #if DEBUG
            MicrophoneLifecycleLog.mark("refresh input format end sampleRate=\(inputFormat.sampleRate) channels=\(inputFormat.channelCount) commonFormat=\(inputFormat.commonFormat.rawValue) interleaved=\(inputFormat.isInterleaved)")
            #endif

            guard inputFormat.sampleRate > 0, inputFormat.channelCount > 0 else {
                let error = Self.error("没有检测到可用的麦克风输入，请检查 Mac 的输入设备设置。", code: 3)
                self.complete(.failure(error), operationID: operationID, completion: completion)
                return
            }

            #if DEBUG
            MicrophoneLifecycleLog.mark("converter creation begin")
            #endif
            guard let converter = AVAudioConverter(from: inputFormat, to: targetFormat) else {
                let error = Self.error("无法准备麦克风音频转换器。", code: 4)
                #if DEBUG
                MicrophoneLifecycleLog.mark("converter creation end failed")
                #endif
                self.complete(.failure(error), operationID: operationID, completion: completion)
                return
            }
            #if DEBUG
            MicrophoneLifecycleLog.mark("converter creation end")
            MicrophoneLifecycleLog.mark("install tap begin")
            #endif
            input.installTap(onBus: 0, bufferSize: 1024, format: inputFormat) { [weak self] buffer, _ in
                guard let self else { return }
                #if DEBUG
                if !self.isFirstCallbackLogged(for: operationID) {
                    MicrophoneLifecycleLog.mark("first raw callback frameLength=\(buffer.frameLength) sampleRate=\(buffer.format.sampleRate) channels=\(buffer.format.channelCount) commonFormat=\(buffer.format.commonFormat.rawValue) interleaved=\(buffer.format.isInterleaved)")
                }
                #endif
                onRawInput()
                self.markRawCallback()
                guard let ownedBuffer = Self.copy(buffer, format: inputFormat) else {
                    #if DEBUG
                    self.logFirstConversion("raw buffer copy failed")
                    #endif
                    return
                }
                self.processingQueue.async { [weak self] in
                    self?.convert(ownedBuffer, with: converter, to: targetFormat, onAudio: onAudio)
                }
            }
            #if DEBUG
            MicrophoneLifecycleLog.mark("install tap end")
            MicrophoneLifecycleLog.mark("engine prepare begin")
            #endif
            engine.prepare()
            #if DEBUG
            MicrophoneLifecycleLog.mark("engine prepare end")
            MicrophoneLifecycleLog.mark("engine start begin")
            #endif
            do {
                try engine.start()
                #if DEBUG
                MicrophoneLifecycleLog.mark("engine start end")
                #endif
            } catch {
                #if DEBUG
                MicrophoneLifecycleLog.mark("engine start end failed")
                #endif
                input.removeTap(onBus: 0)
                self.complete(.failure(error), operationID: operationID, completion: completion)
                return
            }

            guard operationID == self.operationID else {
                input.removeTap(onBus: 0)
                engine.stop()
                return
            }
            self.engine = engine
            self.installConfigurationObserver(for: engine)
            self.setRunning(true)
            self.complete(.success(inputFormat), operationID: operationID, completion: completion)
        }
    }

    func stop() {
        controlQueue.async { [weak self] in
            guard let self else { return }
            self.operationID &+= 1
            self.teardownOnControlQueue()
        }
    }

    private func teardownOnControlQueue() {
        removeConfigurationObserver()
        guard let engine else {
            setRunning(false)
            return
        }
        engine.stop()
        engine.inputNode.removeTap(onBus: 0)
        self.engine = nil
        setRunning(false)
    }

    private func installConfigurationObserver(for engine: AVAudioEngine) {
        removeConfigurationObserver()
        configurationObserver = NotificationCenter.default.addObserver(
            forName: Notification.Name("AVAudioEngineConfigurationChangeNotification"),
            object: engine,
            queue: .main
        ) { [weak self] _ in
            self?.onConfigurationChange?()
        }
    }

    private func removeConfigurationObserver() {
        if let configurationObserver {
            NotificationCenter.default.removeObserver(configurationObserver)
            self.configurationObserver = nil
        }
    }

    private func complete(
        _ result: Result<AVAudioFormat, Error>,
        operationID: UInt64,
        completion: @escaping (Result<AVAudioFormat, Error>) -> Void
    ) {
        guard operationID == self.operationID else { return }
        DispatchQueue.main.async {
            completion(result)
        }
    }

    private func convert(
        _ buffer: AVAudioPCMBuffer,
        with converter: AVAudioConverter,
        to targetFormat: AVAudioFormat,
        onAudio: @escaping (AVAudioPCMBuffer) -> Void
    ) {
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
        #if DEBUG
        logFirstConversion("status=\(conversionStatus.rawValue) inputFrames=\(buffer.frameLength) outputFrames=\(converted.frameLength) error=\(conversionError?.localizedDescription ?? "none")")
        #endif
        guard conversionStatus != .error, converted.frameLength > 0 else { return }
        onAudio(converted)
    }

    private static func copy(_ buffer: AVAudioPCMBuffer, format: AVAudioFormat) -> AVAudioPCMBuffer? {
        guard let copy = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: buffer.frameLength) else { return nil }
        copy.frameLength = buffer.frameLength
        let sourceBuffers = UnsafeMutableAudioBufferListPointer(buffer.mutableAudioBufferList)
        let destinationBuffers = UnsafeMutableAudioBufferListPointer(copy.mutableAudioBufferList)
        guard sourceBuffers.count == destinationBuffers.count else { return nil }
        for index in 0..<sourceBuffers.count {
            let source = sourceBuffers[index]
            guard let sourceData = source.mData, let destinationData = destinationBuffers[index].mData else { return nil }
            let byteCount = min(Int(source.mDataByteSize), Int(destinationBuffers[index].mDataByteSize))
            memcpy(destinationData, sourceData, byteCount)
            destinationBuffers[index].mDataByteSize = UInt32(byteCount)
        }
        return copy
    }

    private func setRunning(_ value: Bool) {
        stateLock.lock()
        running = value
        stateLock.unlock()
    }

    private func markRawCallback() {
        stateLock.lock()
        lastRawCallbackUptime = ProcessInfo.processInfo.systemUptime
        stateLock.unlock()
    }

    #if DEBUG
    private var firstCallbackOperationID: UInt64?
    private var firstConversionOperationID: UInt64?

    private func isFirstCallbackLogged(for operationID: UInt64) -> Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        if firstCallbackOperationID == operationID { return true }
        firstCallbackOperationID = operationID
        return false
    }

    private func logFirstConversion(_ message: String) {
        stateLock.lock()
        let shouldLog = firstConversionOperationID != operationID
        if shouldLog { firstConversionOperationID = operationID }
        stateLock.unlock()
        if shouldLog { MicrophoneLifecycleLog.mark("first conversion \(message)") }
    }
    #endif

    private static func error(_ message: String, code: Int) -> NSError {
        NSError(domain: "Echo", code: code, userInfo: [NSLocalizedDescriptionKey: message])
    }

    deinit {
        controlQueue.sync {
            removeConfigurationObserver()
            teardownOnControlQueue()
        }
    }
}
