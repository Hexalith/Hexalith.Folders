import hashlib, json, subprocess, sys
from pathlib import Path
ROOT=Path("/home/administrator/projects/hexalith/folders")
OUT=Path(__file__).parent
label=sys.argv[1]
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name,data):
 p=OUT/name;p.write_text(json.dumps(data,indent=2)+"\n");p.chmod(0o600)
previous=json.loads((OUT/"selected-modules.json").read_text())
observer=ROOT/"tests/tools/Task36FoldObserver/bin/Debug/net10.0/Task36FoldObserver.dll"
previous=[*previous,str(observer)]
modules=sorted({str(p) for directory in {Path(p).parent for p in previous} for p in directory.glob("Hexalith.*.dll")} | {str(observer)})
write(label+"-selected-modules.json",modules)
result=subprocess.run(["dotnet",str(OUT/"pdb-tool/bin/Debug/net10.0/Inspect.dll"),str(OUT/(label+"-selected-modules.json"))],text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
rows=json.loads(result.stdout)
for row in rows:
 if row.get("pdbPath") and Path(row["pdbPath"]).is_file():row["pdbSha256"]=digest(Path(row["pdbPath"]))
write(label+"-pdb-audit.json",rows)
issues=[{k:r.get(k) for k in ("path","error","mismatches","missingOrdinarySource","pdbGuidMatches")} for r in rows if r.get("error") or r.get("mismatches") or r.get("missingOrdinarySource") or r.get("pdbGuidMatches") is False]
variants={}
for row in rows:
 if row.get("assemblyName", "").startswith("Hexalith.EventStore"):
  variants.setdefault(row["assemblyName"],set()).add((row["sha256"],row["mvid"]))
variant_failures={k:len(v) for k,v in variants.items() if len(v)!=1}
assets=[]
asset_inputs=json.loads((OUT/"selected-assets-audit.json").read_text())
asset_inputs.append({"path":str(ROOT/"tests/tools/Task36FoldObserver/obj/project.assets.json")})
for item in asset_inputs:
 path=Path(item["path"]);obj=json.loads(path.read_text());edges=[{"name":k,"type":v.get("type")} for k,v in obj["libraries"].items() if k.startswith("Hexalith.")]
 assets.append({"path":str(path),"sha256":digest(path),"hexalith_edges":edges})
asset_failures=[r["path"] for r in assets if any(e["type"]!="project" for e in r["hexalith_edges"])]
write(label+"-assets-audit.json",assets)
expected={"OperationalStatusDurabilityAcceptanceTests.cs":"d29afa3ca0649744e84413c37bfbb41c9de0bd106160c900cb2a8c8e970c1b82","FolderDurabilityAcceptanceTests.cs":"f5e8767ad02d57441dc3c46f1366e72eb77853fc9c6c0371555a73e1efd1babc","OperationalStatusBoundedHistoryAcceptanceTests.cs":"2ae3113138f54e35fa0137e72eabc0dd26f486159f418273a0b24b6e8687258d"}
bodies={k:digest(ROOT/"tests/Hexalith.Folders.AppHost.Tests"/k)==v for k,v in expected.items()}
spec=(ROOT/"_bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md").read_bytes();block=spec[spec.index(b"<frozen-after-approval"):spec.index(b"</frozen-after-approval>")+len(b"</frozen-after-approval>")]
frozen=digest_block=hashlib.sha256(block).hexdigest()
summary={"modules":len(rows),"source_or_pdb_issues":issues,"sdk_variant_failures":variant_failures,"asset_failures":asset_failures,"bodies_match_approved_expectations":bodies,"approved_strict_body_transition":{"owner_reply":"approve","date":"2026-11-04","before_sha256":"f5743f9fa2b33803c8a3a29f9a55e5751b5bcce9a31897d1982a1ccd99316b90","approved_after_sha256":expected["OperationalStatusDurabilityAcceptanceTests.cs"],"actual_sha256":digest(ROOT/"tests/Hexalith.Folders.AppHost.Tests/OperationalStatusDurabilityAcceptanceTests.cs")},"companion_bodies_unchanged":{k:v for k,v in bodies.items() if k!="OperationalStatusDurabilityAcceptanceTests.cs"},"frozen_sha256":frozen,"frozen_unchanged":frozen=="497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422"}
write(label+"-audit-summary.json",summary);print(json.dumps(summary))
if result.returncode or issues or variant_failures or asset_failures or not all(bodies.values()) or not summary["frozen_unchanged"]:sys.exit(1)
