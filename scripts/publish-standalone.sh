#!/usr/bin/env bash
set -euo pipefail

# Publishes Balsm.API (with embedded Supervisor module) as a self-contained
# single-file executable for Standalone (self-hosted) deployment.
#
# Usage: ./publish-standalone.sh [platform ...] [--version X.Y.Z]
# Examples:
#   ./publish-standalone.sh                          # all platforms
#   ./publish-standalone.sh osx-arm64                # macOS only
#   ./publish-standalone.sh --version 1.0.0          # all platforms, version 1.0.0

PROJECT="src/Balsm.API/Balsm.API.csproj"
CONFIG="Release"
OUTPUT_BASE="artifacts"
ALL_RIDS=("osx-arm64" "linux-x64" "win-x64")
GREEN='\033[1;32m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROPS_FILE="$SCRIPT_DIR/../Directory.Build.props"

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

echo "=== Balsm Standalone Bundle Publisher ==="
echo "Version:   $VERSION"
echo "Platforms: ${RIDS[*]}"
echo ""

COMMON_ARGS=(
    -c "$CONFIG"
    --self-contained true
    -p:PublishSingleFile=true
    -p:IncludeNativeLibrariesForSelfExtract=true
    -p:DebugType=none
    -p:DebugSymbols=false
    -p:Version="$VERSION"
)

for rid in "${RIDS[@]}"; do
    out="$OUTPUT_BASE/standalone/$rid"
    rm -rf "$out"

    echo "▸ Publishing for $rid → $out"
    dotnet publish "$PROJECT" -r "$rid" -o "$out" "${COMMON_ARGS[@]}"
    echo -e "  ${GREEN}✓ Done${NC}"
    echo ""
done

echo "=== Creating distributable archives ==="
echo ""

DIST="$OUTPUT_BASE/dist"
mkdir -p "$DIST"

for rid in "${RIDS[@]}"; do
    src="$OUTPUT_BASE/standalone/$rid"
    archive_name="balsm-standalone-${VERSION}-${rid}"

    echo "▸ Packaging $rid"

    if [[ "$rid" == win-* ]]; then
        (cd "$src" && zip -q -r "../../$DIST/${archive_name}.zip" .)
        echo -e "  → ${GREEN}$DIST/${archive_name}.zip${NC}"
    else
        tar -czf "$DIST/${archive_name}.tar.gz" -C "$src" .
        echo -e "  → ${GREEN}$DIST/${archive_name}.tar.gz${NC}"
    fi
done

echo ""
echo "=== Generating checksums ==="
if command -v sha256sum &>/dev/null; then
    (cd "$DIST" && sha256sum balsm-standalone-* > checksums-standalone-sha256.txt)
else
    (cd "$DIST" && shasum -a 256 balsm-standalone-* > checksums-standalone-sha256.txt)
fi
cat "$DIST/checksums-standalone-sha256.txt"

echo ""
echo "=== Standalone bundles ready ==="
ls -lh "$DIST"/balsm-standalone-* 2>/dev/null || true
