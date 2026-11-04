from pathlib import Path
import hashlib,json,shutil,sys,xml.etree.ElementTree as ET
W=Path(__file__).parent;P=Path(sys.argv[1]).resolve();assert not P.exists();P.mkdir(parents=True)
(P/".gitattributes").write_text("** -text -whitespace\n")
def write(name,data):
 f=P/name;f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(data,indent=2)+"\n")
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
def safe_tests(file):
 root=ET.parse(file).getroot();return {"assemblies":[{k:v for k,v in a.attrib.items() if k in {"name","total","passed","failed","skipped","errors","time","run-date","run-time"}} for a in root.iter("assembly")],"tests":[{**{k:v for k,v in t.attrib.items() if k in {"type","method","result","time"}},"case_ordinal":i,"display_name_sha256":hashlib.sha256(t.get("name","").encode()).hexdigest()} for i,t in enumerate(root.iter("test"),1)]}
labels=[]
for file in sorted(W.glob("*-receipt.json")):
 label=file.name[:-13];labels.append(label);data=json.loads(file.read_text())
 if data.get("summary"):
  for i,t in enumerate(data["summary"]["tests"],1):
   t["display_name_sha256"]=hashlib.sha256(t.pop("name","").encode()).hexdigest();t["case_ordinal"]=i
 cleanup=data.get("cleanup",{})
 if "remaining_unattributed_containers" in cleanup:cleanup["remaining_unattributed_container_count"]=len(cleanup.pop("remaining_unattributed_containers"))
 data["original_private_receipt_sha256"]=sha(file);write("commands/"+file.name,data)
 for suffix in ["before","after"]:
  f=W/(label+"-"+suffix+".json")
  if f.exists():
   obj=json.loads(f.read_text());obj.pop("containers",None);obj.pop("directories",None)
   for repo in obj.get("repos",[]):
    tree=repo.pop("sources");digest=hashlib.sha256(json.dumps(tree,sort_keys=True,separators=(",",":")).encode()).hexdigest();name="source/trees/"+digest+".json"
    if not (P/name).exists():write(name,tree)
    repo["source_tree"]={"path":name,"canonical_sha256":digest,"file_count":len(tree)}
   write("source/"+f.name,obj)
 f=W/(label+"-runtime.json")
 if f.exists():
  obj=json.loads(f.read_text());raw=(W/(label+"-private.log")).read_text(errors="replace");obj["containers"]=[c for c in obj.get("containers",[]) if c["Names"] in raw];write("runtime/"+f.name,obj)
 f=W/(label+"-observations.log")
 if f.exists():shutil.copy2(f,P/f.name)
for pattern in ["*-pdb-audit.json","*-assets-audit.json","*-audit-summary.json","*-selected-modules.json","*-source-patches.json","*-owned-cleanup-audit.json","*-host-context-*.json","*-diagnosis.json","*-assessment.json","*-assessment-private.json","*-diagnosis-private.json","*-preservation*.json","*-comparison*.json","external-*.json","continuation.json","prior-gates.json","qualification-summary.json","matrix-test-audit.json","published-package-origin-observation.json","official-tenants-client-dependency-observation.json","*-extra-environment.json"]:
 for f in sorted(W.glob(pattern)):write("audit/"+f.name,json.loads(f.read_text()))
for f in sorted(W.glob("*-source.patch")):
 (P/"patches").mkdir(exist_ok=True);shutil.copy2(f,P/"patches"/f.name)
for f in sorted(W.glob("*-lifecycle-private.jsonl")):
 rows=[json.loads(line) for line in f.read_text().splitlines()];write("audit/"+f.stem.replace("-private","")+".json",{"original_private_sha256":sha(f),"records":rows})
F=Path("/tmp/story12-2-tenant-maintenance-implementation-20261103")
write("focused/handoff.json",json.loads((F/"handoff.json").read_text()))
for f in sorted(F.glob("*.xml")):write("focused/"+f.stem+".json",safe_tests(f))
(P/"patches").mkdir(exist_ok=True);shutil.copy2(F/"phase.patch",P/"patches/approved-fixture-phase.patch")
F=Path("/tmp/story12-2-current-cohort-followup-20261103")
write("focused/current-cohort/handoff.json",json.loads((F/"handoff.json").read_text()))
for f in sorted(F.glob("*.xml")):write("focused/current-cohort/"+f.stem+".json",safe_tests(f))
for name in ["root-phase.patch","sdk-phase.patch","composition-only.patch","followup-before-composition.patch"]:
 shutil.copy2(F/name,P/"patches"/("current-cohort-"+name))
F=Path("/tmp/story12-2-admission-rebase-20261103")
if (F/"assessment.json").exists():write("focused/admission-rebase/assessment.json",json.loads((F/"assessment.json").read_text()))
if (F/"handoff.json").exists():
 write("focused/admission-rebase/handoff.json",json.loads((F/"handoff.json").read_text()))
 for f in sorted(F.glob("*.xml")):write("focused/admission-rebase/"+f.stem+".json",safe_tests(f))
 for f in sorted(F.glob("*.patch")):shutil.copy2(f,P/"patches"/("admission-rebase-"+f.name))
F=Path("/tmp/story12-2-broad-hygiene-20261103")
if (F/"handoff.json").exists():
 write("focused/broad-hygiene/handoff.json",json.loads((F/"handoff.json").read_text()))
 for f in sorted(F.glob("*.xml")):write("focused/broad-hygiene/"+f.stem+".json",safe_tests(f))
 for f in sorted(F.glob("*.patch")):shutil.copy2(f,P/"patches"/("broad-hygiene-"+f.name))
for name in ["qualify.py","audit.py","source_patch.py","record_cleanup.py","stages.py","export_continuation.py","finalize_continuation.py","post_live_plan.py","matrix_audit.py","final_controls.py","qualify_admission_runtime.py","post_admission_broad.py"]:shutil.copy2(W/name,P/name)
(P/"pdb-tool").mkdir()
for name in ["Program.cs","Inspect.csproj"]:shutil.copy2(W/"pdb-tool"/name,P/"pdb-tool"/name)
D=Path("/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts")
(P/"decisions").mkdir()
for name in ["proposal-12-2-live-tenant-freshness-prerequisite.md","proposal-12-2-foreign-task-scope-proof.md","proposal-12-2-foreign-task-scope-proof.patch","spec-12-2-durable-projections-and-task-completion-pipeline.md"]:shutil.copy2(D/name,P/"decisions"/name)
write("export-index.json",{"phases":labels,"raw_logs_exported":False,"raw_xml_exported":False,"scope":"Approved foreground fixture tenant maintenance; see actual cohort and gate dispositions"})
print(json.dumps({"packet":str(P),"phases":len(labels)}))
