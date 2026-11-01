import json,hashlib,shutil,xml.etree.ElementTree as E
from pathlib import Path
from datetime import datetime,timezone
p=Path(__file__).parent;root=Path('/home/administrator/projects/hexalith/folders');packet=root/'_bmad-output/implementation-artifacts/evidence'/p.name
packet.mkdir(exist_ok=True)
sha=lambda q:hashlib.sha256(q.read_bytes()).hexdigest()
excluded={'legacy-canonical-intents.json'}
for q in sorted(p.glob('*.json')):
 if q.name in excluded:continue
 try:doc=json.loads(q.read_text())
 except json.JSONDecodeError:continue
 shutil.copyfile(q,packet/q.name)
results={}
for q in sorted(p.glob('*.xml')):
 try:doc=E.parse(q).getroot()
 except E.ParseError:continue
 results[q.name]={'assemblies':[{k:a.get(k) for k in ['name','total','passed','failed','errors','skipped','time']} for a in doc.findall('assembly')],
 'tests':[{k:t.get(k) for k in ['name','result','time','type','method']}|({'exceptionType':t.find('failure').get('exception-type')} if t.find('failure') is not None else {}) for t in doc.iter('test')],
 'privateXml':{'path':str(q),'sha256':sha(q)}}
(packet/'test-results.json').write_text(json.dumps(results,indent=2)+'\n')
private=[]
for q in sorted(p.iterdir()):
 if q.is_file() and (q.name.endswith('.raw.log') or q.suffix=='.xml' or q.name=='legacy-canonical-intents.json'):
  private.append({'path':str(q),'sha256':sha(q),'bytes':q.stat().st_size})
  if 'build' in q.stem and q.name.endswith('.raw.log'):
   lines=[line for line in q.read_text(errors='replace').splitlines() if any(term in line for term in ['Build succeeded','Build FAILED',' Error(s)',' Warning(s)',' error ','Time Elapsed'])]
   (packet/(q.name.removesuffix('.raw.log')+'.build.log')).write_text('\n'.join(lines)+'\n')
(packet/'private-raw-receipts.json').write_text(json.dumps(private,indent=2)+'\n')
for name in ['audit.py','summarize_live.py','live.py','run.py','run_lanes.py','prepare_packet.py']:
 shutil.copyfile(p/name,packet/name)
(packet/'.gitattributes').write_text('* -text\n')
(packet/'packet-generation.json').write_text(json.dumps({'generatedAtUtc':datetime.now(timezone.utc).isoformat(),'source':str(p),'packet':str(packet),'policy':'Raw runtime stdout, raw XML and synthetic canonical payload fixture bodies stay private in ignored artifacts. Sealed packet contains exact command receipts, source/runtime identities, safe summaries and cleanup evidence.'},indent=2)+'\n')
print(packet)
