#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
if [[ -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
    dotnet_command="$DOTNET_ROOT/dotnet"
elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
    dotnet_command="$HOME/.dotnet/dotnet"
else
    dotnet_command="$(command -v dotnet)" || { printf '%s\n' '.NET SDK is required.' >&2; exit 127; }
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
exec "$dotnet_command" run --project "$project_root/Tests/FeihualingStorage/FeihualingStorageTests.csproj" --configuration Release -- "$project_root/Assets/Resources/Feihualing/catalog.bytes"
