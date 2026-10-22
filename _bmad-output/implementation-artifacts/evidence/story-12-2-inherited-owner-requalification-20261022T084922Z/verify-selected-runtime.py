from pathlib import Path
import json,hashlib,sys
r=Path('/home/administrator/projects/hexalith/folders');o=Path(__file__).parent;sdk=Path('/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0')
manifest=json.loads((o/'sdk-handoff-source-binary-identities.json').read_text());expected={Path(x['path']).name:x['sha256'].lower() for x in manifest['binaries'] if Path(x['path']).parent==sdk and Path(x['path']).suffix in ('.dll','.pdb')}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
projects=[r/'src'/p for p in ['Hexalith.Folders.Server','Hexalith.Folders.Workers','Hexalith.Folders.EventStore','Hexalith.Folders.AppHost','Hexalith.Folders.UI']]+[r/'tests'/p for p in ['Hexalith.Folders.AppHost.Tests','Hexalith.Folders.Tests']]+[r/'tests/tools/Task36FoldObserver',r/'references/Hexalith.Tenants/src/Hexalith.Tenants.Server']
checks=[];errors=[];deps=[]
for project in projects:
 target=project/'bin/Debug/net10.0'
 for name in ['Hexalith.EventStore.Client.dll','Hexalith.EventStore.Client.pdb','Hexalith.EventStore.Contracts.dll','Hexalith.EventStore.Contracts.pdb','Hexalith.EventStore.DomainService.dll','Hexalith.EventStore.DomainService.pdb','Hexalith.Commons.UniqueIds.dll','Hexalith.Commons.UniqueIds.pdb']:
  p=target/name;actual=sha(p) if p.is_file() else None;row=dict(path=str(p),expected=expected[name],actual=actual,matches=actual==expected[name]);checks.append(row)
  if not row['matches']:errors.append(row)
 for p in target.glob('*.deps.json'):
  if p.name.startswith('Hexalith.EventStore.'):continue
  d=json.loads(p.read_text());t=next(iter(d['targets'].values()));ok=all(k in t for k in ['Hexalith.Commons.UniqueIds/1.0.0','ByteAether.Ulid/1.4.2']);deps.append(dict(path=str(p),sha256=sha(p),required_assets_present=ok))
  if not ok:errors.append(str(p))
 p=target/'ByteAether.Ulid.dll';row=dict(path=str(p),expected=sha(sdk/p.name),actual=sha(p));checks.append(row)
 if row['actual']!=row['expected']:errors.append(row)
label=sys.argv[1];dest=o/(label+'.json');assert not dest.exists();dest.write_text(json.dumps(dict(checks=checks,runtime_dependency_manifests=deps,errors=errors,passed=not errors,note='Read-only verification of required outputs after select-final-runtime failed on an unused optional Memories path. Necessary nine outputs were materialized before that check; exact binary and dependency identities are independently checked here.'),indent=2)+'\n');print(dict(checks=len(checks),manifests=len(deps),errors=len(errors)));assert not errors
