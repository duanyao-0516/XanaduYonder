#!/usr/bin/env bash
set -eu
root="$(cd "$(dirname "$0")/../.." && pwd)"
device="${1:?pass a booted simulator UDID}"
output="${2:-$root/artifacts/validation/ios-dialogs}"
mkdir -p "$output"
app="$(mktemp -d)/DialogChecks.app"
mkdir "$app"
sdk="$(xcrun --sdk iphonesimulator --show-sdk-path)"
arch="$(uname -m)"
xcrun --sdk iphonesimulator clang++ -fobjc-arc -std=c++17 -isysroot "$sdk" -target "$arch-apple-ios15.0-simulator" \
    "$root/Tests/IosDialogs/DialogChecks.mm" "$root/Assets/Plugins/iOS/XanaduWebAssetServer.mm" \
    -framework UIKit -framework WebKit -framework AVFoundation -framework Foundation -framework QuartzCore -framework CoreGraphics -o "$app/DialogChecks"
bundle=com.xanaduyonder.validation.dialogs
cat > "$app/Info.plist" <<PLIST
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>$bundle</string>
<key>CFBundleExecutable</key><string>DialogChecks</string>
<key>CFBundleName</key><string>DialogChecks</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1</string>
<key>CFBundleVersion</key><string>1</string>
<key>UILaunchScreen</key><dict/>
</dict></plist>
PLIST
codesign -s - "$app"
xcrun simctl install "$device" "$app"
data="$(xcrun simctl get_app_container "$device" "$bundle" data)/Documents"
rm -f "$data/result.txt"
xcrun simctl launch --terminate-running-process "$device" "$bundle"
for i in {1..60}; do
    if test -s "$data/result.txt"; then
        cat "$data/result.txt"
        grep -qx PASS "$data/result.txt" || exit 1
        cp "$data/"*.png "$output/"
        exit 0
    fi
    sleep 1
done
echo "Timed out waiting for the UIKit dialog checks" >&2
exit 1
