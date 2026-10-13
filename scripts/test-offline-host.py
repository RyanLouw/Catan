#!/usr/bin/env python3
"""Required publication smoke test: runs the self-contained app without dotnet on PATH."""
import json
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parent.parent
exe = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else root / 'unity/Catan.Client/Assets/StreamingAssets/Backend/linux-x64/Game.LocalHost'
assert exe.is_file(), 'Publish the backend first'
with subprocess.Popen([str(exe)], cwd=root, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                      stderr=subprocess.PIPE, text=True, env={**os.environ, 'PATH': '/nonexistent'}) as host:
    def request(payload):
        host.stdin.write(json.dumps(payload) + '\n')
        host.stdin.flush()
        value = json.loads(host.stdout.readline())
        assert value['Id'] == payload['Id'], value
        return value
    try:
        created = request({'Id': 'create', 'Op': 'create', 'Viewer': 'A', 'Players': ['A', 'B'], 'Seed': 10})
        assert created['Success'] and created['View']['Phase'] == 'Lobby', created
        assert created['View']['Players'][1]['Resources'] is None, created
        invalid = request({'Id': 'invalid', 'Op': 'command', 'Viewer': 'A', 'Command': {'Kind': 'Roll', 'Player': 'A'}})
        assert not invalid['Success'], invalid
        assert invalid['View']['Version'] == created['View']['Version'], invalid
        for name in ['A', 'B']:
            ready = request({'Id': 'ready-' + name, 'Op': 'command', 'Viewer': name, 'Command': {'Kind': 'Ready', 'Player': name}})
            assert ready['Success'], ready
        started = request({'Id': 'start', 'Op': 'command', 'Viewer': 'A', 'Command': {'Kind': 'Start', 'Player': 'A'}})
        assert started['Success'] and started['View']['Phase'] == 'SetupSettlement', started
        assert len(started['View']['Hexes']) == 19 and len(started['View']['Edges']) == 72, started
        host.stdin.close()
        assert host.wait(timeout=5) == 0, host.stderr.read()
    finally:
        if host.poll() is None:
            host.kill()
print('Self-contained offline host passed: no SDK on PATH, privacy, rejection integrity, lobby and board generation.')
