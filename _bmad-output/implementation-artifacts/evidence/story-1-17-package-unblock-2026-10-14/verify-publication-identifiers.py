import concurrent.futures
import hashlib
import io
import json
from pathlib import Path
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

scratch = Path('/tmp/story117-package-unblock-20261014')
version = '3.121.0'
sha = subprocess.check_output(['git', 'rev-parse', 'v3.121.0^{commit}'], text=True).strip()
release = json.loads(subprocess.check_output(['gh', 'release', 'view', 'v3.121.0', '--repo', 'Hexalith/Hexalith.EventStore', '--json', 'tagName,publishedAt,url,assets'], text=True))
runs = json.loads(subprocess.check_output(['gh', 'run', 'list', '--repo', 'Hexalith/Hexalith.EventStore', '--workflow', 'release.yml', '--limit', '8', '--json', 'databaseId,headSha,status,conclusion,url'], text=True))
run = next(r for r in runs if r['headSha'] == sha and r['conclusion'] == 'success')
manifest = json.loads(subprocess.check_output(['git', 'show', 'v3.121.0:tools/release-packages.json'], text=True))

def fetch(url):
    with urllib.request.urlopen(url, timeout=90) as response:
        return response.read()

def check_package(row):
    package_id = row['id']
    lower = package_id.lower()
    index_url = f'https://api.nuget.org/v3-flatcontainer/{lower}/index.json'
    assert version in json.loads(fetch(index_url))['versions'], package_id
    url = f'https://api.nuget.org/v3-flatcontainer/{lower}/{version}/{lower}.{version}.nupkg'
    data = fetch(url)
    archive = zipfile.ZipFile(io.BytesIO(data))
    metadata = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
    ns = {'n': metadata.tag.partition('}')[0].lstrip('{')}
    m = metadata.find('n:metadata', ns)
    repo = m.find('n:repository', ns).attrib
    assert m.find('n:id', ns).text == package_id
    assert m.find('n:version', ns).text == version
    assert repo['commit'] == sha, (package_id, repo)
    asset = next(a for a in release['assets'] if a['name'] == f'{package_id}.{version}.nupkg')
    payload = {n: hashlib.sha256(archive.read(n)).hexdigest() for n in archive.namelist() if n.endswith(('.dll', '.xml'))}
    required_api_names = {
        'Hexalith.EventStore.Contracts': ['BoundOwnerQueryRequest', 'DomainServiceRequest.HumanAuthorityReceipt'],
        'Hexalith.EventStore.Client': ['BoundOwnerQueryProof'],
        'Hexalith.EventStore.Server': ['DomainServiceSafeDenialException'],
        'Hexalith.EventStore.ServiceDefaults': ['HumanAuthorityReceiptClaims', 'HumanAuthorityReceiptCodec'],
    }.get(package_id, [])
    docs = '\n'.join(archive.read(n).decode('utf-8') for n in archive.namelist() if n.startswith('lib/') and n.endswith('.xml'))
    binaries = b'\n'.join(archive.read(n) for n in archive.namelist() if n.startswith('lib/') and n.endswith('.dll'))
    for name in required_api_names:
        assert name in docs or name.split('.')[-1].encode() in binaries, (package_id, name)
    package_path = scratch / f'{package_id}.{version}.nupkg'
    package_path.write_bytes(data)
    return {'id': package_id, 'version': version, 'repository': repo, 'index_url': index_url,
            'nuget_url': url, 'nuget_archive_sha256': hashlib.sha256(data).hexdigest(),
            'release_asset': {'name': asset['name'], 'url': asset['url'], 'size': asset['size']},
            'assembly_and_documentation_sha256': payload, 'required_api_identifiers_verified': required_api_names}

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    packages = list(pool.map(check_package, manifest['packages']))
record = {'version': version, 'source_revision': sha, 'release': release['url'],
          'published_at_utc': release['publishedAt'], 'release_workflow_run': run,
          'package_count': len(packages), 'packages': packages,
          'publication_required': False}
(scratch/'publication.json').write_text(json.dumps(record, indent=2)+'\n')
print(json.dumps({k:v for k,v in record.items() if k != 'packages'}, indent=2))
for p in packages:
    print(p['id'], p['version'], p['repository']['commit'], p['nuget_archive_sha256'], p['required_api_identifiers_verified'])
