"""Seal local consumer results and reproduce the untouched pending inventory."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + "\n")


def main():
    result = json.loads((OUT / "final/result.json").read_text())
    require(result["status"] == "passed-local-consumer", "Required package/source run did not finish")
    scratch = Path(os.environ["TMPDIR"])
    inventory_path = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"
    inventory = ROOT / inventory_path
    generations = []
    for number in (1, 2):
        output = scratch / ("generation-" + str(number) + ".yaml")
        argv = ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--repository-root", str(ROOT), "--output", str(output)]
        process = subprocess.run(argv, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        require(process.returncode == 0 and output.read_bytes() == inventory.read_bytes(), "Pending generation differs")
        generations.append({"argv": [arg.replace(str(ROOT), "{workspace}").replace(str(scratch), "{temp}") for arg in argv],
                            "exit_code": process.returncode, "sha256": digest(output), "matches_pending": True})
    write("generations.json", generations)
    before = json.loads((OUT / "before.json").read_text())
    for relative, expected in before["protected"].items():
        require(digest(ROOT / relative) == expected, "Protected input drift: " + relative)
    counts = {}
    for ordinal in range(6):
        tests = ET.parse(OUT / "final" / ("projects-" + str(ordinal) + ".xml")).getroot().findall(".//test")
        require(tests and all(test.attrib["result"] == "Pass" for test in tests), "Required selector failed or empty")
        counts[tests[0].attrib["type"]] = len(tests)
    text = inventory.read_text()
    count = int(re.search(r"^artifact_count: ([0-9]+)$", text, re.MULTILINE).group(1))
    set_digest = re.search(r"^candidate_set_sha256: ([0-9a-f]{64})$", text, re.MULTILINE).group(1)
    source = json.loads((OUT / "final/source-inputs.json").read_text())
    patch = subprocess.check_output(["git", "-C", str(ROOT / "references/Hexalith.Builds"), "diff", "--binary", "HEAD"])
    (OUT / "builds-working.patch").write_bytes(patch)
    catalog = ROOT / "references/Hexalith.Builds/Props/Directory.Packages.props"
    (OUT / "catalog.after.props").write_bytes(catalog.read_bytes())
    changed = subprocess.check_output(["git", "-C", str(ROOT / "references/Hexalith.Builds"), "diff", "--name-only", "HEAD"], text=True).splitlines()
    require(set(changed) == {"Props/Directory.Packages.props", "Tools/test-authoritative-package-catalog.ps1"},
            "Unexpected Builds change")
    public = json.loads((OUT / "final/public-packages.json").read_text())
    receipt = {
        "schema_version": 1,
        "status": "local-consumer-verified-operational-hold",
        "recorded_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "spec": "_bmad-output/implementation-artifacts/spec-1-17-complete-pd10-v2-migration-and-closure.md",
        "readiness": "_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md",
        "migration": "_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md",
        "source_revisions": {relative: value["head"] for relative, value in before["repositories"].items()},
        "published_folders_source": "209dacbdb42fcde728d9e190781b69945bccc93b",
        "published_version": "2.0.0",
        "release_evidence": "final/publication-verification.retained.json",
        "release_observation": "previous successful Release43755274209 and ten GitHub assets retained; five signed NuGet archives freshly downloaded and rehashed in this run",
        "catalog_committed": False,
        "catalog_selection": {"property": "HexalithFoldersVersion", "before": "1.4.0", "after": "2.0.0",
                              "affected_packages": [package["id"] for package in public],
                              "owner": "references/Hexalith.Builds", "patch": "builds-working.patch",
                              "patch_sha256": digest(OUT / "builds-working.patch"), "resulting_catalog": "catalog.after.props",
                              "resulting_catalog_sha256": digest(catalog)},
        "projects_local_version_override": False,
        "source_inputs": "final/source-inputs.json",
        "source_input_count": sum(len(value["files"]) for value in source.values()),
        "command_ledger": "final/commands.json",
        "test_selectors": list(counts), "test_counts": counts, "test_total": sum(counts.values()),
        "runtime_package_bindings": "final/runtime-bindings.json",
        "local_projects_artifact": "final/projects-local-artifact.json",
        "hosted_projects_artifact": None,
        "profile_limit": "Projects production libraries and adapter tests use Release/package; its existing AppHost executable resources also build their declared Debug source projects. This is no hosted topology execution.",
        "candidate_inventory": inventory_path, "candidate_artifacts": count,
        "candidate_sha256": digest(inventory), "candidate_set_sha256": set_digest,
        "candidate_generations": "generations.json",
        "protected_inputs": len(before["protected"]),
        "controls": {"route_mode": "V1Only", "migration_t0": None, "activation_authority": None,
                     "staging_checks_executed": 0, "parent_status": "in-progress", "tracker": "backlog"},
        "consumer_call_inventory": [
            {"operation": "GetFolderLifecycleStatusAsync", "adapter": "FoldersProjectFolderDirectory",
             "triggers": ["SetProjectFolder", "ProposeNewProject", "direct reference refresh"], "kind": "read"},
            {"operation": "GetEffectivePermissionsAsync", "adapter": "FoldersProjectFolderDirectory",
             "triggers": ["successful lifecycle validation in the same direct operations"], "kind": "read"},
            {"operation": "GetFolderFileMetadataAsync", "adapter": "FoldersProjectFileReferenceDirectory",
             "triggers": ["LinkFileReference", "ProposeNewProject", "explicit file-reference directory refresh"], "kind": "metadata-only read"},
            {"operation": "FolderOwnerReadQuery via DelegatedOwnerQueryClient.InvokeAsync",
             "adapter": "DelegatedProjectFolderDirectory", "triggers": ["verified workload reference refresh", "Project read/advisory/legacy resolution authorization"],
             "kind": "delegated owner read; direct fallback only when no verified workload caller exists"},
        ],
        "source_periodic_caller": "No periodic Folders caller identified in inspected Projects source; deployed schedules and consumer census remain unverified.",
        "setup_failures": [
            {"path": "preparation-attempt-1", "result": "Full cache-copy setup interrupted before build; owned copy process exited and scratch was cleaned."},
            {"path": "projects-release-build.log", "result": "Initial primary/fallback cache layout omitted the tooling4.48.2 manual HintPath; final run seeds that existing package privately. No product source workaround."},
        ],
        "remaining": [
            {"owner": "Hexalith.Builds maintainer", "requirement": "Review/commit the two-file catalog adoption, then regenerate and validate the revision-bound hexalith-folders audit. Existing audit is intentionally unchanged and currently refuses six catalog/selection differences.",
             "commands_after_commit": ["run-with-temp pwsh -NoProfile -File Tools/audit-central-package-versions.ps1 -ChangedFamily hexalith-folders -PriorAuditPath Tools/package-version-audit.json -OutputPath Tools/package-version-audit.json", "run-with-temp pwsh -NoProfile -File Tools/validate-package-version-audit.ps1"],
             "evidence": ["final/audit-precommit-refusal.log", "final/audit-current-refusal.log"]},
            {"owner": "Projects release/deployment owner", "requirement": "Land the approved Builds selection in the consuming tuple and supply an immutable hosted Projects image/deployment artifact and deployed delegation. The local runtime bundle supplies no release/deployment identity."},
            {"owner": "Platform operational owner", "requirement": "Supply current executable source/tool/catalog/composition/intake qualification and attempt-locked target/consumer/data disposition, hosted identity/delegation, named operators, UTC slots, thresholds, staging, observation and recovery."},
            {"owner": "Story1.17 decision owner", "requirement": "Accept evidence for the actual first-install or existing-v1 branch and authorize closure/tracker transition only after all applicable operational gates pass."},
        ],
    }
    proposal = OUT / "provisional-audit/result.json"
    if proposal.exists():
        receipt["provisional_audit"] = json.loads(proposal.read_text())
        receipt["provisional_audit"]["evidence_directory"] = "provisional-audit"
    write("receipt.json", receipt)
    print("Receipt prepared:", sum(counts.values()), "passing consumer cases;", count, "unchanged inventory rows")


if __name__ == "__main__":
    main()
