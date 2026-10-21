"""Verify recorded mapped-module hashes and MVIDs against each exact archived build."""
from pathlib import Path
import datetime
import hashlib
import json
import sys

ROOT = Path('/home/administrator/projects/hexalith/folders')
PACKET = Path(__file__).resolve().parent.parent
LANES = {
    'capacity-live-attempt-4.log': ('parent-source-binding-build5', 'capacity-build5-snapshot/archived-bytes.json'),
    'capacity-live-attempt-5.log': ('parent-source-binding-build6', 'final-build6-byte-archive.json'),
    'real-provider-live-attempt-1.log': ('parent-source-binding-build7', 'final-build7-byte-archive.json'),
    'real-provider-live-attempt-2.log': ('parent-source-binding-build7', 'final-build7-byte-archive.json'),
    'real-provider-live-attempt-3.log': ('parent-source-binding-build7', 'final-build7-byte-archive.json'),
}
result = {'command': [sys.executable, str(Path(__file__).resolve())],
          'observed_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'scope': 'Mapped module observations from failed/partial executions; identity verification is not an acceptance pass.',
          'lanes': {}, 'errors': []}
for name, (binding_dir, manifest_path) in LANES.items():
    bindings = json.loads((PACKET / binding_dir / 'all-pdb-documents.json').read_text())
    manifest = json.loads((PACKET / manifest_path).read_text())
    by_pe = {row['pe']: row for row in bindings}
    lane = {'observations': 0, 'managed_process_ids': [], 'launcher_process_ids': [], 'replacements': {}, 'errors': []}
    processes, launchers, by_resource = set(), set(), {}
    for line in (PACKET / name).read_text().splitlines():
        marker = 'RUNTIME_BINDING '
        if marker not in line:
            continue
        try:
            row, _ = json.JSONDecoder().raw_decode(line.split(marker, 1)[1])
        except json.JSONDecodeError:
            continue
        lane['observations'] += 1
        processes.add(row['processId'])
        launchers.add(row['launcherProcessId'])
        by_resource.setdefault(row['resource'], set()).add(row['processId'])
        bound = by_pe.get(row['path'])
        if bound is None:
            lane['errors'].append({'path': row['path'], 'error': 'Mapped module absent from exact build PDB binding'})
            continue
        checks = {'pe_hash': row['sha256'].lower() == bound['peSha256'].lower(),
                  'pdb_hash': row['pdbSha256'].lower() == bound['pdbSha256'].lower(),
                  'mvid': row['mvid'].lower() == bound['mvid'].lower(),
                  'pe_pdb_identity': bound['identityMatches']}
        for path, digest in [(row['path'], row['sha256']), (row['pdbPath'], row['pdbSha256'])]:
            relative = str(Path(path).relative_to(ROOT))
            entry = manifest.get(relative)
            if entry is None:
                checks[relative] = False
                continue
            archive = PACKET / entry['archive']
            checks[relative] = hashlib.sha256(archive.read_bytes()).hexdigest() == digest.lower() == entry['sha256'].lower()
        if not all(checks.values()):
            lane['errors'].append({'path': row['path'], 'checks': checks})
    lane['managed_process_ids'] = sorted(processes)
    lane['launcher_process_ids'] = sorted(launchers)
    lane['replacements'] = {key: sorted(value) for key, value in sorted(by_resource.items())}
    lane['passed'] = not lane['errors']
    result['lanes'][name] = lane
    result['errors'].extend({'lane': name, **error} for error in lane['errors'])
result['passed'] = not result['errors']
(PACKET / 'parent-runtime-binding-audit.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'passed': result['passed'], 'errors': result['errors'],
                  'observations': {key: value['observations'] for key, value in result['lanes'].items()}}, indent=2))
sys.exit(0 if result['passed'] else 1)
