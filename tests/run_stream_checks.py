"""Isolated logic and loopback transport checks. No credentials/audio hardware."""
import base64
import hashlib
from pathlib import Path
import socketserver
import struct
import subprocess
import tempfile
import threading


class WebSocketFixture(socketserver.StreamRequestHandler):
    def handle(self):
        try:
            self.rfile.readline()
            headers = {}
            while line := self.rfile.readline().strip():
                name, value = line.decode().split(":", 1)
                headers[name.lower()] = value.strip()
            if "sec-websocket-key" not in headers:
                return  # Expected when the client cancels before its handshake.
            digest = hashlib.sha1((headers["sec-websocket-key"] +
                                   "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()
            accept = base64.b64encode(digest).decode()
            self.wfile.write(("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\n"
                              "Connection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n").encode())
            packets = []
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
                if opcode == 2:
                    if data:
                        packets.append(data)
                    else:
                        assert packets == [bytes([i]) for i in range(32)]
                        reply = b"ordered:32"
                        self.wfile.write(bytes([0x81, len(reply)]) + reply)
        except (ConnectionError, BrokenPipeError):
            pass


root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="echo-stream-checks-") as folder:
    executable = str(Path(folder) / "checks")
    sdk = subprocess.check_output(["xcrun", "--sdk", "macosx", "--show-sdk-path"], text=True).strip()
    subprocess.run(["xcrun", "swiftc", "-sdk", sdk, "-O", "-o", executable,
                    str(root / "macOS/Audio/PCM16AudioPipeline.swift"),
                    str(root / "macOS/Services/SonioxWebSocketClient.swift"),
                    str(root / "macOS/Models/TranscriptModels.swift"),
                    str(root / "macOS/Services/DeepSeekService.swift"),
                    str(root / "macOS/Services/SRTExporter.swift"),
                    str(root / "tests/CorrectionChecks.swift"),
                    str(root / "tests/StreamChecks.swift")], check=True)
    with socketserver.ThreadingTCPServer(("127.0.0.1", 0), WebSocketFixture) as server:
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            subprocess.run([executable, str(server.server_address[1])], check=True, timeout=120)
        finally:
            server.shutdown()
