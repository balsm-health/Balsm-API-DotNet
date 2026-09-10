#!/usr/bin/env bash
# Regenerate the per-module + aggregate OpenAPI documents from the running API.
#
# Usage: ./scripts/generate-openapi.sh
#
# Boots Balsm.API in the background against an in-memory SQLite DB, waits for
# readiness, curls each /openapi/v1/{doc}.json into docs/api/openapi/v1/, then
# shuts the host down. Safe to run repeatedly; output files are overwritten.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$REPO_ROOT/docs/api/openapi/v1"
API_PROJECT="$REPO_ROOT/src/Balsm.API"
PORT="${OPENAPI_PORT:-5550}"
BASE_URL="http://127.0.0.1:${PORT}"

DOCUMENTS=(identity entity inventory pos customer prescription caredirectory supervisor all)

mkdir -p "$OUT_DIR"

echo "==> Building Balsm.API"
dotnet build "$REPO_ROOT/Balsm.API.slnx" -c Release --nologo --verbosity quiet

DB_FILE="$(mktemp -t balsm-openapi.XXXXXX.db)"
LOG_FILE="$(mktemp)"

cleanup() {
    if [ -n "${PID:-}" ] && kill -0 "$PID" 2>/dev/null; then
        kill "$PID" 2>/dev/null || true
        wait "$PID" 2>/dev/null || true
    fi
    rm -f "$DB_FILE" "$LOG_FILE"
}
trap cleanup EXIT

echo "==> Starting host on :$PORT (file SQLite at $DB_FILE)"
# The host wires JWT bearer auth at startup and its handler throws on the first
# request when Jwt:Secret is unset — which failed every /openapi fetch, silently,
# leaving the committed specs stale. Doc generation authenticates nothing, so an
# ephemeral key is generated per run: never written to disk, never committed.
JWT_SECRET="${Jwt__Secret:-$(openssl rand -base64 48)}"

env \
  ASPNETCORE_ENVIRONMENT=Production \
  Jwt__Secret="$JWT_SECRET" \
  DeploymentMode=Cloud \
  BALSM_GENERATE_OPENAPI=true \
  Server__Urls="http://0.0.0.0:${PORT}" \
  Database__Provider=Sqlite \
  Database__ConnectionString="Data Source=${DB_FILE}" \
  Serilog__MinimumLevel__Default=Information \
  dotnet run --project "$API_PROJECT" -c Release --no-build --no-launch-profile \
  > "$LOG_FILE" 2>&1 &
PID=$!

echo "==> Waiting for readiness"
for attempt in $(seq 1 60); do
    if curl -fsS "${BASE_URL}/openapi/v1/all.json" -o /dev/null 2>/dev/null; then
        echo "    ready after ${attempt}s"
        break
    fi
    if ! kill -0 "$PID" 2>/dev/null; then
        echo "ERROR: host exited before becoming ready"
        cat "$LOG_FILE"
        exit 1
    fi
    sleep 1
    if [ "$attempt" = "60" ]; then
        echo "ERROR: host did not become ready within 60s"
        cat "$LOG_FILE"
        exit 1
    fi
done

echo "==> Capturing documents"
for doc in "${DOCUMENTS[@]}"; do
    target="${OUT_DIR}/${doc}.json"
    url="${BASE_URL}/openapi/v1/${doc}.json"
    if curl -fsS "$url" -o "${target}.tmp"; then
        # Pretty-print with stable key ordering for diff-friendly output.
        if command -v python3 >/dev/null 2>&1; then
            python3 -c "import json,sys; d=json.load(open(sys.argv[1])); json.dump(d, open(sys.argv[2],'w'), indent=2, sort_keys=True); open(sys.argv[2],'a').write('\n')" "${target}.tmp" "$target"
            rm -f "${target}.tmp"
        else
            mv "${target}.tmp" "$target"
        fi
        echo "    ${doc}.json ($(wc -c < "$target" | tr -d ' ') bytes)"
    else
        echo "    SKIP ${doc}.json (endpoint returned non-2xx)"
        rm -f "${target}.tmp"
    fi
done

echo "==> Done. Output in ${OUT_DIR}"
