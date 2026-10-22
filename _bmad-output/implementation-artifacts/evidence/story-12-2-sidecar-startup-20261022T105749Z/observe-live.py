from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import ssl
import subprocess
import sys
import time
import urllib.request

OUT = Path(__file__).resolve().parent
label = sys.argv[1]
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
observed_ids = set()
modules = {}
sidecars = {}
containers = {}
probes = []
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
freeze = json.loads((OUT / 'executed-freeze.json').read_text())
query = (OUT / 'observe-admission.py').read_text().split('script = """', 1)[1].split('"""', 1)[0]
snapshots = OUT / (label + '-admission')
snapshots.mkdir(exist_ok=True)
while not (OUT / ('stop-' + label + '-observer')).exists():
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    names = subprocess.check_output(['docker', 'ps', '--format', '{{.Names}}'], text=True).splitlines()
    for name in names:
        if not re.fullmatch(r'folders-(?:durability|provider)-[0-9a-f]{16}', name):
            continue
        metadata = json.loads(subprocess.check_output(['docker', 'inspect', name], text=True))[0]
        if metadata['Created'] < started:
            continue
        ident = name.rsplit('-', 1)[1]
        observed_ids.add(ident)
        containers[name] = dict(name=name, id=metadata['Id'], created_utc=metadata['Created'],
                                image_id=metadata['Image'], observed_utc=now, running=metadata['State']['Running'])
        if name.startswith('folders-provider-'):
            ports = metadata['NetworkSettings']['Ports'].get('3000/tcp')
            mounts = [m['Source'] for m in metadata['Mounts'] if m['Destination'] == '/certs']
            if ports and mounts:
                url = 'https://127.0.0.1:' + ports[0]['HostPort'] + '/api/v1/version'
                try:
                    context = ssl.create_default_context(cafile=str(Path(mounts[0]) / 'certificate.pem'))
                    with urllib.request.urlopen(url, context=context, timeout=1) as response:
                        probe = dict(utc=now, fixture_id=ident, status=response.status)
                except Exception as error:
                    probe = dict(utc=now, fixture_id=ident, error_type=type(error).__name__, error=str(error)[:512])
                probes.append(probe)
        else:
            result = subprocess.run(['docker', 'exec', name, 'redis-cli', '--raw', 'EVAL', query, '0'],
                                    capture_output=True, text=True, timeout=5)
            if result.returncode == 0 and result.stdout.startswith('['):
                rows = json.loads(result.stdout)
                for row in rows:
                    row['storage_key_sha256'] = hashlib.sha256(row.pop('key').encode()).hexdigest()
                (snapshots / (now.replace(':', '') + '.json')).write_text(json.dumps(dict(
                    utc=now, container=name, rows=rows, query_sha256=hashlib.sha256(query.encode()).hexdigest()), indent=2) + '\n')
    for process in Path('/proc').iterdir():
        if not process.name.isdigit():
            continue
        try:
            if process.stat().st_uid != os.getuid():
                continue
            environment = (process / 'environ').read_bytes().split(b'\0')
            namespace = next((item.split(b'=', 1)[1].decode() for item in environment if item.startswith(b'NAMESPACE=')), '')
            if not any(namespace == 'folders-durability-' + ident for ident in observed_ids):
                continue
            argv = [part.decode() for part in (process / 'cmdline').read_bytes().split(b'\0') if part]
            if argv and Path(argv[0]).name == 'daprd' and process.name not in sidecars:
                directories = [argv[i + 1] for i, part in enumerate(argv[:-1]) if part == '--resources-path']
                configs = [argv[i + 1] for i, part in enumerate(argv[:-1]) if part == '--config']
                sidecars[process.name] = dict(pid=int(process.name), utc=now, argv=argv, namespace=namespace,
                    config_files=[dict(path=n, sha256=sha(Path(n))) for n in configs if Path(n).is_file()],
                    resource_files=[dict(path=str(path), sha256=sha(path)) for n in directories
                                    for path in Path(n).glob('*.yaml')])
            for line in (process / 'maps').read_text().splitlines():
                if '/' not in line or not line.endswith('.dll'):
                    continue
                path = line[line.index('/'):]
                if not Path(path).name.startswith(('Hexalith.', 'Task36')):
                    continue
                key = process.name + ':' + path
                if key not in modules:
                    actual = sha(Path(path))
                    modules[key] = dict(pid=int(process.name), path=path, sha256=actual,
                                        expected_sha256=freeze['assemblies_sha256'].get(path),
                                        matches=actual == freeze['assemblies_sha256'].get(path))
        except (OSError, ValueError, StopIteration):
            continue
    record = dict(started_utc=started, observed_utc=now, fixture_ids=sorted(observed_ids),
                  containers=list(containers.values()), provider_probes=probes,
                  sidecars=list(sidecars.values()), modules=list(modules.values()))
    (OUT / (label + '-observations.json')).write_text(json.dumps(record, indent=2) + '\n')
    time.sleep(2)
print('Fixture-owned live observer stopped.', flush=True)
