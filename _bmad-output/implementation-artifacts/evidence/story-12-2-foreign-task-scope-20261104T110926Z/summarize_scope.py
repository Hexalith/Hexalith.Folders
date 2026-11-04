"""Describe closed qualification phases without adding overlapping selections."""
from pathlib import Path
import hashlib,json,re
W=Path(__file__).parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
phases={p.name[:-13]:json.loads(p.read_text()) for p in sorted(W.glob('*-receipt.json'))}
def suite(label):
 d=phases[label]
 return {'phase':label,'private_receipt_sha256':sha(W/(label+'-receipt.json')),'exit_code':d['exit_code'],'assemblies':(d.get('summary') or {}).get('assemblies',[]),'failures':[{k:t[k] for k in ['type','method']} for t in (d.get('summary') or {}).get('tests',[]) if t['result']=='Fail'],'source_or_head_drift':d['source_or_head_drift'],'runtime_file_drift':d['runtime_file_drift']}
strict=[label for label in phases if label.startswith('strict-foreign-scope-')]
passed=any(phases[label]['exit_code']==0 for label in strict)
observations={label:(W/(label+'-observations.log')).read_text().splitlines() for label in strict}
metrics=[]
for label,lines in observations.items():
 for line in lines:
  m=re.fullmatch(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([0-9.]+) p95_ms=([0-9.]+) budget_p95_ms=([0-9.]+) over_budget=(True|False)',line)
  if m:metrics.append({'phase':label,'operation':m[1],'count':int(m[2]),'p50_ms':float(m[3]),'p95_ms':float(m[4]),'budget_p95_ms':float(m[5]),'over_budget':m[6]=='True'})
package='scope-release-contract-spine-gate'
diagnosis=None
if package in phases:
 raw=(W/(package+'-private.log')).read_text(errors='replace')
 errors=sorted(set(re.findall(r'error ([A-Z]+\d+): ([^\r\n]+)',raw)))
 diagnosis={'phase':package,'exit_code':phases[package]['exit_code'],'error_codes_and_messages':[{'code':code,'message':message} for code,message in errors], 'source_lane_is_separate':True,'package_pin_or_publication_changed':False}
 (W/(package+'-closed-diagnosis.json')).write_text(json.dumps(diagnosis,indent=2)+'\n')
q={
 'status':'in-progress','workflow_step':'step-03-implement','record_scope':'Closed implementation qualification; independent review advances separately in the spec.','whole_story_complete':False,'full_acceptance':False,
 'approval':{'date':'2026-11-04','owner_reply':'approve','original_strict_sha256':'f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90','approved_strict_sha256':'d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82','exact_patch_sha256':'c832f58f6c1b05516c1b62f97e2bc8a820d6ed2b4a51f6006952db28a16d7856','production_evidence_or_policy_changed':False},
 'implementation_handoff_sha256':sha(W/'handoff.json'),
 'fixture_compatibility_handoff_sha256':sha(W/'scope-compatibility-handoff.json') if (W/'scope-compatibility-handoff.json').exists() else None,
 'stop_correlation_handoff_sha256':sha(W/'stop-correlation-handoff.json') if (W/'stop-correlation-handoff.json').exists() else None,
 'runtime_audits':[p.name for p in sorted(W.glob('*-audit-summary.json'))],
 'phase_results':[suite(label) for label in phases],
 'strict_attempts':[suite(label) for label in strict],
 'strict_observations':observations,'revised_strict_passed':passed,
 'sm4_measurements':metrics,
 'observer_loaded_identity_limit':'Selected candidate DLL/PDB/source are qualified and actual opaque behavior/type/simple assembly name is recorded; loaded Tool path/MVID/SHA are not separately captured. SDK actual observations match; missing Tool mappings are not a mismatch.',
 'strict_attribution':{'strict-foreign-scope-1':'Scheduler host health timeout before bootstrap/body; no permanent mismatch established.','strict-foreign-scope-2':'Same-code startup and bootstrap passed; human command rejected by existing scope guard because fixture sent tenant header. Fixture-only compatibility corrected separately; no probe outcome.'},
 'approved_probe_milestone_passed':any(any(line.startswith('PASS real-provider task pipeline:') for line in lines) for lines in observations.values()),
 'matrix_component_audit':json.loads((W/'matrix-test-audit.json').read_text()),
 'package_gate_diagnosis':diagnosis,
 'open_gates':{'release_package':'Actual current command result is retained separately; no package publication/pin changes.','pd10_governance':'Dated immutable manifest unchanged, only pending source inventory may reflect changed compile inputs; publicv2 remains disabled.','sm4':'Use exact recorded performance samples; exceeding original budgets remains an open finding, with no performance redesign.','independent_review':'Step04 has not been entered; whole-story completion is not claimed.','optional_lanes':'Configured optional skips remain separately unqualified; no skipped case supplies required matrix coverage.'},
 'prior_packet_preservation':json.loads((W/'prior-packet-preservation.json').read_text()),
 'evidence_privacy':{'raw_logs_exported':False,'raw_xml_exported':False,'parameter_display_payloads_exported':False},
 'preserved':{'frozen_sha256':'497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422','dated_pd10_sha256':'6289c143af409f72e53aff555310d37faa25da0bb852488fe9a2f5ab7196a286','no_staging_commit_push_branch_pin_publication_or_deployment':True},
 'counting':'Each suite is listed separately; focused selections overlap and are never added to broad counts. Source/runtime receipts determine exact attribution.'}
(W/'qualification-summary.json').write_text(json.dumps(q,indent=2)+'\n')
print(json.dumps({'phases':len(phases),'strict_attempts':len(strict),'revised_strict_passed':passed,'whole_story_complete':False}))
