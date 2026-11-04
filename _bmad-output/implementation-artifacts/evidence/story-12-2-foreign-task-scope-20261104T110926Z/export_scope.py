"""Export closed, payload-free Story12.2 qualification metadata; never raw logs/XML."""
from pathlib import Path
import hashlib,json,shutil,sys,xml.etree.ElementTree as ET

W=Path(__file__).parent
P=Path(sys.argv[1]).resolve()
assert not P.exists()
P.mkdir(parents=True)
(P/'.gitattributes').write_text('** -text -whitespace\n')

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,obj):
 p=P/name;p.parent.mkdir(parents=True,exist_ok=True)
 p.write_text(json.dumps(obj,indent=2)+'\n')
def safe_tests(p):
 r=ET.parse(p).getroot()
 return {'assemblies':[{k:v for k,v in a.attrib.items() if k in {'name','total','passed','failed','skipped','errors','time','run-date','run-time'}} for a in r.iter('assembly')],
 'tests':[{**{k:v for k,v in t.attrib.items() if k in {'type','method','result','time'}},'case_ordinal':i,'display_name_sha256':hashlib.sha256(t.get('name','').encode()).hexdigest()} for i,t in enumerate(r.iter('test'),1)]}

labels=[]
for p in sorted(W.glob('*-receipt.json')):
 label=p.name[:-13];labels.append(label);d=json.loads(p.read_text())
 for i,t in enumerate((d.get('summary') or {}).get('tests',[]),1):
  t['display_name_sha256']=hashlib.sha256(t.pop('name','').encode()).hexdigest();t['case_ordinal']=i
 c=d.get('cleanup',{})
 if 'remaining_unattributed_containers' in c:c['remaining_unattributed_container_count']=len(c.pop('remaining_unattributed_containers'))
 d['original_private_receipt_sha256']=sha(p);write('commands/'+p.name,d)
 for phase in ['before','after']:
  f=W/(label+'-'+phase+'.json')
  if not f.exists():continue
  o=json.loads(f.read_text());o.pop('containers',None);o.pop('directories',None)
  for repo in o.get('repos',[]):
   tree=repo.pop('sources');digest=hashlib.sha256(json.dumps(tree,sort_keys=True,separators=(',',':')).encode()).hexdigest();name='source/trees/'+digest+'.json'
   if not (P/name).exists():write(name,tree)
   repo['source_tree']={'path':name,'canonical_sha256':digest,'file_count':len(tree)}
  write('source/'+f.name,o)
 f=W/(label+'-runtime.json')
 if f.exists():
  o=json.loads(f.read_text());raw=(W/(label+'-private.log')).read_text(errors='replace')
  o['containers']=[c for c in o.get('containers',[]) if c['Names'] in raw]
  write('runtime/'+f.name,o)
 f=W/(label+'-observations.log')
 if f.exists():shutil.copy2(f,P/f.name)

for pattern in ['*-pdb-audit.json','*-assets-audit.json','*-audit-summary.json','*-selected-modules.json','*-source-patches.json','*-owned-cleanup-audit.json','*-host-context-*.json','*-closed-diagnosis-private.json','*-closed-startup-diagnosis-private.json','*-closed-stop-diagnosis-private.json','*-closed-opaque-diagnosis-private.json','*-observer-identity-limit-supplement-private.json','*-closed-diagnosis.json','*-extra-environment.json','approval.json','qualification-summary.json','matrix-test-audit.json','prior-packet-preservation.json','scope-compatibility-output-hashes.json','scope-compatibility-before-hashes.json','implementation-before-hashes.json','implementation-guards-before.json','actual-sdk-runtime-binding-audit.json','scope-sdk-optional-and-abi-audit.json','stop-correlation-output-hashes.json','stop-correlation-before-hashes.json']:
 for f in sorted(W.glob(pattern)):write('audit/'+f.name,json.loads(f.read_text()))
for f in sorted(W.glob('*-lifecycle-private.jsonl')):
 write('audit/'+f.stem.replace('-private','')+'.json',{'original_private_sha256':sha(f),'records':[json.loads(line) for line in f.read_text().splitlines()]})
for f in sorted(W.glob('*-source.patch')):
 (P/'patches').mkdir(exist_ok=True);shutil.copy2(f,P/'patches'/f.name)
for name in ['handoff.json','scope-compatibility-handoff.json','stop-correlation-handoff.json']:
 f=W/name
 if f.exists():write('focused/'+name,json.loads(f.read_text()))
for f in sorted(W.glob('*-private.xml')):
 if not (W/(f.name[:-12]+'-receipt.json')).exists():write('focused/'+f.stem+'.json',safe_tests(f))
for name in ['implementation-phase.patch','scope-compatibility-phase.patch','stop-correlation-phase.patch']:
 f=W/name
 if f.exists():
  (P/'patches').mkdir(exist_ok=True);shutil.copy2(f,P/'patches'/name)
for name in ['qualify.py','audit.py','audit_with_observer.py','source_patch.py','record_cleanup.py','stages.py','matrix_audit.py','export_scope.py','verify_scope.py','privacy_scan.py','prepare_runtime.py','run_strict.py','run_strict_2.py','run_strict_3.py','run_strict_4.py','run_strict_5.py','post_scope.py','sdk_broad.py','summarize_scope.py','audit_runtime_bindings.py','write_packet_readme.py']:
 f=W/name
 if f.exists():shutil.copy2(f,P/name)
(P/'pdb-tool').mkdir()
for name in ['Program.cs','Inspect.csproj']:shutil.copy2(W/'pdb-tool'/name,P/'pdb-tool'/name)
D=Path('/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts')
(P/'decisions').mkdir()
for name in ['proposal-12-2-foreign-task-scope-proof.md','proposal-12-2-foreign-task-scope-proof.patch','spec-12-2-durable-projections-and-task-completion-pipeline.md']:shutil.copy2(D/name,P/'decisions'/name)
write('export-index.json',{'phases':labels,'raw_logs_exported':False,'raw_xml_exported':False,'scope':'Exact approved foreign-task scope correction, current runtime qualification and separately retained startup/compatibility outcomes.'})
print(json.dumps({'packet':str(P),'phases':len(labels)}))
