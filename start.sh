#!/bin/sh
set -eu
if [ -z "${OPENAI_API_KEY:-}" ]; then
  echo "请先设置 OPENAI_API_KEY，例如：export OPENAI_API_KEY=sk-..."
  exit 1
fi
exec python3 server.py
