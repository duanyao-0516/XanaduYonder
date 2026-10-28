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

# Use the exact Unity production assembly, including when this checkout is a worktree.
COMMON_GIT_DIR="$(git -C "$PROJECT_ROOT" rev-parse --path-format=absolute --git-common-dir)"
PRIMARY_ROOT="$(dirname "$COMMON_GIT_DIR")"
ZUMA_NEWTONSOFT_DLL="${ZUMA_NEWTONSOFT_DLL:-}"
if [[ -z "$ZUMA_NEWTONSOFT_DLL" ]]; then
  for ZUMA_PACKAGE_ROOT in "$PROJECT_ROOT" "$PRIMARY_ROOT"; do
    for ZUMA_DLL_CANDIDATE in "$ZUMA_PACKAGE_ROOT"/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll; do
      if [[ -f "$ZUMA_DLL_CANDIDATE" ]]; then
        ZUMA_NEWTONSOFT_DLL="$ZUMA_DLL_CANDIDATE"
        break 2
      fi
    done
  done
fi
if [[ ! -f "$ZUMA_NEWTONSOFT_DLL" ]]; then
  echo "找不到 Unity 生产 Newtonsoft.Json.dll；请先解析 Unity 包，或设置 ZUMA_NEWTONSOFT_DLL。" >&2
  exit 2
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
exec "$DOTNET_COMMAND" run --project "$PROJECT_ROOT/Tests/ZumaPersistence/ZumaPersistenceTests.csproj" --configuration Release -p:ZumaNewtonsoftDll="$ZUMA_NEWTONSOFT_DLL" -- "$@"
