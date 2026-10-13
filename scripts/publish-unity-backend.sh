#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
rid="${1:-linux-x64}"
case "$rid" in linux-x64|win-x64|osx-x64|osx-arm64) ;; *) echo "Use linux-x64, win-x64, osx-x64 or osx-arm64" >&2; exit 1 ;; esac
source scripts/dotnet-env.sh
dotnet publish src/Game.LocalHost -c Release -r "$rid" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:NuGetLockFilePath=packages.publish.lock.json \
    -o "unity/Catan.Client/Assets/StreamingAssets/Backend/$rid"
