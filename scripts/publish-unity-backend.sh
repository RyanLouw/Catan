#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
rid="${1:-linux-x64}"
case "$rid" in linux-x64|win-x64|osx-x64|osx-arm64) ;; *) echo "Use linux-x64, win-x64, osx-x64 or osx-arm64" >&2; exit 1 ;; esac
CATAN_TOOLS_ROOT="${CATAN_TOOLS_ROOT:-/workspace/.tools}"
export PATH="$CATAN_TOOLS_ROOT/dotnet:$PATH"
export DOTNET_CLI_HOME="$CATAN_TOOLS_ROOT" NUGET_PACKAGES="$CATAN_TOOLS_ROOT/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet publish src/Game.LocalHost -c Release -r "$rid" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:NuGetLockFilePath=packages.publish.lock.json \
    -o "unity/Catan.Client/Assets/StreamingAssets/Backend/$rid"
