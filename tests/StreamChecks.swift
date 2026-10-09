import Foundation

@main
struct StreamChecks {
    private static let fixtureAPIKey = "fixture-token"
    private static let fixtureConfiguration = #"{"model":"stt-rt-v5","audio_format":"pcm_s16le"}"#

    static func main() throws {
        try APIKeyVaultChecks.run()
        LifecycleRecoveryChecks.run()
        SonioxErrorChecks.run()
        let fixtureDirectory = CommandLine.arguments.count > 2
            ? URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
            : nil
        try CorrectionChecks.run(archiveFixtureDirectory: fixtureDirectory)
        let pcm = PCM16TimelineMixer.self
        let samples: [Int16] = [.min, -1, 0, 1, .max]
        precondition(pcm.samples(from: pcm.data(from: samples)) == samples)
        let mixer = PCM16TimelineMixer()
        let a = pcm.data(from: [Int16](repeating: 30_000, count: 320))
        let b = pcm.data(from: [Int16](repeating: -10_000, count: 320))
        let start = ProcessInfo.processInfo.systemUptime
        // Thirty minutes of synthetic dual-input audio, without devices or sleep.
        for index in 0..<90_000 {
            let frame = Int64(index * 320)
            let now = Date(timeIntervalSince1970: Double(index) * 0.02)
            precondition(mixer.append(a, source: .microphone, startFrame: frame, now: now).isEmpty)
            let output = mixer.append(b, source: .computer, startFrame: frame, now: now)
            precondition(output.count == 1 && output[0].startFrame == frame)
            precondition(pcm.samples(from: output[0].data).allSatisfy { $0 == 10_000 })
        }
        precondition(mixer.flush().isEmpty)
        mixer.reset()
        _ = mixer.append(a, source: .microphone, startFrame: 0)
        precondition(pcm.samples(from: mixer.flush()[0].data).allSatisfy { $0 == 30_000 })
        let prebuffer = PCM16Prebuffer()
        for index in 0..<10_000 {
            prebuffer.append(PCM16Chunk(data: a, startFrame: Int64(index * 320)))
            precondition(prebuffer.frameCount <= prebuffer.maxFrames)
        }
        let drained = prebuffer.drain()
        precondition(drained.count == 125 && drained.last?.startFrame == 9_999 * 320)
        precondition(prebuffer.frameCount == 0 && prebuffer.drain().isEmpty)
        print("PASS: 30-minute synthetic mixer continuity, sample equivalence, reset, bounded prebuffer (\(ProcessInfo.processInfo.systemUptime - start)s)")

        let port = CommandLine.arguments[1]
        let client = SonioxWebSocketClient(url: URL(string: "ws://127.0.0.1:\(port)")!)
        for _ in 0..<20 {
            let done = DispatchSemaphore(value: 0)
            client.connect(apiKey: fixtureAPIKey, configuration: fixtureConfiguration, onReady: {
                for index in 0..<32 { client.sendAudio(Data([UInt8(index)])) }
                client.finish()
            }, onMessage: { message in
                precondition(message == "ordered:32")
                done.signal()
            }, onFailure: { error in fatalError("Unexpected transport error: \(error)") })
            precondition(done.wait(timeout: .now() + 10) == .success)
            client.cancel()
            precondition(!client.isReady && !client.isActive)
        }
        let failed = DispatchSemaphore(value: 0)
        client.connect(apiKey: fixtureAPIKey, configuration: fixtureConfiguration, onReady: {
            client.sendAudio(Data(repeating: 0, count: 160_001))
        }, onMessage: { _ in fatalError("Unexpected data") }, onFailure: { _ in failed.signal() })
        precondition(failed.wait(timeout: .now() + 10) == .success)
        precondition(!client.isReady && !client.isActive)
        let notReady = DispatchSemaphore(value: 0)
        client.sendAudio(Data([1])) { error in
            precondition(error != nil)
            notReady.signal()
        }
        precondition(notReady.wait(timeout: .now() + 5) == .success)
        // Cancel immediately during handshake; late completion of these tasks
        // must not fail or mark the replacement socket ready.
        for _ in 0..<20 {
            client.connect(apiKey: fixtureAPIKey, configuration: fixtureConfiguration, onReady: {}, onMessage: { _ in }, onFailure: { _ in })
            client.cancel()
        }
        let replacement = DispatchSemaphore(value: 0)
        client.connect(apiKey: fixtureAPIKey, configuration: fixtureConfiguration, onReady: {
            for index in 0..<32 { client.sendAudio(Data([UInt8(index)])) }
            client.finish()
        }, onMessage: { message in
            precondition(message == "ordered:32")
            replacement.signal()
        }, onFailure: { error in fatalError("Replacement socket failed: \(error)") })
        precondition(replacement.wait(timeout: .now() + 10) == .success)
        client.cancel()
        print("PASS: 20 loopback reconnects, ordered PCM/EOF, cancel reset, bounded backlog failure")
        print("PASS: not-ready rejection and replacement connection after 20 handshake cancellations")

        let callbackFixtureDirectory = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
        let callbackMarker = callbackFixtureDirectory.appendingPathComponent("callback-probe-received")
        try? FileManager.default.removeItem(at: callbackMarker)
        let callbackQueue = DispatchQueue(label: "test.soniox-callbacks")
        let callbackGate = DispatchSemaphore(value: 0)
        callbackQueue.async { callbackGate.wait() }
        let staleReady = LockedCounter()
        let staleMessage = LockedCounter()
        let callbackClient = SonioxWebSocketClient(
            url: URL(string: "ws://127.0.0.1:\(port)")!, callbackQueue: callbackQueue)
        let probe = #"{"model":"stt-rt-v5","callback_probe":true}"#
        callbackClient.connect(apiKey: fixtureAPIKey, configuration: probe,
                               onReady: { staleReady.increment() },
                               onMessage: { _ in staleMessage.increment() },
                               onFailure: { _ in })
        let markerDeadline = Date().addingTimeInterval(5)
        while !FileManager.default.fileExists(atPath: callbackMarker.path) && Date() < markerDeadline {
            Thread.sleep(forTimeInterval: 0.01)
        }
        precondition(FileManager.default.fileExists(atPath: callbackMarker.path), "fixture did not deliver callback probe")
        callbackClient.cancel()
        callbackGate.signal()
        callbackQueue.sync {}
        precondition(staleReady.value == 0 && staleMessage.value == 0,
                     "queued callbacks from a cancelled connection must be discarded")
        print("PASS: queued callbacks from a cancelled WebSocket are discarded")
    }
}

private final class LockedCounter {
    private let lock = NSLock()
    private var storage = 0
    var value: Int { lock.lock(); defer { lock.unlock() }; return storage }
    func increment() { lock.lock(); storage += 1; lock.unlock() }
}
