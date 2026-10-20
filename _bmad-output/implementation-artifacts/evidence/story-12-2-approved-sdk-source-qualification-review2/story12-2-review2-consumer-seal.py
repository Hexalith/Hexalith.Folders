from pathlib import Path
import json,hashlib,shutil,datetime,re,subprocess,xml.etree.ElementTree as E
root=Path('/home/administrator/projects/hexalith/folders');rel=Path('_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-review2');p=root/rel
assert not p.exists();p.mkdir(parents=True)
for f in Path('/tmp').glob('story12-2-sdk-review2-consumer-*'):
 if f.is_file():shutil.copy2(f,p/f.name)
for name in ('story12-2-sdk-review2-independent-audit.json','story12-2-sdk-review2-independent-worker-audit.json','story12-2-review2-historical-packet-audit.json'):
 shutil.copy2(Path('/tmp')/name,p/name)
for name in ('story12-2-sdk-review2-consumer-final-verify.py','story12-2-review2-consumer-provenance.py','story12-2-review2-consumer-seal.py'):
 shutil.copy2(Path('/tmp')/name,p/name)
(p/'.gitattributes').write_text('* -text\n')
old=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-approved-sdk-source-qualification-20261020'
q=json.loads((old/'qualification.json').read_text());components=json.loads((p/'story12-2-sdk-review2-consumer-final-components.json').read_text());assert all(x['exit_code']==0 for x in components)
passed=sum(int(a['passed']) for c in components for a in c.get('assemblies',[]));skips=sum(int(a['skipped']) for c in components for a in c.get('assemblies',[]))
integration=(p/'story12-2-sdk-review2-consumer-final-integration.log').read_text();m=re.search(r'Passed:\s*(\d+)',integration)
if not m:m=re.search(r'(?:passed|succeeded):\s*(\d+)',integration,re.I)
assert m,integration[-2500:];passed+=int(m[1]);assert passed==4195 and skips==2,(passed,skips)
live=E.parse(p/'story12-2-sdk-review2-consumer-live.xml').getroot();a=list(live.iter('assembly'))[0];assert a.get('passed')=='1' and a.get('failed')=='0' and a.get('skipped')=='0'
log=(p/'story12-2-sdk-review2-consumer-live.log').read_text(errors='replace');metrics=[]
for match in re.finditer(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([\d.]+) p95_ms=([\d.]+) budget_p95_ms=([\d.]+) over_budget=(True|False)',log):
 name,count,p50,p95,budget,over=match.groups();metrics.append({'operation':name,'count':int(count),'p50_ms':float(p50),'p95_ms':float(p95),'budget_p95_ms':float(budget),'over_budget':over=='True'})
assert len(metrics)==3
(p/'performance.json').write_text(json.dumps(metrics,indent=2)+'\n')
clean=[]
for cmd in [['aspire','ps','--format','Json'],['docker','ps','--format','{{.Names}}']]:
 r=subprocess.run(cmd,cwd=root,text=True,capture_output=True);assert not r.returncode,r.stderr;clean.append({'command':cmd,'exit_code':r.returncode,'stdout':r.stdout,'stderr':r.stderr})
assert json.loads(clean[0]['stdout'])==[];assert set(clean[1]['stdout'].split())=={'dapr_placement','dapr_scheduler','dapr_redis','dapr_zipkin'}
(p/'cleanup.json').write_text(json.dumps(clean,indent=2)+'\n')
source=json.loads((p/'story12-2-sdk-review2-consumer-final-source.json').read_text());sdkaudit=json.loads((p/'story12-2-sdk-review2-independent-audit.json').read_text());builds=json.loads((p/'story12-2-sdk-review2-consumer-builds.json').read_text())
q.update(recorded_at_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),status='Approved SDK review-two corrections and Folders source adoption locally qualified; pending independent review. Whole Story12.2 remains in progress.',sdk_packet='../../../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review2/qualification.json')
q['sdk_results']={'client_passed':1756,'client_optional_skips':2,'domain_default_parallel':{'passed':1056,'failed':1,'skipped':0},'domain_serial_same_binary':{'passed':1057,'failed':0,'skipped':0},'unique_live_cases':51,'expiry_scratch_unique_case_union':37,'final_child_workers_passed':12,'required_live_skips':0,'independent_audit':'story12-2-sdk-review2-independent-audit.json','independent_raw_worker_audit':'story12-2-sdk-review2-independent-worker-audit.json','note':'Failed parallel Domain and initial live34/37 attempts remain in SDK packet; same-binary serialized Domain and affected final live9/9 qualify recorded union, not a nonexistent single all37 pass.'}
q['consumer_results']={'components_passed':passed,'existing_optional_domain_skips':skips,'failures':0,'results':'story12-2-sdk-review2-consumer-final-components.json','live_passed':1,'live_failed':0,'live_skipped':0,'live_seconds':float(a.get('time')),'live_xml':'story12-2-sdk-review2-consumer-live.xml','live_log':'story12-2-sdk-review2-consumer-live.log'}
q['consumer_source']={'inventory':'story12-2-sdk-review2-consumer-final-source.json','files':len(source['files_sha256']),'assembly_files':len(source['assemblies_sha256']),'pdb_inspection':'story12-2-sdk-review2-consumer-pdb-source-inspection.json','pdb_audit':'story12-2-sdk-review2-consumer-pdb-audit.json','mismatches':0,'after_live_audit':'story12-2-sdk-review2-consumer-after-live-audit.json','note':'All captured source and62 assembly/PDB hashes remained unchanged through component and live runs. SDK test and consumer dependency graph PE/PDB groups are separately retained.'}
q['builds']=builds
q['checks']={'format_and_analyzer_source_carry':'story12-2-sdk-review2-consumer-format-carry.json','retained_qualification':'../story-12-2-approved-sdk-source-qualification-20261020/qualification.json','note':'All seven included C# files are byte-identical to previous format/analyzer-qualified source. New serialized source build is zero warnings/errors.'}
q['live_command']['command'][-1]='/tmp/story12-2-sdk-review2-consumer-live.xml'
for k in ('package_gate','contract_fallback'):
 q[k]['log']='../story-12-2-approved-sdk-source-qualification-20261020/'+q[k]['log'];q[k]['retained_for_unchanged_api_and_candidate']='WriteExpiring public API and frozen package/PD10 candidate inventory are unchanged; no package publication or requalification was authorized.'
q['historical_evidence'].update(all_seven_prior_stages_preserved=True,previous_consumer_packet_unchanged=True,historical_packet_audit='story12-2-review2-historical-packet-audit.json')
ack=next(x for x in metrics if x['operation']=='command_acknowledgement')
q['remaining_story_acceptance']=['SDK owner-identity capability beyond10000, qualified consumption and Task36 rerun','Release/package and frozen PD10 source-inventory requalification',f"SM4 acknowledgement p95:{ack['p95_ms']}ms versus1000ms on this run; earlier observations preserved",'Independent review pending for this approved SDK/consumer change at packet seal; whole-story review remains separate']
q['sdk_packet_sha256_manifest']=sdkaudit['manifest_sha256'];q['heads']=source['heads']
(p/'qualification.json').write_text(json.dumps(q,indent=2)+'\n')
(p/'components.json').write_text(json.dumps(components,indent=2)+'\n')
manifest=[]
for f in sorted(p.rglob('*')):
 if f.is_file() and f.name!='SHA256SUMS':manifest.append(hashlib.sha256(f.read_bytes()).hexdigest()+'  '+str(f.relative_to(p)))
(p/'SHA256SUMS').write_text('\n'.join(manifest)+'\n')
print(json.dumps({'packet':str(p),'artifacts':len(manifest),'components_passed':passed,'live_seconds':float(a.get('time')),'performance':metrics},indent=2))
