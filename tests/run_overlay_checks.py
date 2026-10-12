"""Compile the real overlay UI with a feed-only model; never launch Echo."""
from pathlib import Path
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="echo-overlay-checks-") as folder:
    executable = str(Path(folder) / "checks")
    sdk = subprocess.check_output(["xcrun", "--sdk", "macosx", "--show-sdk-path"], text=True).strip()
    sources = ["macOS/Models/TranscriptModels.swift",
               "macOS/Models/DesktopSubtitleOverlayModels.swift",
               "macOS/Services/DesktopSubtitleOverlayFeed.swift",
               "macOS/Views/DesktopSubtitleOverlay.swift",
               "tests/OverlayViewChecks.swift"]
    subprocess.run(["xcrun", "swiftc", "-sdk", sdk, "-o", executable,
                    *[str(root / source) for source in sources]], check=True)
    subprocess.run([executable, *sys.argv[1:]], check=True, timeout=30)
