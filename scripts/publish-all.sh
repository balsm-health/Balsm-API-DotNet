#!/usr/bin/env bash
set -euo pipefail

PROJECT="src/Balsam.API/Balsam.API.csproj"
CONFIG="Release"
OUTPUT_BASE="artifacts"
ALL_RIDS=("osx-arm64" "linux-x64" "win-x64")

# Usage: ./publish-all.sh [platform ...] [--version X.Y.Z]
# If no platforms specified, all platforms are built.
# If no version specified, reads from Directory.Build.props.
# Examples:
#   ./publish-all.sh                          # all platforms, version from props
#   ./publish-all.sh osx-arm64                # macOS only
#   ./publish-all.sh win-x64 linux-x64        # Windows + Linux
#   ./publish-all.sh osx-arm64 --version 1.0  # macOS, version 1.0

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROPS_FILE="$SCRIPT_DIR/../Directory.Build.props"

# Read version from Directory.Build.props
DEFAULT_VERSION=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$PROPS_FILE" 2>/dev/null || echo "0.1.0")
VERSION="$DEFAULT_VERSION"
RIDS=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)
            VERSION="$2"
            shift 2
            ;;
        *)
            RIDS+=("$1")
            shift
            ;;
    esac
done

if [[ ${#RIDS[@]} -eq 0 ]]; then
    RIDS=("${ALL_RIDS[@]}")
fi

echo "=== Balsam API — Executable Bundle Publisher ==="
echo "Version:   $VERSION"
echo "Platforms: ${RIDS[*]}"
echo ""

for rid in "${RIDS[@]}"; do
    out="$OUTPUT_BASE/$rid"
    rm -rf "$out"
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
