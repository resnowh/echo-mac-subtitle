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
                                               interleaved: true),
              let source = AVAudioPCMBuffer(pcmFormat: sourceFormat,
                                            frameCapacity: AVAudioFrameCount(input.frameCount)) else {
            throw NSError(domain: "EchoAudioParity", code: 1,
                          userInfo: [NSLocalizedDescriptionKey: "Invalid audio input \(input.id)"])
        }
        source.frameLength = AVAudioFrameCount(input.frameCount)
        guard let sourcePointer = source.mutableAudioBufferList.pointee.mBuffers.mData,
              Int(source.mutableAudioBufferList.pointee.mBuffers.mDataByteSize) >= sourceData.count else {
            throw NSError(domain: "EchoAudioParity", code: 2,
                          userInfo: [NSLocalizedDescriptionKey: "Could not allocate interleaved source audio for \(input.id)"])
        }
        sourceData.withUnsafeBytes { bytes in
            memcpy(sourcePointer, bytes.baseAddress!, sourceData.count)
        }
        source.mutableAudioBufferList.pointee.mBuffers.mDataByteSize = UInt32(sourceData.count)

        let runner = ProductionAudioConverterHarness()
        let converted = try runner.run(source, targetFormat: targetFormat)
        guard let outputPointer = converted.audioBufferList.pointee.mBuffers.mData else {
            throw NSError(domain: "EchoAudioParity", code: 4,
                          userInfo: [NSLocalizedDescriptionKey: "Production converter returned no output for \(input.id)"])
        }
        let outputBytes = Int(converted.audioBufferList.pointee.mBuffers.mDataByteSize)
        guard outputBytes == Int(converted.frameLength) * MemoryLayout<Int16>.size else {
            throw NSError(domain: "EchoAudioParity", code: 5,
                          userInfo: [NSLocalizedDescriptionKey: "Unexpected PCM16 byte count for \(input.id)"])
        }
        let pcm16 = Data(bytes: outputPointer, count: outputBytes)
        return AudioCaseOutput(id: input.id,
                               sampleCount: Int(converted.frameLength),
                               pcm16Base64: pcm16.base64EncodedString())
    }
}

final class ProductionAudioConverterHarness {
    private let activeOperationID: UInt64 = 1

    func run(_ source: AVAudioPCMBuffer, targetFormat: AVAudioFormat) throws -> AVAudioPCMBuffer {
        guard let converter = AVAudioConverter(from: source.format, to: targetFormat) else {
            throw NSError(domain: "EchoAudioParity", code: 3,
                          userInfo: [NSLocalizedDescriptionKey: "Could not allocate AVAudioConverter"])
        }
        var output: AVAudioPCMBuffer?
        convert(source, with: converter, to: targetFormat, operationID: activeOperationID) { output = $0 }
        guard let output else {
            throw NSError(domain: "EchoAudioParity", code: 4,
                          userInfo: [NSLocalizedDescriptionKey: "Production converter returned no output"])
        }
        return output
    }

    private func isCurrentOperation(_ operationID: UInt64) -> Bool { operationID == activeOperationID }
    private func logFirstConversion(operationID: UInt64, message: String) {}

    // PRODUCTION_CONVERTER_METHOD
}
