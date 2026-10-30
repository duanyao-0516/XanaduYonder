#!/usr/bin/env bash
set -euo pipefail
AOW_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
AOW_DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if [[ ! -x "$AOW_DOTNET" ]]; then AOW_DOTNET="$(command -v dotnet || true)"; fi
if [[ ! -x "$AOW_DOTNET" ]]; then printf 'Install .NET 10 SDK or set DOTNET_ROOT.\n' >&2; exit 1; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec "$AOW_DOTNET" run --project "$AOW_ROOT/Tests/AgeOfWarParity/Two/AgeOfWarTwoParity.csproj" --configuration Release -- "$@"
