#!/usr/bin/env bash
set -euo pipefail

env_file="${1:-.env.staging}"

if [[ -f "$env_file" ]]; then
  echo "Using existing $env_file"
  exit 0
fi

postgres_password="$(openssl rand -base64 36 | tr -d '\n' | tr '+/' 'AB' | cut -c1-40)"
jwt_secret="$(openssl rand -hex 48)"
otp_secret="$(openssl rand -hex 48)"
dob_key="$(openssl rand -base64 32 | tr -d '\n')"
recovery_secret="$(openssl rand -hex 48)"

cat <<EOF > "$env_file"
COMPOSE_PROJECT_NAME=balsm-api-stg
APP_DOMAIN=api-stg-balsm-health.mosalam.com
APP_PORT=8080
TRAEFIK_NETWORK=proxy
TRAEFIK_CERTRESOLVER=le
BALSM_INTERNAL_SUBNET=10.250.240.0/24

POSTGRES_DB=balsm_stg
POSTGRES_USER=balsm
POSTGRES_PASSWORD=${postgres_password}

JWT_SECRET=${jwt_secret}
JWT_ISSUER=balsm
JWT_AUDIENCE=balsm-app
OTP_HMAC_SECRET=${otp_secret}
DOB_ENCRYPTION_KEY=${dob_key}
RECOVERY_SECRET=${recovery_secret}

GOOGLE_CLIENT_ID=
APPLE_CLIENT_ID=
RESEND_API_KEY=
SENTRY_DSN=
SENTRY_TRACES_SAMPLE_RATE=0.1
SENTRY_PROFILES_SAMPLE_RATE=0.0
EOF

chmod 600 "$env_file"
echo "Created $env_file"
