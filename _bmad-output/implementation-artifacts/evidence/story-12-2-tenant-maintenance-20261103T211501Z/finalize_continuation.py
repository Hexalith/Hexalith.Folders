"""Summarize only closed commands, without summing overlapping selections."""
from pathlib import Path
import hashlib,json
W=Path(__file__).parent

def load(name):return json.loads((W/name).read_text())
def suite(label):
 d=load(label+'-receipt.json')
 return {'phase':label,'receipt_sha256':hashlib.sha256((W/(label+'-receipt.json')).read_bytes()).hexdigest(),'exit_code':d['exit_code'],'assemblies':(d.get('summary') or {}).get('assemblies',[]),'failures':[{k:t[k] for k in ['type','method']} for t in (d.get('summary') or {}).get('tests',[]) if t['result']=='Fail'],'source_or_head_drift':d['source_or_head_drift'],'runtime_file_drift':d['runtime_file_drift']}
def drift_assessment():
 rows=[]
 for p in sorted(W.glob('*-receipt.json')):
  d=json.loads(p.read_text());label=p.name[:-13]
  if not d['source_or_head_drift']:continue
  a=load(label+'-before.json');b=load(label+'-after.json');before={x['root']:x for x in a['repos']};changes=[]
  for y in b['repos']:
   x=before.get(y['root'])
   if x is None:changes.append({'repository':y['root'],'new_repository':True});continue
   changed=[{'path':k,'sha256_before':x['sources'].get(k),'sha256_after':y['sources'].get(k)} for k in sorted(x['sources'].keys()|y['sources'].keys()) if x['sources'].get(k)!=y['sources'].get(k)]
   if changed or x['head']!=y['head']:changes.append({'repository':y['root'],'head_before':x['head'],'head_after':y['head'],'changed_sources':changed})
  rows.append({'phase':label,'changes':changes,'runtime_file_drift':d['runtime_file_drift'],'disposition':'Executed built producer only; external revision and source transitions preserved; not a claim that current working source matches binaries.' if any(r.get('changed_sources') for r in changes) else 'External HEAD-only transition; captured source maps unchanged.'})
 return rows
consumers=['admission-domain-tests','admission-server-tests','admission-workers-tests','admission-integration-tests','admission-gateway-tests','admission-contracts-tests','admission-client-generation-tests']
labels=sorted(p.name[:-13] for p in W.glob('*-receipt.json'))
strict=[x for x in labels if x.startswith('strict-tenant-maintenance-')]
sdk=['admission-sdk-server-tests','sdk-client-tests','sdk-domainservice-tests','sdk-contracts-tests']
result={
 'status':'in-progress','workflow_step':'step-03-implement','whole_story_complete':False,'full_acceptance':False,
 'approved_fixture_maintenance':{'exact_disposable_administrator_permission':'tenants:update','foreground_native_source_age_trigger_seconds':120,'whole_operation_budget_seconds':90,'production_freshness_seconds':300,'mutation_evidence_budget_seconds':2,'live_control':suite('live-tenant-maintenance-1'),'standalone_live_qualified':load('live-tenant-maintenance-1-receipt.json')['exit_code']==0,'full_strict_replacement':False},
 'consumer_phase_selections':[suite(x) for x in consumers if x in labels],
 'sdk_broad_phase_selections':[suite(x) for x in sdk if x in labels],
 'sdk_failure_rechecks':{'test_secret_scanner':'Prior story-owned scanner inputs repaired275focused passes; latest broad scanner finds one external Admin test bearer literal. Latest hygiene handoff retains generated-value correction and unchanged274case scanner recheck; broad failure remains historical.','public_package':suite('official-published-abi-tests') if 'official-published-abi-tests' in labels else None,'public_package_origin':load('published-package-origin-observation.json'),'whole_sdk_broad_green_claimed':False},
 'strict_attempts':[suite(x) for x in strict],
 'source_transition_assessments':drift_assessment(),
 'pre_live_runtime_audit':load('repaired-runtime-audit-summary.json'),
 'frozen_matrix_component_audit':load('matrix-test-audit.json'),
 'latest_source_runtime_audit':load('continuation-final-audit-summary.json'),
 'test_hygiene_handoff':json.loads(Path('/tmp/story12-2-broad-hygiene-20261103/handoff.json').read_text()) if Path('/tmp/story12-2-broad-hygiene-20261103/handoff.json').exists() else None,
 'strict_body_decision':{'status':'pending','proposal':'proposal-12-2-foreign-task-scope-proof.md','patch_applied':False,'strict_original_sha256':'f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90'},
 'admission_rebase_handoff':json.loads(Path('/tmp/story12-2-admission-rebase-20261103/handoff.json').read_text()) if Path('/tmp/story12-2-admission-rebase-20261103/handoff.json').exists() else None,
 'release_package_gate':suite('current-release-contract-spine-gate') if 'current-release-contract-spine-gate' in labels else None,
 'release_package_diagnosis':load('current-release-contract-spine-gate-closed-diagnosis.json'),
 'latest_focused_rechecks':[suite(x) for x in ['final-cohort-domain-controls-2','admission-reader-tests','admission-fixture-tests'] if x in labels],
 'open_gates':{'strict_live':'Latest unchanged strict4 passes restart/task persistence/replay, then fails unsupported absent-folder scope365: expected404,actual503. Owner decision pending on explicit two-probe contract correction; original body still unchanged.','sdk_optional_and_red_lanes':'Existing 30 skipped cases remain separately unqualified; no skips added.','release_package':'See actual current exact gate receipt and safe diagnosis. No package pin changes authorized or made.','pd10_governance':'Only pending compile inventory refreshed; original dated manifest unchanged; public v2 disabled.','sm4':'No current full strict completion establishes SM4 acceptance.','independent_review':'Step04 not entered while step03 acceptance criteria remain unsatisfied.'},
 'preserved':{'frozen_approval_and_original_strict_capacity_bodies':True,'prior_sealed_packets':True,'no_git_mutation_by_this_continuation':True,'external_git_activity_preserved':True,'no_pin_update_publication_deployment':True,'no_manual_shared_package_cache_edits':True},
 'evidence_privacy':{'raw_logs_exported':False,'raw_xml_exported':False,'parameter_display_payloads_exported':False,'static_test_type_method_result_time_and_display_name_hash_exported':True},
 'commands':labels,
 'attribution':'All counts bind to individual command/module/source receipts. Focused rechecks overlap broad suites and are not added to them. External source transitions are explicit; old built outputs are never described as newly rebuilt source.'
}
(W/'all-closed-phase-drift-assessment.json').write_text(json.dumps(result['source_transition_assessments'],indent=2)+'\n')
(W/'qualification-summary.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'phases':len(labels),'strict_attempts':len(strict),'maintenance_live_qualified':result['approved_fixture_maintenance']['standalone_live_qualified'],'whole_story_complete':False}))
