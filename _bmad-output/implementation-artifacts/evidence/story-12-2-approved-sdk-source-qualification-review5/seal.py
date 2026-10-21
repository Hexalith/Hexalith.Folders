from pathlib import Path
import json,hashlib,shutil,re,datetime,gzip,subprocess,xml.etree.ElementTree as ET
root=Path('/home/administrator/projects/hexalith/folders');scratch=Path(__file__).parent
evidence=root/'_bmad-output/implementation-artifacts/evidence';packet=evidence/'story-12-2-approved-sdk-source-qualification-review5'
assert not packet.exists(), 'Never overwrite an existing qualification packet'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text())
components=load(scratch/'components.json');assert all(x['exit_code']==0 for x in components)
counts={x['name']:{k:int(x['assemblies'][0][k]) for k in ('total','passed','failed','skipped')} for x in components if x.get('assemblies')}
m=re.search(r'total:\s*(\d+).*?failed:\s*(\d+).*?succeeded:\s*(\d+).*?skipped:\s*(\d+)',(scratch/'integration.log').read_text(),re.S);assert m
counts['integration']={k:int(v) for k,v in zip(('total','failed','passed','skipped'),m.groups())};assert counts['integration']['failed']==0
live=load(scratch/'live-attempt-1-result.json');assert live['exit_code']==0
livecounts={k:int(live['assemblies'][0][k]) for k in ('total','passed','failed','skipped')};assert livecounts=={'total':1,'passed':1,'failed':0,'skipped':0}
livexml=ET.parse(scratch/'live-attempt-1.xml').getroot();liveoutput='\n'.join(x.text or '' for x in livexml.iter('output'))
latencies=[]
for m in re.finditer(r'PERFORMANCE (\w+): count=(\d+) p50_ms=([\d.]+) p95_ms=([\d.]+) budget_p95_ms=([\d.]+) over_budget=(True|False)',liveoutput):
 latencies.append(dict(operation=m[1],count=int(m[2]),p50_ms=float(m[3]),p95_ms=float(m[4]),budget_p95_ms=float(m[5]),over_budget=m[6]=='True'))
assert len(latencies)==3
for x in load(scratch/'after-execution-audit.json').values():assert not x
history=load(scratch/'historical-freeze.json');canonical=load(evidence/'story-12-2-durability.json')
prior=[hashlib.sha256(json.dumps(s,sort_keys=True,separators=(',',':')).encode()).hexdigest() for s in canonical['stages']]
assert prior==history['stages_sha256']
changed=[p for p,h in history['packet_files_sha256'].items() if not(root/p).exists() or sha(root/p)!=h];assert not changed
sdkpacket=root/'references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review5/qualification.json';sdk=load(sdkpacket)
packet.mkdir(parents=True);(packet/'.gitattributes').write_text('* -text\n')
for p in scratch.iterdir():
 if p.is_file() and p.name not in ('pdb-inspection.json','historical-freeze.json'):
  shutil.copyfile(p,packet/p.name)
for name in ('pdb-inspection.json','historical-freeze.json'):
 with gzip.GzipFile(filename=str(packet/(name+'.gz')),mode='wb',mtime=0) as f:f.write((scratch/name).read_bytes())
(packet/'diagnostics').mkdir()
for p in (scratch/'diagnostics').iterdir():
 target=packet/'diagnostics'/(p.name+'.gz')
 with gzip.GzipFile(filename=str(target),mode='wb',mtime=0) as f:f.write(p.read_bytes())
(packet/'performance.json').write_text(json.dumps(latencies,indent=2)+'\n')
(packet/'component-summary.json').write_text(json.dumps(counts,indent=2)+'\n')
(packet/'live-observations.json').write_text(json.dumps({'observations':[line for line in liveoutput.splitlines() if line.startswith(('PASS ','PERFORMANCE '))],'output_sha256':hashlib.sha256(liveoutput.encode()).hexdigest()},indent=2)+'\n')
(packet/'historical-audit.json').write_text(json.dumps({'prior_stages':len(prior),'prior_stages_canonical_sha256':prior,'prior_packet_files':len(history['packet_files_sha256']),'mismatches':changed},indent=2)+'\n')
freeze=load(scratch/'source-freeze.json');pdb=load(scratch/'pdb-audit.json');sdk_audit=load(scratch/'sdk-packet-source-audit.json')
qualification={'schema':'hexalith-folders-approved-sdk-source-qualification/v1','recorded_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'status':'Approved SDK review-five source consumption locally qualified; independent review and whole-story acceptance remain open.','full_acceptance':False,'approval':'Existing approved source-deadline expiry and pending scratch recovery scope; no additional release, Git or dependency work.','sdk_packet':'../../../../references/Hexalith.EventStore/_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review5/qualification.json','sdk_packet_sha256':sha(sdkpacket),'sdk_results':sdk['results'],'sdk_live_cases':sdk['unique_live_cases'],'sdk_worker_invocations':sdk['worker_invocations'],'sdk_source_audit':'sdk-packet-source-audit.json','consumer_results':{'components_passed':sum(x['passed'] for x in counts.values()),'components_failed':sum(x['failed'] for x in counts.values()),'existing_optional_domain_skips':counts['domain']['skipped'],'summary':'component-summary.json','commands':'components.json','live':livecounts,'live_seconds':float(live['assemblies'][0]['time']),'live_command':'live-attempt-1-result.json','live_xml':'live-attempt-1.xml','live_log':'live-attempt-1.log'},'consumer_source':{'inventory':'source-freeze.json','source_config_files':len(freeze['files_sha256']),'runtime_test_pe_pdb_files':len(freeze['assemblies_sha256']),'pdbs':len(pdb),'pdb_source_mismatches':sum(x['mismatches'] for x in pdb),'pdb_missing_sources':sum(len(x['missing_sources']) for x in pdb),'pdb_audit':'pdb-audit.json','full_pdb_documents':'pdb-inspection.json.gz','full_pdb_uncompressed_sha256':sha(scratch/'pdb-inspection.json'),'pe_pdb_identity':'pe-pdb-identity.json','sdk_runtime_groups':'sdk-runtime-groups.json','after_execution_audit':'after-execution-audit.json','note':'External commits advanced root and module revisions during build. Both Client and DomainService have two retained PE/PDB identity groups with identical complete current source-document sets; every exact output group is retained. SDK packet source/config hashes are unchanged.'},'builds':'builds.json','format_and_analyzers':'format.json','restoration':'restoration.json','matrix':'matrix-audit.json','performance':'performance.json','cleanup':'cleanup.json','external_changes':'source-freeze.json','historical_evidence':'historical-audit.json','package_pd10':{'status':'not requalified; separately owned','current_eventstore_catalog_version':'3.126.0','historical_gate':'../story-12-2-approved-sdk-source-qualification-20261020/qualification.json','note':'Earlier package CS0117 at3.125.1 and frozen PD10 failures remain exact-source history. Incoming Builds now pins3.126.0; no current Release/package result is inferred and no package/pin change was made by this task.'},'remaining_acceptance':['Task36: production captured-identity histories beyond10000 and qualified separate SDK consumption','Release/package and frozen PD10 requalification','SM4 performance acceptance; all previous over-budget observations remain open without causal attribution','Independent approved-scope and whole-story review'],'boundaries':['No retained event replacement, content/cleanup changes, publicv2 exposure, deployment, package publication or dependency mutation by this task.','No Git staging/commit/push/branch/submodule updates by this task; incoming external revisions preserved.','Production Redis remains Resumable; SDK forced transaction controls do not establish atomicity.'],'diagnostics':['diagnostics/overbroad-inventory-pdb-inspection.json.gz','baseline-summary.json'],'evidence_retention':{'rule':'* -text','manifest':'SHA256SUMS'}}
(packet/'qualification.json').write_text(json.dumps(qualification,indent=2)+'\n')
# Add one current stage; never change the ten historical stage objects.
stage={'stage':len(canonical['stages'])+1,'name':'Approved SDK review-five corrections and current merged Folders source qualification','date':'2026-10-20','full_acceptance':False,'qualification':packet.name+'/qualification.json','consumer_component_passed':qualification['consumer_results']['components_passed'],'consumer_existing_optional_skips':counts['domain']['skipped'],'live':{**livecounts,'seconds':qualification['consumer_results']['live_seconds']},'sdk':{'qualified_source_inputs':sdk_audit['sdk_packet_source_inputs'],'source_mismatches':len(sdk_audit['mismatches']),'results':sdk['results'],'unique_live_cases':sdk['unique_live_cases'],'worker_invocations':sdk['worker_invocations'],'distinct_worker_pids':sdk['distinct_worker_pids']},'performance':latencies,'independent_review':'pending for approved SDK/consumer scope; whole-story review separate','earlier_stages_canonical_sha256':prior}
canonical['stages'].append(stage);canonical['status']='Current merged approved SDK review-five source consumption locally qualified; whole Story12.2 remains in-progress.'
canonical['approved_sdk_review5'].update(status='source-consumption-locally-qualified-pending-independent-review',qualification=packet.name+'/qualification.json',current_root_head=freeze['root_head'],current_sdk_head=sdk_audit['current_head'])
for v in latencies:
 if v['over_budget']:canonical['open_acceptance'].append(f"Stage11 latency observation: {v['operation']} p95={v['p95_ms']}ms versus{v['budget_p95_ms']}ms; cause unclassified")
(evidence/'story-12-2-durability.json').write_text(json.dumps(canonical,indent=2)+'\n')
print(json.dumps({'packet':str(packet),'components_passed':qualification['consumer_results']['components_passed'],'live':livecounts,'latencies':latencies,'stage':stage['stage']},indent=2))
