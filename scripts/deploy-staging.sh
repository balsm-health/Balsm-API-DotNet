#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${ENV_FILE:-$repo_root/.env.staging}"
compose_file="${COMPOSE_FILE:-$repo_root/docker-compose.yml}"

cd "$repo_root"

# BALSM_ENV (staging|production) picks the bootstrap defaults; production
# runs this same flow through scripts/deploy-production.sh.
bash "$repo_root/scripts/bootstrap-staging-env.sh" "$env_file"

# CI / secret overrides: any of these present in the environment (e.g. injected
# from a GitHub Actions secret over SSH) are upserted into the compose env file,
# so secret-backed values reach `docker compose --env-file` without ever living
# in the repo. Unset/empty vars leave the existing file value untouched.
for var in APP_DOMAIN_LEGACY \
          RESEND_API_KEY RESEND_FROM OTP_LINK_BASE_URL OTP_APP_LINK_SCHEME OTP_WEB_APP_URL \
          GOOGLE_CLIENT_ID APPLE_CLIENT_ID \
          SENTRY_DSN SENTRY_TRACES_SAMPLE_RATE SENTRY_PROFILES_SAMPLE_RATE; do
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

# `up` has been seen to build a new image and leave the old container running
# ("Running", not "Recreated"). The health check below then passes against the
# old code and the deploy reports a success it did not have. So compare the
# image the container runs with the one just built, and replace the container
# when they differ.
compose() { docker compose --env-file "$env_file" -f "$compose_file" "$@"; }
want_image="$(docker image inspect --format '{{.Id}}' "$(compose config --images | grep -m1 'balsm-api' || true)" 2>/dev/null || true)"
container_id="$(compose ps -q balsm-api | head -1)"
running_image="$(docker inspect --format '{{.Image}}' "$container_id" 2>/dev/null || true)"
if [[ -n "$want_image" && -n "$running_image" && "$want_image" != "$running_image" ]]; then
  echo "balsm-api is still running the previous image — recreating it"
  compose up -d --no-deps --force-recreate balsm-api
  container_id="$(compose ps -q balsm-api | head -1)"
  running_image="$(docker inspect --format '{{.Image}}' "$container_id" 2>/dev/null || true)"
  if [[ "$want_image" != "$running_image" ]]; then
    echo "balsm-api is not running the image that was just built" >&2
    exit 1
  fi
fi

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
