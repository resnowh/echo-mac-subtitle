import AVFoundation
import Foundation

struct AudioCaseInput: Decodable {
    let id: String
    let sampleRate: Double
    let channels: UInt32
    let frameCount: Int
    let inputBase64: String
}

struct AudioInputFixture: Decodable {
    let format: String
    let cases: [AudioCaseInput]
}

struct AudioCaseOutput: Encodable {
    let id: String
    let sampleCount: Int
    let pcm16Base64: String
}

struct AudioOutputFixture: Encodable {
    let sourceCommit: String
    let audioCaptureSourceSHA256: String
    let speechViewModelSourceSHA256: String
    let converter: String
    let cases: [AudioCaseOutput]
}

@main
struct AudioConversionParityChecks {
    static func main() throws {
        guard CommandLine.arguments.count == 3 else {
            fatalError("usage: audio-conversion-checks <input.json> <output.json>")
        }
        let inputURL = URL(fileURLWithPath: CommandLine.arguments[1])
        let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
        let fixture = try JSONDecoder().decode(AudioInputFixture.self, from: Data(contentsOf: inputURL))
        guard fixture.format == "f32le-interleaved", !fixture.cases.isEmpty else {
            fatalError("unsupported or empty input fixture")
        }

        let outputs = try fixture.cases.map(convert)
        let sourceCommit = ProcessInfo.processInfo.environment["ECHO_MAC_SOURCE_COMMIT"] ?? "unknown"
        let result = AudioOutputFixture(
            sourceCommit: sourceCommit,
            audioCaptureSourceSHA256: ProcessInfo.processInfo.environment["ECHO_MAC_AUDIO_CAPTURE_SHA256"] ?? "unknown",
            speechViewModelSourceSHA256: ProcessInfo.processInfo.environment["ECHO_MAC_SPEECH_VIEW_MODEL_SHA256"] ?? "unknown",
            converter: "AVAudioConverter",
            cases: outputs
        )
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(result).write(to: outputURL, options: .atomic)
        print("PASS: macOS AVAudioConverter generated \(outputs.count) PCM16 conversion references")
    }

    private static func convert(_ input: AudioCaseInput) throws -> AudioCaseOutput {
        guard input.sampleRate > 0, input.channels > 0, input.frameCount > 0,
              let sourceData = Data(base64Encoded: input.inputBase64),
              sourceData.count == input.frameCount * Int(input.channels) * MemoryLayout<Float>.size,
              let sourceFormat = AVAudioFormat(commonFormat: .pcmFormatFloat32,
                                               sampleRate: input.sampleRate,
                                               channels: input.channels,
                                               interleaved: true),
              let targetFormat = AVAudioFormat(commonFormat: .pcmFormatInt16,
                                               sampleRate: 16_000,
                                               channels: 1,
                                               interleaved: true) else {
            throw NSError(domain: "EchoAudioParity", code: 1,
                          userInfo: [NSLocalizedDescriptionKey: "Invalid audio input \(input.id)"])
        }
        let runner = try ProductionAudioConverterHarness(sourceFormat: sourceFormat, targetFormat: targetFormat)
        var pcm16 = Data()
        var outputSampleCount = 0
        let inputSamplesPerFrame = Int(input.channels) * MemoryLayout<Float>.size
        for startFrame in stride(from: 0, to: input.frameCount, by: 1024) {
            let chunkFrameCount = min(1024, input.frameCount - startFrame)
            let byteRange = startFrame * inputSamplesPerFrame ..< (startFrame + chunkFrameCount) * inputSamplesPerFrame
            let chunkData = sourceData.subdata(in: byteRange)
            guard let sourceChunk = AVAudioPCMBuffer(pcmFormat: sourceFormat,
                                                      frameCapacity: AVAudioFrameCount(chunkFrameCount)) else {
                throw NSError(domain: "EchoAudioParity", code: 3,
                              userInfo: [NSLocalizedDescriptionKey: "Could not allocate source chunk for \(input.id)"])
            }
            sourceChunk.frameLength = AVAudioFrameCount(chunkFrameCount)
            guard let sourcePointer = sourceChunk.mutableAudioBufferList.pointee.mBuffers.mData,
                  Int(sourceChunk.mutableAudioBufferList.pointee.mBuffers.mDataByteSize) >= chunkData.count else {
                throw NSError(domain: "EchoAudioParity", code: 2,
                              userInfo: [NSLocalizedDescriptionKey: "Could not allocate interleaved source chunk for \(input.id)"])
            }
            chunkData.withUnsafeBytes { bytes in
                memcpy(sourcePointer, bytes.baseAddress!, chunkData.count)
            }
            sourceChunk.mutableAudioBufferList.pointee.mBuffers.mDataByteSize = UInt32(chunkData.count)
            guard let converted = runner.convertChunk(sourceChunk),
                  let outputPointer = converted.audioBufferList.pointee.mBuffers.mData else { continue }
            let outputBytes = Int(converted.audioBufferList.pointee.mBuffers.mDataByteSize)
            guard outputBytes == Int(converted.frameLength) * MemoryLayout<Int16>.size else {
                throw NSError(domain: "EchoAudioParity", code: 5,
                              userInfo: [NSLocalizedDescriptionKey: "Unexpected PCM16 byte count for \(input.id)"])
            }
            pcm16.append(Data(bytes: outputPointer, count: outputBytes))
            outputSampleCount += Int(converted.frameLength)
        }
        return AudioCaseOutput(id: input.id,
                               sampleCount: outputSampleCount,
                               pcm16Base64: pcm16.base64EncodedString())
    }
}

final class ProductionAudioConverterHarness {
    private let activeOperationID: UInt64 = 1
    private let converter: AVAudioConverter
    private let targetFormat: AVAudioFormat

    init(sourceFormat: AVAudioFormat, targetFormat: AVAudioFormat) throws {
        guard let converter = AVAudioConverter(from: sourceFormat, to: targetFormat) else {
            throw NSError(domain: "EchoAudioParity", code: 3,
                          userInfo: [NSLocalizedDescriptionKey: "Could not allocate AVAudioConverter"])
        }
        self.converter = converter
        self.targetFormat = targetFormat
    }

    func convertChunk(_ source: AVAudioPCMBuffer) -> AVAudioPCMBuffer? {
        var output: AVAudioPCMBuffer?
        convert(source, with: converter, to: targetFormat, operationID: activeOperationID) { output = $0 }
        return output
    }

    private func isCurrentOperation(_ operationID: UInt64) -> Bool { operationID == activeOperationID }
    private func logFirstConversion(operationID: UInt64, message: String) {}

    // PRODUCTION_CONVERTER_METHOD
}
