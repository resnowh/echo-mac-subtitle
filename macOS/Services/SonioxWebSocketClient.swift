import Foundation

/// Transport-only Soniox realtime WebSocket client. It has no SwiftUI or
/// transcript knowledge; session ownership and token interpretation remain in
/// SpeechViewModel.
final class SonioxWebSocketClient {
    private let url: URL
    private var session: URLSession?
    private var task: URLSessionWebSocketTask?
    private(set) var isActive = false
    private(set) var isReady = false

    init(url: URL = URL(string: "wss://stt-rt.soniox.com/transcribe-websocket")!) {
        self.url = url
    }

    func connect(
        configuration: String,
        onReady: @escaping () -> Void,
        onMessage: @escaping (String) -> Void,
        onFailure: @escaping (Error) -> Void
    ) {
        cancel()
        let session = URLSession(configuration: .default)
        let task = session.webSocketTask(with: url)
        self.session = session
        self.task = task
        isActive = true
        isReady = false
        task.resume()
        sendConfiguration(configuration, task: task, attempt: 0, onReady: onReady, onFailure: onFailure)
        receiveMessages(from: task, onMessage: onMessage, onFailure: onFailure)
    }

    func sendAudio(_ data: Data, completion: ((Error?) -> Void)? = nil) {
        guard isActive, isReady, let task else {
            completion?(NSError(domain: "Echo.Soniox", code: 1, userInfo: [NSLocalizedDescriptionKey: "Soniox WebSocket 尚未 ready。"]))
            return
        }
        task.send(.data(data), completionHandler: completion ?? { _ in })
    }

    func finish(completion: ((Error?) -> Void)? = nil) {
        guard isActive, let task else {
            completion?(nil)
            return
        }
        task.send(.data(Data()), completionHandler: completion ?? { _ in })
    }

    func cancel() {
        isActive = false
        isReady = false
        task?.cancel(with: .goingAway, reason: nil)
        task = nil
        session?.invalidateAndCancel()
        session = nil
    }

    private func sendConfiguration(
        _ configuration: String,
        task: URLSessionWebSocketTask,
        attempt: Int,
        onReady: @escaping () -> Void,
        onFailure: @escaping (Error) -> Void
    ) {
        guard isActive else { return }
        task.send(.string(configuration)) { [weak self, weak task] error in
            guard let self, self.isActive else { return }
            if let error {
                guard attempt < 8 else {
                    onFailure(error)
                    return
                }
                DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + 0.25) {
                    guard let task, self.isActive else { return }
                    self.sendConfiguration(configuration, task: task, attempt: attempt + 1, onReady: onReady, onFailure: onFailure)
                }
                return
            }
            self.isReady = true
            onReady()
        }
    }

    private func receiveMessages(
        from task: URLSessionWebSocketTask,
        onMessage: @escaping (String) -> Void,
        onFailure: @escaping (Error) -> Void
    ) {
        task.receive { [weak self] result in
            guard let self, self.isActive else { return }
            switch result {
            case .success(let message):
                let text: String?
                switch message {
                case .string(let value): text = value
                case .data(let data): text = String(data: data, encoding: .utf8)
                @unknown default: text = nil
                }
                if let text { onMessage(text) }
                self.receiveMessages(from: task, onMessage: onMessage, onFailure: onFailure)
            case .failure(let error):
                onFailure(error)
            }
        }
    }
}
