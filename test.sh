#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p build
swiftc -swift-version 5 Sources/History.swift Sources/ImageClipboard.swift Tests/main.swift -framework AppKit -o build/HistoryTests
build/HistoryTests
swiftc -swift-version 5 Sources/History.swift Sources/ImageClipboard.swift Sources/Design.swift Sources/App.swift Tests/Integration/main.swift -o build/CaptureTests -framework AppKit -framework Carbon -framework ApplicationServices -framework ServiceManagement
COPILUME_DEMO=1 build/CaptureTests
