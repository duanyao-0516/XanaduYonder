#!/bin/bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$dotnet" run --project "$repo/Tests/AgeOfWarOpening/AgeOfWarOpeningTests.csproj" -c Release
