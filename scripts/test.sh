#!/usr/bin/env bash
set -euo pipefail

dotnet test --configuration Release --verbosity normal
