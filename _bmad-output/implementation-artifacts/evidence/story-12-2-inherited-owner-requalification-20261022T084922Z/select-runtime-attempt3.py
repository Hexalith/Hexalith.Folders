from pathlib import Path
import hashlib,json,shutil,sys
r=Path('/home/administrator/projects/hexalith/folders');o=Path(__file__).resolve().parent
label=sys.argv[1];receipt=o/(label+'.json');assert not receipt.exists()
sdk=Path('/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0')
m=json.loads((o/'sdk-handoff-source-binary-identities.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=[x for x in m['binaries'] if Path(x['path']).parent==sdk and Path(x['path']).suffix in ('.dll','.pdb') and Path(x['path']).name.startswith('Hexalith.') and 'Tests' not in Path(x['path']).name]
assert len(rows)==30
for row in rows:assert sha(Path(row['path']))==row['sha256'].lower(),row
# Actual host/runtime and consumer test outputs; no historical evidence or SDK-owned outputs.
projects=[r/'src'/p for p in ['Hexalith.Folders.Server','Hexalith.Folders.Workers','Hexalith.Folders.EventStore','Hexalith.Folders.AppHost','Hexalith.Folders.UI']]
projects += [r/'tests'/p for p in ['Hexalith.Folders.AppHost.Tests','Hexalith.Folders.Tests']]
projects += [r/'tests/tools/Task36FoldObserver', r/'references/Hexalith.Tenants/src/Hexalith.Tenants.Server', r/'references/Hexalith.Memories/src/Hexalith.Memories.Server']
archive=o/(label+'-previous-output');archive.mkdir()
def preserve(path):
 if not path.exists():return None
 digest=sha(path);dst=archive/digest
 if not dst.exists():shutil.copy2(path,dst)
 return digest
changes=[];probes=[]
core={'Hexalith.EventStore.Client.dll','Hexalith.EventStore.Contracts.dll','Hexalith.EventStore.DomainService.dll','Hexalith.Commons.UniqueIds.dll','Hexalith.Commons.UniqueIds.pdb'}
for project in projects:
 target=project/'bin/Debug/net10.0';assert target.is_dir(),target
 for row in rows:
  src=Path(row['path']);dst=target/src.name
  if not dst.exists() and src.name not in core:continue
  before=preserve(dst);shutil.copy2(src,dst)
  assert sha(dst)==row['sha256'].lower()
  changes.append(dict(path=str(dst),before=before,source=str(src),sha256=sha(dst)))
 # The source-reference consumer's generated manifest can omit a dependency required by the exact selected images.
 # Materialize that dependency from the qualified graph's own manifest; no package/project pins are changed.
 candidate=json.loads((sdk/'Hexalith.EventStore.DomainService.deps.json').read_text());ct=next(iter(candidate['targets'].values()))
 for depfile in target.glob('*.deps.json'):
  if depfile.name.startswith('Hexalith.EventStore.'):continue
  doc=json.loads(depfile.read_text());dt=next(iter(doc['targets'].values()));before=preserve(depfile)
  for key in ['Hexalith.Commons.UniqueIds/1.0.0','ByteAether.Ulid/1.4.2']:
   if key not in dt:
    dt[key]=ct[key];doc['libraries'][key]=candidate['libraries'][key]
  depfile.write_text(json.dumps(doc,indent=2)+'\n');probes.append(dict(path=str(depfile),before=before,sha256=sha(depfile)))
 src=sdk/'ByteAether.Ulid.dll';dst=target/src.name;before=preserve(dst);shutil.copy2(src,dst)
 changes.append(dict(path=str(dst),before=before,source=str(src),sha256=sha(dst),dependency='ByteAether.Ulid/1.4.2 from SDK runtime graph'))
 for name in core:assert (target/name).is_file(),(target,name)
receipt.write_text(json.dumps(dict(qualified_sdk_manifest_sha256=sha(o/'sdk-handoff-source-binary-identities.json'),selection=changes,generated_runtime_dependency_probes=probes,required_projects=[str(p) for p in projects],candidate_dependency_manifest=str(sdk/'Hexalith.EventStore.DomainService.deps.json'),candidate_dependency_manifest_sha256=sha(sdk/'Hexalith.EventStore.DomainService.deps.json')),indent=2)+'\n')
print(dict(selected_files=len(changes),runtime_manifests=len(probes),receipt=str(receipt)))
