#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
  DOTNET_COMMAND="$DOTNET_ROOT/dotnet"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
  DOTNET_COMMAND="$HOME/.dotnet/dotnet"
elif command -v dotnet >/dev/null 2>&1; then
  DOTNET_COMMAND="$(command -v dotnet)"
else
  echo "找不到 .NET SDK。请安装 .NET 10 SDK，或设置 DOTNET_ROOT。" >&2
  exit 127
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
exec "$DOTNET_COMMAND" run --project "$PROJECT_ROOT/Tests/AircraftFirePersistence/AircraftFirePersistenceTests.csproj" --configuration Release -- "$@"
