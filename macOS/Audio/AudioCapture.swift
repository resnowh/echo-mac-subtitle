import AVFoundation

protocol AudioCaptureSource: AnyObject {
    var inputFormat: AVAudioFormat { get }
    func start(onAudio: @escaping (AVAudioPCMBuffer) -> Void) throws
    func stop()
}

protocol SystemAudioCaptureSource: AnyObject {
    func start(completion: @escaping (Error?) -> Void)
    func stop(completion: (() -> Void)?)
}

final class MacMicrophoneCapture: AudioCaptureSource {
    private let engine = AVAudioEngine()
    private(set) var inputFormat: AVAudioFormat
    private(set) var isRunning = false

    init() {
        inputFormat = engine.inputNode.outputFormat(forBus: 0)
    }

    func start(onAudio: @escaping (AVAudioPCMBuffer) -> Void) throws {
        let input = engine.inputNode
        let format = input.outputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0 else {
            throw NSError(domain: "Echo", code: 3, userInfo: [NSLocalizedDescriptionKey: "没有检测到可用的麦克风输入，请检查 Mac 的输入设备设置。"])
        }
        inputFormat = format
        input.removeTap(onBus: 0)
        input.installTap(onBus: 0, bufferSize: 1024, format: format) { buffer, _ in
            onAudio(buffer)
        }
        engine.prepare()
        do {
            try engine.start()
            isRunning = true
        } catch {
            input.removeTap(onBus: 0)
            throw error
        }
    }

    func stop() {
        engine.stop()
        engine.inputNode.removeTap(onBus: 0)
        isRunning = false
    }
}
