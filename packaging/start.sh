#!/usr/bin/env bash
set -euo pipefail

# Launches the Balsm Supervisor, which manages the API lifecycle.
# The Supervisor runs on port 5001 and the API on port 5000.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUPERVISOR="$SCRIPT_DIR/supervisor/Balsm.Supervisor"

if [ ! -f "$SUPERVISOR" ]; then
    echo "Error: Supervisor executable not found at $SUPERVISOR"
    echo "Make sure you extracted the complete Balsm Standalone bundle."
    exit 1
fi

chmod +x "$SUPERVISOR" 2>/dev/null || true
chmod +x "$SCRIPT_DIR/api/Balsm.API" 2>/dev/null || true

exec "$SUPERVISOR"
