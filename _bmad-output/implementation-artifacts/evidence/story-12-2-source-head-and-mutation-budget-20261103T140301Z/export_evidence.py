"""Export safe Story12.2 command/source/runtime metadata; retain raw logs privately."""
from pathlib import Path
import hashlib,json,re,shutil,sys,xml.etree.ElementTree as ET
WORK=Path(__file__).parent
PACKET=Path(sys.argv[1]).resolve()
assert not PACKET.exists(),"Use a fresh packet path"
PACKET.mkdir(parents=True)
(PACKET/".gitattributes").write_text("** -text -whitespace\n")
def write(name,value):
 path=PACKET/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,indent=2)+"\n")
def safe_tests(path):
 root=ET.parse(path).getroot()
 return {"assemblies":[{k:v for k,v in a.attrib.items() if k in {"name","total","passed","failed","skipped","errors","time","run-date","run-time"}} for a in root.iter("assembly")],"tests":[{**{k:v for k,v in t.attrib.items() if k in {"type","method","result","time"}}, "display_name_sha256":hashlib.sha256(t.get("name", "").encode()).hexdigest(), "case_ordinal":ordinal} for ordinal,t in enumerate(root.iter("test"),1)]}
labels=[]
for path in sorted(WORK.glob("*-receipt.json")):
 label=path.name[:-len("-receipt.json")];labels.append(label);data=json.loads(path.read_text())
 cleanup=data.get("cleanup",{})
 if data.get("summary"):
  for ordinal,test in enumerate(data["summary"].get("tests",[]),1):
   name=test.pop("name","");test["display_name_sha256"]=hashlib.sha256(name.encode()).hexdigest();test["case_ordinal"]=ordinal
 if "remaining_unattributed_containers" in cleanup:cleanup["remaining_unattributed_container_count"]=len(cleanup.pop("remaining_unattributed_containers"))
 data["original_private_receipt_sha256"]=hashlib.sha256(path.read_bytes()).hexdigest();write("commands/"+path.name,data)
 for suffix in ("before","after"):
  source=WORK/(label+"-"+suffix+".json")
  if source.exists():
   state=json.loads(source.read_text());state.pop("containers",None);state.pop("directories",None)
   for repo in state.get("repos",[]):
    sources=repo.pop("sources");digest=hashlib.sha256(json.dumps(sources,sort_keys=True,separators=(",",":")).encode()).hexdigest()
    object_name="source/trees/"+digest+".json"
    if not (PACKET/object_name).exists():write(object_name,sources)
    repo["source_tree"]={"path":object_name,"canonical_sha256":digest,"file_count":len(sources)}
   write("source/"+source.name,state)
 runtime=WORK/(label+"-runtime.json")
 if runtime.exists():
  state=json.loads(runtime.read_text());raw=(WORK/(label+"-private.log")).read_text(errors="replace")
  state["containers"]=[c for c in state.get("containers",[]) if c["Names"] in raw]
  write("runtime/"+runtime.name,state)
 observation=WORK/(label+"-observations.log")
 if observation.exists():shutil.copy2(observation,PACKET/observation.name)
 for suffix in ("owned-cleanup-audit","host-context-before","host-context-after","audit-summary"):
  source=WORK/(label+"-"+suffix+".json")
  if source.exists():write("audit/"+source.name,json.loads(source.read_text()))
for source in sorted(WORK.glob("strict-source-head-*-lifecycle-private.jsonl")):
 rows=[json.loads(line) for line in source.read_text().splitlines()]
 allowed={"phase","processId","utc","elapsedMilliseconds","inFlightRequests","hostStopSeen","applicationStoppedSeen","droppedRecords","sinkFailed","stopConcurrently","shutdownTimeoutMilliseconds","serviceRegistrationOrder","serviceRegistrationCount","serviceOrderTruncated","serverRegisteredLast","factoryRegistrationCount","nonSingletonRegistrationCount","knownLifecycleRegistrationCount","capture","hostedServiceStates"}
 assert all(set(row).issubset(allowed) for row in rows),"Unexpected lifecycle record keys"
 write("audit/lifecycle/"+source.stem.replace("-private","")+".json",{"original_private_sha256":hashlib.sha256(source.read_bytes()).hexdigest(),"records":rows})
for pattern in ("*-pdb-audit.json","*-assets-audit.json","*-audit-summary.json","*-source-patches.json","strict-source-head-failure-diagnosis.json","matrix-test-audit.json","qualification-summary.json","package-gate-diagnosis.json","final-preservation-audit.json","existing-hosted-inventory-first-attempt.json"):
 for source in sorted(WORK.glob(pattern)):write("audit/"+source.name,json.loads(source.read_text()))
for source in sorted(WORK.glob("*-source.patch")):
 target=PACKET/"patches"/source.name;target.parent.mkdir(exist_ok=True);shutil.copy2(source,target)
for folder,label in (("/tmp/story12-2-source-head-implementation-20261103","source-head"),("/tmp/story12-2-activation-perf","performance"),("/tmp/story12-2-fixture-corrections-20261103","fixtures"),("/tmp/story12-2-mutation-budget-20261103","mutation-budget"),("/tmp/story12-2-task-status-diagnostics-20261103","task-diagnostics"),("/tmp/story12-2-lifecycle-observer-20261103","lifecycle-diagnostics"),("/tmp/story12-2-existing-hosted-observer-20261103","existing-hosted-diagnostics")):
 directory=Path(folder)
 if not directory.exists():continue
 for source in sorted(directory.glob("*.json")):
  if any(term in source.name for term in ("handoff","diagnosis","assessment","existing-instance-plan","maintenance-inputs")):write("focused/"+label+"-"+source.name,json.loads(source.read_text()))
 for source in sorted(directory.glob("*.xml")):write("focused/"+label+"-"+source.stem+".json",safe_tests(source))
for name in ("qualify.py","audit.py","stages.py","source_patch.py","record_cleanup.py","export_evidence.py","finalize_summary.py"):
 if (WORK/name).exists():shutil.copy2(WORK/name,PACKET/name)
(PACKET/"pdb-tool").mkdir()
for name in ("Program.cs","Inspect.csproj"):shutil.copy2(WORK/"pdb-tool"/name,PACKET/"pdb-tool"/name)
write("export-index.json",{"labels":labels,"raw_logs_exported":False,"raw_xml_exported":False,"qualification":"incomplete; consult phase outcomes and pending gates"})
print(json.dumps({"packet":str(PACKET),"command_phases":len(labels)}))
