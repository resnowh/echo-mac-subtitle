import CoreMedia
import ScreenCaptureKit

final class MacSystemAudioCapture: NSObject, SystemAudioCaptureSource, SCStreamOutput, SCStreamDelegate {
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
        SCShareableContent.getExcludingDesktopWindows(false, onScreenWindowsOnly: false) { [weak self] content, error in
            guard let self else { return }
            if let error {
                DispatchQueue.main.async { completion(error) }
                return
            }
            guard let display = content?.displays.first else {
                let error = NSError(domain: "Echo", code: 20, userInfo: [NSLocalizedDescriptionKey: "没有找到可用于捕获电脑音频的显示器。"])
                DispatchQueue.main.async { completion(error) }
                return
            }
            let configuration = SCStreamConfiguration()
            configuration.width = 2
            configuration.height = 2
            configuration.minimumFrameInterval = CMTime(value: 1, timescale: 1)
            configuration.queueDepth = 1
            configuration.capturesAudio = true
            configuration.sampleRate = 16_000
            configuration.channelCount = 1
            configuration.excludesCurrentProcessAudio = true
            let stream = SCStream(filter: SCContentFilter(display: display, excludingWindows: []), configuration: configuration, delegate: self)
            do {
                try stream.addStreamOutput(self, type: .audio, sampleHandlerQueue: self.outputQueue)
            } catch {
                DispatchQueue.main.async { completion(error) }
                return
            }
            self.stream = stream
            stream.startCapture { error in
                DispatchQueue.main.async { completion(error) }
            }
        }
    }

    func stop(completion: (() -> Void)? = nil) {
        guard let stream else { completion?(); return }
        self.stream = nil
        stream.stopCapture { _ in DispatchQueue.main.async { completion?() } }
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .audio, CMSampleBufferIsValid(sampleBuffer) else { return }
        onAudio(sampleBuffer)
    }

    func stream(_ stream: SCStream, didStopWithError error: Error) { onError(error) }
}
