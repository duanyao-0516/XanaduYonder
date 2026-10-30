#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if [[ ! -x "$DOTNET" ]]; then DOTNET="$(command -v dotnet || true)"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec "$DOTNET" run --project "$PROJECT_ROOT/Tests/AgeOfWarHost/AgeOfWarHostTests.csproj" --configuration Release
