"""Retain reconstructible source patches for one audited runtime phase."""
from pathlib import Path
import subprocess,hashlib,json,re,sys
root=Path("/home/administrator/projects/hexalith/folders");out=Path(__file__).parent;label=sys.argv[1];rows=[]
assert re.fullmatch(r"[a-z0-9-]+",label)
for repo,name in ((root,"folders"),(root/"references/Hexalith.EventStore","eventstore")):
 patch=subprocess.check_output(["git","diff","--binary","HEAD"],cwd=repo)
 names=subprocess.check_output(["git","ls-files","--others","--exclude-standard","-z"],cwd=repo).decode().split("\0");included=[]
 for relative in sorted(names):
  source=repo/relative
  if not relative or not source.is_file() or source.suffix not in {".cs",".md",".py",".txt",".json",".yaml",".yml"}:continue
  if relative.startswith(("references/","_bmad/render/","_bmad-output/implementation-artifacts/evidence/")):continue
  result=subprocess.run(["git","diff","--no-index","--binary","--","/dev/null",relative],cwd=repo,stdout=subprocess.PIPE)
  if result.returncode not in (0,1):raise RuntimeError(relative)
  patch+=result.stdout;included.append(relative)
 target=out/(label+"-"+name+"-source.patch");assert not target.exists();target.write_bytes(patch);target.chmod(0o600)
 rows.append({"repository":name,"path":str(target),"bytes":len(patch),"sha256":hashlib.sha256(patch).hexdigest(),"untrackedInputs":included})
target=out/(label+"-source-patches.json");assert not target.exists();target.write_text(json.dumps(rows,indent=2)+"\n");target.chmod(0o600)
print(json.dumps([{k:v for k,v in row.items() if k!="untrackedInputs"} for row in rows]))
