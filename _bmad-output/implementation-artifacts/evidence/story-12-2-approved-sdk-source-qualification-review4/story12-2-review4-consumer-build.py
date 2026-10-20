from pathlib import Path
import json, subprocess, time

root = Path('/home/administrator/projects/hexalith/folders')
records = []
for name, command in (
    ('restore', ['dotnet', 'restore', 'Hexalith.Folders.slnx', '-p:Configuration=Debug', '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false']),
    ('build', ['dotnet', 'build', 'Hexalith.Folders.slnx', '-c', 'Debug', '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false', '--no-restore', '-m:1']),
):
    path = Path('/tmp/story12-2-sdk-review4-consumer-' + name + '.log')
    assert not path.exists(), str(path) + ' would overwrite an earlier attempt'
    start = time.time()
    with path.open('w') as log:
        result = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT)
    records.append(dict(id=name, command=command, exit_code=result.returncode, seconds=time.time()-start, log=str(path)))
    Path('/tmp/story12-2-sdk-review4-consumer-builds.json').write_text(json.dumps(records, indent=2) + '\n')
    print(json.dumps(records[-1]), flush=True)
    assert result.returncode == 0, path
    if name == 'build':
        assert '0 Warning(s)' in path.read_text() and '0 Error(s)' in path.read_text()

