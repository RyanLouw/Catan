#!/usr/bin/env bash
# Source this helper. Prefer the caller's installed SDK and preserve its caches.
if [[ -z "${CATAN_TOOLS_ROOT:-}" ]] && ! command -v dotnet >/dev/null 2>&1 && [[ -x /workspace/.tools/dotnet/dotnet ]]; then
    CATAN_TOOLS_ROOT=/workspace/.tools
fi
if [[ -n "${CATAN_TOOLS_ROOT:-}" ]]; then
    export PATH="$CATAN_TOOLS_ROOT/dotnet:$PATH"
    export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$CATAN_TOOLS_ROOT}"
    export NUGET_PACKAGES="${NUGET_PACKAGES:-$CATAN_TOOLS_ROOT/nuget}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if ! command -v dotnet >/dev/null 2>&1; then
    echo "Install .NET SDK 10.0.100 or set CATAN_TOOLS_ROOT to its local tools directory." >&2
    return 1
fi
