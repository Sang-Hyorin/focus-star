#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
output="$root/dist/macos"
app="$output/Focus Star Mac.app"
mkdir -p "$app/Contents/MacOS"
cp "$root/assets/Info.plist" "$app/Contents/Info.plist"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -sdk "$sdk" -target "$arch-apple-macosx13.0" \
    "$root"/mac-source/*.swift -o "$root/dist/FocusStarMac-$arch" \
    -framework AppKit -framework CoreGraphics -framework SwiftUI -framework Charts
done
lipo -create "$root/dist/FocusStarMac-arm64" "$root/dist/FocusStarMac-x86_64" -output "$app/Contents/MacOS/FocusStarMac"
codesign --force --sign - "$app"
if [ "${1:-}" = --test ]; then "$app/Contents/MacOS/FocusStarMac" --self-test; fi
cp "$root/settings.example.json" "$output/settings.json"
cp "$root/README.md" "$output/README.md"
cp "$root/LICENSE" "$output/LICENSE"
ditto -c -k --sequesterRsrc "$output" "$root/dist/FocusStar-macOS.zip"
