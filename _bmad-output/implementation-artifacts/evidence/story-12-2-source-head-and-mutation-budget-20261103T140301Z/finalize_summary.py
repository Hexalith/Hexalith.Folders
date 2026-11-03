from pathlib import Path
import json,hashlib,sys
P=Path(__file__).parent

def receipt(label):return json.loads((P/(label+"-receipt.json")).read_text())
def suite(label):
 d=receipt(label)
 return {"phase":label,"receipt_sha256":hashlib.sha256((P/(label+"-receipt.json")).read_bytes()).hexdigest(),"exit_code":d["exit_code"],"assemblies":d["summary"]["assemblies"] if d.get("summary") else [],"failures":[{"type":t["type"],"method":t["method"]} for t in d.get("summary",{}).get("tests",[]) if t["result"]=="Fail"],"source_or_head_drift":d["source_or_head_drift"],"runtime_file_drift":d["runtime_file_drift"]}
consumers=["diagnostic-domain-tests","diagnostic-server-tests","final-workers-tests","final-integration-tests","current-gateway-tests","current-contracts-tests","current-client-generation-tests"]
sdk=["current-sdk-server-tests","current-sdk-client-tests","current-sdk-domainservice-tests","current-sdk-contracts-tests"]
strict=sorted(q.name[:-13] for q in P.glob("strict-source-head-*-receipt.json"))
# Summary preserves phase attribution. No sum of overlapping focused and full suites.
result={
 "status":"in-progress","full_acceptance":False,"enabled_mutation_budget_component_qualified":True,"workflow_step":"step-03-implement",
 "approvals":{"independent_source_head_transport":"approved","enabled_mutation_evidence_budget_seconds":2,"budget_approval_answer":"Approve 2-second mutation budget (Recommended)"},
 "consumer_suites":[suite(v) for v in consumers if (P/(v+"-receipt.json")).exists()],
 "sdk_suites":[suite(v) for v in sdk],
 "strict_attempts":[suite(v) for v in strict],
 "matrix_component_coverage":json.loads((P/"matrix-test-audit.json").read_text()),
 "latest_runtime_audit":json.loads((P/"existing-hosted-audit-summary.json").read_text()),
 "package_gate":json.loads((P/"package-gate-diagnosis.json").read_text()),
 "pending_gates":{
  "strict_current_source_live_acceptance":"open: strict8 denies postrestart task read at unchanged five-minute tenant freshness; both restart cycles completed",
  "sdk_fixed_catalog_parity":"three tests fail against retained catalog; clean current Tenants compiled owners add one query and explicit admissions, no pristine-baseline execution claimed",
  "sdk_red_phase_specs":"25 existing DW1 server skips; remain unqualified",
  "sdk_optional_lanes":"two isolated Dapr recovery, one content-live, two release-package contract skips",
  "release_package":"NU1109 minimum EventStore4.0.4 versus unchanged central4.0.0 required by Tenants7.0.0",
  "live_capacity_current_cohort":"not rerun; historical selected-candidate evidence stays historical",
  "pd10_governance":"only pending source inventory refreshed; historical manifest unchanged and v2 disabled",
  "sm4":"no current full strict completion or measured acceptance",
  "fixture_tenant_freshness":"owner decision required for fixture-only tenants:update permission and real TenantUpdated maintenance; no grant applied",
  "independent_whole_story_review":"not entered while step03 criteria remain open"},
 "preserved":{"strict_capacity_and_frozen_hashes":True,"historical_packets":True,"dependency_pins_gitlinks":True,"v2_disabled":True,"no_stage_commit_push_publish_deploy":True},
 "raw_logs_and_xml_exported":False,
 "source_tree_export":"Exact path/SHA256 maps deduplicated by canonical JSON SHA256; each before/after phase binds its immutable source-tree object.",
 "test_case_export":"Static type/method/result/time plus case ordinal and original display-name SHA256; parameter display payload excluded.",
}
(P/"qualification-summary.json").write_text(json.dumps(result,indent=2)+"\n")
print(json.dumps({"consumer_suites":len(result["consumer_suites"]),"sdk_suites":len(result["sdk_suites"]),"strict_attempts":len(strict),"full_acceptance":False}))
