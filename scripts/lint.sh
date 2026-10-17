#!/usr/bin/env bash
set -euo pipefail

dotnet format --verify-no-changes --verbosity diagnostic
