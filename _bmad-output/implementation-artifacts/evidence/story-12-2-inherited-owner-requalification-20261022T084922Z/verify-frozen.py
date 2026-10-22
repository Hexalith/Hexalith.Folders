from pathlib import Path
import json,hashlib,sys
out=Path(__file__).parent;f=json.loads((out/(sys.argv[2] if len(sys.argv)>2 else 'accepted-source-freeze.json')).read_text());errors=[];counts={}
for group in ('files_sha256','assemblies_sha256'):
 counts[group]=len(f[group])
 for name,expected in f[group].items():
  p=Path(name);actual=hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
  if actual!=expected:errors.append(dict(group=group,path=name,expected=expected,actual=actual))
label=sys.argv[1];dest=out/(label+'.json');assert not dest.exists();dest.write_text(json.dumps(dict(counts=counts,errors=errors,passed=not errors),indent=2)+'\n');print(dict(counts=counts,errors=len(errors)));assert not errors
