from pathlib import Path
import datetime
import hashlib
import json
import re
import sys
import time

root = Path('/home/administrator/projects/hexalith/folders')
out = Path(__file__).parent
attempt = sys.argv[1]
log = out / f'real-provider-live-attempt-{attempt}.log'
receipt = out / f'real-provider-attempt-{attempt}-proof-process-bindings.json'
freeze = json.loads((out / 'source-freeze.json').read_text())
bound = {str((root / row['pe']).resolve()): row for row in json.loads(
    (out / 'parent-source-binding-build7/all-pdb-documents.json').read_text())}
expected_host = root / 'src/Hexalith.Folders.Workers/bin/Debug/net10.0/Hexalith.Folders.Workers.dll'

def descendants(pid):
    pending = [pid]
    seen = set()
    while pending:
        current = pending.pop()
        if current in seen:
            continue
        seen.add(current)
        yield current
        try:
            pending.extend(map(int, Path(f'/proc/{current}/task/{current}/children').read_text().split()))
        except (OSError, ValueError):
            pass

deadline = time.monotonic() + 1800
while time.monotonic() < deadline:
    text = log.read_text() if log.exists() else ''
    match = re.search(r'PASS ordinary-write processes: proof-a=(\d+); proof-b=(\d+)', text)
    if match:
        break
    if log.with_name(f'real-provider-live-attempt-{attempt}-result.json').exists():
        receipt.write_text(json.dumps({'status': 'proof-substep-not-reached', 'attempt': attempt}, indent=2) + '\n')
        print('Proof substep was not reached', flush=True)
        raise SystemExit(0)
    time.sleep(0.5)
else:
    raise TimeoutError('No proof launcher witness appeared within observer budget')

rows = []
for resource, launcher in zip(('folders-workers-proof-a', 'folders-workers-proof-b'), map(int, match.groups())):
    observed = False
    for pid in descendants(launcher):
        try:
            paths = sorted({line[line.index('/'):].strip() for line in Path(f'/proc/{pid}/maps').read_text().splitlines()
                if '/' in line and line.rstrip().endswith('.dll') and '/Hexalith.' in line})
        except OSError:
            continue
        if str(expected_host) not in paths:
            continue
        observed = True
        for path in paths:
            pe = Path(path)
            relative = str(pe.relative_to(root))
            sha = hashlib.sha256(pe.read_bytes()).hexdigest()
            assert freeze['assemblies_sha256'][relative] == sha, relative
            metadata = bound[str(pe)]
            assert metadata['peSha256'].lower() == sha
            pdb = pe.with_suffix('.pdb')
            pdb_sha = hashlib.sha256(pdb.read_bytes()).hexdigest()
            assert freeze['assemblies_sha256'][str(pdb.relative_to(root))] == pdb_sha
            assert metadata['pdbSha256'].lower() == pdb_sha
            rows.append(dict(resource=resource, launcher_pid=launcher, observed_managed_host_pid=pid,
                mapped_path=str(pe), sha256=sha, mvid=metadata['mvid'], pdb_path=str(pdb), pdb_sha256=pdb_sha))
    assert observed, resource
receipt.write_text(json.dumps(dict(status='actual-owned-descendant-mappings-bound', attempt=attempt,
    observed_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    provenance='DCP launcher witness in retained test output; only those two descendant trees inspected; actual expected Worker module mapping required.',
    graph='frozen build7; runtime files checked against original freeze and parent PE/PDB audit', rows=rows), indent=2) + '\n')
print(json.dumps({'observed_proof_modules': len(rows), 'managed_pids': sorted({r['observed_managed_host_pid'] for r in rows})}), flush=True)
