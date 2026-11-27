#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-.env.staging}"
# Which environment's defaults to seed: staging (default) or production.
deploy_env="${BALSM_ENV:-staging}"
case "$deploy_env" in
  staging|production) ;;
  *) echo "BALSM_ENV must be staging or production, got: $deploy_env" >&2; exit 1 ;;
esac

# Host naming. Every host is the production name with -<suffix> appended to its
# first label: api-stg.balsm.health, app-stg.balsm.health. An empty suffix is
# production. HOST_SUFFIX overrides the environment's own (stg for staging).
if [[ "$deploy_env" == production ]]; then host_suffix=''; else host_suffix="${HOST_SUFFIX:-stg}"; fi
if [[ -n "$host_suffix" && ! "$host_suffix" =~ ^[a-z0-9]+$ ]]; then
  echo "HOST_SUFFIX must be lowercase letters and digits, got: $host_suffix" >&2; exit 1
fi
balsm_host() { printf '%s%s.balsm.health' "$1" "${host_suffix:+-$host_suffix}"; }

# Every key docker-compose.yml interpolates, in file order. Missing keys are
# topped up on an existing file — an early exit here is what let a long-lived
# .env.staging drift behind compose and silently interpolate empty config.
plain_keys=(
  COMPOSE_PROJECT_NAME ASPNETCORE_ENVIRONMENT APP_DOMAIN APP_DOMAIN_LEGACY APP_PORT TRAEFIK_NETWORK
  TRAEFIK_ROUTER TRAEFIK_CERTRESOLVER
  BALSM_INTERNAL_SUBNET POSTGRES_DB POSTGRES_USER JWT_ISSUER JWT_AUDIENCE
  GOOGLE_CLIENT_ID APPLE_CLIENT_ID RESEND_API_KEY RESEND_FROM
  OTP_LINK_BASE_URL OTP_APP_LINK_SCHEME OTP_WEB_APP_URL CORS_ALLOWED_ORIGINS
  SENTRY_DSN SENTRY_TRACES_SAMPLE_RATE SENTRY_PROFILES_SAMPLE_RATE
)

# Generated once, at file creation. Never invented during a top-up: a fresh
# POSTGRES_PASSWORD would not match the already-initialised postgres volume, and
# rotating JWT/OTP/DOB/recovery secrets would invalidate live tokens and data.
secret_keys=(POSTGRES_PASSWORD JWT_SECRET OTP_HMAC_SECRET DOB_ENCRYPTION_KEY RECOVERY_SECRET)

# Secrets added after env files already existed in the field. Safe to mint on a
# top-up: while the key was absent the service refused to encrypt, so no
# ciphertext under an older key can exist.
late_secret_keys=(CARE_TEAM_ENCRYPTION_KEY)

default_for() {
  case "$1" in
    APP_DOMAIN)           balsm_host api; return ;;
    OTP_LINK_BASE_URL)    printf 'https://%s' "$(balsm_host api)"; return ;;
    CORS_ALLOWED_ORIGINS) printf 'https://%s' "$(balsm_host app)"; return ;;
  esac
  if [[ "$deploy_env" == production ]]; then
    case "$1" in
      COMPOSE_PROJECT_NAME)   printf 'balsm-api-prod'; return ;;
      ASPNETCORE_ENVIRONMENT) printf 'Production'; return ;;
      TRAEFIK_ROUTER)         printf 'balsm-api-prod'; return ;;
      # api.balsm.health is behind the Cloudflare proxy, so HTTP-01 never
      # reaches Traefik; the cloudflare resolver issues via DNS-01 instead.
      TRAEFIK_CERTRESOLVER)   printf 'cloudflare'; return ;;
      BALSM_INTERNAL_SUBNET)  printf '10.250.243.0/24'; return ;;
      POSTGRES_DB)            printf 'balsm_prod'; return ;;
    esac
  fi
  case "$1" in
    COMPOSE_PROJECT_NAME)        printf 'balsm-api-stg' ;;
    ASPNETCORE_ENVIRONMENT)      printf 'Staging' ;;
    APP_DOMAIN_LEGACY)           printf '' ;;
    APP_PORT)                    printf '8080' ;;
    TRAEFIK_NETWORK)             printf 'proxy' ;;
    TRAEFIK_ROUTER)              printf 'balsm-api-stg' ;;
    TRAEFIK_CERTRESOLVER)        printf 'le' ;;
    BALSM_INTERNAL_SUBNET)       printf '10.250.240.0/24' ;;
    POSTGRES_DB)                 printf 'balsm_stg' ;;
    POSTGRES_USER)               printf 'balsm' ;;
    JWT_ISSUER)                  printf 'balsm' ;;
    JWT_AUDIENCE)                printf 'balsm-app' ;;
    RESEND_FROM)                 printf 'Balsm <noreply@balsm.health>' ;;
    OTP_APP_LINK_SCHEME)         printf 'balsm' ;;
    SENTRY_TRACES_SAMPLE_RATE)   printf '0.1' ;;
    SENTRY_PROFILES_SAMPLE_RATE) printf '0.0' ;;
    # Credentials and endpoints supplied per-environment by the deploy secrets.
    GOOGLE_CLIENT_ID|APPLE_CLIENT_ID|RESEND_API_KEY|OTP_WEB_APP_URL|SENTRY_DSN) printf '' ;;
    POSTGRES_PASSWORD) openssl rand -base64 36 | tr -d '\n' | tr '+/' 'AB' | cut -c1-40 ;;
    JWT_SECRET|OTP_HMAC_SECRET|RECOVERY_SECRET) openssl rand -hex 48 ;;
    DOB_ENCRYPTION_KEY|CARE_TEAM_ENCRYPTION_KEY) openssl rand -base64 32 | tr -d '\n' ;;
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

for key in "${late_secret_keys[@]}"; do
  grep -qE "^${key}=" "$env_file" && continue
  printf '%s=%s\n' "$key" "$(default_for "$key")" >> "$env_file"
  added+=("$key")
done

for key in "${plain_keys[@]}"; do
  grep -qE "^${key}=" "$env_file" && continue
  printf '%s=%s\n' "$key" "$(default_for "$key")" >> "$env_file"
  added+=("$key")
done

# A suffix named by the pipeline owns the host keys: they are rewritten in an
# existing file too, which is how a server moves to a renamed host. Without
# one (production, or a hand run) the file's own values stand.
if [[ -n "${HOST_SUFFIX:-}" && "$deploy_env" != production ]]; then
  for key in APP_DOMAIN OTP_LINK_BASE_URL CORS_ALLOWED_ORIGINS; do
    want="$(default_for "$key")"
    grep -qxF "${key}=${want}" "$env_file" && continue
    grep -v "^${key}=" "$env_file" > "${env_file}.tmp" || true
    printf '%s=%s\n' "$key" "$want" >> "${env_file}.tmp"
    mv "${env_file}.tmp" "$env_file"
    added+=("$key")
  done
fi

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
