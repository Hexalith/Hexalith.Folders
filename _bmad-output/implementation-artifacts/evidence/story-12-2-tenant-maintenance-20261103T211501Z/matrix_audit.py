"""Rebind the inspected frozen-matrix mapping to actually executed component cases."""
from pathlib import Path
import hashlib,json
W=Path(__file__).parent;root=Path('/home/administrator/projects/hexalith/folders')
prior=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-source-head-and-mutation-budget-20261103T140301Z/audit/matrix-test-audit.json'
rows=json.loads(prior.read_text())['matrix_rows'];phase='admission-domain-tests' if (W/'admission-domain-tests-receipt.json').exists() else 'final-cohort-domain-controls-2' if (W/'final-cohort-domain-controls-2-receipt.json').exists() else 'repaired-domain-tests';source=W/(phase+'-receipt.json');tests=json.loads(source.read_text())['summary']['tests']
for row in rows:
 row['component_coverage_passed']=True
 for method in row['covering_methods']:
  cases=[t for t in tests if t.get('type')==method['type'] and t.get('method')==method['method']]
  assert cases and all(t['result']=='Pass' for t in cases),(row['row'],method['method'])
  method['passed_cases']=len(cases);method['display_names_sha256']=[hashlib.sha256(t['name'].encode()).hexdigest() for t in cases]
capacity=[]
for count in [10001,32769]:
 cases=[t for t in tests if t.get('type','').endswith('.OperationalStatusCapacityTests') and t.get('method')=='CompleteOriginalOwnersRebuildGrowAndPreserveHistoricalTasksWithBoundedValues' and 'count: '+str(count)+')' in t['name']]
 assert len(cases)==1 and cases[0]['result']=='Pass',count
 capacity.append({'count':count,'type':cases[0]['type'],'method':cases[0]['method'],'result':'Pass','time':cases[0]['time'],'display_name_sha256':hashlib.sha256(cases[0]['name'].encode()).hexdigest()})
d={'scope':'Executed repaired-producer component coverage of frozen matrix. Original full strict live criterion remains unsatisfied. Source maps and runtime identities bind to each exact phase, not to later externally edited source. No skipped case used.','source_receipt_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'phase':phase,'matrix_rows':rows,'all_component_rows_passed':True,'required_capacity_components':capacity,'capacity_composition':'Real Folders handler and production dispatcher through supported SDK protocol DTOs with enforced batch implementation; this is component evidence. Separate historical SDK Dapr process coverage is not relabeled as current Folders HTTP. An extra32769 Folders HTTP case is not required by the recorded2026-10-21 clarification.','whole_story_complete':False}
(W/'matrix-test-audit.json').write_text(json.dumps(d,indent=2)+'\n');print(json.dumps({'matrix_rows':len(rows),'capacity_counts':[x['count'] for x in capacity],'all_component_rows_passed':True,'whole_story_complete':False}))
