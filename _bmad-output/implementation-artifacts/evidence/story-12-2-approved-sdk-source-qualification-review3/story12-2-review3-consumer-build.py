from pathlib import Path
import json, subprocess, time

root = Path('/home/administrator/projects/hexalith/folders')
records = []
for name, command in (
    ('restore', ['dotnet', 'restore', 'Hexalith.Folders.slnx', '-p:Configuration=Debug', '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false']),
    ('build', ['dotnet', 'build', 'Hexalith.Folders.slnx', '-c', 'Debug', '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false', '--no-restore', '-m:1']),
):
    path = Path('/tmp/story12-2-sdk-review3-consumer-' + name + '.log')
    assert not path.exists(), str(path) + ' would overwrite an earlier attempt'
    start = time.time()
    with path.open('w') as log:
        result = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT)
    records.append(dict(id=name, command=command, exit_code=result.returncode, seconds=time.time()-start, log=str(path)))
    Path('/tmp/story12-2-sdk-review3-consumer-builds.json').write_text(json.dumps(records, indent=2) + '\n')
    print(json.dumps(records[-1]), flush=True)
    assert result.returncode == 0, path
    if name == 'build':
        assert '0 Warning(s)' in path.read_text() and '0 Error(s)' in path.read_text()

old = root / '_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-20261020'
prior = json.loads((old / 'story12-2-sdk-review1-consumer-final-source.json').read_text())
include = json.loads(Path('/tmp/story12-2-sdk-review2-consumer-format-carry.json').read_text())['include_files']
import hashlib
rows = []
for item in include:
    name = item['path']
    h = hashlib.sha256((root / name).read_bytes()).hexdigest()
    assert h == prior['files_sha256'][name], name
    rows.append(dict(path=name, sha256=h, matches_previous_qualified_source=True))
Path('/tmp/story12-2-sdk-review3-consumer-format-carry.json').write_text(json.dumps(dict(include_files=rows, retained_qualification='story-12-2-approved-sdk-source-qualification-20261020/qualification.json', note='All seven included C# files match the exact previous format/analyzer-qualified source; current source build is recorded separately.'), indent=2) + '\n')
