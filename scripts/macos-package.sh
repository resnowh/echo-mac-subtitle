#!/usr/bin/env bash
# Package a separately built Echo.app without launching or replacing any running Echo.
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: bash scripts/macos-package.sh <preflight|signed> <Echo.app> <output-dir>" >&2
  exit 2
fi

mode="$1"
app="$2"
outdir="$3"
if [[ "$mode" != "preflight" && "$mode" != "signed" ]]; then
  echo "Unknown packaging mode: $mode" >&2
  exit 2
fi
if [[ ! -d "$app/Contents" || ! -f "$app/Contents/Info.plist" ]]; then
  echo "Expected an independently built Echo.app bundle: $app" >&2
  exit 2
fi

info="$app/Contents/Info.plist"
/usr/bin/plutil -lint "$info"
bundle_id=$(/usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "$info")
version=$(/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" "$info")
build=$(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" "$info")
if [[ "$bundle_id" != "local.echo.subtitle" ]]; then
  echo "Unexpected bundle identifier: $bundle_id" >&2
  exit 1
fi
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ || ! "$build" =~ ^[0-9]+$ ]]; then
  echo "Release bundle has invalid version/build metadata." >&2
  exit 1
fi

mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd -P)"
label="Echo-macOS-v${version}-build${build}-universal"
if [[ "$mode" == "preflight" ]]; then
  label="Echo-macOS-preflight-untrusted"
fi
dmg="$outdir/$label.dmg"
if [[ -e "$dmg" ]]; then
  echo "Refusing to overwrite: $dmg" >&2
  exit 1
fi

if [[ "$mode" == "signed" ]]; then
  : "${APPLE_SIGN_IDENTITY:?Missing Developer ID Application signing identity}"
  : "${APPLE_NOTARY_KEY_PATH:?Missing notarization API private key path}"
  : "${APPLE_NOTARY_KEY_ID:?Missing App Store Connect API key ID}"
  : "${APPLE_NOTARY_ISSUER_ID:?Missing App Store Connect API issuer ID}"
  if [[ ! -f "$APPLE_NOTARY_KEY_PATH" ]]; then
    echo "Notarization key file not found." >&2
    exit 1
  fi
  /usr/bin/codesign --force --sign "$APPLE_SIGN_IDENTITY" \
    --options runtime --timestamp \
    --entitlements macOS/Entitlements/EchoRelease.entitlements "$app"
  /usr/bin/codesign --verify --deep --strict --verbose=2 "$app"
  /usr/bin/codesign -d --verbose=2 "$app" 2>&1 | /usr/bin/grep -q 'flags=.*runtime'
fi

stage_dir="$(/usr/bin/mktemp -d "${TMPDIR:-/tmp}/echo-mac-package.XXXXXX")"
trap '/bin/rm -rf "$stage_dir"' EXIT
/usr/bin/ditto "$app" "$stage_dir/Echo.app"
/bin/ln -s /Applications "$stage_dir/Applications"

/usr/bin/hdiutil create -volname Echo -srcfolder "$stage_dir" -format UDZO -ov "$dmg"
/usr/bin/hdiutil verify "$dmg"

if [[ "$mode" == "signed" ]]; then
  /usr/bin/xcrun notarytool submit "$dmg" \
    --key "$APPLE_NOTARY_KEY_PATH" \
    --key-id "$APPLE_NOTARY_KEY_ID" \
    --issuer "$APPLE_NOTARY_ISSUER_ID" --wait
  /usr/bin/xcrun stapler staple "$dmg"
  /usr/bin/xcrun stapler validate "$dmg"
fi

(cd "$outdir" && /usr/bin/shasum -a 256 "$(basename "$dmg")" > "$(basename "$dmg").sha256")
echo "Created: $dmg"
echo "Mode: $mode (preflight is NOT trusted or suitable for public distribution)"
