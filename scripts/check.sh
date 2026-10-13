#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
source scripts/dotnet-env.sh
python3 scripts/test-dotnet-env.py
dotnet restore Game.sln --locked-mode
dotnet build Game.sln --configuration Release --no-restore
dotnet test Game.sln --configuration Release --no-build --no-restore
dotnet run --project src/Game.Console --configuration Release --no-build -- simulate "${1:-1000}" "${2:-1}"

# Packaging is opt-in, after gameplay checks succeed; this never deploys anything.
if [[ "${CATAN_CHECK_PACKAGE:-0}" == 1 ]]; then
    bash scripts/publish-unity-backend.sh linux-x64
    dotnet restore Game.sln --locked-mode
    python3 scripts/test-offline-host.py
fi
