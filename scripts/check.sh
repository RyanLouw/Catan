#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
CATAN_TOOLS_ROOT="${CATAN_TOOLS_ROOT:-/workspace/.tools}"
export PATH="$CATAN_TOOLS_ROOT/dotnet:$PATH"
export DOTNET_CLI_HOME="$CATAN_TOOLS_ROOT" NUGET_PACKAGES="$CATAN_TOOLS_ROOT/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet restore Game.sln --locked-mode
dotnet build Game.sln --configuration Release --no-restore
bash scripts/publish-unity-backend.sh linux-x64
# Regression: publication must preserve the normal development lockfiles.
dotnet restore Game.sln --locked-mode
python3 scripts/test-offline-host.py
dotnet test Game.sln --configuration Release --no-build --no-restore
dotnet run --project src/Game.Console --configuration Release --no-build -- simulate "${1:-1000}" "${2:-1}"
