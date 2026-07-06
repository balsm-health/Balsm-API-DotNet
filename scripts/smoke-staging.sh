#!/usr/bin/env bash
set -uo pipefail

# Smoke tests for a deployed Balsm API instance.
# Usage: ./scripts/smoke-staging.sh [base_url]
#   base_url defaults to $BASE_URL or the staging deployment.

base_url="${1:-${BASE_URL:-https://api-stg-balsm-health.mosalam.com}}"
base_url="${base_url%/}"
timeout_s="${SMOKE_TIMEOUT:-15}"

pass=0
fail=0

check() {
  local name="$1" path="$2" expected_status="$3" expected_body="${4:-}"
  local url="${base_url}${path}"
  local body status
  body="$(curl -fsS -m "$timeout_s" -o - -w '\n%{http_code}' "$url" 2>/dev/null)" || body=$'\n000'
  status="${body##*$'\n'}"
  body="${body%$'\n'*}"

  if [[ "$status" != "$expected_status" ]]; then
    echo "FAIL  $name — $path expected HTTP $expected_status, got $status"
    fail=$((fail + 1))
    return
  fi
  if [[ -n "$expected_body" && "$body" != *"$expected_body"* ]]; then
    echo "FAIL  $name — $path body missing '$expected_body'"
    fail=$((fail + 1))
    return
  fi
  echo "PASS  $name"
  pass=$((pass + 1))
}

check_unauthorized() {
  local name="$1" path="$2"
  local url="${base_url}${path}"
  local status
  status="$(curl -s -m "$timeout_s" -o /dev/null -w '%{http_code}' "$url" 2>/dev/null)" || status=000
  if [[ "$status" == "401" || "$status" == "403" ]]; then
    echo "PASS  $name"
    pass=$((pass + 1))
  else
    echo "FAIL  $name — $path expected HTTP 401/403, got $status"
    fail=$((fail + 1))
  fi
}

echo "Smoke tests against ${base_url}"
echo

check "platform status (/status)"           "/status"         200 '"status":"operational"'
check "platform health (/api/v1/health)"    "/api/v1/health"  200 '"ready":true'

for slug in auth account deletion disclosure emergency-qr sessions \
            identity entity inventory pos customer prescription; do
  check "module health (${slug})" "/${slug}/health" 200 '"status":"Healthy"'
done

check_unauthorized "auth guard rejects anonymous (/sessions)" "/sessions"

echo
echo "${pass} passed, ${fail} failed"
[[ "$fail" -eq 0 ]]
