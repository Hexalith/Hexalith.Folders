"""UNEXECUTED proposal: rebind only the original driver's two root identity fields.

All earlier artifacts stay immutable. Separate owner authorization is required for
the exact root-recording predicate below. The original SDK/containment/probe code
is copied unchanged, not patched or imported into this process.
"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path('/home/administrator/projects/hexalith/folders')
BASE = ROOT / '_bmad-output/implementation-artifacts'
PLAN_PATH = BASE / 'story-1-17-cli-4-38-loader-root-rebind-plan-2026-10-26.json'
def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest(data): return hashlib.sha256(data).hexdigest()
def sha(path):
    with Path(path).open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def same(path, binding):
    path = Path(path)
    assert path.is_file() and not path.is_symlink(), str(path)
    assert path.stat().st_size == binding['bytes'] and sha(path) == binding['sha256'], str(path)
def git(repo, *args): return subprocess.check_output(['git', '-C', str(ROOT / repo), *args])
def write(path, value): Path(path).write_text(json.dumps(value, indent=2) + '\n')
def snapshot(inputs):
    return {rel: dict(revision=git(rel, 'rev-parse', 'HEAD').decode().strip(),
        indexEntriesSha256=digest(git(rel, 'ls-files', '--stage')),
        status=git(rel, 'status', '--porcelain=v2', '--untracked-files=all').decode())
        for rel in inputs['repositories']}

parser = argparse.ArgumentParser()
parser.add_argument('--authorization', required=True, type=Path)
args = parser.parse_args()
expected_authorization = BASE / 'story-1-17-cli-4-38-loader-root-rebind-authorization-2026-10-26.json'
assert args.authorization.resolve() == expected_authorization
authorization_bytes = args.authorization.read_bytes()
authorization = json.loads(authorization_bytes)
assert authorization['authorized'] is True and authorization['decisionMaker'] == 'Jerome'
assert authorization['scope'] == 'story-1-17-cli-4-38-root-rebind-only'
assert authorization['planSha256'] == sha(PLAN_PATH)
plan = json.loads(PLAN_PATH.read_text())
assert authorization['decisionPacketReceiptSha256'] == sha(ROOT / plan['decisionPacketReceiptPath'])
bindings = authorization['rootRecordingBindings']
assert set(bindings) == set(plan['rootRecordingPaths'])
for name, binding in bindings.items(): same(ROOT / name, binding)
same(ROOT / plan['adapter']['path'], plan['adapter'])
assert authorization['adapterSha256'] == sha(Path(__file__)) == plan['adapter']['sha256']
same(ROOT / plan['originalPlan']['path'], plan['originalPlan'])
same(ROOT / plan['originalExecutionApproval']['path'], plan['originalExecutionApproval'])
original_plan = json.loads((ROOT / plan['originalPlan']['path']).read_text())
original_approval = json.loads((ROOT / plan['originalExecutionApproval']['path']).read_text())
assert original_approval['authorized'] is True and original_approval['decisionMaker'] == 'Jerome'
assert original_approval['scope'] == original_plan['scope']
assert original_approval['planSha256'] == plan['originalPlan']['sha256']
by_name = {Path(item['path']).name: item for item in original_plan['artifacts']}
for item in original_plan['artifacts'] + original_plan['supportingArtifacts']:
    same(ROOT / item['path'], item)
original_inputs = json.loads((ROOT / by_name['inputs.json']['path']).read_text())
initial = snapshot(original_inputs)
for rel, expected in original_inputs['repositories'].items():
    assert git(rel, 'diff', '--cached', '--raw') == b'', rel
    assert git(rel, 'diff', '--name-only') == b'', rel
    if rel != '.':
        assert initial[rel]['revision'] == expected['revision'], rel
        assert initial[rel]['indexEntriesSha256'] == expected['indexEntriesSha256'], rel
        assert initial[rel]['status'] == '', rel

baseline = plan['rootBaselineRevision']
current = initial['.']['revision']
assert subprocess.run(['git', '-C', str(ROOT), 'merge-base', '--is-ancestor', baseline, current]).returncode == 0
raw = git('.', 'diff', '--raw', '--no-abbrev', '--no-renames', '-z', baseline, current)
fields = raw.split(b'\0'); changes = []
allowed = set(plan['rootRecordingPaths']) | {str(expected_authorization.relative_to(ROOT))}
position = 0
while position < len(fields) and fields[position]:
    header = fields[position].decode(); path = fields[position + 1].decode(); position += 2
    parts = header.split()
    assert parts[0] == ':000000' and parts[1] == '100644' and parts[-1] == 'A', (header, path)
    assert path in allowed, path
    if path in bindings: same(ROOT / path, bindings[path])
    else: assert digest((ROOT / path).read_bytes()) == digest(authorization_bytes)
    changes.append(dict(rawHeader=header, path=path))
untracked = [p.decode() for p in git('.', 'ls-files', '--others', '--exclude-standard', '-z').split(b'\0') if p]
assert set(untracked) <= allowed, untracked
assert current == git('.', 'rev-parse', 'HEAD').decode().strip()
assert initial == snapshot(original_inputs), 'Repository drift before materialization.'
assert digest(args.authorization.read_bytes()) == digest(authorization_bytes)

# Metadata-only private adapter. The approved driver will allocate its own WORK.
adapter = Path(tempfile.mkdtemp(prefix='story117-cli438-root-rebind-20261026-'))
adapter.chmod(0o700)
for name in ['LoaderProbe.cs', 'execute_after_authorization.py']:
    source = ROOT / by_name[name]['path']; target = adapter / name
    target.write_bytes(source.read_bytes()); same(target, by_name[name])
rebound = json.loads(json.dumps(original_inputs))
rebound['repositories']['.'] = {key: initial['.'][key] for key in ['revision', 'indexEntriesSha256']}
control = json.loads(json.dumps(rebound)); control['repositories']['.'] = original_inputs['repositories']['.']
assert control == original_inputs, 'Only root revision and stage-entry hash may differ.'
write(adapter / 'inputs.json', rebound)
derived_authorization = dict(decisionMaker='Jerome', authorized=True, scope=original_plan['scope'],
    planSha256=plan['originalPlan']['sha256'],
    sha256={name: sha(adapter / name) for name in ['LoaderProbe.cs', 'execute_after_authorization.py', 'inputs.json']},
    repositoryState=initial,
    originalExecutionApproval=plan['originalExecutionApproval'],
    rootRebindApprovalSha256=digest(authorization_bytes), rootRebindPlanSha256=sha(PLAN_PATH),
    rootRebindDecision='Owner-approved recording-only descendant predicate; no technical input or execution-scope change.',
    currentTupleAccepted=False, publicationAuthorized=False, deploymentAuthorized=False, trackerTransitionAuthorized=False)
write(adapter / 'derived-authorization.json', derived_authorization)
(adapter / 'original-execution-approval.json').write_bytes((ROOT / plan['originalExecutionApproval']['path']).read_bytes())
(adapter / 'root-rebind-authorization.json').write_bytes(authorization_bytes)
write(adapter / 'root-rebind-audit.json', dict(atUtc=now(), baselineRevision=baseline, actualRootRevision=current,
    initialRepositoryState=initial, rootChanges=changes, untrackedPaths=untracked,
    sourceInputsSha256=by_name['inputs.json']['sha256'], reboundInputsSha256=sha(adapter / 'inputs.json'),
    onlyDerivedFields=['repositories["."].revision', 'repositories["."].indexEntriesSha256'],
    scope='Metadata adapter only; original SDK, compiler, runtime, first-use, isolation, process, stop and cleanup gates unchanged.'))
argv = ['python3', '-B',
    str(adapter / 'execute_after_authorization.py'), '--authorization', str(adapter / 'derived-authorization.json')]
record = dict(atUtc=now(), argv=argv, cwd=str(ROOT))
print(str(adapter), flush=True)
with (adapter / 'driver.stdout').open('wb') as stdout, (adapter / 'driver.stderr').open('wb') as stderr:
    result = subprocess.run(argv, cwd=ROOT, stdout=stdout, stderr=stderr)
record.update(endedAtUtc=now(), exitCode=result.returncode,
    stdoutSha256=sha(adapter / 'driver.stdout'), stderrSha256=sha(adapter / 'driver.stderr'))
write(adapter / 'driver.command.json', record)
write(adapter / 'repository-after.json', snapshot(original_inputs))
assert snapshot(original_inputs) == initial
# No retries, repairs, altered versions, cleanup or acceptance. Preserve all outputs.
raise SystemExit(result.returncode)
