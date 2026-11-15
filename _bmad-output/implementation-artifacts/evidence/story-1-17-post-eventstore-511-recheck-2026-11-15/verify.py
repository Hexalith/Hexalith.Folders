#!/usr/bin/env python3
"""Replay the Story 1.17 post-EventStore-5.1.1 local recheck evidence without executing any binary.

Default mode checks retained bytes and that every result claimed by receipt.json matches the retained
logs, counts and generations. ``--workspace PATH`` also rehashes the owning checkout. Every check is an
explicit comparison, so optimized Python (-O) enforces the same rules.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
PENDING = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"
HANDOFF = "_bmad-output/implementation-artifacts/story-1-17-publication-handoff-2026-11-15-post-eventstore-511.json"
INERT = {"routeMode": "V1Only", "migrationT0": None, "firstInstallActivation": None, "activationAuthority": None,
         "stagingChecksExecuted": 0, "trackerStatus": "backlog", "parentStatus": "in-progress",
         "storyClosureClaimed": False}


class VerificationError(Exception):
    """Raised when retained evidence does not support a claimed result."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load(root: Path, relative: str) -> object:
    return json.loads((root / relative).read_text())


def check_manifest(root: Path) -> int:
    manifest = load(root, "manifest.json")
    listed = manifest["files"]
    present = {str(path.relative_to(root)) for path in root.rglob("*")
               if path.is_file() and "__pycache__" not in path.parts and path.name != "manifest.json"}
    require(present == set(listed), f"Unlisted or missing files: {sorted(present ^ set(listed))[:5]}")
    for relative, entry in listed.items():
        data = (root / relative).read_bytes()
        require(len(data) == entry["bytes"] and digest(data) == entry["sha256"], f"Retained file changed: {relative}")
    return len(listed)


def check_attempt(root: Path, name: str, claim: dict) -> None:
    commands = load(root, f"{name}/commands.json")
    for entry in commands:
        log = root / name / entry["log"]
        require(log.is_file() and digest(log.read_bytes()) == entry["log_sha256"], f"{name} log changed: {entry['log']}")
    exits = {entry["label"]: entry["exit_code"] for entry in commands}
    require(exits == claim["exit_codes"], f"{name} exit codes differ from receipt")
    for label, totals in claim["counts"].items():
        counts = load(root, f"{name}/{label}-counts.json")
        xml = root / name / f"{label}.xml"
        require(xml.is_file() and digest(xml.read_bytes()) == counts["xml_sha256"], f"{name} {label} XML changed")
        require(counts["totals"] == totals, f"{name} {label} totals differ from receipt")
        require(counts["every_selector_passed_unskipped"] is claim["selectors_pass"][label],
                f"{name} {label} selector verdict differs from receipt")
    summary = load(root, f"{name}/run-summary.json")
    require(summary["owning_workspace_unchanged"] is True, f"{name} owning workspace changed during the run")
    generation = load(root, f"{name}/generation.json")
    require(generation == claim["generation"], f"{name} generation result differs from receipt")
    if generation.get("ran"):
        for label in ("first", "second"):
            data = (root / name / f"candidate-generated-{label}.yaml").read_bytes()
            require(digest(data) == generation[f"{label}_sha256"], f"{name} {label} generation bytes changed")


def check_controls(handoff: dict) -> None:
    require(handoff["qualification"] == "HOLD", "Handoff qualification is not HOLD")
    for key, value in INERT.items():
        require(handoff["operationalControls"].get(key, "absent") == value, f"Operational control changed: {key}")


def verify(root: Path, workspace: Path | None) -> dict:
    files = check_manifest(root)
    receipt = load(root, "receipt.json")
    for name, claim in receipt["attempts"].items():
        check_attempt(root, name, claim)
    diagnosis = load(root, "attempt-1/diagnosis/diagnose-source-binding.json")
    versions = {key.split("/")[0]: next(iter(value["runtime"].values()))["assemblyVersion"]
                for key, value in diagnosis["deps_targets"].items()}
    require(versions == receipt["rootCause"]["assemblyVersions"], "Diagnosed assembly identities differ from receipt")
    patch = (root / "attempt-2-private-fix-trial/private-copy.patch").read_bytes()
    require(digest(patch) == receipt["fix"]["patchSha256"], "Trial patch differs from the recorded fix")
    solution = load(root, "final/source-solution.json")
    require(solution == receipt["attempts"]["final"]["solution"], "Source solution result differs from receipt")
    rerun = load(root, "final-solution-with-pwsh-path/source-solution.json")
    require(rerun == receipt["solutionRerun"]["result"], "Solution rerun result differs from receipt")
    rerun_commands = load(root, "final-solution-with-pwsh-path/commands.json")
    require(rerun_commands == receipt["solutionRerun"]["commands"], "Solution rerun commands differ from receipt")
    for entry in rerun_commands:
        log = root / "final-solution-with-pwsh-path" / entry["log"]
        require(log.is_file() and digest(log.read_bytes()) == entry["log_sha256"], f"Rerun log changed: {entry['log']}")
    # The concurrent narrative edit is the only allowed change; it is not a build input of the private copy.
    rerun_before = load(root, "final-solution-with-pwsh-path/before.json")
    rerun_after = load(root, "final-solution-with-pwsh-path/after.json")
    changed = {key for key in set(rerun_before["protected"]) | set(rerun_after["protected"])
               if rerun_before["protected"].get(key) != rerun_after["protected"].get(key)}
    require(changed <= set(receipt["intentionalProtectedEdits"]), f"Unexpected protected change during rerun: {sorted(changed)}")
    for name, value in rerun_before["repositories"].items():
        other = rerun_after["repositories"][name]
        require(value["head"] == other["head"] and value["index_entries_sha256"] == other["index_entries_sha256"]
                and value["staged"] == other["staged"], f"Repository identity changed during rerun: {name}")
        edited = {key for key in set(value["changed_files"]) | set(other["changed_files"])
                  if value["changed_files"].get(key) != other["changed_files"].get(key)}
        require(edited <= set(receipt["intentionalProtectedEdits"]), f"Unexpected working-tree change during rerun: {name}")
    require(rerun.get("restore") == 0 and rerun.get("build") == 0, "Solution rerun did not pass")
    evaluation = load(root, "profile-evaluation.json")
    require(evaluation["status"] == "pass" and evaluation["package_inert"] is True, "Package-profile inertness not proven")
    reuse = load(root, "reuse-identity.json")
    require(reuse["reusable"] is True and reuse["profile_evaluation_sha256"] == digest((root / "profile-evaluation.json").read_bytes()),
            "Release/package reuse identity not established")
    handoff = load(root, "handoff.json")
    check_controls(handoff)
    check_controls({"qualification": receipt["qualification"], "operationalControls": receipt["operationalControls"]})
    before = load(root, "final/before.json")
    after = load(root, "final/after.json")
    require(before["repositories"] == after["repositories"] and before["protected"] == after["protected"],
            "Final run snapshots differ")
    if workspace is not None:
        require((workspace / HANDOFF).read_bytes() == (root / "handoff.json").read_bytes(), "Owning handoff differs")
        require((workspace / PENDING).read_bytes() == (root / "final/candidate-generated-first.yaml").read_bytes(),
                "Owning pending inventory differs from the final generation")
        require(digest((workspace / "Directory.Build.targets").read_bytes()) == receipt["fix"]["resultSha256"],
                "Owning Directory.Build.targets differs from the recorded fix")
        tracker = "_bmad-output/implementation-artifacts/sprint-status.yaml"
        require(digest((workspace / tracker).read_bytes()) == before["protected"][tracker], "Tracker changed")
        for relative, expected in before["protected"].items():
            if relative in receipt["intentionalProtectedEdits"]:
                continue
            require(digest((workspace / relative).read_bytes()) == expected, f"Protected input drift: {relative}")
        documents = "_bmad-output/implementation-artifacts/"
        for name in ("spec-1-17-complete-pd10-v2-migration-and-closure.md",
                     "story-1-17-current-candidate-technical-readiness-2026-09-24.md"):
            require((workspace / documents / name).read_bytes().startswith((root / "final" / (name + ".before")).read_bytes()),
                    f"Historical prefix changed: {name}")
        migration = "story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md"
        header = receipt["migrationHeaderCorrection"]
        original = (root / "final" / (migration + ".before")).read_text()
        require(original.count(header["old"]) == 1, "Recorded migration header is not unique")
        require((workspace / documents / migration).read_text().startswith(original.replace(header["old"], header["new"], 1)),
                "Migration package changed beyond the recorded header correction and appended section")
    return {"status": "pass", "evidence_files": files, "attempts": sorted(receipt["attempts"]),
            "qualification": receipt["qualification"], "workspace_rehashed": workspace is not None,
            "interpretation": "local Debug/source recheck and reused dated Release/package evidence; no hosted acceptance"}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", type=Path)
    parser.add_argument("--root", type=Path, default=HERE)
    arguments = parser.parse_args()
    try:
        print(json.dumps(verify(arguments.root.resolve(), arguments.workspace), sort_keys=True))
    except (VerificationError, OSError, KeyError, ValueError, StopIteration) as error:
        print(f"FAIL: {error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
