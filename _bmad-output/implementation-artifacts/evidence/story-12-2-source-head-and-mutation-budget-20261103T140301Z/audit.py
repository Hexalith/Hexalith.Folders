import hashlib, json, subprocess, sys
from pathlib import Path
ROOT=Path("/home/administrator/projects/hexalith/folders")
OUT=Path(__file__).parent
label=sys.argv[1]
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,data):
 p=OUT/name;p.write_text(json.dumps(data,indent=2)+"\n");p.chmod(0o600)
previous=json.loads((OUT/"selected-modules.json").read_text())
modules=sorted({str(p) for directory in {Path(p).parent for p in previous} for p in directory.glob("Hexalith.*.dll")})
write(label+"-selected-modules.json",modules)
result=subprocess.run(["dotnet",str(OUT/"pdb-tool/bin/Debug/net10.0/Inspect.dll"),str(OUT/(label+"-selected-modules.json"))],text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
rows=json.loads(result.stdout);write(label+"-pdb-audit.json",rows)
issues=[{k:r.get(k) for k in ("path","error","mismatches","missingOrdinarySource","pdbGuidMatches")} for r in rows if r.get("error") or r.get("mismatches") or r.get("missingOrdinarySource") or r.get("pdbGuidMatches") is False]
variants={}
for row in rows:
 if row.get("assemblyName", "").startswith("Hexalith.EventStore"):
  variants.setdefault(row["assemblyName"],set()).add((row["sha256"],row["mvid"]))
variant_failures={k:len(v) for k,v in variants.items() if len(v)!=1}
assets=[]
for item in json.loads((OUT/"selected-assets-audit.json").read_text()):
 path=Path(item["path"]);obj=json.loads(path.read_text());edges=[{"name":k,"type":v.get("type")} for k,v in obj["libraries"].items() if k.startswith("Hexalith.")]
 assets.append({"path":str(path),"sha256":digest(path),"hexalith_edges":edges})
asset_failures=[r["path"] for r in assets if any(e["type"]!="project" for e in r["hexalith_edges"])]
write(label+"-assets-audit.json",assets)
expected={"OperationalStatusDurabilityAcceptanceTests.cs":"f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90","FolderDurabilityAcceptanceTests.cs":"f5e8767ad02d57441dc3c46f1366e72eb77853fc9c6c0371555a73e1efd1babc","OperationalStatusBoundedHistoryAcceptanceTests.cs":"2ae3113138f54e35fa0137e72eabc0dd26f486159f418273a0b24b6e8687258d"}
bodies={k:digest(ROOT/"tests/Hexalith.Folders.AppHost.Tests"/k)==v for k,v in expected.items()}
spec=(ROOT/"_bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md").read_bytes();block=spec[spec.index(b"<frozen-after-approval"):spec.index(b"</frozen-after-approval>")+len(b"</frozen-after-approval>")]
frozen=digest_block=hashlib.sha256(block).hexdigest()
summary={"modules":len(rows),"source_or_pdb_issues":issues,"sdk_variant_failures":variant_failures,"asset_failures":asset_failures,"strict_bodies_unchanged":bodies,"frozen_sha256":frozen,"frozen_unchanged":frozen=="497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422"}
write(label+"-audit-summary.json",summary);print(json.dumps(summary))
if result.returncode or issues or variant_failures or asset_failures or not all(bodies.values()) or not summary["frozen_unchanged"]:sys.exit(1)
