from pathlib import Path
import json,hashlib,re,xml.etree.ElementTree as ET
out=Path(__file__).resolve().parent
sha=lambda s:hashlib.sha256(s.encode()).hexdigest()
def result(label):
 p=out/(label+'.xml');cmd=out/(label+'.json')
 if not p.exists():return dict(status='not-complete')
 a=list(ET.parse(p).getroot().iter('assembly'));stats={k:sum(int(x.get(k,'0')) for x in a) for k in ('total','passed','failed','skipped','errors')}
 stats['pass']=stats['total']>0 and stats['failed']==0 and stats['errors']==0
 if cmd.exists():stats['command']=json.loads(cmd.read_text())
 return stats
log=(out/'capacity-final-live.log').read_text();events=[];sessions=[];replacements=[];phases=[]
for line in log.splitlines():
 for marker,target in [('TASK36_EVIDENCE ',events),('TASK36 session=',sessions)]:
  if marker+'{' in line:target.append(json.JSONDecoder().raw_decode(line.split(marker,1)[1])[0])
 if 'CAPACITY process-replacement' in line:
  m=re.search(r'resource=(\S+) before=(\d+) after=(\d+)',line)
  if m:replacements.append(dict(resource=m[1],before=int(m[2]),after=int(m[3])))
 if 'CAPACITY phase=' in line:phases.append(line.split('CAPACITY ',1)[1])
lookup={e['phase']:e for e in events};before=lookup.get('before-inheritance');after=lookup.get('after-inherited-replay')
proof=None
if before and after:
 proof=dict(raw_checkpoint_sha256_before=sha(before['rawCheckpoint']),raw_checkpoint_sha256_after=sha(after['rawCheckpoint']),raw_checkpoint_equal=before['rawCheckpoint']==after['rawCheckpoint'],reference_equal=before['checkpoint']==after['checkpoint'],canonical_sha256_before=before['canonicalSha256'],canonical_sha256_after=after['canonicalSha256'],canonical_equal=before['canonicalSha256']==after['canonicalSha256'],captured_identity_sha256=before['capturedIdentitySha256'],fold_callback_delta=after['foldCallbackDelta'],canonical_origin=before['canonicalOrigin'],current_capture=after['epoch']['captureHighWatermarks'])
cap=result('capacity-final-live');strict=result('strict-live')
cap_pass=cap.get('pass',False) and cap.get('total')==1 and cap.get('skipped')==0
strict_pass=strict.get('pass',False) and strict.get('total')==1 and strict.get('skipped')==0
runtime=json.loads((out/'runtime-audit-final.json').read_text()) if (out/'runtime-audit-final.json').exists() else None
managed={}
if runtime:
 for row in runtime['rows']:
  if row['lane']=='capacity-final-live':managed.setdefault(row['resource'],set()).add(row['processId'])
managed={k:sorted(v) for k,v in managed.items()}
summary=dict(task36_closure_pass=cap_pass and strict_pass,corrected_inherited_owner_live_case_pass=cap_pass,strict_consumer_pass=strict_pass,capacity=cap,strict=strict,component_results={k:result(k) for k in ['sdk-focused','sdk-full','sdk-domain-full','final-consumer-components','final-apphost-helpers']},original_rebuild_sessions=sessions,phase_results=phases,launcher_replacements=replacements,managed_processes=managed,retained_inherited_proof=proof,events=events,fold_observations=[json.loads(line) for line in (out/'capacity-final-fold-callbacks.jsonl').read_text().splitlines()],remaining_gates=dict(consumer='Strict real-provider lane failed waiting for BranchRefPolicyConfigured with internal actor HTTP429/admission-unavailable directly observed; no terminal admission state inferred. Broader current-source Server/Workers/integration suites are outside this narrow rerun.',package_PD10='No publication or dependency pin change. Package/Release and PD10 qualification remain open.',SM4='Acknowledgement performance remains open; synthetic projection timings do not establish the SM4 budget.',whole_story='Story12.2 and its independent review/whole-story acceptance remain open. Root Task36 checkbox is unchanged.'))
(out/'qualification.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(dict(task36_closure_pass=summary['task36_closure_pass'],corrected_inherited_owner_live_case_pass=cap_pass,strict_consumer_pass=strict_pass,capacity=cap,proof=proof),indent=2))
