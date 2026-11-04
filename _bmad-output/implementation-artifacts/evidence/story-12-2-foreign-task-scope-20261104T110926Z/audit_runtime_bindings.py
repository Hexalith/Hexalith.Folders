"""Compare saved actual SDK runtime observations to the saved audited producer."""
from pathlib import Path
import hashlib,json
W=Path(__file__).parent
audit=W/'scope-compatibility-pdb-audit.json'
expected={}
for row in json.loads(audit.read_text()):
 if row.get('assemblyName','').startswith('Hexalith.EventStore'):
  expected.setdefault(row['assemblyName'],set()).add(row['sha256'].lower())
assert all(len(values)==1 for values in expected.values())
observer_expected=None
observer_audit=W/'stop-correlation-parent-runtime-pdb-audit.json'
if observer_audit.exists():
 matches=[r for r in json.loads(observer_audit.read_text()) if r.get('assemblyName')=='Task36FoldObserver']
 assert len(matches)==1
 observer_expected={'sha256':matches[0]['sha256'].lower(),'pdb_sha256':matches[0]['pdbSha256'].lower(),'mvid':matches[0]['mvid']}
results=[]
for path in sorted(W.glob('strict-foreign-scope-*-runtime.json')):
 d=json.loads(path.read_text());rows=[];issues=[]
 for kind,field in [('poll','poll_observations'),('fixture','fixture_bindings')]:
  for row in d.get(field,[]):
   p=row.get('path','');name=Path(p).stem
   if not name.startswith('Hexalith.EventStore') or not p.endswith('.dll'):continue
   h=row['sha256'].lower();match=name in expected and h in expected[name]
   observed={'kind':kind,'pid':row.get('pid',row.get('processId')),'resource':row.get('resource'),'path':p,'sha256':h,'matches_saved_audited_sdk_producer':match}
   rows.append(observed)
   if not match:issues.append(observed)
 phase=path.name[:-13]
 observer_rows=[]
 observer_visibility=None
 if observer_expected and int(phase.rsplit('-',1)[1])>=5:
  for row in d.get('poll_observations',[]):
   if Path(row.get('path','')).name!='Task36FoldObserver.dll':continue
   match=row['sha256'].lower()==observer_expected['sha256'] and row.get('pdb_sha256','').lower()==observer_expected['pdb_sha256']
   observed={'pid':row['pid'],'path':row['path'],'sha256':row['sha256'].lower(),'pdb_sha256':row.get('pdb_sha256','').lower(),'matches_current_qualified_observer':match}
   observer_rows.append(observed)
   if not match:issues.append(observed)
  eventstore_pids={r['processId'] for r in d.get('fixture_bindings',[]) if r.get('resource')=='eventstore' and r.get('processId')}
  missing=sorted(eventstore_pids-{r['pid'] for r in observer_rows})
  observer_visibility={'eventstore_fixture_pids':sorted(eventstore_pids),'mapped_observer_pids':sorted({r['pid'] for r in observer_rows}),'missing_mapped_observer_pids':missing,'actual_observer_mapping_complete':not missing,'limit':'Missing poll mappings are an observation gap, not a binary mismatch. Selected observer DLL/PDB/source are separately qualified; lifecycle emission proves execution of the opaque seam but does not supply an unrecorded module hash/MVID.'}
 results.append({'phase':phase,'saved_runtime_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'observations':rows,'observer_observations':observer_rows,'observer_runtime_visibility':observer_visibility,'issues':issues,'limit':'Actual saved process mappings and fixture assembly bindings only. A startup-only attempt supplies no application behavior result.'})
out={'saved_expected_audit_sha256':hashlib.sha256(audit.read_bytes()).hexdigest(),'expected_sdk_sha256':{k:next(iter(v)) for k,v in expected.items()},'expected_current_observer':observer_expected,'attempts':results,'all_saved_observations_match':not any(r['issues'] for r in results)}
(W/'actual-sdk-runtime-binding-audit.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps({'attempts':len(results),'actual_saved_sdk_observations':sum(len(r['observations']) for r in results),'issues':sum(len(r['issues']) for r in results)}))
assert out['all_saved_observations_match']
