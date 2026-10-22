from pathlib import Path
import datetime
import hashlib
import json
import os
import socket
import subprocess
import tempfile
import time

OUT = Path(__file__).resolve().parent
ROOT = Path('/home/administrator/projects/hexalith/folders')
PRIOR = ROOT / '_bmad-output/implementation-artifacts/evidence/story-12-2-strict-continuation-20261022T100829Z'


def audit():
    freeze = json.loads((PRIOR / 'extended-source-runtime-freeze.json').read_text())
    errors = []
    for group, files in freeze.items():
        for name, expected in files.items():
            path = Path(name)
            actual = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
            if actual != expected:
                errors.append(dict(group=group, path=name, expected=expected, actual=actual))
    seal_errors = []
    for relative, expected in ((line.split('  ', 1)[1], line.split('  ', 1)[0])
                               for line in (PRIOR / 'SHA256SUMS').read_text().splitlines()):
        actual = hashlib.sha256((PRIOR / relative).read_bytes()).hexdigest()
        if actual != expected:
            seal_errors.append(dict(path=relative, expected=expected, actual=actual))
    data = dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                counts={group: len(files) for group, files in freeze.items()},
                drift=errors, sealed_packet_errors=seal_errors, passed=not errors and not seal_errors)
    (OUT / 'pre-change-audit.json').write_text(json.dumps(data, indent=2) + '\n')
    print(json.dumps(dict(counts=data['counts'], drift=len(errors), seal_errors=len(seal_errors))), flush=True)
    # The isolated native diagnostic does not load the changed SDK sources. Preserve their drift;
    # only the unchanged selected binaries and sealed history are prerequisites for this diagnostic.
    assert not seal_errors and not any(error['group'] == 'assemblies_sha256' for error in errors)


def port():
    with socket.socket() as value:
        value.bind(('127.0.0.1', 0))
        return str(value.getsockname()[1])


def case(separate):
    label = 'separate' if separate else 'colocated'
    with tempfile.TemporaryDirectory(prefix='folders-sidecar-control-') as temporary:
        directory = Path(temporary)
        resources = directory / 'resources'
        resources.mkdir()
        config = directory / 'accesscontrol.yaml' if separate else resources / 'accesscontrol.yaml'
        config.write_text('apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: accesscontrol\nspec:\n  accessControl:\n    defaultAction: deny\n')
        argv = ['/home/administrator/.dapr/bin/daprd', '--app-id', 'folders-startup-control-' + label,
                '--config', str(config), '--resources-path', str(resources),
                '--dapr-http-port', port(), '--dapr-grpc-port', port(),
                '--dapr-internal-grpc-port', port(), '--metrics-port', port(),
                '--placement-host-address', '', '--scheduler-host-address', '', '--log-level', 'debug']
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        log_path = OUT / (label + '.log')
        with log_path.open('w') as log:
            process = subprocess.Popen(argv, stdout=log, stderr=subprocess.STDOUT,
                                       env={**os.environ, 'NAMESPACE': 'folders-startup-control-' + label})
            try:
                deadline = time.monotonic() + 20
                while 'dapr initialized. Status: Running' not in log_path.read_text():
                    assert process.poll() is None, log_path.read_text()
                    assert time.monotonic() < deadline, log_path.read_text()
                    time.sleep(.1)
                # A non-resource directory event must not restart an unchanged configured runtime.
                (resources / 'unrelated-observation.txt').write_text('metadata-only trigger\n')
                time.sleep(5)
                observed = log_path.read_text()
                reload = 'Received Configuration CREATED event: accesscontrol - triggering SIGHUP reload' in observed
                assert reload is (not separate), observed
            finally:
                if process.poll() is None:
                    process.terminate()
                    try:
                        process.wait(15)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
        result = dict(started_utc=started, argv=argv, pid=process.pid, exit_code=process.returncode,
                      config_sha256=hashlib.sha256(config.read_bytes()).hexdigest(),
                      hot_reload_disabled=False, unchanged_configuration_created_reload=reload,
                      observed_before_cleanup=True, temporary_directory_removed_after_return=True)
        (OUT / (label + '.json')).write_text(json.dumps(result, indent=2) + '\n')
        print(json.dumps(dict(case=label, reload=reload, exit_code=process.returncode)), flush=True)


audit()
case(False)
case(True)
