#!/bin/sh
set -eu
cd "$(dirname "$0")"
DERIVED="${TMPDIR:-/tmp}/echo-derived"
xcodebuild -project macOS/EchoMac.xcodeproj -scheme Echo -configuration Release -sdk macosx -derivedDataPath "$DERIVED" CODE_SIGNING_ALLOWED=NO build
APP="$DERIVED/Build/Products/Release/Echo.app"
echo "已构建：$APP"
open "$APP"
