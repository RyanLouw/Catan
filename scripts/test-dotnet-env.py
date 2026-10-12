#!/usr/bin/env python3
"""Regression for Actions/local shells: do not replace their SDK cache paths with /workspace."""
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as temp:
    sdk = Path(temp) / 'dotnet'
    sdk.write_text('#!/bin/sh\nexit 0\n')
    sdk.chmod(0o755)
    env = {**os.environ, 'PATH': temp + os.pathsep + os.environ['PATH'],
           'DOTNET_CLI_HOME': temp + '/cli', 'NUGET_PACKAGES': temp + '/packages'}
    env.pop('CATAN_TOOLS_ROOT', None)
    subprocess.run(['bash', '-c', 'set -eu; source scripts/dotnet-env.sh; test "$DOTNET_CLI_HOME" = "$EXPECTED_CLI"; test "$NUGET_PACKAGES" = "$EXPECTED_PACKAGES"; test -z "${CATAN_TOOLS_ROOT:-}"'],
                   cwd=root, env={**env, 'EXPECTED_CLI': env['DOTNET_CLI_HOME'], 'EXPECTED_PACKAGES': env['NUGET_PACKAGES']}, check=True)
    env.pop('DOTNET_CLI_HOME'); env.pop('NUGET_PACKAGES')
    subprocess.run(['bash', '-c', 'set -eu; source scripts/dotnet-env.sh; test -z "${DOTNET_CLI_HOME:-}"; test -z "${NUGET_PACKAGES:-}"; test -z "${CATAN_TOOLS_ROOT:-}"'], cwd=root, env=env, check=True)
print('SDK environment regression passed: installed SDK respected; caller cache paths preserved; no cloud paths injected.')
