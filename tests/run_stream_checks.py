"""Isolated logic and loopback transport checks. No credentials/audio hardware."""
import base64
import hashlib
import json
from pathlib import Path
import socketserver
import struct
import subprocess
import sys
import tempfile
import threading


class WebSocketFixture(socketserver.StreamRequestHandler):
    probe_marker = None

    def handle(self):
        try:
            self.rfile.readline()
            headers = {}
            while line := self.rfile.readline().strip():
                name, value = line.decode().split(":", 1)
                headers[name.lower()] = value.strip()
            if "sec-websocket-key" not in headers:
                return  # Expected when the client cancels before its handshake.
            assert headers.get("authorization") == "Bearer fixture-token"
            digest = hashlib.sha1((headers["sec-websocket-key"] +
                                   "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()
            accept = base64.b64encode(digest).decode()
            self.wfile.write(("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\n"
                              "Connection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n").encode())
            packets = []
            configuration = None
            while True:
                header = self.rfile.read(2)
                if len(header) != 2:
                    return
                opcode, length = header[0] & 15, header[1] & 127
                if length == 126:
                    length = struct.unpack("!H", self.rfile.read(2))[0]
                elif length == 127:
                    length = struct.unpack("!Q", self.rfile.read(8))[0]
                mask = self.rfile.read(4) if header[1] & 128 else None
                data = self.rfile.read(length)
                if mask:
                    data = bytes(v ^ mask[i % 4] for i, v in enumerate(data))
                if opcode == 8:
                    return
                if opcode == 10 and data == b"probe":
                    Path(self.probe_marker).write_text("received", encoding="utf-8")
                if opcode == 1 and data:
                    configuration = json.loads(data)
                    assert "api_key" not in configuration
                    assert b"fixture-token" not in data
                    if configuration.get("callback_probe"):
                        reply = b"queued:stale"
                        self.wfile.write(bytes([0x81, len(reply)]) + reply)
                        self.wfile.write(bytes([0x89, 5]) + b"probe")
                elif opcode == 2:
                    if data:
                        packets.append(data)
                    else:
                        assert packets == [bytes([i]) for i in range(32)]
                        assert configuration is not None
                        reply = b"ordered:32"
                        self.wfile.write(bytes([0x81, len(reply)]) + reply)
        except (ConnectionError, BrokenPipeError):
            pass


root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="echo-stream-checks-") as folder:
    fixture_directory = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(folder) / "archive-fixture"
    fixture_directory.mkdir(parents=True, exist_ok=True)
    WebSocketFixture.probe_marker = str(fixture_directory / "callback-probe-received")
    executable = str(Path(folder) / "checks")
    sdk = subprocess.check_output(["xcrun", "--sdk", "macosx", "--show-sdk-path"], text=True).strip()
    subprocess.run(["xcrun", "swiftc", "-sdk", sdk, "-O", "-framework", "Security", "-o", executable,
                    str(root / "macOS/Audio/PCM16AudioPipeline.swift"),
                    str(root / "macOS/Services/SonioxWebSocketClient.swift"),
                    str(root / "macOS/Models/TranscriptModels.swift"),
                    str(root / "macOS/Models/LifecycleRecoveryState.swift"),
                    str(root / "macOS/Storage/TranscriptArchiveStore.swift"),
                    str(root / "macOS/Services/SonioxRequestBuilder.swift"),
                    str(root / "macOS/Services/SonioxServiceError.swift"),
                    str(root / "macOS/Services/DeepSeekService.swift"),
                    str(root / "macOS/Services/APIKeyVault.swift"),
                    str(root / "macOS/Services/SRTExporter.swift"),
                    str(root / "tests/CorrectionChecks.swift"),
                    str(root / "tests/SonioxErrorChecks.swift"),
                    str(root / "tests/APIKeyVaultChecks.swift"),
                    str(root / "tests/LifecycleRecoveryChecks.swift"),
                    str(root / "tests/StreamChecks.swift")], check=True)
    with socketserver.ThreadingTCPServer(("127.0.0.1", 0), WebSocketFixture) as server:
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            subprocess.run([executable, str(server.server_address[1]), str(fixture_directory)], check=True, timeout=120)
        finally:
            server.shutdown()
    for fixture in (fixture_directory / "mac-archive.json", fixture_directory / "mac-archive.srt"):
        data = fixture.read_bytes()
        print(f"Mac contract fixture {fixture.name}: {len(data)} bytes, sha256={hashlib.sha256(data).hexdigest()}")
