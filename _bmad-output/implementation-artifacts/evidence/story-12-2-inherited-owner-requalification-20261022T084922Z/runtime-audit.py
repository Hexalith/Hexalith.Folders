from pathlib import Path
import json,hashlib,re,sys
out=Path(__file__).parent;label=sys.argv[1];freeze=json.loads((out/'final-accepted-source-freeze.json').read_text())
bind=json.loads((out/'accepted-source-binding/all-pdb-documents.json').read_text());identities={x['pe']:x for x in bind}
sdk=Path('/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0')
manifest=json.loads((out/'sdk-handoff-source-binary-identities.json').read_text());expected={Path(x['path']).name:x['sha256'].lower() for x in manifest['binaries'] if Path(x['path']).parent==sdk}
rows=[];errors=[];observed=[]
for lane in ('capacity-final-live','strict-live'):
 log=out/(lane+'.log')
 if not log.exists():continue
 for line in log.read_text().splitlines():
  if 'RUNTIME_BINDING {' not in line:continue
  item=json.JSONDecoder().raw_decode(line.split('RUNTIME_BINDING ',1)[1])[0];path=item['path'];pdb=item['pdbPath'];pe=identities.get(path)
  issues=[]
  if freeze['assemblies_sha256'].get(path)!=item['sha256'].lower():issues.append('DLL differs from executed freeze')
  if pdb and freeze['assemblies_sha256'].get(pdb)!=item['pdbSha256'].lower():issues.append('PDB differs from executed freeze')
  if pe is None:issues.append('No PE/PDB/source binding')
  elif pe['mvid']!=item['mvid'] or not pe['identityMatches']:issues.append('MVID or PE/PDB mismatch')
  if Path(path).name in expected and item['sha256'].lower()!=expected[Path(path).name]:issues.append('SDK image differs from qualified handoff')
  if pdb and Path(pdb).name in expected and item['pdbSha256'].lower()!=expected[Path(pdb).name]:issues.append('SDK PDB differs from qualified handoff')
  row=dict(lane=lane,**item,issues=issues);rows.append(row)
  if issues:errors.append(row)
  if item['resource']=='folders':
   proc=Path('/proc')/str(item['processId'])/'maps'
   if proc.exists():
    for entry in proc.read_text().splitlines():
     if 'Task36FoldObserver.dll' not in entry:continue
     path=entry[entry.index('/'):];key=(item['processId'],path)
     if any(x['processId']==key[0] and x['path']==key[1] for x in observed):continue
     actual=hashlib.sha256(Path(path).read_bytes()).hexdigest();ok=actual==freeze['assemblies_sha256'].get(path)
     observed.append(dict(processId=key[0],path=path,sha256=actual,matches=ok))
     if not ok:errors.append(observed[-1])
assert rows,'No actual mapped-module bindings observed.'
modules={}
for row in rows:
 if Path(row['path']).name.startswith('Hexalith.EventStore.'):
  modules.setdefault(Path(row['path']).name,set()).add((row['sha256'],row['mvid'],row['pdbSha256']))
dest=out/(label+'.json');assert not dest.exists();dest.write_text(json.dumps(dict(observations=len(rows),rows=rows,observer_mappings=observed,sdk_modules={k:[dict(sha256=a,mvid=b,pdbSha256=c) for a,b,c in sorted(v)] for k,v in modules.items()},errors=errors,passed=not errors),indent=2)+'\n');print(dict(observations=len(rows),sdk_modules=len(modules),observer_mappings=len(observed),errors=len(errors)));assert not errors
