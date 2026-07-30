#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${ENV_FILE:-$repo_root/.env.staging}"
compose_file="${COMPOSE_FILE:-$repo_root/docker-compose.yml}"

cd "$repo_root"

bash "$repo_root/scripts/bootstrap-staging-env.sh" "$env_file"

# CI / secret overrides: any of these present in the environment (e.g. injected
# from a GitHub Actions secret over SSH) are upserted into the compose env file,
# so secret-backed values reach `docker compose --env-file` without ever living
# in the repo. Unset/empty vars leave the existing file value untouched.
for var in RESEND_API_KEY RESEND_FROM OTP_LINK_BASE_URL OTP_APP_LINK_SCHEME OTP_WEB_APP_URL; do
  val="${!var:-}"
  [[ -z "$val" ]] && continue
  grep -v "^${var}=" "$env_file" > "${env_file}.tmp" 2>/dev/null || true
  printf '%s=%s\n' "$var" "$val" >> "${env_file}.tmp"
  mv "${env_file}.tmp" "$env_file"
done
chmod 600 "$env_file"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required" >&2
  exit 1
fi

if ! docker network inspect "$(grep '^TRAEFIK_NETWORK=' "$env_file" | cut -d= -f2-)" >/dev/null 2>&1; then
  echo "Traefik network from $env_file does not exist" >&2
  exit 1
fi

docker compose --env-file "$env_file" -f "$compose_file" up -d --build --remove-orphans

app_domain="$(grep '^APP_DOMAIN=' "$env_file" | cut -d= -f2-)"

for attempt in $(seq 1 60); do
  health="$(curl -fsS --resolve "${app_domain}:443:127.0.0.1" "https://${app_domain}/api/v1/health" || true)"
  if [[ "$health" == *'"ready":true'* ]]; then
    echo "$health"
    exit 0
  fi
  sleep 5
done

docker compose --env-file "$env_file" -f "$compose_file" ps
docker compose --env-file "$env_file" -f "$compose_file" logs --tail=200 balsm-api balsm-postgres
echo "Deployment verification failed" >&2
exit 1
