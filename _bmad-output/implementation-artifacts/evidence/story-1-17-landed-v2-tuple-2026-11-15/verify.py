"""Replay the landed-tuple evidence, optionally checking the current workspace."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(root, name):
    return json.loads((root / name).read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path)
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parent
    controls = {"route_mode": "V1Only", "migration_t0": None, "activation_authority": None,
                "staging_checks_executed": 0, "parent_status": "in-progress", "tracker": "backlog"}
    receipt = load(root, "receipt.json")
    require(receipt["status"] == "landed-catalog-local-qualification-operational-hold" and
            receipt["controls"] == controls and receipt["hosted_projects_artifact"] is None,
            "Unsafe qualification or operational claim")
    manifest = load(root, "artifact-manifest.json")
    for relative, expected in manifest.items():
        path = root / relative
        require(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(root),
                "Missing or unsafe evidence member: " + relative)
        require(sha(path) == expected, "Evidence member changed: " + relative)
    require(load(root, "handoff.json")["controls"] == controls and
            load(root, "handoff.json")["hosted_projects_artifact"] is None,
            "Unsafe retained handoff controls")
    prior = root.parent / receipt["reused_evidence_directory"]
    require(sha(prior / "receipt.json") == receipt["reused_receipt_sha256"] and
            sha(prior / "artifact-manifest.json") == receipt["reused_manifest_sha256"], "Prior identity drift")
    for relative, expected in load(prior, "artifact-manifest.json").items():
        path = prior / relative
        require(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(prior) and
                sha(path) == expected, "Reused evidence changed: " + relative)
    commands = load(root, "commands.json")
    required_passes = {"retained-evidence-replay", "catalog-validation", "catalog-authority",
                       "catalog-platform", "catalog-exceptions-corrected"}
    required_refusals = {"current-audit-refusal", "current-source-proposal-refusal"}
    indexed = {row["name"]: row for row in commands}
    require(len(indexed) == len(commands) and set(indexed) == required_passes | required_refusals | {"catalog-exceptions"},
            "Wrong command denominator")
    for name in required_passes:
        require(indexed[name]["exit_code"] == 0, "Required check did not pass: " + name)
    for name in required_refusals | {"catalog-exceptions"}:
        require(indexed[name]["exit_code"] == 1, "Unclassified refusal/setup result: " + name)
    require("missing mandatory parameters: InventoryPath CatalogPath" in (root / "catalog-exceptions.log").read_text(),
            "The retained setup failure has a different cause")
    audit = (root / "current-audit-refusal.log").read_text()
    require("failed with 6 error(s)" in audit and audit.count("no matching accepted audit selection") == 5,
            "Wrong revision-bound audit refusal")
    require("source changed: src/Hexalith.Folders.Server/FoldersServerHostComposition.cs" in
            (root / "current-source-proposal-refusal.log").read_text(), "Wrong proposal refusal")
    require("357 exact positive/negative probes" in (root / "catalog-authority.log").read_text() and
            "authority controls executed: 17" in (root / "catalog-authority.log").read_text(), "Wrong catalog result")
    candidate = load(root, "candidate-verification.json")
    require(candidate["entries_rehashed"] == 1635 and candidate["two_generations_equal_current"] is True and
            candidate["sha256"] == "8b3ab8eb2fb99e4a9eb07262bcb8281d8d1eee6d8f8d2d3999d1c76d76a10a0f",
            "Wrong candidate result")
    require(len(load(root, "candidate-commands.json")) == 2 and
            all(row["exit_code"] == 0 for row in load(root, "candidate-commands.json")), "Generation failed")
    require(load(root, "before.json") == load(root, "checks-after.json"), "Checks altered owner or protected inputs")
    comparison = load(root, "source-comparison.json")
    require(sum(owner["compared_files"] for owner in comparison["owners"].values()) == 13139 and
            sum(len(owner["changed"]) for owner in comparison["owners"].values()) == 3 and
            all(not owner["added"] and not owner["removed"] for owner in comparison["owners"].values()),
            "Wrong source-equivalence denominator")
    if arguments.workspace is not None:
        workspace = arguments.workspace.resolve()
        before = load(root, "before.json")
        for relative, owner in before["repositories"].items():
            checkout = workspace / relative
            head = subprocess.check_output(["git", "-C", str(checkout), "rev-parse", "HEAD"], text=True).strip()
            index = subprocess.check_output(["git", "-C", str(checkout), "ls-files", "--stage", "-z"])
            require(head == owner["head"] and hashlib.sha256(index).hexdigest() == owner["index_sha256"],
                    "Owner revision or index changed: " + relative)
        for relative, expected in before["protected"].items():
            require(sha(workspace / relative) == expected, "Protected input changed: " + relative)
        for document in receipt["document_appends"]:
            actual = (workspace / document["path"]).read_bytes()
            prefix = (root / document["prefix"]).read_bytes()
            require(actual.startswith(prefix) and hashlib.sha256(actual[len(prefix):]).hexdigest() == document["append_sha256"],
                    "Document prefix or accepted append changed: " + document["path"])
        prior_inputs = load(prior / "final", "source-inputs.json")
        allowed = {row["path"] for row in receipt["document_appends"]}
        for relative, owner in prior_inputs.items():
            for name, expected in owner["files"].items():
                if relative == "." and name in allowed:
                    continue
                require(sha(workspace / relative / name) == expected, "Reused source changed: " + relative + "/" + name)
        inventory = workspace / candidate["path"]
        require(sha(inventory) == candidate["sha256"], "Current candidate changed")
        rows = re.findall(r'  - path: "([^"\n]+)"\n    kind: file\n    bytes: (\d+)\n    sha256: ([a-f0-9]{64})', inventory.read_text())
        require(len(rows) == 1635, "Wrong current candidate denominator")
        for name, size, expected in rows:
            path = workspace / name
            require(path.stat().st_size == int(size) and sha(path) == expected, "Current candidate row changed: " + name)
        handoff = workspace / receipt["handoff_path"]
        require(sha(handoff) == receipt["handoff_sha256"] and load(workspace, receipt["handoff_path"])["controls"] == controls,
                "Handoff identity or inert controls changed")
    print("PASS: landed tuple evidence, reused artifacts, explicit blockers and inert controls verified.")


if __name__ == "__main__":
    main()
