from pathlib import Path
import datetime
import hashlib
import json
import shutil

OUT = Path(__file__).resolve().parent
ROOT = Path('/home/administrator/projects/hexalith/folders')
PRIOR = ROOT / '_bmad-output/implementation-artifacts/evidence/story-12-2-strict-continuation-20261022T100829Z'
original = json.loads((PRIOR / 'extended-source-runtime-freeze.json').read_text())
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
copies = []
for name, expected in original['assemblies_sha256'].items():
    target = Path(name)
    if sha(target) == expected or target.name.startswith('Hexalith.Folders.AppHost.Tests.'):
        continue
    assert 'tests/Hexalith.Folders.AppHost.Tests/bin/' in name, name
    candidates = [Path(source) for source, digest in original['assemblies_sha256'].items()
                  if digest == expected and Path(source).name == target.name and source != name]
    source = next((source for source in candidates if source.is_file() and sha(source) == expected), None)
    assert source is not None, name
    before = sha(target)
    shutil.copy2(source, target)
    copies.append(dict(source=str(source), target=name, before_sha256=before,
                       selected_sha256=sha(target), expected_sha256=expected))

drift = []
for name, expected in original['assemblies_sha256'].items():
    target = Path(name)
    if sha(target) != expected:
        drift.append(dict(path=name, original_sha256=expected, actual_sha256=sha(target)))
assert len(drift) == 2 and all(Path(row['path']).name.startswith('Hexalith.Folders.AppHost.Tests.') for row in drift)
freeze = {group: {name: sha(Path(name)) for name in files} for group, files in original.items()}
manifest = ROOT / 'tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.deps.json'
deps = json.loads(manifest.read_text())
assert all(key in next(iter(deps['targets'].values())) for key in ('Hexalith.Commons.UniqueIds/1.0.0', 'ByteAether.Ulid/1.4.2'))
freeze['runtime_manifests_sha256'] = {str(manifest): sha(manifest)}
(OUT / 'executed-freeze.json').write_text(json.dumps(freeze, indent=2) + '\n')
(OUT / 'selected-runtime.json').write_text(json.dumps(dict(
    utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), copies=copies,
    remaining_binary_delta=drift, production_and_sdk_binary_drift=0,
    source_note='Seven pre-existing external SDK source deltas remain outside the selected historical runtime identity. Only the fixture test assembly was rebuilt; no corrected SDK runtime or current-external-source qualification is claimed.',
    runtime_manifest_sha256=sha(manifest), required_runtime_assets_present=True), indent=2) + '\n')
print(dict(restored_runner_copies=len(copies), remaining_fixture_binary_delta=len(drift), production_and_sdk_binary_drift=0))
