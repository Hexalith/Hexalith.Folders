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
evidence = Path(__file__).resolve().parent
original_record = json.loads((evidence/'publication.json').read_text())
sha = original_record['source_revision']
release = {'url':original_record['release'], 'publishedAt':original_record['published_at_utc'],
           'assets':[p['release_asset'] for p in original_record['packages']]}
run_raw = json.loads(subprocess.check_output(['gh','api','repos/Hexalith/Hexalith.EventStore/actions/runs/38523534652'],text=True))
assert run_raw['head_sha'] == sha and run_raw['conclusion'] == 'success'
run = {'databaseId':run_raw['id'],'headSha':run_raw['head_sha'],'status':run_raw['status'],
       'conclusion':run_raw['conclusion'],'url':run_raw['html_url']}
manifest_bytes = (evidence/'release-packages.json').read_bytes()
assert manifest_bytes == subprocess.check_output(['git','show',sha+':tools/release-packages.json'])
manifest = json.loads(manifest_bytes)

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
    payload = {n: hashlib.sha256(archive.read(n)).hexdigest() for n in archive.namelist() if n != '.signature.p7s'}
    github_path = scratch / 'github' / asset['name']
    github_archive = zipfile.ZipFile(github_path)
    github_payload = {n: hashlib.sha256(github_archive.read(n)).hexdigest() for n in github_archive.namelist() if n != '.signature.p7s'}
    assert github_payload == payload, package_id
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
            'release_asset': {'name': asset['name'], 'url': asset['url'], 'size': asset['size'],
                              'sha256': hashlib.sha256(github_path.read_bytes()).hexdigest(),
                              'runtime_and_docs_match_nuget': True},
            'assembly_and_documentation_sha256': {n:h for n,h in payload.items() if n.endswith(('.dll','.xml'))},
            'unsigned_package_entry_sha256': payload, 'all_unsigned_entries_match_release_asset': True,
            'required_api_identifiers_verified': required_api_names}

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    packages = list(pool.map(check_package, manifest['packages']))
record = {'version': version, 'source_revision': sha, 'release': release['url'],
          'published_at_utc': release['publishedAt'], 'release_workflow_run': run,
          'package_count': len(packages), 'packages': packages,
          'publication_required': False}
(scratch/'publication-complete.json').write_text(json.dumps(record, indent=2)+'\n')
print(json.dumps({k:v for k,v in record.items() if k != 'packages'}, indent=2))
for p in packages:
    print(p['id'], p['version'], p['repository']['commit'], p['nuget_archive_sha256'], p['required_api_identifiers_verified'])
