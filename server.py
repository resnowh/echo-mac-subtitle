#!/usr/bin/env python3
import json
import os
import re
import sys
import urllib.error
import urllib.request
from email.parser import BytesParser
from email.policy import default
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).parent
PORT = int(os.getenv("PORT", "8787"))


def load_dotenv():
    """Load simple KEY=VALUE entries so the app can be launched from Finder."""
    env_file = ROOT / ".env"
    if not env_file.exists():
        return
    for raw_line in env_file.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        key, value = key.strip(), value.strip().strip('"').strip("'")
        os.environ.setdefault(key, value)


load_dotenv()
PORT = int(os.getenv("PORT", "8787"))
API_KEY = os.getenv("OPENAI_API_KEY", "")


def multipart_file(body: bytes, content_type: str):
    message = BytesParser(policy=default).parsebytes(
        b"Content-Type: " + content_type.encode() + b"\r\n\r\n" + body
    )
    for part in message.iter_parts():
        if part.get_param("name", header="content-disposition") == "audio":
            data = part.get_payload(decode=True) or b""
            filename = part.get_filename() or "recording.webm"
            return filename, data
    raise ValueError("未找到音频文件")


def openai_json(url, payload):
    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {API_KEY}", "Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=90) as response:
        return json.loads(response.read())


def transcribe(filename, audio):
    boundary = "----CodexAudioBoundary7MA4YWxkTrZu0gW"
    chunks = []
    def field(name, value):
        chunks.extend([
            f"--{boundary}\r\n".encode(),
            f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode(),
            value.encode(), b"\r\n",
        ])
    chunks.extend([
        f"--{boundary}\r\n".encode(),
        f'Content-Disposition: form-data; name="file"; filename="{filename}"\r\n'.encode(),
        b"Content-Type: audio/webm\r\n\r\n", audio, b"\r\n",
    ])
    field("model", "gpt-4o-mini-transcribe")
    field("language", "en")
    chunks.append(f"--{boundary}--\r\n".encode())
    req = urllib.request.Request(
        "https://api.openai.com/v1/audio/transcriptions",
        data=b"".join(chunks),
        headers={"Authorization": f"Bearer {API_KEY}", "Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=90) as response:
        return json.loads(response.read()).get("text", "").strip()


def translate(text):
    result = openai_json("https://api.openai.com/v1/responses", {
        "model": "gpt-4o-mini",
        "input": (
            "Translate the following English speech into natural, accurate Simplified Chinese. "
            "Return only the Chinese translation, with no explanation or quotation marks.\n\n" + text
        ),
    })
    if result.get("output_text"):
        return result["output_text"].strip()
    parts = []
    for item in result.get("output", []):
        for content in item.get("content", []):
            if content.get("type") == "output_text":
                parts.append(content.get("text", ""))
    return "".join(parts).strip()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        sys.stderr.write("%s - %s\n" % (self.address_string(), format % args))

    def send_json(self, status, payload):
        data = json.dumps(payload, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path == "/api/health":
            self.send_json(200, {"ok": True, "configured": bool(API_KEY)})
            return
        path = ROOT / ("index.html" if self.path in ("/", "") else self.path.lstrip("/"))
        if path.exists() and path.is_file() and ROOT in path.parents:
            content = path.read_bytes()
            types = {".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8", ".js": "text/javascript; charset=utf-8"}
            self.send_response(200)
            self.send_header("Content-Type", types.get(path.suffix, "application/octet-stream"))
            self.send_header("Content-Length", str(len(content)))
            self.end_headers()
            self.wfile.write(content)
            return
        self.send_error(404)

    def do_POST(self):
        if self.path != "/api/process":
            self.send_error(404)
            return
        if not API_KEY:
            self.send_json(500, {"error": "服务端尚未配置 OPENAI_API_KEY"})
            return
        try:
            size = int(self.headers.get("Content-Length", "0"))
            if size > 25 * 1024 * 1024:
                raise ValueError("录音不能超过 25 MB")
            filename, audio = multipart_file(self.rfile.read(size), self.headers.get("Content-Type", ""))
            if not audio:
                raise ValueError("录音为空，请重新录制")
            english = transcribe(filename, audio)
            chinese = translate(english) if english else ""
            self.send_json(200, {"english": english, "chinese": chinese})
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "ignore")
            self.send_json(502, {"error": f"OpenAI API 返回错误（{exc.code}）", "detail": detail[:500]})
        except Exception as exc:
            self.send_json(400, {"error": str(exc)})


if __name__ == "__main__":
    print(f"语音转写翻译服务运行于 http://localhost:{PORT}")
    print("提示：请先设置 OPENAI_API_KEY")
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
