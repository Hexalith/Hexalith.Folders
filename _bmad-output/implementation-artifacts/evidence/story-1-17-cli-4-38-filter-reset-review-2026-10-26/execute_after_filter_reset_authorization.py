"""UNEXECUTED proposal: one fresh attempt with the exact filter-offset repair.

All earlier artifacts stay immutable. Separate owner authorization is required for
the exact root-recording predicate below. The original probe is copied unchanged. Only the declared filter-offset lines
are changed in a private driver copy; SDK and containment policy is retained.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import signal
import stat
import subprocess
import sys
import tempfile

ROOT = Path('/home/administrator/projects/hexalith/folders')
BASE = ROOT / '_bmad-output/implementation-artifacts'
PLAN_PATH = BASE / 'story-1-17-cli-4-38-filter-reset-plan-2026-10-26.json'
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
def require_safe_python(optimized, isolated):
    if optimized != 0 or isolated != 1:
        raise RuntimeError('Use the bound interpreter with -I -B and optimization disabled.')
def validate_recording_state(changes, untracked, allowed):
    for change in changes:
        parts = change['rawHeader'].split()
        if len(parts) != 5 or parts[0] != ':000000' or parts[1] != '100644' or parts[-1] != 'A':
            raise ValueError('Only ordinary-file additions are authorized.')
        if change['path'] not in allowed:
            raise ValueError('Nonallowlisted root addition: ' + change['path'])
    if not set(untracked) <= set(allowed):
        raise ValueError('Nonallowlisted untracked root file.')
def reject_hidden_index_flags(output):
    for row in output.split(b'\0'):
        if row and (row[:1] == b'S' or row[:1].islower()):
            raise ValueError('assume-unchanged/skip-worktree index flags are forbidden.')
def verify_recording_bindings(root, bindings):
    for name, binding in bindings.items(): same(root / name, binding)
def claim_attempt(parent, key):
    if len(key) != 64 or any(c not in '0123456789abcdef' for c in key):
        raise ValueError('Expected SHA256 authorization key.')
    parent.mkdir(mode=0o700, exist_ok=True)
    mode = parent.lstat()
    if not stat.S_ISDIR(mode.st_mode) or mode.st_uid != os.getuid() or mode.st_mode & 0o077:
        raise ValueError('Unsafe attempt-claim directory.')
    claim = parent / key
    claim.mkdir(mode=0o700)  # Atomic exclusive claim; never removed automatically.
    return claim

require_safe_python(sys.flags.optimize, sys.flags.isolated)

parser = argparse.ArgumentParser()
parser.add_argument('--authorization', required=True, type=Path)
args = parser.parse_args()
expected_authorization = BASE / 'story-1-17-cli-4-38-filter-reset-authorization-2026-10-26.json'
assert args.authorization.resolve() == expected_authorization
authorization_bytes = args.authorization.read_bytes()
authorization = json.loads(authorization_bytes)
assert authorization['authorized'] is True and authorization['decisionMaker'] == 'Jerome'
assert authorization['scope'] == 'story-1-17-cli-4-38-filter-reset-one-fresh-attempt'
assert authorization['planSha256'] == sha(PLAN_PATH)
assert authorization['freshAttemptsAuthorized'] == 1
plan = json.loads(PLAN_PATH.read_text())
if str(Path(sys.executable).resolve()) != plan['pythonInterpreter']['path']:
    raise RuntimeError('Unexpected Python interpreter.')
same(Path(sys.executable).resolve(), plan['pythonInterpreter'])
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
same(ROOT / plan['priorRootRebindApproval']['path'], plan['priorRootRebindApproval'])
same(ROOT / plan['failedExecutionReceipt']['path'], plan['failedExecutionReceipt'])
same(ROOT / plan['driverRepair']['patch']['path'], plan['driverRepair']['patch'])
by_name = {Path(item['path']).name: item for item in original_plan['artifacts']}
for item in original_plan['artifacts'] + original_plan['supportingArtifacts']:
    same(ROOT / item['path'], item)
original_inputs = json.loads((ROOT / by_name['inputs.json']['path']).read_text())
initial = snapshot(original_inputs)
for rel, expected in original_inputs['repositories'].items():
    reject_hidden_index_flags(git(rel, 'ls-files', '-v', '-z'))
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
    if path in bindings: same(ROOT / path, bindings[path])
    else: assert digest((ROOT / path).read_bytes()) == digest(authorization_bytes)
    changes.append(dict(rawHeader=header, path=path))
untracked = [p.decode() for p in git('.', 'ls-files', '--others', '--exclude-standard', '-z').split(b'\0') if p]
validate_recording_state(changes, untracked, allowed)
assert current == git('.', 'rev-parse', 'HEAD').decode().strip()
assert initial == snapshot(original_inputs), 'Repository drift before materialization.'
assert digest(args.authorization.read_bytes()) == digest(authorization_bytes)

# Claim is retained even if materialization or a later gate fails.
claim = claim_attempt(Path(plan['attemptClaimDirectory']), digest(authorization_bytes))
write(claim / 'claim.json', dict(atUtc=now(), authorizationSha256=digest(authorization_bytes),
    planSha256=sha(PLAN_PATH), rootRevision=current, state='claimed-before-any-sdk', pid=os.getpid()))

# Private metadata adapter and declared repair; driver allocates its own WORK.
adapter = Path(tempfile.mkdtemp(prefix='story117-cli438-filter-reset-20261026-'))
adapter.chmod(0o700)
for name in ['LoaderProbe.cs', 'execute_after_authorization.py']:
    source = ROOT / by_name[name]['path']; target = adapter / name
    data = source.read_bytes()
    if name == 'execute_after_authorization.py':
        repair = plan['driverRepair']
        assert digest(data) == repair['beforeSha256']
        text = data.decode(); assert text.count(repair['anchor']) == 1
        data = text.replace(repair['anchor'], repair['replacement']).encode()
        assert digest(data) == repair['afterSha256'] and len(data) == repair['afterBytes']
        target.write_bytes(data)
    else:
        target.write_bytes(data); same(target, by_name[name])
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
    rootRebindApprovalSha256=plan['priorRootRebindApproval']['sha256'],
    freshAttemptPlanSha256=sha(PLAN_PATH),
    rootRebindDecision='Exact named-record predicate extended only to owner-bound repair/execution records; two root fields derived.',
    freshAttemptApprovalSha256=digest(authorization_bytes), driverRepair=plan['driverRepair'],
    priorFailedExecutionReceipt=plan['failedExecutionReceipt'],
    freshAttemptScope='Only the declared filter reset/audit lines in a private driver copy; same compiler/cases/gates and no retries.',
    currentTupleAccepted=False, publicationAuthorized=False, deploymentAuthorized=False, trackerTransitionAuthorized=False)
write(adapter / 'derived-authorization.json', derived_authorization)
(adapter / 'original-execution-approval.json').write_bytes((ROOT / plan['originalExecutionApproval']['path']).read_bytes())
(adapter / 'prior-root-rebind-approval.json').write_bytes((ROOT / plan['priorRootRebindApproval']['path']).read_bytes())
(adapter / 'fresh-attempt-authorization.json').write_bytes(authorization_bytes)
write(adapter / 'filter-reset-audit.json', dict(atUtc=now(), baselineRevision=baseline, actualRootRevision=current,
    initialRepositoryState=initial, rootChanges=changes, untrackedPaths=untracked,
    sourceInputsSha256=by_name['inputs.json']['sha256'], reboundInputsSha256=sha(adapter / 'inputs.json'),
    onlyDerivedFields=['repositories["."].revision', 'repositories["."].indexEntriesSha256'],
    driverRepair=plan['driverRepair'], actualPrivateDriverSha256=sha(adapter / 'execute_after_authorization.py'),
    freshAttemptsAuthorized=1,
    attemptClaim=str(claim),
    scope='Metadata adapter plus exact filter-offset repair in private copy; SDK/compiler/runtime, filter bytes, containment, first-use, deadlines and stop/cleanup gates retained.'))
argv = [sys.executable, '-I', '-B',
    str(adapter / 'execute_after_authorization.py'), '--authorization', str(adapter / 'derived-authorization.json')]
record = dict(atUtc=now(), argv=argv, cwd=str(ROOT), phase='before-launch',
    pythonInterpreter=plan['pythonInterpreter'], optimize=sys.flags.optimize, isolated=sys.flags.isolated,
    attemptClaim=str(claim), exitCode=None)
print(str(adapter), flush=True)
write(adapter / 'driver.command.json', record)
child = None
def interrupted(signum, frame):
    raise InterruptedError('Received signal ' + str(signum))
for signum in [signal.SIGTERM, signal.SIGINT]: signal.signal(signum, interrupted)
try:
    verify_recording_bindings(ROOT, bindings)
    assert digest(args.authorization.read_bytes()) == digest(authorization_bytes)
    assert snapshot(original_inputs) == initial
    with (adapter / 'driver.stdout').open('wb') as stdout, (adapter / 'driver.stderr').open('wb') as stderr:
        child = subprocess.Popen(argv, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr)
        record.update(phase='launched', driverPid=child.pid)
        write(adapter / 'driver.command.json', record)
        record['exitCode'] = child.wait()
except BaseException as error:
    record.update(error=repr(error), exitCode=1)
finally:
    if child is not None and child.poll() is None:
        child.terminate()
        try: child.wait(timeout=2)
        except subprocess.TimeoutExpired: child.kill(); child.wait()
    record['endedAtUtc'] = now()
    for name in ['driver.stdout', 'driver.stderr']:
        path = adapter / name
        if path.exists(): record[name + 'Sha256'] = sha(path)
    errors = []
    try:
        verify_recording_bindings(ROOT, bindings)
        assert digest(args.authorization.read_bytes()) == digest(authorization_bytes)
        assert snapshot(original_inputs) == initial
    except (AssertionError, OSError) as error: errors.append(repr(error))
    record['postExecutionBindingErrors'] = errors
    if errors: record['exitCode'] = 1
    write(adapter / 'driver.command.json', record)
    write(adapter / 'repository-after.json', snapshot(original_inputs))
    write(claim / 'outcome.json', dict(atUtc=now(), adapter=str(adapter), exitCode=record['exitCode'],
        claimRetained=True, errors=errors))
# No retries, repairs, altered versions, cleanup or acceptance. Preserve all outputs.
raise SystemExit(record['exitCode'])
