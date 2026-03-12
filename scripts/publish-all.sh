#!/usr/bin/env bash
set -euo pipefail

PROJECT="src/Balsam.API/Balsam.API.csproj"
CONFIG="Release"
OUTPUT_BASE="artifacts"
RIDS=("osx-arm64" "linux-x64" "win-x64")
VERSION="${1:-0.1.0}"

echo "=== Balsam API — Executable Bundle Publisher ==="
echo "Version: $VERSION"
echo ""

rm -rf "$OUTPUT_BASE"

for rid in "${RIDS[@]}"; do
    out="$OUTPUT_BASE/$rid"
    echo "▸ Publishing for $rid → $out"
    dotnet publish "$PROJECT" \
        -c "$CONFIG" \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -p:Version="$VERSION" \
        -o "$out"
    echo "  ✓ Done"
    echo ""
done

echo "=== Creating distributable archives ==="
echo ""

DIST="$OUTPUT_BASE/dist"
mkdir -p "$DIST"

for rid in "${RIDS[@]}"; do
    src="$OUTPUT_BASE/$rid"
    archive_name="balsam-api-${VERSION}-${rid}"

    echo "▸ Packaging $rid"

    if [[ "$rid" == win-* ]]; then
        (cd "$src" && zip -q -r "../../$DIST/${archive_name}.zip" .)
        echo "  → $DIST/${archive_name}.zip"
    else
        tar -czf "$DIST/${archive_name}.tar.gz" -C "$src" .
        echo "  → $DIST/${archive_name}.tar.gz"
    fi
done

echo ""
echo "=== Generating checksums ==="
if command -v sha256sum &>/dev/null; then
    (cd "$DIST" && sha256sum balsam-api-* > checksums-sha256.txt)
else
    (cd "$DIST" && shasum -a 256 balsam-api-* > checksums-sha256.txt)
fi
cat "$DIST/checksums-sha256.txt"

echo ""
echo "=== Published executables ==="
ls -lh "$OUTPUT_BASE"/*/Balsam.API* 2>/dev/null || true

echo ""
echo "=== Distributable archives ==="
ls -lh "$DIST"/ 2>/dev/null || true
