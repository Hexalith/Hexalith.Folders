from pathlib import Path
import hashlib,json
P=Path(__file__).parent
manifest=json.loads((P/"seal.json").read_text())
actual={q.relative_to(P).as_posix() for q in P.rglob("*") if q.is_file() and q.name!="seal.json"}
assert actual==set(manifest["files"]),"Sealed file inventory differs"
for name,want in manifest["files"].items():
 q=P/name;assert not q.is_symlink() and hashlib.sha256(q.read_bytes()).hexdigest()==want,name
for phase in (P/"source").glob("*-before.json"):
 for suffix in ["-before.json","-after.json"]:
  doc=P/"source"/(phase.name[:-12]+suffix)
  if not doc.exists():continue
  for repo in json.loads(doc.read_text()).get("repos",[]):
   obj=repo["source_tree"];tree=json.loads((P/obj["path"]).read_text())
   assert len(tree)==obj["file_count"]
   assert hashlib.sha256(json.dumps(tree,sort_keys=True,separators=(",",":")).encode()).hexdigest()==obj["canonical_sha256"]
for file in (P/"commands").glob("*-receipt.json"):
 d=json.loads(file.read_text());assert not d["source_or_head_drift"],file.name
 assert not d["runtime_file_drift"],file.name
 assert not d["cleanup"]["remaining_observed_descendants"],file.name
 assert not d["cleanup"]["remaining_owned_containers"],file.name
 if d.get("summary"):
  for a in d["summary"]["assemblies"]:
   assert int(a["passed"])+int(a["failed"])+int(a["skipped"])==int(a["total"]),(file.name,a)
  for t in d["summary"].get("tests",[]):assert "name" not in t and "display_name_sha256" in t
for file in (P/"audit").glob("*-owned-cleanup-audit.json"):
 d=json.loads(file.read_text());assert not d["runtimeOwnedProcessRemainders"] and not d["ownedContainerRemainders"]
 assert all(not row["exists"] for row in d["ownedDaprDirectories"])
 assert d["actionOnUnattributedResources"] in {"None", "None; preserve separate topologies"}
q=json.loads((P/"audit/qualification-summary.json").read_text())
assert q["full_acceptance"] is False and q["approvals"]["enabled_mutation_evidence_budget_seconds"]==2
assert len(q["consumer_suites"])==7 and all(s["exit_code"]==0 for s in q["consumer_suites"])
assert len(q["strict_attempts"])==8 and all(s["exit_code"]==1 for s in q["strict_attempts"])
assert q["matrix_component_coverage"]["all_component_rows_passed"]
assert q["latest_runtime_audit"]["frozen_unchanged"] and all(q["latest_runtime_audit"]["strict_bodies_unchanged"].values())
assert q["package_gate"]["exit_code"]==1 and all(r["required_minimum"]=="4.0.4" and r["centrally_pinned"]=="4.0.0" for r in q["package_gate"]["downgrades"])
assert q["raw_logs_and_xml_exported"] is False
sdk={k:sum(int(a[k]) for suite in q["sdk_suites"] for a in suite["assemblies"]) for k in ["passed","failed","skipped"]}
assert sdk=={"passed":12599,"failed":3,"skipped":30},sdk
consumer={k:sum(int(a[k]) for s in q["consumer_suites"] for a in s["assemblies"]) for k in ["passed","failed","skipped","errors","total"]}
print(json.dumps({"sealed_files":len(actual),"consumer_final_phase_selections":consumer,"strict_attempts":len(q["strict_attempts"]),"strict_live_qualified":False,"mutation_budget_seconds":2,"fixture_grant_applied":False,"whole_story_complete":False}))
