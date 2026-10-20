from pathlib import Path
import base64, hashlib, json, subprocess, xml.etree.ElementTree as E

root = Path('/home/administrator/projects/hexalith/folders')
sdk = root / 'references/Hexalith.EventStore'
p = sdk / '_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review4'

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
fresh_pdb = json.loads(Path('/tmp/story12-2-sdk-review4-parent-preseal-pdb.json').read_text())
for group in fresh_pdb:
    for name, digest in group['pdb_sha256'].items():
        assert sha(Path(name)) == digest, name
    assert all(not row['mismatches'] and not row['missing_sources'] for row in group['result'])
Path('/tmp/story12-2-sdk-review4-independent-pdb-inspection.json').write_text(json.dumps(dict(reused_unchanged_preseal=True, groups=fresh_pdb), indent=2) + '\n')
qualification = json.loads((p / 'qualification.json').read_text())
assert len(live_cases) == qualification['unique_live_cases'], (len(live_cases), qualification['unique_live_cases'])
out = dict(manifest_artifacts=len(checks), manifest_sha256=sha(p / 'SHA256SUMS'), source=source, commands=commands, unique_live_cases=len(live_cases), binary_groups=binaries, pdb=pdb, independent_pdb_inspection='story12-2-sdk-review4-independent-pdb-inspection.json', qualification=qualification)
Path('/tmp/story12-2-sdk-review4-independent-audit.json').write_text(json.dumps(out, indent=2) + '\n')

workers = json.loads((p / 'process-workers.json').read_text())
assert len(workers) == qualification['worker_invocations'] == 20
assert len({w['pid'] for w in workers}) == qualification['distinct_worker_pids'] == 20
for w in workers:
    a = next(E.parse(p / w['xml']).getroot().iter('assembly'))
    assert a.get('passed') == '1' and a.get('failed') == a.get('skipped') == '0'
pairs = []
for item in json.loads((p / 'raw-persistence-observations.json').read_text()):
    line = item['observation']; start = line.find('{')
    if start < 0: continue
    try: row = json.loads(line[start:])
    except ValueError: continue
    kind = row.get('observation')
    if kind == 'cleaned-after-distinct-recovery-process-exit':
        assert row['originalPid'] != row['resumedPid']
        epoch = json.loads(base64.b64decode(row['epoch_base64']))
        assert epoch['phase'] == 0 and epoch['operationId'] == 'fresh'
        assert not row['scratch_redis_exists'] and not row['manifest_redis_exists']
        pairs.append(dict(kind='production-named', raw=row, decoded_epoch=epoch))
    elif kind == 'review4-exited-installer':
        assert row['installer_pid'] != row['recovery_pid']
        assert row['installer_exited_before_recovery'] and row['exact_open_preserved'] and row['private_absent'] and row['fresh_epoch_cycle']
        pairs.append(dict(kind='primitive-late-installer', raw=row))
assert len(pairs) == qualification['exited_worker_pairs'] == 10
assert sum(x['kind'] == 'production-named' for x in pairs) == qualification['production_named_pairs'] == 6
assert sum(x['kind'] == 'primitive-late-installer' for x in pairs) == qualification['primitive_late_installer_pairs'] == 4
for namespace in json.loads((p / 'cleanup-summary.json').read_text())['namespaces']:
    assert namespace['remaining_keys'] == 0 and not namespace['sidecar_active']
for old in json.loads((p / 'historical-packets-unchanged.json').read_text()):
    oldp=p.parent / old['packet']
    for line in (oldp/'SHA256SUMS').read_text().splitlines():
        digest, name=line.split(None,1)
        assert sha(oldp/name.lstrip('*')) == digest
Path('/tmp/story12-2-sdk-review4-independent-worker-audit.json').write_text(json.dumps(dict(workers=workers, pairs=pairs), indent=2) + '\n')
print(json.dumps(dict(manifest_artifacts=len(checks),manifest_sha256=out['manifest_sha256'],source=source,unique_live_cases=len(live_cases),commands=commands,workers=len(workers),pairs=len(pairs)),indent=2))
