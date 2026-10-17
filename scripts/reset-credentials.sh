#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$SCRIPT_DIR/.."

CREDENTIAL_FILES=(
    "$ROOT/src/Balsm.API/bin/Debug/net10.0/admin-credentials.json"
    "$ROOT/src/Balsm.API/bin/Release/net10.0/admin-credentials.json"
)

deleted=0
for f in "${CREDENTIAL_FILES[@]}"; do
    if [[ -f "$f" ]]; then
        rm "$f"
        echo "Deleted: $f"
        deleted=$((deleted + 1))
    fi
done

if [[ $deleted -eq 0 ]]; then
    echo "No credential files found — already clean."
else
    echo "Done. Server will prompt for setup on next run."
fi
