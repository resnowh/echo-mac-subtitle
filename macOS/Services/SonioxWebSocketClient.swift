import Foundation

/// Serial ownership for socket state and bounded, ordered PCM sends. Consumer
/// callbacks run separately, allowing cancel/reconnect without re-entrancy.
final class SonioxWebSocketClient {
    private let url: URL
    private let control = DispatchQueue(label: "local.echo.soniox-transport")
    private let callbacks = DispatchQueue(label: "local.echo.soniox-callbacks")
    private var session: URLSession?
    private var task: URLSessionWebSocketTask?
    private var ready = false
    private var finishing = false
    private var failureHandler: ((Error) -> Void)?
    private var pending: [(Data, ((Error?) -> Void)?)] = []
    private var pendingBytes = 0
    private var sending = false
    private var watchdog: DispatchWorkItem?
    // Five seconds of 16 kHz mono PCM16, including the in-flight packet.
    private let maximumPendingBytes = 160_000
    private let sendTimeout: TimeInterval = 15

    var isActive: Bool { control.sync { task != nil } }
    var isReady: Bool { control.sync { ready } }

    init(url: URL = URL(string: "wss://stt-rt.soniox.com/transcribe-websocket")!) {
        self.url = url
    }

    func connect(configuration: String, onReady: @escaping () -> Void,
                 onMessage: @escaping (String) -> Void, onFailure: @escaping (Error) -> Void) {
        control.sync {
            cancelLocked()
            let config = URLSessionConfiguration.default
            config.timeoutIntervalForRequest = sendTimeout
            let session = URLSession(configuration: config)
            let task = session.webSocketTask(with: url)
            self.session = session
            self.task = task
            failureHandler = onFailure
            task.resume()
            armTimeout(for: task)
            // A failed configuration send has ambiguous delivery. Never resend
            // it on the same socket; a new recording opens a new connection.
            task.send(.string(configuration)) { [weak self, weak task] error in
                guard let self, let task else { return }
                self.control.async {
                    guard self.task === task else { return }
                    self.watchdog?.cancel()
                    if let error { self.failLocked(error); return }
                    self.ready = true
                    self.callbacks.async(execute: onReady)
                }
            }
            receiveMessages(from: task, onMessage: onMessage)
        }
    }

    func sendAudio(_ data: Data, completion: ((Error?) -> Void)? = nil) {
        control.sync {
            guard ready, task != nil, !finishing else {
                deliver(completion, error: Self.error("Soniox WebSocket 尚未 ready。"))
                return
            }
            guard pending.count < 512, data.count <= maximumPendingBytes - pendingBytes else {
                let error = Self.error("网络发送持续积压，已停止连接以避免音频延迟无限增长。请检查网络后重新开始录音。")
                deliver(completion, error: error)
                failLocked(error)
                return
            }
            pending.append((data, completion))
            pendingBytes += data.count
            pumpLocked()
        }
    }

    func finish(completion: ((Error?) -> Void)? = nil) {
        control.sync {
            guard task != nil else { deliver(completion, error: nil); return }
            guard ready, !finishing else {
                deliver(completion, error: Self.error("Soniox 尚未就绪或正在结束录音。"))
                return
            }
            finishing = true
            // End-of-stream follows every accepted PCM packet.
            pending.append((Data(), completion))
            pumpLocked()
        }
    }

    func cancel() { control.sync { cancelLocked() } }

    private func pumpLocked() {
        guard !sending, let task, let packet = pending.first else { return }
        sending = true
        armTimeout(for: task)
        task.send(.data(packet.0)) { [weak self, weak task] error in
            guard let self, let task else { return }
            self.control.async {
                guard self.task === task else { return }
                self.watchdog?.cancel()
                if let error { self.failLocked(error); return }
                self.pending.removeFirst()
                self.pendingBytes -= packet.0.count
                self.sending = false
                self.deliver(packet.1, error: nil)
                self.pumpLocked()
            }
        }
    }

    private func receiveMessages(from task: URLSessionWebSocketTask,
                                 onMessage: @escaping (String) -> Void) {
        task.receive { [weak self, weak task] result in
            guard let self, let task else { return }
            self.control.async {
                guard self.task === task else { return }
                switch result {
                case .success(let message):
                    let text: String?
                    switch message {
                    case .string(let value): text = value
                    case .data(let data): text = String(data: data, encoding: .utf8)
                    @unknown default: text = nil
                    }
                    if let text { self.callbacks.async { onMessage(text) } }
                    self.receiveMessages(from: task, onMessage: onMessage)
                case .failure(let error): self.failLocked(error)
                }
            }
        }
    }

    private func armTimeout(for task: URLSessionWebSocketTask) {
        watchdog?.cancel()
        let work = DispatchWorkItem { [weak self, weak task] in
            guard let self, let task, self.task === task else { return }
            self.failLocked(Self.error("Soniox 连接或音频发送超时，请检查网络。"))
        }
        watchdog = work
        control.asyncAfter(deadline: .now() + sendTimeout, execute: work)
    }

    private func failLocked(_ error: Error) {
        let handler = failureHandler
        cancelLocked(error: error)
        if let handler { callbacks.async { handler(error) } }
    }

    private func cancelLocked(error: Error = URLError(.cancelled)) {
        watchdog?.cancel()
        watchdog = nil
        task?.cancel(with: .goingAway, reason: nil)
        task = nil
        session?.invalidateAndCancel()
        session = nil
        ready = false
        finishing = false
        sending = false
        failureHandler = nil
        let abandoned = pending
        pending.removeAll(keepingCapacity: false)
        pendingBytes = 0
        for packet in abandoned { deliver(packet.1, error: error) }
    }

    private func deliver(_ completion: ((Error?) -> Void)?, error: Error?) {
        if let completion { callbacks.async { completion(error) } }
    }

    private static func error(_ message: String) -> NSError {
        NSError(domain: "Echo.Soniox", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
    }
}
