import Foundation

@main
struct StreamChecks {
    static func main() throws {
        try CorrectionChecks.run()
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
            client.connect(configuration: "fixture", onReady: {
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
        client.connect(configuration: "fixture", onReady: {
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
            client.connect(configuration: "fixture", onReady: {}, onMessage: { _ in }, onFailure: { _ in })
            client.cancel()
        }
        let replacement = DispatchSemaphore(value: 0)
        client.connect(configuration: "fixture", onReady: {
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
    }
}
