#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.0}"
BINDING="${2:-local}"
STAGING="$(mktemp -d)"
INSTALL_ROOT="$STAGING/usr/local/balsm/api"

echo "=== Building macOS .pkg installer ==="
echo "Version: $VERSION | Binding: $BINDING"

mkdir -p "$INSTALL_ROOT"

# Copy application files
cp artifacts/osx-arm64/Balsm.API "$INSTALL_ROOT/"
cp artifacts/osx-arm64/appsettings.json "$INSTALL_ROOT/" 2>/dev/null || true
chmod +x "$INSTALL_ROOT/Balsm.API"

# Copy launchd plist
cp packaging/macos/com.balsm.api.plist "$INSTALL_ROOT/"

# Write appsettings.Production.json
if [ "$BINDING" = "public" ]; then
    URLS="http://0.0.0.0:5000"
else
    URLS="http://localhost:5000"
fi

cat > "$INSTALL_ROOT/appsettings.Production.json" << EOF
{
  "Server": { "Urls": "$URLS" },
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=balsm.db"
  }
}
EOF

# Make scripts executable
chmod +x packaging/macos/scripts/preinstall
chmod +x packaging/macos/scripts/postinstall

# Build .pkg
mkdir -p artifacts/dist
pkgbuild \
    --root "$STAGING" \
    --identifier com.balsm.api \
    --version "$VERSION" \
    --install-location / \
    --scripts packaging/macos/scripts \
    "artifacts/dist/balsm-api-${VERSION}-osx-arm64.pkg"

# Cleanup
rm -rf "$STAGING"

echo -e "✓ Created \033[1;32martifacts/dist/balsm-api-${VERSION}-osx-arm64.pkg\033[0m"
