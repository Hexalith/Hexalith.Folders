"""UNEXECUTED proposal. Requires Jerome's separately recorded, hash-bound authorization.

No restore, model rebuild, CLI/AppHost entrypoint, ComposeRun invocation, prerequisite
probe, network, or external write is in this experiment. Assembly loading can trigger
initializers: execution authority is required even though methods are never invoked.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import signal
import struct
import subprocess
import tarfile
import tempfile
import time
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = Path('/home/administrator/projects/hexalith/folders')
PLATFORM = ROOT / 'references/Hexalith.Platform'
DOTNET = Path('/home/administrator/.dotnet/dotnet')
def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()
def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + '\n')
def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def same(path, item):
    assert Path(path).is_file() and not Path(path).is_symlink(), str(path)
    assert sha(path) == item['sha256'] and Path(path).stat().st_size == item['bytes'], str(path)
def git(repo, *args):
    return subprocess.check_output(['git', '-C', str(ROOT / repo), *args])
def repository_state():
    result = {}
    for rel in INPUTS['repositories']:
        result[rel] = dict(revision=git(rel, 'rev-parse', 'HEAD').decode().strip(),
            indexEntriesSha256=hashlib.sha256(git(rel, 'ls-files', '--stage')).hexdigest(),
            status=git(rel, 'status', '--porcelain=v2', '--untracked-files=all').decode())
    return result
def sdk_gate():
    for item in INPUTS['sdk']['files']:
        same(item['path'], item)
    for name, expected in INPUTS['installedFrameworkDirectories'].items():
        assert sorted(p.name for p in Path(name).iterdir() if p.is_dir()) == expected
def certificate_metadata():
    # No certificate contents, private keys, or store cleanup.
    result = {}
    for base in [Path('/home/administrator/.dotnet/corefx/cryptography/x509stores'), WORK / 'dotnet-home/.dotnet/corefx/cryptography/x509stores']:
        entries = []
        if base.exists():
            for path in sorted(base.rglob('*')):
                stat = path.lstat()
                entries.append(dict(path=str(path.relative_to(base)), mode=stat.st_mode,
                    bytes=stat.st_size, mtimeNs=stat.st_mtime_ns, ctimeNs=stat.st_ctime_ns))
        result[str(base)] = dict(exists=base.exists(), entries=entries)
    return result
def processes():
    result = {}
    for path in Path('/proc').iterdir():
        if not path.name.isdigit(): continue
        try:
            raw = (path / 'stat').read_text(); fields = raw[raw.rfind(')') + 2:].split()
            result[int(path.name)] = dict(pid=int(path.name), ppid=int(fields[1]),
                pgid=int(fields[2]), startTime=fields[19], rawStat=raw)
        except (FileNotFoundError, ProcessLookupError, PermissionError): pass
    return result
def observe(process, seen):
    table = processes(); owned = {process.pid}
    while True:
        expanded = owned | {pid for pid, v in table.items() if v['ppid'] in owned or v['pgid'] == process.pid}
        if expanded == owned: break
        owned = expanded
    rows = [table[pid] for pid in sorted(owned) if pid in table]
    for row in rows: seen.add((row['pid'], row['startTime']))
    return dict(atUtc=now(), rootPid=process.pid, processGroup=process.pid, rows=rows)
def run(label, argv, limit=60):
    sdk_gate()  # Exact executable/global.json/compiler/runtime/reference-pack hashes before each command.
    sandbox = ['/usr/bin/bwrap', '--unshare-all', '--die-with-parent', '--ro-bind', '/', '/',
        '--tmpfs', '/tmp', '--tmpfs', '/run', '--bind', str(WORK), str(WORK), '--proc', '/proc', '--dev', '/dev',
        '--seccomp', str(SECCOMP_FD),
        '--chdir', str(PLATFORM), '--', *map(str, argv)]
    record = dict(atUtc=now(), argv=sandbox, targetArgv=list(map(str, argv)), cwd=str(PLATFORM),
        environment=ENV, timeoutSeconds=limit, termGraceSeconds=5, observations=[])
    seen = set()
    with (WORK / (label + '.stdout')).open('wb') as stdout, (WORK / (label + '.stderr')).open('wb') as stderr:
        process = subprocess.Popen(sandbox, cwd=PLATFORM, env=ENV, stdin=subprocess.DEVNULL,
            stdout=stdout, stderr=stderr, start_new_session=True, pass_fds=(SECCOMP_FD,))
        deadline = time.monotonic() + limit
        while process.poll() is None:
            record['observations'].append(observe(process, seen))
            if time.monotonic() >= deadline:
                record['timedOut'] = True
                os.killpg(process.pid, signal.SIGTERM)
                try: process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL); process.wait()
                break
            time.sleep(0.2)
        record['exitCode'] = process.wait()
    record['observations'].append(observe(process, seen))
    table = processes()
    record['observedSurvivors'] = [row for row in table.values()
        if (row['pid'], row['startTime']) in seen or row['pgid'] == process.pid]
    record['observationLimit'] = '200ms sampled descendants/group only; short-lived processes can be missed. PID namespace, read-only filesystem, unshared network and loaded seccomp socket denial supply containment. No whole-host observation claim.'
    record['endedAtUtc'] = now()
    record['stdoutSha256'] = sha(WORK / (label + '.stdout'))
    record['stderrSha256'] = sha(WORK / (label + '.stderr'))
    write(WORK / (label + '.command.json'), record)
    assert not record.get('timedOut') and not record['observedSurvivors'], label
    output = (WORK / (label + '.stdout')).read_text(errors='replace')
    errors = (WORK / (label + '.stderr')).read_text(errors='replace')
    assert not any(x in (output + errors).lower() for x in ['installed an asp.net core https development certificate', 'trusting the https development certificate']), label
    return record['exitCode'], output
def checked_version(label):
    code, output = run(label, [DOTNET, '--version'], 30)
    assert code == 0 and output.strip() == '10.0.402', (code, output)
    sdk_gate()  # Rehash after asserted selection, immediately before the dependent launch.

parser = argparse.ArgumentParser()
parser.add_argument('--authorization', required=True, type=Path)
args = parser.parse_args()
authorization = json.loads(args.authorization.read_text())
assert authorization['decisionMaker'] == 'Jerome' and authorization['authorized'] is True
assert authorization['scope'] == 'story-1-17-cli-4-38-loader-only-two-cases'
for filename in ['LoaderProbe.cs', 'execute_after_authorization.py', 'inputs.json']:
    assert authorization['sha256'][filename] == sha(HERE / filename)
plan_path = ROOT / '_bmad-output/implementation-artifacts/story-1-17-cli-4-38-runtime-resolution-plan-2026-10-26.json'
assert authorization['planSha256'] == sha(plan_path)
for item in json.loads(plan_path.read_text())['supportingArtifacts']:
    same(ROOT / item['path'], item)
INPUTS = json.loads((HERE / 'inputs.json').read_text())
assert os.uname().machine == 'x86_64', 'Seccomp syscall table is x86_64 only; no fallback.'
for rel, expected in INPUTS['repositories'].items():
    assert git(rel, 'rev-parse', 'HEAD').decode().strip() == expected['revision'], rel
    assert hashlib.sha256(git(rel, 'ls-files', '--stage')).hexdigest() == expected['indexEntriesSha256'], rel
# The authored proposal files may be untracked; all current dirty bytes must equal the authorized manifest.
initial_state = repository_state()
assert initial_state == authorization['repositoryState'], 'Record current status/indices/HEADs at authorization; drift requires a new disposition.'
for item in INPUTS['preservedFiles']: same(ROOT / item['path'], item)
for path in INPUTS['absentPaths']: assert not (ROOT / path).exists(), path
same(INPUTS['package']['path'], INPUTS['package'])
same(ROOT / INPUTS['modelArchive']['path'], INPUTS['modelArchive'])
sdk_gate()
WORK = Path(tempfile.mkdtemp(prefix='story117-cli438-loader-20261026-'))
os.chmod(WORK, 0o700)
for name in ['host', 'model', 'tmp', 'dotnet-home', 'cache', 'nuget', 'bundle']: (WORK / name).mkdir()
# Classic BPF: require AUDIT_ARCH_X86_64, reject x32 ABI; EPERM socket,
# connect, socketpair, io_uring_setup. No inherited sockets are passed.
instructions = [(0x20, 0, 0, 4), (0x15, 1, 0, 0xc000003e), (0x06, 0, 0, 0x80000000),
    (0x20, 0, 0, 0), (0x35, 0, 1, 0x40000000), (0x06, 0, 0, 0x80000000)]
for syscall in [41, 42, 53, 425]:
    instructions.extend([(0x15, 0, 1, syscall), (0x06, 0, 0, 0x00050001)])
instructions.append((0x06, 0, 0, 0x7fff0000))
filter_bytes = b''.join(struct.pack('<HBBI', *row) for row in instructions)
assert hashlib.sha256(filter_bytes).hexdigest() == INPUTS['seccompSha256']
(WORK / 'deny-sockets.bpf').write_bytes(filter_bytes)
SECCOMP_FD = os.open(WORK / 'deny-sockets.bpf', os.O_RDONLY)
write(WORK / 'authorization.json', authorization)
ENV = dict(PATH='/usr/bin:/bin', LANG='C.UTF-8', LC_ALL='C.UTF-8', DOTNET_ROOT='/home/administrator/.dotnet',
    DOTNET_CLI_HOME=str(WORK / 'dotnet-home'), TMPDIR=str(WORK / 'tmp'), TMP=str(WORK / 'tmp'), TEMP=str(WORK / 'tmp'),
    XDG_CACHE_HOME=str(WORK / 'cache'), NUGET_PACKAGES=str(WORK / 'nuget'),
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(WORK / 'bundle'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
    DOTNET_NOLOGO='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='true',
    DOTNET_EnableDiagnostics='0', DOTNET_EnableDiagnostics_IPC='0',
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true', DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1')
write(WORK / 'environment.json', dict(environment=ENV, inheritance='Popen receives exactly this mapping; HOME is not repurposed. Real home/root filesystem mounted read-only.'))
before_cert = certificate_metadata(); write(WORK / 'certificate-before.json', before_cert)
write(WORK / 'repository-before.json', initial_state)
try:
    with zipfile.ZipFile(INPUTS['package']['path']) as archive:
        for item in INPUTS['hostMembers']:
            name = item['path']; suffix = name.removeprefix(INPUTS['hostPrefix'])
            assert suffix != name and not Path(suffix).is_absolute() and '..' not in Path(suffix).parts
            assert archive.namelist().count(name) == 1
            info = archive.getinfo(name); assert (info.external_attr >> 16) & 0o170000 != 0o120000
            data = archive.read(name); assert hashlib.sha256(data).hexdigest() == item['sha256'] and len(data) == item['bytes']
            target = WORK / 'host' / suffix; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    with tarfile.open(ROOT / INPUTS['modelArchive']['path']) as archive:
        for item in INPUTS['modelMembers']:
            suffix = item['path'].removeprefix('hosts/platform-model/')
            assert suffix != item['path'] and not Path(suffix).is_absolute() and '..' not in Path(suffix).parts
            member = archive.getmember(item['path']); assert member.isfile()
            data = archive.extractfile(member).read(); assert hashlib.sha256(data).hexdigest() == item['sha256'] and len(data) == item['bytes']
            target = WORK / 'model' / suffix; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    (WORK / 'LoaderProbe.cs').write_bytes((HERE / 'LoaderProbe.cs').read_bytes())
    checked_version('sdk-before-compiler')
    code, info = run('sdk-info', [DOTNET, '--info'], 30)
    assert code == 0 and '10.0.402' in info and str(PLATFORM / 'global.json') in info
    code, msbuild = run('sdk-msbuild-version-only', [DOTNET, 'msbuild', '-version', '-nologo'], 30)
    assert code == 0 and msbuild.strip() == INPUTS['msbuildVersion']
    checked_version('sdk-immediate-compiler')
    references = [item['path'] for item in INPUTS['sdk']['files'] if '/packs/Microsoft.NETCore.App.Ref/10.0.13/ref/net10.0/' in item['path']]
    compiler = [DOTNET, 'exec', '/home/administrator/.dotnet/sdk/10.0.402/Roslyn/bincore/csc.dll',
        '-noconfig', '-nostdlib+', '-target:exe', '-langversion:14', '-nullable:enable', '-deterministic+',
        '-out:' + str(WORK / 'host/LoaderProbe.dll'), *['-r:' + p for p in references], str(WORK / 'LoaderProbe.cs')]
    code, _ = run('compile-probe-only', compiler, 60); assert code == 0
    write(WORK / 'compiled-probe.json', dict(path=str(WORK / 'host/LoaderProbe.dll'), sha256=sha(WORK / 'host/LoaderProbe.dll')))
    for mode in ['cold', 'host-first']:
        for item in INPUTS['hostMembers']:
            same(WORK / 'host' / item['path'].removeprefix(INPUTS['hostPrefix']), item)
        for item in INPUTS['modelMembers']:
            same(WORK / 'model' / item['path'].removeprefix('hosts/platform-model/'), item)
        checked_version('sdk-immediate-' + mode)
        probe_env = dict(ENV)
        ENV['DOTNET_HOST_TRACE'] = '1'; ENV['DOTNET_HOST_TRACEFILE'] = str(WORK / (mode + '.host-trace.log')); ENV['DOTNET_HOST_TRACE_VERBOSITY'] = '4'
        code, _ = run('probe-' + mode, [DOTNET, 'exec', '--runtimeconfig', WORK / 'host/Hexalith.Builds.Module.AppHost.runtimeconfig.json',
            '--depsfile', WORK / 'host/Hexalith.Builds.Module.AppHost.deps.json', WORK / 'host/LoaderProbe.dll',
            WORK / 'model/Hexalith.Platform.Composition.dll', mode], 60)
        ENV = probe_env
        assert code in (0, 10), 'Unexpected failure; no second case, retry, or repair.'
    write(WORK / 'experiment-completed.json', dict(atUtc=now(), executionOnly='two explicit binding cases; no acceptance or application entrypoints'))
finally:
    after_cert = certificate_metadata(); write(WORK / 'certificate-after.json', after_cert)
    final_state = repository_state(); write(WORK / 'repository-after.json', final_state)
    drifts = []
    try: sdk_gate()
    except (AssertionError, OSError) as error: drifts.append('SDK: ' + str(error))
    for prefix, items in [('host', INPUTS['hostMembers']), ('model', INPUTS['modelMembers'])]:
        for item in items:
            relative = item['path'].removeprefix(INPUTS['hostPrefix'] if prefix == 'host' else 'hosts/platform-model/')
            try: same(WORK / prefix / relative, item)
            except (AssertionError, OSError) as error: drifts.append('copied input: ' + str(error))
    for item in INPUTS['preservedFiles']:
        try: same(ROOT / item['path'], item)
        except (AssertionError, OSError) as error: drifts.append('preserved input: ' + str(error))
    write(WORK / 'preservation.json', dict(gitStateEqual=initial_state == final_state,
        certificateMetadataEqual=before_cert == after_cert,
        inputDrifts=drifts,
        boundary='bwrap read-only / except fresh private WORK, no network; no claim to all external state'))
    os.close(SECCOMP_FD)
    print(str(WORK))
    assert initial_state == final_state and before_cert == after_cert and not drifts
# Retain all outputs on success or failure. No automatic deletion or certificate/cache cleanup.
