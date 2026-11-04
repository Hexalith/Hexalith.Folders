"""Check portable export shape and recognizable secret material without printing it."""
from pathlib import Path
import re,json,sys,hashlib
P=Path(sys.argv[1]).resolve()
patterns={
 'jwt':re.compile(rb'eyJ[A-Za-z0-9_-]{6,}\.eyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{10,}'),
 'private_key':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'),
}
findings=[];count=0;size=0
for p in sorted(P.rglob('*')):
 if not p.is_file() or p.name in {'seal.json','privacy-scan.json'}:continue
 data=p.read_bytes();count+=1;size+=len(data)
 for name,pattern in patterns.items():
  for m in pattern.finditer(data):findings.append({'path':p.relative_to(P).as_posix(),'kind':name,'value_sha256':hashlib.sha256(m.group()).hexdigest()})
 assert not p.name.endswith('-private.log') and not p.name.endswith('-private.xml'),p.name
 if p.parent.name=='commands':
  d=json.loads(data)
  for case in (d.get('summary') or {}).get('tests',[]):assert 'name' not in case and 'display_name_sha256' in case,p.name
result={'files_scanned':count,'bytes_scanned':size,'recognizable_secret_findings':findings,'raw_logs_and_xml_excluded':True,'command_parameter_display_payloads_removed':True,'recognizable_secret_patterns':list(patterns),'scope':'Exported static test identifiers, hashes, source paths, module/process/cleanup metadata and structured closed diagnoses. Private raw traces/XML remain outside the packet.'}
(P/'privacy-scan.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'files_scanned':count,'bytes_scanned':size,'secret_findings':len(findings)}))
if findings:sys.exit(1)
