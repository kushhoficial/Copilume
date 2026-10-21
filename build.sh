#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
VERSION="$(cat VERSION)"
BUILD_NUMBER="$(cat BUILD_NUMBER)"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ && "$BUILD_NUMBER" =~ ^[0-9]+$ ]] || { printf "Versão inválida.\n" >&2; exit 1; }
APP="$PWD/build/Copilume.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" build/arch
swiftc -swift-version 5 Sources/Design.swift scripts/MakeIcon.swift -o build/MakeIcon -framework AppKit
cp Assets/AppIcon.png "$APP/Contents/Resources/AppIcon.png"
build/MakeIcon build/Copilume.iconset Assets/AppIcon.png "$APP/Contents/Resources"
iconutil --convert icns build/Copilume.iconset --output "$APP/Contents/Resources/Copilume.icns"
for ARCH in arm64 x86_64; do
  swiftc -swift-version 5 -O -target "$ARCH-apple-macosx13.0" Sources/History.swift Sources/ImageClipboard.swift Sources/Design.swift Sources/App.swift Sources/main.swift -o "build/arch/Copilume-$ARCH" -framework AppKit -framework Carbon -framework ApplicationServices -framework ServiceManagement
done
lipo -create build/arch/Copilume-arm64 build/arch/Copilume-x86_64 -output "$APP/Contents/MacOS/Copilume"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>Copilume</string>
<key>CFBundleIdentifier</key><string>io.github.kushhoficial.Copilume</string>
<key>CFBundleName</key><string>Copilume</string>
<key>CFBundleDisplayName</key><string>Copilume</string>
<key>CFBundleIconFile</key><string>Copilume.icns</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>CFBundleVersion</key><string>$BUILD_NUMBER</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSApplicationCategoryType</key><string>public.app-category.productivity</string>
<key>NSHumanReadableCopyright</key><string>© 2026 kushhoficial. MIT License.</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PLIST
IDENTITY="${SIGN_IDENTITY:--}"
if [[ "$IDENTITY" == "-" ]]; then
  codesign --force --options runtime --timestamp=none --sign - "$APP"
else
  codesign --force --options runtime --timestamp --sign "$IDENTITY" "$APP"
fi
codesign --verify --strict "$APP"
printf 'Build universal: %s\n' "$APP"
