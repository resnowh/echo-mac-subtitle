"""Generate deterministic float32 PCM inputs for Mac/Windows converter parity."""

import base64
import json
import math
import struct
from pathlib import Path


def make_case(case_id, sample_rate, channels, frame_count, signal):
    samples = []
    for frame in range(frame_count):
        frame_values = signal(frame, sample_rate)
        if len(frame_values) != channels:
            raise ValueError(f"{case_id}: expected {channels} channels")
        samples.extend(frame_values)
    raw = struct.pack(f"<{len(samples)}f", *samples)
    return {
        "id": case_id,
        "sampleRate": sample_rate,
        "channels": channels,
        "frameCount": frame_count,
        "inputBase64": base64.b64encode(raw).decode("ascii"),
    }


def tone(amplitude, frequency, delay_seconds=0):
    def sample(frame, sample_rate):
        time = frame / sample_rate - delay_seconds
        value = 0.0 if time < 0 else amplitude * math.sin(2 * math.pi * frequency * time)
        return value
    return sample


cases = [
    make_case("mono-48k-440hz", 48_000, 1, 48_000,
              lambda frame, rate: [tone(0.42, 440)(frame, rate)]),
    make_case("stereo-44k-downmix", 44_100, 2, 44_100,
              lambda frame, rate: [tone(0.18, 660)(frame, rate), tone(0.62, 660)(frame, rate)]),
    make_case("mono-48k-delayed-523hz", 48_000, 1, 48_000,
              lambda frame, rate: [tone(0.35, 523, 0.15)(frame, rate)]),
    make_case("stereo-48k-phase-cancel", 48_000, 2, 48_000,
              lambda frame, rate: [tone(0.3, 997)(frame, rate), -tone(0.3, 997)(frame, rate)]),
]

output = Path(__file__).resolve().parents[1] / "Echo.CoreChecks" / "Fixtures" / "audio-conversion-inputs.json"
output.write_text(json.dumps({"format": "f32le-interleaved", "cases": cases}, indent=2) + "\n", encoding="utf-8")
print(f"Wrote {len(cases)} deterministic audio conversion cases to {output}")
