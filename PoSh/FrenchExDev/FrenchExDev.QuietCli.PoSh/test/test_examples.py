"""Integration checks in disposable repositories; one bounded JSON report."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--pwsh', required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
checks = []

def run(argv, cwd=None):
    return subprocess.run(argv, cwd=cwd, capture_output=True, text=True, timeout=30)

def native(argv, cwd):
    r = run(argv, cwd)
    if r.returncode:
        raise RuntimeError('Fixture setup failed')

def check(name, body):
    try:
        body()
        checks.append({'Name': name, 'Passed': True})
    except Exception as e:
        checks.append({'Name': name, 'Passed': False, 'Exception': type(e).__name__})

def inspect_result(r, expected='Success'):
    assert not r.stderr, 'Unexpected agent-facing stderr'
    assert len(r.stdout.strip().splitlines()) == 1
    assert len(r.stdout.strip().encode()) <= 2048
    obj = json.loads(r.stdout)
    assert obj['Outcome'] == expected
    return obj

with tempfile.TemporaryDirectory(prefix='quiet-cli-repo-') as temp:
    repo = Path(temp) / 'repo with spaces'
    repo.mkdir()
    native(['git', 'init', '--initial-branch=main'], repo)
    (repo / 'tracked.txt').write_text('initial\n')
    native(['git', 'add', '--', 'tracked.txt'], repo)
    native(['git', '-c', 'user.name=QuietCli Test', '-c', 'user.email=test@example.invalid',
            'commit', '-m', 'fixture'], repo)

    def clean_pwsh():
        r = run([a.pwsh, '-NoLogo', '-NoProfile', '-File',
                 str(root / 'examples/Generated-GitOverview.ps1'), '-Repository', str(repo)])
        o = inspect_result(r)
        assert o['Data']['Clean'] and o['Data']['Changed'] == 0 and o['Data']['Branch'] == 'main'
    check('PowerShell clean Git repository', clean_pwsh)

    native(['git', 'mv', '--', 'tracked.txt', 'renamed with spaces.txt'], repo)
    (repo / 'untracked é with newline\nname.txt').write_text('untracked\n')

    def changed_pwsh():
        r = run([a.pwsh, '-NoLogo', '-NoProfile', '-File',
                 str(root / 'examples/Generated-GitOverview.ps1'), '-Repository', str(repo)])
        o = inspect_result(r)
        assert o['Data']['Changed'] == 1 and o['Data']['Untracked'] == 1
        assert not o['Data']['Clean'] and o['Execution']['Steps'] == 2
    check('PowerShell Git rename and unusual untracked path', changed_pwsh)

    def changed_bash():
        r = run(['bash', str(root / 'examples/generated-git-status.sh'), str(repo)])
        o = inspect_result(r)
        assert o['Data']['Changed'] == 1 and o['Data']['Untracked'] == 1
    check('Bash reducer matches the selected Git counts', changed_bash)

    def invalid_bash():
        r = run(['bash', str(root / 'examples/generated-git-status.sh'), str(repo / 'missing')])
        o = inspect_result(r, 'Failed')
        assert r.returncode != 0 and o['Data'] is None and not o['Execution']['AnalysisComplete']
    check('Bash native failure emits one structured result', invalid_bash)

failed = sum(not c['Passed'] for c in checks)
print(json.dumps({'Passed': len(checks)-failed, 'Failed': failed, 'Checks': checks}, separators=(',', ':')))
raise SystemExit(bool(failed))
