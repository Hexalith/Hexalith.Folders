from pathlib import Path
import base64, hashlib, json, subprocess, xml.etree.ElementTree as E

root = Path('/home/administrator/projects/hexalith/folders')
sdk = root / 'references/Hexalith.EventStore'
p = sdk / '_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review3'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

checks = []
for line in (p / 'SHA256SUMS').read_text().splitlines():
    h, name = line.split(None, 1)
    name = name.lstrip('*')
    assert (p / name).is_file(), name
    assert sha(p / name) == h, name
    checks.append(name)
allfiles = {str(f.relative_to(p)) for f in p.rglob('*') if f.is_file() and f.name != 'SHA256SUMS'}
assert allfiles == set(checks), (allfiles - set(checks), set(checks) - allfiles)
source = []
for name in ('source-freeze.json', 'source-final.json'):
    data = json.loads((p / name).read_text())
    mismatches = [x['path'] for x in data if not (sdk / x['path']).exists() or sha(sdk / x['path']) != x['sha256']]
    source.append(dict(inventory=name, files=len(data), mismatches=mismatches))
assert source[-1]['mismatches'] == [], source[-1]
commands = []
accepted_groups = set()
live_cases = set()
for c in json.loads((p / 'commands.json').read_text()):
    if c.get('binary_group'):
        accepted_groups.add(c['binary_group'])
    if c.get('log_sha256'):
        assert sha(p / c['log']) == c['log_sha256'], c['id']
    if c.get('xml'):
        assert sha(p / c['xml']) == c['xml_sha256'], c['id']
        assemblies = list(E.parse(p / c['xml']).getroot().iter('assembly'))
        counts = {k: sum(int(x.get(k, '0')) for x in assemblies) for k in ('total', 'passed', 'failed', 'skipped', 'errors', 'not-run')}
        assert counts == c['result'], c['id']
        if c['id'].startswith('live-'):
            for test in E.parse(p / c['xml']).getroot().iter('test'):
                assert test.get('result') == 'Pass', (c['id'], test.get('name'))
                live_cases.add(test.get('name'))
        commands.append(dict(id=c['id'], exit_code=c['exit_code'], counts=counts))
assert all(x['exit_code'] == 0 and x['counts']['failed'] == 0 for x in commands)
assert all(x['counts']['skipped'] == (2 if x['id'] == 'client-full' else 0) for x in commands)
binaries = []
for f in sorted(p.glob('*binary-group.json')):
    data = json.loads(f.read_text())
    bad = [x['path'] for x in data if not (sdk / x['path']).exists() or sha(sdk / x['path']) != x['sha256']]
    binaries.append(dict(group=f.name, files=len(data), current_drift=bad))
assert not any(x['current_drift'] for x in binaries if x['group'] in accepted_groups), binaries
pdb = []
for f in sorted(p.glob('pdb-*.json')):
    data = json.loads(f.read_text())
    if not isinstance(data, list) or not data or 'pdb' not in data[0]:
        continue
    for x in data:
        assert not x['mismatches'] and not x['missing_sources'], f.name
        pdb.append(dict(record=f.name, **{k:v for k,v in x.items() if k != 'documents'}))
fresh_pdb = []
for project in ('Hexalith.EventStore.Client.Tests', 'Hexalith.EventStore.DomainService.Tests', 'Hexalith.EventStore.Server.LiveSidecar.Tests'):
    paths = sorted((sdk / 'tests' / project / 'bin/Debug/net10.0').glob('Hexalith.EventStore*.pdb'))
    assert paths, project
    command = ['dotnet', '/tmp/review1-pdb-verifier/bin/Debug/net10.0/Verifier.dll', *map(str, paths)]
    run = subprocess.run(command, cwd=sdk, text=True, capture_output=True)
    assert run.returncode == 0, run.stderr
    records = json.loads(run.stdout)
    assert all(not row['mismatches'] and not row['missing_sources'] for row in records), project
    fresh_pdb.append(dict(project=project, command=command, result=records))
Path('/tmp/story12-2-sdk-review3-independent-pdb-inspection.json').write_text(json.dumps(fresh_pdb, indent=2) + '\n')
qualification = json.loads((p / 'qualification.json').read_text())
assert len(live_cases) == qualification['unique_live_cases_qualified'], (len(live_cases), qualification['unique_live_cases_qualified'])
out = dict(manifest_artifacts=len(checks), manifest_sha256=sha(p / 'SHA256SUMS'), source=source, commands=commands, unique_live_cases=len(live_cases), binary_groups=binaries, pdb=pdb, independent_pdb_inspection='story12-2-sdk-review3-independent-pdb-inspection.json', qualification=qualification)
Path('/tmp/story12-2-sdk-review3-independent-audit.json').write_text(json.dumps(out, indent=2) + '\n')

workers = []
for item in json.loads((p / 'final-process-pairs.json').read_text()):
    assert item['original_pid'] != item['recovery_pid'], item
    persist = json.loads((p / item['persist']).read_text())
    resume = json.loads((p / item['resume']).read_text())
    assert persist['exit_code'] == resume['exit_code'] == 0
    for record in (persist, resume):
        xml = (p / item['persist' if record is persist else 'resume']).with_suffix('.xml')
        a = next(E.parse(xml).getroot().iter('assembly'))
        assert a.get('passed') == '1' and a.get('failed') == a.get('skipped') == '0'
    observed = []
    for line in resume['stdout'].splitlines():
        start = line.find('{"observation":')
        if start < 0:
            continue
        row = json.loads(line[start:])
        if row['observation'] != 'worker-cleaned-before-ordinary-delivery':
            continue
        epoch = json.loads(base64.b64decode(row['epoch_base64']))
        session = json.loads(base64.b64decode(row['session_base64']))
        assert epoch['phase'] == 0 and session['phase'] == 4
        assert not row['scratch_exists'] and not row['manifest_exists'] and not row['original_marker_exists']
        observed.append(dict(raw=row, epoch=epoch, session=session))
    assert len(observed) == 1
    workers.append(dict(pair=item, decoded_end_state=observed[0]))
assert len(workers) == qualification['child_workers']['distinct_exited_owner_recovery_pairs']
Path('/tmp/story12-2-sdk-review3-independent-worker-audit.json').write_text(json.dumps(workers, indent=2) + '\n')
print(json.dumps({k:out[k] for k in ('manifest_artifacts', 'source', 'commands', 'binary_groups')}, indent=2))
print('Independently decoded distinct process pairs:', len(workers))
