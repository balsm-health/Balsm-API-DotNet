#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.0}"
BINDING="${2:-local}"
PKG_NAME="balsm-api"
PKG_DIR="$(mktemp -d)/${PKG_NAME}_${VERSION}_amd64"

echo "=== Building .deb package ==="
echo "Version: $VERSION | Binding: $BINDING"

# Create deb directory structure
mkdir -p "$PKG_DIR/DEBIAN"
mkdir -p "$PKG_DIR/opt/balsm/api"
mkdir -p "$PKG_DIR/etc/systemd/system"

# Copy application files
cp artifacts/linux-x64/Balsm.API "$PKG_DIR/opt/balsm/api/"
cp artifacts/linux-x64/appsettings.json "$PKG_DIR/opt/balsm/api/" 2>/dev/null || true

# Copy systemd unit
cp packaging/linux/balsm-api.service "$PKG_DIR/etc/systemd/system/"

# Copy and template debian control files
cp packaging/linux/debian/control "$PKG_DIR/DEBIAN/"
sed -i "s/__VERSION__/$VERSION/" "$PKG_DIR/DEBIAN/control"

cp packaging/linux/debian/conffiles "$PKG_DIR/DEBIAN/"
cp packaging/linux/debian/postinst "$PKG_DIR/DEBIAN/"
cp packaging/linux/debian/prerm "$PKG_DIR/DEBIAN/"
cp packaging/linux/debian/postrm "$PKG_DIR/DEBIAN/"
chmod 755 "$PKG_DIR/DEBIAN/postinst" "$PKG_DIR/DEBIAN/prerm" "$PKG_DIR/DEBIAN/postrm"
chmod 755 "$PKG_DIR/opt/balsm/api/Balsm.API"

# Write appsettings.Production.json
if [ "$BINDING" = "public" ]; then
    URLS="http://0.0.0.0:5000"
else
    URLS="http://localhost:5000"
fi
cat > "$PKG_DIR/opt/balsm/api/appsettings.Production.json" << EOF
{
  "Server": { "Urls": "$URLS" },
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=balsm.db"
  }
}
EOF

# Build .deb
mkdir -p artifacts/dist
dpkg-deb --build "$PKG_DIR" "artifacts/dist/${PKG_NAME}_${VERSION}_amd64.deb"

echo -e "✓ Created \033[1;32martifacts/dist/${PKG_NAME}_${VERSION}_amd64.deb\033[0m"
