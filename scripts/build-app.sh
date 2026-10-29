#!/usr/bin/env bash
# Builds a universal, ad-hoc signed Ferry.app into ./build.
set -euo pipefail

cd "$(dirname "$0")/.."

CONFIG="${CONFIG:-release}"
APP="build/Ferry.app"
ARCHS=(--arch arm64 --arch x86_64)

# In an iCloud Drive folder like Documents, syncing the build products while
# they are being replaced leaves copies like "Ferry 2.app" behind.
mkdir -p build .build
xattr -w "com.apple.fileprovider.ignore#P" 1 build .build

swift build -c "$CONFIG" "${ARCHS[@]}"
BIN_DIR="$(swift build -c "$CONFIG" "${ARCHS[@]}" --show-bin-path)"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/Ferry" "$APP/Contents/MacOS/Ferry"

# SwiftPM records the deployment target as the SDK version, which makes AppKit
# fall back to legacy styling on newer macOS. Stamp the real SDK version.
MIN_OS="$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' Resources/Info.plist)"
SDK_VERSION="$(xcrun --sdk macosx --show-sdk-version)"
vtool -set-build-version macos "$MIN_OS" "$SDK_VERSION" -replace \
    -output "$APP/Contents/MacOS/Ferry" "$APP/Contents/MacOS/Ferry"

cp Resources/Info.plist "$APP/Contents/Info.plist"
if [[ -f Resources/AppIcon.icns ]]; then
    cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
fi

xattr -cr "$APP"
codesign --force --sign - "$APP"
echo "Built $APP"
