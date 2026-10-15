import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile
import xml.etree.ElementTree as ET

root = Path.cwd()
evidence = root / '_bmad-output/implementation-artifacts/evidence/story-1-17-package-unblock-2026-10-14'
baseline = json.loads((evidence / 'baseline.json').read_text())
publication = json.loads((evidence / 'publication.json').read_text())

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

checks = []
for path in sorted(evidence.glob('*.command.json')):
    record = json.loads(path.read_text())
    log = root / record['log']
    assert digest(log) == record['log_sha256'], record['id']
    text = log.read_text(errors='replace')
    summaries = re.findall(r'Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)', text)
    if summaries:
        record['test_summaries'] = [dict(zip(['total','errors','failed','skipped','not_run'], map(int, row))) for row in summaries]
        record['executed_tests'] = sum(row['total'] - row['skipped'] - row['not_run'] for row in record['test_summaries'])
    build_summary = re.findall(r'(\d+) Warning\(s\)\s+(\d+) Error\(s\)', text)
    if build_summary:
        record['build_summary'] = dict(zip(['warnings','errors'],map(int,build_summary[-1])))
    checks.append(record)

invariants = {}
for name, repo in baseline['repos'].items():
    repo_root = Path(repo['path'])
    current = {p: digest(repo_root/p) for p in repo['tracked_invariant_hashes']}
    changed = [p for p,h in repo['tracked_invariant_hashes'].items() if current[p] != h]
    assert not changed, (name,changed)
    invariants[name] = {'count':len(current),'before_sha256':hashlib.sha256(json.dumps(repo['tracked_invariant_hashes'],sort_keys=True).encode()).hexdigest(),
                        'after_sha256':hashlib.sha256(json.dumps(current,sort_keys=True).encode()).hexdigest(),'changed_paths':changed}
assert all(digest(root/p)==h for p,h in baseline['protected'].items())

consumers = []
targets = [
    ('folders',root,'src/Hexalith.Folders.Server','tests/Hexalith.Folders.Server.Tests'),
    ('folders-eventstore',root,'src/Hexalith.Folders.EventStore','tests/Hexalith.Folders.EventStore.Tests'),
    ('projects',root/'references/Hexalith.Projects','src/Hexalith.Projects.Server','tests/Hexalith.Projects.Server.Tests'),
]
package_map = {p['id']:p for p in publication['packages']}
for name,repo_root,server,test in targets:
    assets_path = repo_root / server / 'obj/project.assets.json'
    assets = json.loads(assets_path.read_text())
    libraries = {key:lib['type'] for key,lib in assets['libraries'].items() if key.startswith('Hexalith.EventStore.') or key.startswith('Hexalith.Folders.')}
    for key,kind in libraries.items():
        if key.startswith('Hexalith.EventStore.'):
            assert kind=='package' and key.endswith('/3.121.0'), (name,key,kind)
    output = repo_root / test / 'bin/Release/net10.0'
    test_assets = json.loads((repo_root/test/'obj/project.assets.json').read_text())
    expected_runtime = {Path(asset).name for target in test_assets['targets'].values()
                        for identity,entry in target.items() if identity.startswith('Hexalith.EventStore.')
                        for asset in entry.get('runtime',{}) if asset.endswith('.dll')}
    assert expected_runtime, name
    for filename in expected_runtime:
        assert (output/filename).is_file(), (name,filename)
    required_packages = {'Hexalith.EventStore.Contracts','Hexalith.EventStore.Client','Hexalith.EventStore.ServiceDefaults','Hexalith.EventStore.DomainService'}
    if name != 'folders':
        required_packages |= {'Hexalith.EventStore.Server','Hexalith.EventStore.Gateway'}
    assert required_packages <= {identity.partition('/')[0] for identity in libraries}, (name,libraries)
    binaries = {}
    for path in sorted(output.glob('Hexalith.EventStore.*.dll')):
        package = package_map[path.stem]
        payload_hash = package['assembly_and_documentation_sha256']['lib/net10.0/'+path.name]
        assert digest(path)==payload_hash,(name,path.name)
        binaries[path.name] = {'sha256':digest(path),'matches_published_3_121_0_payload':True}
    own_binaries = {p.name:digest(p) for p in sorted(output.glob('*.dll')) if p.name.startswith(('Hexalith.Folders.','Hexalith.Projects.'))}
    consumers.append({'name':name,'repository':str(repo_root),'assets_path':str(assets_path.relative_to(root)),
                      'assets_sha256':digest(assets_path),'selected_libraries':libraries,
                      'test_output':str(output.relative_to(root)), 'eventstore_runtime_payloads':binaries,'consumer_binary_sha256':own_binaries})
    consumers[-1]['expected_eventstore_runtime_inventory'] = sorted(expected_runtime)

executed_assemblies = []
for repo_root,name in [(root,n) for n in ['Contracts','Client','Cli','Mcp','Tests','IntegrationTests','Server','EventStore']] + [(root/'references/Hexalith.Projects','Server')]:
    module = 'Projects' if repo_root.name=='Hexalith.Projects' else 'Folders'
    test_name = f'Hexalith.{module}.{name}' if name in ['Tests','IntegrationTests'] else f'Hexalith.{module}.{name}.Tests'
    output = repo_root/'tests'/test_name/'bin/Release/net10.0'
    assembly = output/(test_name+'.dll')
    assert assembly.is_file(), assembly
    dependencies = {}
    for path in output.glob('Hexalith.EventStore.*.dll'):
        package = package_map[path.stem]
        expected = package['assembly_and_documentation_sha256']['lib/net10.0/'+path.name]
        assert digest(path)==expected,(test_name,path.name)
        dependencies[path.name]=digest(path)
    executed_assemblies.append({'assembly':str(assembly.relative_to(root)),'sha256':digest(assembly),'eventstore_dependency_sha256':dependencies})

gate_reports = {}
for path in ['_bmad-output/gates/governance-completeness/latest.json','_bmad-output/gates/contract-parity-ci/latest.json']:
    original = subprocess.check_output(['git','show',baseline['repos']['folders']['head']+':'+path],cwd=root)
    current = (root/path).read_bytes()
    assert original==current,path
    gate_reports[path]={'comparison_source_revision':baseline['repos']['folders']['head'],
                        'before_sha256':hashlib.sha256(original).hexdigest(),'after_sha256':digest(root/path),'byte_identical':True}

pair = []
cache = Path.home()/'.nuget/packages'
projects_output = root/'references/Hexalith.Projects/tests/Hexalith.Projects.Server.Tests/bin/Release/net10.0'
for package_id in ['Hexalith.Folders.Client','Hexalith.Folders.Contracts']:
    archive_path = cache/package_id.lower()/'1.2.1'/(package_id.lower()+'.1.2.1.nupkg')
    archive = zipfile.ZipFile(archive_path)
    metadata = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
    ns = {'n':metadata.tag.partition('}')[0].lstrip('{')}
    repo = metadata.find('n:metadata/n:repository',ns).attrib
    dll_name=package_id+'.dll'
    payload=digest(projects_output/dll_name)
    assert payload==hashlib.sha256(archive.read('lib/net10.0/'+dll_name)).hexdigest()
    pair.append({'id':package_id,'version':'1.2.1','repository':repo,'cached_archive_sha256':digest(archive_path),
                 'runtime_payload_sha256':payload,'runtime_matches_cached_archive':True})

catalog = root/'references/Hexalith.Builds/Props/Directory.Packages.props'
prior_catalog_blob = subprocess.check_output(['git','show','HEAD:Props/Directory.Packages.props'],cwd=catalog.parents[1])
prior_catalog = prior_catalog_blob.replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')
record = {
    'record_id':'STORY-1-17-RELEASE-PACKAGE-UNBLOCK-2026-10-14',
    'observed_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'scope':'Published EventStore prerequisite, shared catalog selection, local Release/package builds and tests; no production entry.',
    'source':{name:repo['head'] for name,repo in baseline['repos'].items()},
    'eventstore_publication':publication,
    'catalog':{'path':str(catalog.relative_to(root)),'owning_repository':'Hexalith/Hexalith.Builds',
               'property':'HexalithEventStoreVersion','before':'3.120.0','after':'3.121.0',
               'before_sha256':hashlib.sha256(prior_catalog).hexdigest(),'after_sha256':digest(catalog),
               'bom_preserved':catalog.read_bytes().startswith(b'\xef\xbb\xbf'),
               'crlf_preserved':catalog.read_bytes().count(b'\n')==catalog.read_bytes().count(b'\r\n'),
               'only_changed_bytes_are_eventstore_version':catalog.read_bytes()==prior_catalog.replace(b'>3.120.0</HexalithEventStoreVersion>',b'>3.121.0</HexalithEventStoreVersion>'),
               'change_state':'local uncommitted pin update; no catalog push'},
    'checks':checks,'consumers':consumers,'executed_assemblies':executed_assemblies,'folders_published_pair':pair,
    'environment':json.loads((evidence/'environment.json').read_text()),
    'baseline_snapshot': {'path':str((evidence/'baseline.json').relative_to(root)), 'sha256':digest(evidence/'baseline.json')},
    'preservation':{'historical_and_control_file_count':len(baseline['protected']),
                    'historical_and_control_file_hashes':baseline['protected'],
                    'all_captured_file_hashes_unchanged':True,'source_test_and_gate_invariants':invariants,
                    'gate_reports':gate_reports,
                    'story_tracker_value':'backlog','routing_mode':'V1Only','t0_utc':None},
    'limitations':[
        'Folders build passes with three existing NU1903 audit warnings for Microsoft.AspNetCore.SignalR.Common 10.0.12; existing WarningsNotAsErrors policy preserved.',
        'Projects preserves the previous NuGetAudit=false environment pin. Its unchanged AppHost still builds explicit EventStore/Tenants executable resource references and E2E fixtures in solution-mapped Debug. Consumer libraries/test outputs use published EventStore 3.121.0; no source or configuration gate was changed.',
        'The initial publication checker failed because Server XML documentation omits DomainServiceSafeDenialException; actual binary API inspection and identifier checks pass. Its failure log remains retained.',
        'Focused tests and existing parity/governance gates ran; full unrelated test suites and remote consumer CI did not run.',
        'No EventStore publication was required; no new release, deployment, migration window, T0, observation, v1 retirement, story closure or tracker transition occurred.'
    ],
    'entry':{'decision':'hold','required_routing_mode':'V1Only','t0_utc':None,'production_entry_established':False,
             'remaining_evidence':json.loads((root/'_bmad-output/implementation-artifacts/evidence/story-1-17-local-recheck-2026-10-14.json').read_text())['entry']['missing_evidence'][1:]}
}
assert record['catalog']['only_changed_bytes_are_eventstore_version']
(evidence/'readiness.json').write_text(json.dumps(record,indent=2)+'\n')
print('Publication: 14/14 same-source packages. Runtime payloads match NuGet in all three consumers.')
print('Preserved:',len(baseline['protected']),'historical/control files;',sum(r['count'] for r in invariants.values()),'source/test/gate files.')
for check in checks:
    print(check['id'], 'exit',check['exit_code'], 'tests',check.get('executed_tests'), 'build',check.get('build_summary'))
