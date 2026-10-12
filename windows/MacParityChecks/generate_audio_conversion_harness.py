"""Insert the unchanged Mac production AVAudioConverter method into a test harness."""

from pathlib import Path
import re
import sys


def extract_converter(source: str) -> str:
    declaration = re.search(r"^    private func convert\(", source, re.MULTILINE)
    if declaration is None:
        raise SystemExit("Production MacMicrophoneCapture.convert method not found")
    opening_brace = source.find("{", declaration.start())
    if opening_brace < 0:
        raise SystemExit("Production converter body not found")
    depth = 0
    for index in range(opening_brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                method = source[declaration.start() : index + 1]
                return re.sub(r"^(    )private func ", r"\1func ", method, count=1)
    raise SystemExit("Unbalanced braces in production converter")


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: generate_audio_conversion_harness.py AudioCapture.swift output.swift")
    root = Path(__file__).resolve().parents[2]
    template = root / "windows/MacParityChecks/AudioConversionParityChecks.swift"
    source = Path(sys.argv[1]).read_text(encoding="utf-8")
    harness = template.read_text(encoding="utf-8")
    marker = "    // PRODUCTION_CONVERTER_METHOD"
    if harness.count(marker) != 1:
        raise SystemExit("Expected exactly one production converter insertion marker")
    output = harness.replace(marker, extract_converter(source))
    output_path = Path(sys.argv[2])
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(output, encoding="utf-8")


if __name__ == "__main__":
    main()
