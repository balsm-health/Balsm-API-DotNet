#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-.env.staging}"

# Every key docker-compose.yml interpolates, in file order. Missing keys are
# topped up on an existing file — an early exit here is what let a long-lived
# .env.staging drift behind compose and silently interpolate empty config.
plain_keys=(
  COMPOSE_PROJECT_NAME APP_DOMAIN APP_PORT TRAEFIK_NETWORK TRAEFIK_CERTRESOLVER
  BALSM_INTERNAL_SUBNET POSTGRES_DB POSTGRES_USER JWT_ISSUER JWT_AUDIENCE
  GOOGLE_CLIENT_ID APPLE_CLIENT_ID RESEND_API_KEY RESEND_FROM
  OTP_LINK_BASE_URL OTP_APP_LINK_SCHEME OTP_WEB_APP_URL
  SENTRY_DSN SENTRY_TRACES_SAMPLE_RATE SENTRY_PROFILES_SAMPLE_RATE
)

# Generated once, at file creation. Never invented during a top-up: a fresh
# POSTGRES_PASSWORD would not match the already-initialised postgres volume, and
# rotating JWT/OTP/DOB/recovery secrets would invalidate live tokens and data.
secret_keys=(POSTGRES_PASSWORD JWT_SECRET OTP_HMAC_SECRET DOB_ENCRYPTION_KEY RECOVERY_SECRET)

default_for() {
  case "$1" in
    COMPOSE_PROJECT_NAME)        printf 'balsm-api-stg' ;;
    APP_DOMAIN)                  printf 'api-stg-balsm-health.mosalam.com' ;;
    APP_PORT)                    printf '8080' ;;
    TRAEFIK_NETWORK)             printf 'proxy' ;;
    TRAEFIK_CERTRESOLVER)        printf 'le' ;;
    BALSM_INTERNAL_SUBNET)       printf '10.250.240.0/24' ;;
    POSTGRES_DB)                 printf 'balsm_stg' ;;
    POSTGRES_USER)               printf 'balsm' ;;
    JWT_ISSUER)                  printf 'balsm' ;;
    JWT_AUDIENCE)                printf 'balsm-app' ;;
    RESEND_FROM)                 printf 'Balsm <noreply@balsm.health>' ;;
    OTP_LINK_BASE_URL)           printf 'https://api-stg-balsm-health.mosalam.com' ;;
    OTP_APP_LINK_SCHEME)         printf 'balsm' ;;
    SENTRY_TRACES_SAMPLE_RATE)   printf '0.1' ;;
    SENTRY_PROFILES_SAMPLE_RATE) printf '0.0' ;;
    # Credentials and endpoints supplied per-environment by the deploy secrets.
    GOOGLE_CLIENT_ID|APPLE_CLIENT_ID|RESEND_API_KEY|OTP_WEB_APP_URL|SENTRY_DSN) printf '' ;;
    POSTGRES_PASSWORD) openssl rand -base64 36 | tr -d '\n' | tr '+/' 'AB' | cut -c1-40 ;;
    JWT_SECRET|OTP_HMAC_SECRET|RECOVERY_SECRET) openssl rand -hex 48 ;;
    DOB_ENCRYPTION_KEY) openssl rand -base64 32 | tr -d '\n' ;;
    *) echo "no default for $1" >&2; return 1 ;;
  esac
}

creating=false
if [[ ! -f "$env_file" ]]; then
  creating=true
  : > "$env_file"
  chmod 600 "$env_file"
fi

added=()
missing_secrets=()

for key in "${secret_keys[@]}"; do
  grep -qE "^${key}=" "$env_file" && continue
  if [[ "$creating" == true ]]; then
    printf '%s=%s\n' "$key" "$(default_for "$key")" >> "$env_file"
    added+=("$key")
  else
    missing_secrets+=("$key")
  fi
done

for key in "${plain_keys[@]}"; do
  grep -qE "^${key}=" "$env_file" && continue
  printf '%s=%s\n' "$key" "$(default_for "$key")" >> "$env_file"
  added+=("$key")
done

chmod 600 "$env_file"

if (( ${#missing_secrets[@]} )); then
  echo "ERROR: $env_file is missing generated secrets: ${missing_secrets[*]}" >&2
  echo "Refusing to generate them: new values would break the existing postgres volume and invalidate live tokens." >&2
  echo "Add them by hand, or delete the file and the postgres volume to start clean." >&2
  exit 1
fi

if [[ "$creating" == true ]]; then
  echo "Created $env_file"
elif (( ${#added[@]} )); then
  echo "Topped up $env_file with: ${added[*]}"
else
  echo "Using existing $env_file (all keys present)"
fi
