#!/usr/bin/env bash
# Kill any running Balsm.API server instance.
#
# Targets:
#   1. Whatever is LISTENing on the server ports (HTTP + HTTPS admin panel).
#   2. Processes whose command line names the Balsm.API host (dotnet DLL or
#      native single-file executable) — backstop for non-default ports.
#
# Sends SIGTERM first (graceful), then SIGKILL to anything still alive.
# Does NOT use `set -e`: kills are best-effort and "no match" is not an error.
set -uo pipefail

# Default ports: HTTP (configPort) and HTTPS admin (configPort + 1).
# Override by passing ports as args, e.g. `kill-server.sh 5000 5001`.
read -r -a PORTS <<< "${*:-5050 5051}"

declare -a TARGETS=()

add_target() {
  local pid="$1"
  [ -z "$pid" ] && return
  [ "$pid" = "$$" ] && return
  for existing in "${TARGETS[@]:-}"; do
    [ "$existing" = "$pid" ] && return
  done
  TARGETS+=("$pid")
}

# Collect PIDs listening on the server ports.
for p in "${PORTS[@]}"; do
  while read -r pid; do
    add_target "$pid"
  done < <(lsof -nP -iTCP:"$p" -sTCP:LISTEN -t 2>/dev/null || true)
done

# Collect PIDs by process name (skip build/msbuild invocations).
while read -r pid; do
  [ -z "$pid" ] && continue
  cmd="$(ps -p "$pid" -o command= 2>/dev/null || true)"
  case "$cmd" in
    *Balsm.API.dll*|*/Balsm.API|*/Balsm.API\ *|*Balsm.API.csproj*)
      case "$cmd" in
        *" build "*|*"msbuild"*) continue ;;
      esac
      add_target "$pid"
      ;;
  esac
done < <(pgrep -f "Balsm.API" 2>/dev/null || true)

if [ "${#TARGETS[@]}" -eq 0 ]; then
  echo "No running Balsm.API instance found."
  exit 0
fi

# Graceful stop.
for pid in "${TARGETS[@]}"; do
  cmd="$(ps -p "$pid" -o command= 2>/dev/null | cut -c1-60 || true)"
  kill "$pid" 2>/dev/null && echo "  SIGTERM  PID $pid  ${cmd}"
done

# Give them a moment, then force-kill survivors.
sleep 2
for pid in "${TARGETS[@]}"; do
  if kill -0 "$pid" 2>/dev/null; then
    kill -9 "$pid" 2>/dev/null && echo "  SIGKILL  PID $pid  (did not exit)"
  fi
done

echo "Balsm.API server stopped (${#TARGETS[@]} process(es))."
