#!/usr/bin/env bash
set -euo pipefail

CONFIG="${1:-Release}"
dotnet build --configuration "$CONFIG"
