"""Read-only checks of original evidence, archived source bytes and Git filtering."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

ROOT = Path('/home/administrator/projects/hexalith/folders')
PACKET = Path(__file__).resolve().parent.parent


def run(args, cwd=ROOT, data=None):
    return subprocess.check_output(args, cwd=cwd, input=data, text=True).strip()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


result = {
    'command': [sys.executable, str(Path(__file__).resolve()), *sys.argv[1:]],
    'observed_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
}
mode = sys.argv[1]
if mode == 'historical':
    frozen = json.loads((PACKET / 'parent-historical-freeze.json').read_text())
    drift = [
        {'path': path, 'expected': expected, 'actual': sha(ROOT / path)}
        for path, expected in frozen.items() if sha(ROOT / path) != expected
    ]
    result.update(count=len(frozen), drift=drift, passed=not drift)
elif mode == 'archived-source-filtering':
    paths = sorted(
        p for p in PACKET.rglob('*') if p.is_file()
        and (('embedded-sources' in p.parts) or ('current-generated-disk' in p.parts)
             or ('external-drift-original-inputs' in p.parts))
    )
    names = [str(p.relative_to(ROOT)) for p in paths]
    stdin = '\n'.join(names) + '\n'
    raw = run(['git', 'hash-object', '--no-filters', '--stdin-paths'], data=stdin).splitlines()
    filtered = run(['git', 'hash-object', '--stdin-paths'], data=stdin).splitlines()
    attrs = run(['git', 'check-attr', 'text', '--stdin'], data=stdin).splitlines()
    differences = [name for name, a, b in zip(names, raw, filtered) if a != b]
    attr_errors = [line for line in attrs if not line.endswith(': text: unset')]
    result.update(
        commands=[['git', 'hash-object', '--no-filters', '--stdin-paths'],
                  ['git', 'hash-object', '--stdin-paths'], ['git', 'check-attr', 'text', '--stdin']],
        count=len(names), crlf_count=sum(b'\r\n' in p.read_bytes() for p in paths),
        differences=differences, attribute_errors=attr_errors,
        files={name: {'rawGitObject': a, 'filteredGitObject': b} for name, a, b in zip(names, raw, filtered)},
        passed=not differences and not attr_errors and len(raw) == len(filtered) == len(names),
    )
elif mode in ('external-recovery', 'external-recovery-strict3'):
    freeze = json.loads((PACKET / 'source-freeze.json').read_text())
    drift = json.loads((PACKET / 'parent-external-drift-observation.json').read_text())
    if mode == 'external-recovery-strict3':
        latest = json.loads((PACKET / 'after-strict3-external-drift-audit.json').read_text())
        drift['input_drift'] = {
            path: {'expected': freeze['files_sha256'][path], 'actual': sha(ROOT / path)}
            for path in latest['files_sha256']
        }
    documents = json.loads((PACKET / 'parent-source-binding-build7/all-pdb-documents.json').read_text())
    by_path = {}
    for pe in documents:
        for doc in pe['documents']:
            by_path.setdefault(doc['path'], []).append({
                'pdb': pe['pdb'], 'expected': doc['expected'], 'algorithm': doc['checksumAlgorithm'],
            })
    snapshots, errors = [], []
    archive = PACKET / 'external-drift-original-inputs'
    archive.mkdir(exist_ok=True)
    for relative, delta in drift['input_drift'].items():
        module = max((m for m in freeze['submodules'] if relative.startswith(m + '/')), key=len)
        revision = freeze['submodules'][module]['head']
        candidates = [revision]
        if module == 'references/Hexalith.EventStore':
            # The first external commit recorded the original uncommitted prerequisite.
            candidates.append('4ba2d839906a3d71e189ac0abbc1439d0b35eadf')
        candidates.append(run(['git', 'rev-parse', 'HEAD'], cwd=ROOT / module))
        match = None
        for candidate in dict.fromkeys(candidates):
            command = ['git', 'show', candidate + ':' + relative[len(module) + 1:]]
            completed = subprocess.run(command, cwd=ROOT / module, capture_output=True)
            if completed.returncode:
                continue
            blob = completed.stdout
            lf = blob.replace(b'\r\n', b'\n')
            variants = {'Git blob': blob, 'LF worktree': lf, 'CRLF worktree': lf.replace(b'\n', b'\r\n')}
            match = next(((kind, raw) for kind, raw in variants.items()
                          if hashlib.sha256(raw).hexdigest() == delta['expected']), None)
            if match is not None:
                revision = candidate
                break
        if match is None:
            errors.append({'path': relative, 'error': 'No exact original frozen SHA-256 match'})
            continue
        kind, raw = match
        digest = hashlib.sha256(raw).hexdigest()
        destination = archive / digest
        if destination.exists():
            assert destination.read_bytes() == raw
        else:
            destination.write_bytes(raw)
        bindings = []
        for doc in by_path.get(str(ROOT / relative), []):
            checksum = (hashlib.sha1(raw).hexdigest() if doc['algorithm'] == 'ff1816ec-aa5e-4d10-87f7-6f4963833460'
                        else digest)
            bindings.append({**doc, 'matchesSnapshot': doc['expected'].lower() == checksum})
        if any(not b['matchesSnapshot'] for b in bindings):
            errors.append({'path': relative, 'error': 'Original snapshot differs from PDB checksum'})
        snapshots.append({'path': relative, 'command': command, 'cwd': str(ROOT / module),
                          'revision': revision,
                          'recoveryMode': kind, 'archive': str(destination.relative_to(PACKET)),
                          'sha256': digest, 'currentSha256': delta['actual'], 'pdbBindings': bindings})
    result.update(count=len(snapshots), compiled_paths=sum(bool(s['pdbBindings']) for s in snapshots),
                  pdb_document_copies=sum(len(s['pdbBindings']) for s in snapshots),
                  snapshots=snapshots, errors=errors, passed=not errors)
else:
    raise ValueError(mode)
destination = PACKET / ('parent-preservation-' + mode + '.json')
destination.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({key: value for key, value in result.items() if key not in ('files', 'snapshots')}, indent=2))
sys.exit(0 if result['passed'] else 1)
