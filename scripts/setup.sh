#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
CATAN_TOOLS_ROOT="${CATAN_TOOLS_ROOT:-/workspace/.tools}"
export DOTNET_CLI_HOME="$CATAN_TOOLS_ROOT"
export NUGET_PACKAGES="$CATAN_TOOLS_ROOT/nuget"
export PATH="$CATAN_TOOLS_ROOT/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
sdk_version=10.0.100
mkdir -p "$CATAN_TOOLS_ROOT/dotnet" "$NUGET_PACKAGES"
if [[ "${CATAN_REFRESH_SDK:-0}" == 1 ]] || ! "$CATAN_TOOLS_ROOT/dotnet/dotnet" --list-sdks 2>/dev/null | awk '{print $1}' | rg -qx "$sdk_version"; then
    archive_dir=$(mktemp -d /tmp/catan-sdk.XXXXXX)
    trap 'rm -rf "$archive_dir"' EXIT
    curl --fail --silent --show-error --location --retry 2 \
        https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o "$archive_dir/releases.json"
    python3 - "$archive_dir" "$sdk_version" <<'PY'
import json, pathlib, sys
root = pathlib.Path(sys.argv[1])
metadata = json.loads((root / 'releases.json').read_text())
for release in metadata['releases']:
    for sdk in [release.get('sdk', {})] + release.get('sdks', []):
        if sdk.get('version') != sys.argv[2]:
            continue
        artifact = next(f for f in sdk['files'] if f['rid'] == 'linux-x64' and f['name'].endswith('.tar.gz'))
        assert artifact['url'].startswith('https://builds.dotnet.microsoft.com/')
        assert len(artifact['hash']) == 128
        (root / 'url').write_text(artifact['url'])
        (root / 'checksum').write_text(artifact['hash'] + '  ' + str(root / 'sdk.tar.gz') + '\n')
        sys.exit(0)
raise SystemExit('Pinned SDK missing from official release metadata')
PY
    curl --fail --silent --show-error --location --retry 2 "$(cat "$archive_dir/url")" -o "$archive_dir/sdk.tar.gz"
    sha512sum --check "$archive_dir/checksum"
    tar -xzf "$archive_dir/sdk.tar.gz" -C "$CATAN_TOOLS_ROOT/dotnet"
fi
dotnet --version
dotnet restore Game.sln --locked-mode
dotnet build Game.sln --no-restore --configuration Release
