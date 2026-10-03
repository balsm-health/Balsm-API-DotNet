#!/usr/bin/env bash
# Production deploy: the staging flow against .env.production and the
# production defaults (api.balsm.health, balsm-api-prod project and router).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export BALSM_ENV=production
export ENV_FILE="${ENV_FILE:-$repo_root/.env.production}"

exec bash "$repo_root/scripts/deploy-staging.sh"
