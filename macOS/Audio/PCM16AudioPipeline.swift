import Foundation

enum PCM16Source: Hashable {
    case microphone
    case computer
}

struct PCM16Chunk {
    let data: Data
    let startFrame: Int64
    var frameCount: Int { data.count / 2 }
}

/// Bounded FIFO used while the WebSocket is connecting. It stores complete
/// PCM chunks and drops the oldest chunks when the time limit is exceeded.
final class PCM16Prebuffer {
    let maxFrames: Int64
    private(set) var chunks: [PCM16Chunk] = []
    private(set) var frameCount: Int64 = 0

    init(seconds: Double = 2.5, sampleRate: Int = 16_000) {
        maxFrames = Int64(max(1, seconds * Double(sampleRate)))
    }

    func append(_ chunk: PCM16Chunk) {
        guard !chunk.data.isEmpty else { return }
        chunks.append(chunk)
        frameCount += Int64(chunk.frameCount)
        while frameCount > maxFrames, !chunks.isEmpty {
            frameCount -= Int64(chunks.removeFirst().frameCount)
        }
    }

    func drain() -> [PCM16Chunk] {
        let result = chunks
        chunks.removeAll(keepingCapacity: true)
        frameCount = 0
        return result
    }

    func clear() {
        chunks.removeAll(keepingCapacity: true)
        frameCount = 0
    }
}

/// Mixes signed 16-bit mono PCM by frame position. It never concatenates the
/// two inputs: overlapping samples are averaged, and the result is clamped.
final class PCM16TimelineMixer {
    let sampleRate: Int
    private let blockFrames: Int
    private let expectedSources: Set<PCM16Source>
    private var nextFrame: Int64?
    private var buffers: [PCM16Source: [PCM16Chunk]] = [:]
    private var latestFrame: [PCM16Source: Int64] = [:]
    private var lastArrival: [PCM16Source: Date] = [:]
    private var firstArrival: Date?

    init(
        sampleRate: Int = 16_000,
        blockMilliseconds: Int = 20,
        expectedSources: Set<PCM16Source> = [.microphone, .computer]
    ) {
        self.sampleRate = sampleRate
        blockFrames = max(1, sampleRate * blockMilliseconds / 1000)
        self.expectedSources = expectedSources
    }

    func append(_ data: Data, source: PCM16Source, startFrame: Int64, now: Date = Date()) -> [PCM16Chunk] {
        let frameCount = data.count / 2
        guard frameCount > 0 else { return [] }
        buffers[source, default: []].append(PCM16Chunk(data: data, startFrame: startFrame))
        latestFrame[source] = max(latestFrame[source] ?? 0, startFrame + Int64(frameCount))
        lastArrival[source] = now
        firstArrival = firstArrival ?? now
        nextFrame = nextFrame ?? startFrame
        return emitReady(now: now)
    }

    func flush() -> [PCM16Chunk] {
        guard let end = latestFrame.values.max() else { return [] }
        return emit(until: end)
    }

    func reset() {
        nextFrame = nil
        buffers.removeAll(keepingCapacity: true)
        latestFrame.removeAll(keepingCapacity: true)
        lastArrival.removeAll(keepingCapacity: true)
        firstArrival = nil
    }

    private func emitReady(now: Date) -> [PCM16Chunk] {
        guard let nextFrame, !buffers.isEmpty else { return [] }
        let allSourcesReady = expectedSources.allSatisfy {
            (latestFrame[$0] ?? 0) >= nextFrame + Int64(blockFrames)
        }
        let startupWaitOver = firstArrival.map { now.timeIntervalSince($0) >= 0.12 } ?? false
        let sourcePaused = expectedSources.contains {
            now.timeIntervalSince(lastArrival[$0] ?? firstArrival ?? now) >= 0.12
        }
        guard allSourcesReady || (startupWaitOver && sourcePaused) else { return [] }
        let end = min(latestFrame.values.max() ?? nextFrame, nextFrame + Int64(sampleRate / 5))
        return emit(until: end)
    }

    private func emit(until endFrame: Int64) -> [PCM16Chunk] {
        guard let startFrame = nextFrame, endFrame > startFrame else { return [] }
        let count = Int(endFrame - startFrame)
        var output = [Int16](repeating: 0, count: count)
        for index in 0..<count {
            let frame = startFrame + Int64(index)
            let contributors = buffers.keys.compactMap { sample(at: frame, source: $0) }
            guard !contributors.isEmpty else { continue }
            let total = contributors.reduce(Int32(0)) { $0 + Int32($1) }
            let mixed = total / Int32(contributors.count)
            output[index] = Int16(max(Int32(Int16.min), min(Int32(Int16.max), mixed)))
        }
        nextFrame = endFrame
        trimBuffers(before: endFrame)
        return [PCM16Chunk(data: Self.data(from: output), startFrame: startFrame)]
    }

    private func sample(at frame: Int64, source: PCM16Source) -> Int16? {
        guard let chunks = buffers[source] else { return nil }
        for chunk in chunks.reversed() {
            let index = frame - chunk.startFrame
            guard index >= 0 else { continue }
            guard index < Int64(chunk.frameCount) else { return nil }
            // Decode only the requested sample, not the entire PCM block for
            // every output frame (quadratic work in the old hot path).
            return chunk.data.withUnsafeBytes { raw in
                let bytes = raw.bindMemory(to: UInt8.self)
                let offset = Int(index) * 2
                return Int16(bitPattern: UInt16(bytes[offset]) | (UInt16(bytes[offset + 1]) << 8))
            }
        }
        return nil
    }

    private func trimBuffers(before frame: Int64) {
        for source in buffers.keys {
            buffers[source]?.removeAll { $0.startFrame + Int64($0.frameCount) <= frame }
        }
    }

    static func samples(from data: Data) -> [Int16] {
        guard data.count >= 2 else { return [] }
        return data.withUnsafeBytes { raw in
            let bytes = raw.bindMemory(to: UInt8.self)
            return stride(from: 0, through: data.count - 2, by: 2).map {
                Int16(bitPattern: UInt16(bytes[$0]) | (UInt16(bytes[$0 + 1]) << 8))
            }
        }
    }

    static func data(from samples: [Int16]) -> Data {
        var data = Data(capacity: samples.count * 2)
        for sample in samples {
            let value = UInt16(bitPattern: sample)
            data.append(UInt8(value & 0xff))
            data.append(UInt8(value >> 8))
        }
        return data
    }
}
