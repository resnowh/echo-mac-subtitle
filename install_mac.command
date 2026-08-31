#!/bin/sh
set -eu
cd "$(dirname "$0")"

if ! command -v python3 >/dev/null 2>&1; then
  echo "没有找到 Python 3。请先从 https://www.python.org/downloads/macos/ 安装 Python 3。"
  read -r -p "按回车退出..." _
  exit 1
fi

PYTHON_VERSION="$(python3 -c 'import sys; print("%s.%s" % sys.version_info[:2])')"
echo "已找到 Python ${PYTHON_VERSION}。本项目只使用 Python 标准库，无需安装额外依赖。"
if ! grep -q '^OPENAI_API_KEY=sk-' .env 2>/dev/null; then
  echo ""
  echo "请编辑当前文件夹中的 .env，把 OPENAI_API_KEY= 后面替换成你的 API Key。"
  open -a TextEdit .env
fi
echo "Mac 配置完成。双击 start_mac.command 即可启动。"
read -r -p "按回车退出..." _
