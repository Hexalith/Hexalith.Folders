"""Append the bounded result and seal its local receipt without changing rollout state."""
import datetime
import hashlib
import json
from pathlib import Path


OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
PRIOR = OUT.parent / "story-1-17-released-v2-consumer-2026-11-15"


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")


def main():
    require(not (OUT / "receipt.json").exists(), "Already sealed")
    before = json.loads((OUT / "before.json").read_text())
    prior = json.loads((PRIOR / "receipt.json").read_text())
    controls = prior["controls"]
    evidence_path = str(OUT.relative_to(ROOT))
    handoff_path = "_bmad-output/implementation-artifacts/story-1-17-landed-v2-handoff-2026-11-15.json"
    remaining = [
        {"owner": "Hexalith.Builds catalog maintainer", "gate": "revision-bound Folders family audit",
         "finding": "Catalog adoption is committed; live audit still binds 1.4.0 and fails six deterministic checks.",
         "required_inputs": ["Committed Builds 801f268cd1b37ecae41d4fb20d115c055b4c5793 or an independently requalified successor",
                             "Existing Tools/package-version-audit.json, preserved as prior audit",
                             "Authorized NuGet V3 feed observation for the five Folders 2.0.0 packages"],
         "working_directory": "references/Hexalith.Builds",
         "commands_when_remote_execution_is_authorized": [
             "run-with-temp pwsh -NoProfile -File Tools/audit-central-package-versions.ps1 -ChangedFamily hexalith-folders -PriorAuditPath Tools/package-version-audit.json -OutputPath Tools/package-version-audit.json",
             "run-with-temp pwsh -NoProfile -File Tools/validate-package-version-audit.ps1"],
         "execution_in_this_continuation": "Not run: generator queries remote feeds; the active spec forbids remote operations.",
         "provisional_audit_limit": "The earlier temporary-commit proposal is retained evidence only; never copy it into the live audit or invent fresh registration responses."},
        {"owner": "Projects release/deployment owner", "gate": "immutable hosted consumer artifact",
         "finding": "Projects source and committed imported catalog are qualified locally; the retained runtime zip is unpublished.",
         "required_inputs": ["Immutable hosted Projects image/deployment identity for the exact qualified source and package pair",
                             "Deployed direct/delegated call inventory, every periodic schedule and verified hosted delegation"]},
        {"owner": "Platform, tool/catalog and module-intake owners", "gate": "current composed hosted qualification",
         "finding": "The older offline proposal refuses the current Folders host; prior mappings and historical tool acceptance do not qualify the current executable tuple.",
         "required_inputs": ["Current revision-bound executable source mapping and tool/catalog/composition/intake acceptance",
                             "Attempt lock and verified target/consumer/retained-data disposition",
                             "Exact hosted Folders and Projects artifact/configuration/identity/delegation tuple",
                             "Staging and applicable workload-removal or retained-data recovery proof",
                             "Named operators, UTC slots, measured thresholds, verification/observation and recovery results"]},
        {"owner": "Story 1.17 decision owner", "gate": "applicable-branch closure and tracker authorization",
         "required_inputs": ["All applicable first-install evidence or existing-v1 migration/retirement evidence",
                             "Explicit closure decision and authorized tracker delta after the operational gates pass"]}
    ]
    handoff = {"schema_version": 1, "status": "landed-catalog-local-qualification-operational-hold",
               "recorded_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
               "source_revisions": {name: row["head"] for name, row in before["repositories"].items()},
               "source_identity_evidence": evidence_path + "/before.json",
               "catalog_committed": True, "catalog_revision": before["repositories"]["references/Hexalith.Builds"]["head"],
               "catalog_sha256": sha(ROOT / "references/Hexalith.Builds/Props/Directory.Packages.props"),
               "released_pair": {"version": "2.0.0", "source": prior["published_folders_source"],
                                 "retained_packages": json.loads((PRIOR / "final/public-packages.json").read_text()),
                                 "observation_limit": "Rehashed retained signed archives only; no new download, feed or CI observation."},
               "verification": {"fresh_commands": evidence_path + "/commands.json",
                                "candidate": evidence_path + "/candidate-verification.json",
                                "source_comparison": evidence_path + "/source-comparison.json",
                                "dependency_comparison": evidence_path + "/dependency-comparison.json",
                                "reused_receipt": str((PRIOR / "receipt.json").relative_to(ROOT)),
                                "reused_results": "Dated Release/package solution builds, Folders Debug/source build, Projects 124/124, parity 12/12 and governance 217/217 + 7/7 + 3/3; no new build or service test execution."},
               "local_projects_artifact": {"path": str((PRIOR / "final/projects-server-local-build.zip").relative_to(ROOT)),
                                          "sha256": "b3af188fb22bab919bdda826265b73322ca06dd02522fe4bcaac11105b9982c5",
                                          "kind": "unpublished local compiled runtime; no hosted identity"},
               "hosted_projects_artifact": None, "controls": controls, "remaining": remaining}
    write(ROOT / handoff_path, handoff)
    write(OUT / "handoff.json", handoff)
    appends = [
        ("spec.before.md", "spec-1-17-complete-pd10-v2-migration-and-closure.md",
         "\n\n## 2026-11-15 landed v2 catalog and unchanged-source qualification\n\n"
         "Builds now commits the Folders 2.0.0 selection at `801f268cd1b37ecae41d4fb20d115c055b4c5793`; Folders `efa9be7f4e3d8c432b057f52a8d6285b01d82899` records it and clean Projects remains `e228f185bb6a870456a2a91dcd12cb8f27964f61`. "
         "The [landed-tuple handoff](story-1-17-landed-v2-handoff-2026-11-15.json) and [receipt](evidence/story-1-17-landed-v2-tuple-2026-11-15/receipt.json) supersede only the preceding uncommitted-catalog status. All 13,139 recorded inputs were compared: only the three appended evidence documents differ, with no added or removed input. McpCli's newer revision changes evidence only; AI.Tools changes baseline instructions only. The earlier Release/package builds, Debug/source build, Projects 124/124, parity 12/12 and governance results are reused dated evidence after those comparisons, not new execution. Optimized replay freshly verifies the retained package, payload and result identities.\n\n"
         "Fresh local catalog validation, 357 compatibility probes, seventeen authority controls, Platform selection and sixteen exception checks pass. The first exception command omitted mandatory path arguments; its parameter failure and corrected passing command are both retained. Independent rehash verifies all 1,635 candidate rows and two private generations match the untouched pending inventory. Live audit validation fails six checks: its catalog digest and five Folders rows still select 1.4.0. The older source proposal refuses the current host. Audit refresh now has its committed input, but its documented generator queries NuGet feeds and is outside this step's no-remote boundary. The handoff gives its owning directory, exact commands and required inputs; no synthetic feed response or temporary-commit audit is promoted.\n\n"
         "Tasks 2 and 4 still lack an immutable hosted Projects artifact and deployed evidence. Current Platform/tool/catalog/composition/intake acceptance, attempt-bound target/data disposition, hosted identity/delegation, staging, operators/UTC slots/thresholds, observation/recovery and applicable-branch closure remain owner gates. All 195 protected hashes, owning revisions/indices, original spec/readiness/migration prefixes, frozen scope/baseline/task values, in-progress, tracker backlog, V1Only, unset T0, null activation authority and fourteen unexecuted staging checks are preserved. No product/configuration/dependency change, remote request, Git mutation, publication, deployment or activation occurred.\n"),
        ("readiness.before.md", "story-1-17-current-candidate-technical-readiness-2026-09-24.md",
         "\n\n## Addendum 2026-11-15: landed v2 catalog qualification and outstanding audit\n\n"
         "The [landed-tuple receipt](evidence/story-1-17-landed-v2-tuple-2026-11-15/receipt.json) binds Folders `efa9be7f4e3d8c432b057f52a8d6285b01d82899`, committed Builds `801f268cd1b37ecae41d4fb20d115c055b4c5793`, unchanged Projects `e228f185bb6a870456a2a91dcd12cb8f27964f61` and all thirteen checkouts/gitlinks. Against the passing released-v2 consumer run, all 13,139 input paths are unchanged; only the three evidence-document contents differ. Builds and Projects inputs are byte-identical, McpCli's delta is evidence-only and AI.Tools changes the baseline only. Earlier full package/source builds, Projects 124/124, parity 12/12 and governance 217/217 + 7/7 + 3/3 remain explicitly dated reused results. Optimized replay rehashes their retained archives, payloads and results; no remote observation or new build/test run is claimed.\n\n"
         "Fresh validation passes the catalog, 357 compatibility probes, seventeen authority controls, EventStore Aspire 5.1.1 Platform selection and sixteen exceptions. A missing-argument exception invocation is retained separately from its corrected pass. All 1,635 pending rows independently rehash and two private generations match raw SHA-256 `8b3ab8eb2fb99e4a9eb07262bcb8281d8d1eee6d8f8d2d3999d1c76d76a10a0f`. The live audit fails six precise mismatches and the older source proposal refuses FoldersServerHostComposition.cs drift. No live audit, proposal or pending inventory is rewritten.\n\n"
         "The [current handoff](story-1-17-landed-v2-handoff-2026-11-15.json) names the Builds owner's first outstanding catalog action requiring remote access: refresh the Folders family audit against the actual committed catalog and preserved prior audit, then validate. Feed queries are prohibited by this implementation boundary, so the earlier provisional audit remains provisional. Hosted Projects artifacts/delegation/schedules, current composition/intake acceptance and the target/data/staging/observation/recovery/closure fields remain absent. All 195 protected inputs and owner revisions/indices are preserved; parent status, original task values, V1Only, unset T0, null activation authority, tracker backlog and fourteen unexecuted staging checks remain unchanged. No Git mutation, remote request, publication, deployment or activation occurred.\n"),
        ("migration.before.md", "story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md",
         "\n\n## 2026-11-15 landed v2 catalog recheck and entry decision\n\n"
         "The [landed-tuple receipt](evidence/story-1-17-landed-v2-tuple-2026-11-15/receipt.json) supersedes the preceding uncommitted-catalog statement: Builds `801f268cd1b37ecae41d4fb20d115c055b4c5793` commits Folders 2.0.0, and Folders records that selection. Projects remains `e228f185bb6a870456a2a91dcd12cb8f27964f61` without a local Folders override. Input comparisons support reuse of the dated passing package/source builds, 124/124 consumer checks, parity and governance; fresh catalog/authority/Platform/exception checks and the 1,635-row/two-generation audit pass. The live revision-bound package audit still fails six catalog/selection mismatches and requires an authorized owner feed refresh; the older source proposal also refuses current host drift. Exact commands and owner inputs are in the [current handoff](story-1-17-landed-v2-handoff-2026-11-15.json).\n\n"
         "**Entry decision: hold.** The retained Projects runtime zip remains unpublished, with no immutable hosted artifact or deployed delegation/schedule/census. Current composed hosted acceptance, attempt-locked target/data disposition, staging, named operators, UTC slots, thresholds, observation, recovery and closure authorization remain unverified. Preserve the accepted conditional branches, original task values, V1Only, unset T0, null activation authority, fourteen unexecuted staging checks and tracker backlog. No remote request, Git mutation, publication, deployment, activation or closure occurred.\n")
    ]
    append_records = []
    for prefix_name, filename, addition in appends:
        target = ROOT / "_bmad-output/implementation-artifacts" / filename
        prefix = (OUT / prefix_name).read_bytes()
        require(target.read_bytes() == prefix, "Concurrent document change: " + filename)
        encoded = addition.encode()
        target.write_bytes(prefix + encoded)
        append_records.append({"path": str(target.relative_to(ROOT)), "prefix": prefix_name,
                               "append_sha256": hashlib.sha256(encoded).hexdigest()})
    write(OUT / "receipt.json", {"schema_version": 1, "status": handoff["status"],
          "recorded_utc": handoff["recorded_utc"], "controls": controls, "hosted_projects_artifact": None,
          "source_revisions": handoff["source_revisions"], "catalog_committed": True,
          "compared_source_inputs": 13139, "protected_inputs": len(before["protected"]),
          "reused_evidence_directory": PRIOR.name, "reused_receipt_sha256": sha(PRIOR / "receipt.json"),
          "reused_manifest_sha256": sha(PRIOR / "artifact-manifest.json"),
          "command_ledger": "commands.json", "candidate_commands": "candidate-commands.json",
          "setup_failure": "catalog-exceptions omitted mandatory paths; corrected command passes. No product fix.",
          "handoff_path": handoff_path, "handoff_sha256": sha(ROOT / handoff_path),
          "document_appends": append_records, "remaining": remaining})
    write(OUT / "artifact-manifest.json", {str(path.relative_to(OUT)): sha(path) for path in sorted(OUT.rglob("*"))
          if path.is_file() and path.name != "artifact-manifest.json" and "__pycache__" not in path.parts})
    print("Sealed landed tuple receipt and append-only owner handoff.")


if __name__ == "__main__":
    main()
