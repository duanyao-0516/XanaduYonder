#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if [[ ! -x "$DOTNET" ]]; then DOTNET="$(command -v dotnet || true)"; fi
if [[ ! -x "$DOTNET" ]]; then printf 'Install .NET 10 SDK or set DOTNET_ROOT.\n' >&2; exit 1; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
exec "$DOTNET" run --project "$PROJECT_ROOT/Tests/GomokuSession/GomokuSessionTests.csproj" --configuration Release -- "$@"
