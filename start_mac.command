#!/bin/sh
set -eu
cd "$(dirname "$0")"

if ! command -v python3 >/dev/null 2>&1; then
  echo "没有找到 Python 3，请先双击 install_mac.command。"
  read -r -p "按回车退出..." _
  exit 1
fi
if ! grep -q '^OPENAI_API_KEY=sk-' .env 2>/dev/null; then
  echo "尚未在 .env 配置 OPENAI_API_KEY。请先双击 install_mac.command。"
  read -r -p "按回车退出..." _
  exit 1
fi

echo "Echo 正在启动，请不要关闭这个窗口。"
python3 server.py &
SERVER_PID=$!
trap 'kill "$SERVER_PID" 2>/dev/null || true' EXIT INT TERM
sleep 1
open "http://localhost:8787"
wait "$SERVER_PID"
