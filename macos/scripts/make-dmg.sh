#!/bin/bash
# Builds Snapline.app and packs it into a zip and a disk image for releases.
# Usage: scripts/make-dmg.sh   (VERSION=x.y.z to name the files)
set -euo pipefail
cd "$(dirname "$0")/.."
bash scripts/build-app.sh release
VERSION="$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' build/Snapline.app/Contents/Info.plist)"
rm -f "build/Snapline-$VERSION-mac.zip" "build/Snapline-$VERSION.dmg"
ditto -c -k --keepParent build/Snapline.app "build/Snapline-$VERSION-mac.zip"
STAGE="$(mktemp -d)/Snapline"
mkdir -p "$STAGE"
cp -R build/Snapline.app "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -quiet -srcfolder "$STAGE" -volname "Snapline" -fs HFS+ -format UDZO "build/Snapline-$VERSION.dmg"
echo "Built build/Snapline-$VERSION-mac.zip and build/Snapline-$VERSION.dmg"
