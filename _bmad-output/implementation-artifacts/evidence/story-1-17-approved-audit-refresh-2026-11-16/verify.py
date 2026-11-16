"""Verify the scoped audit refresh and preservation of the owning workspace."""
import hashlib
import json
from pathlib import Path
import subprocess


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    evidence = Path(__file__).resolve().parent
    workspace = evidence.parents[3]
    before = json.loads((evidence / "before.json").read_text())
    result = json.loads((evidence / "result.json").read_text())
    receipt = json.loads((evidence / "receipt.json").read_text())
    prior = json.loads((evidence / "audit.before.json").read_text())
    audit_path = workspace / "references/Hexalith.Builds/Tools/package-version-audit.json"
    audit = json.loads(audit_path.read_text())
    require(sha(audit_path) == result["audit_sha256"] == receipt["audit_sha256"], "Current audit drift")
    require(sha(evidence / "audit.before.json") == before["audit_before_sha256"], "Prior audit drift")
    require(audit["generatedFromRevision"] == before["repositories"]["references/Hexalith.Builds"]["head"],
            "Wrong source revision")
    require(audit["snapshot"]["mode"] == "incremental" and
            audit["snapshot"]["refreshedFamilies"] == ["hexalith-folders"], "Unexpected feed refresh scope")
    for collection, identity, expected_count in [("packages", "id", 317), ("familyDecisions", "family", 157)]:
        old = {row[identity]: row for row in prior[collection]}
        new = {row[identity]: row for row in audit[collection]}
        require(len(new) == len(audit[collection]) == expected_count and old.keys() == new.keys(),
                "Changed or duplicate audit identities")
        for key, row in old.items():
            if row["family"] != "hexalith-folders":
                require(new[key] == row, "Unrelated row changed: " + key)
            history = row["historicalContext"]
            require(new[key]["historicalContext"][:len(history)] == history, "History rewritten: " + key)
    for row in audit["packages"]:
        if row["family"] == "hexalith-folders":
            require(row["auditedVersion"] == row["selectedVersion"] == row["latestStable"] == "2.0.0" and
                    row["listingState"] == "listed", "Wrong selected-version observation")
    old_consumers = {key: value for key, value in prior["consumerEvidence"].items() if key != "repositoryRevision"}
    new_consumers = {key: value for key, value in audit["consumerEvidence"].items() if key != "repositoryRevision"}
    require(old_consumers == new_consumers, "Owned consumer relations changed")
    controls = {"route_mode": "V1Only", "migration_t0": None, "activation_authority": None,
                "staging_checks_executed": 0, "parent_status": "in-progress", "tracker": "backlog"}
    require(receipt["controls"] == controls and receipt["story_closed"] is False, "Unsafe receipt claim")
    handoff = workspace / receipt["handoff_path"]
    require(sha(handoff) == receipt["handoff_sha256"] and
            json.loads(handoff.read_text())["controls"] == controls, "Handoff drift or unsafe controls")
    for relative, owner in before["repositories"].items():
        checkout = workspace / relative
        head = subprocess.check_output(["git", "-C", str(checkout), "rev-parse", "HEAD"], text=True).strip()
        index = subprocess.check_output(["git", "-C", str(checkout), "ls-files", "--stage", "-z"])
        require(head == owner["head"] and hashlib.sha256(index).hexdigest() == owner["index_sha256"],
                "Owning revision or index changed: " + relative)
    for relative, expected in before["protected"].items():
        require(sha(workspace / relative) == expected, "Protected input changed: " + relative)
    for relative, document in before["documents"].items():
        current = (workspace / relative).read_bytes()
        prefix = (evidence / document["prefix"]).read_bytes()
        require(current.startswith(prefix) and hashlib.sha256(current[len(prefix):]).hexdigest() ==
                receipt["append_sha256"][relative], "Document history or new result changed: " + relative)
    commands = json.loads((evidence / "commands.json").read_text())
    require([row["name"] for row in commands] == ["generate", "validate-candidate", "validate-owner"] and
            all(row["exit_code"] == 0 for row in commands), "Refresh or validation failed")
    manifest = json.loads((evidence / "artifact-manifest.json").read_text())
    for relative, expected in manifest.items():
        path = evidence / relative
        require(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(evidence) and
                sha(path) == expected, "Evidence member changed: " + relative)
    print("PASS: five Folders rows refreshed; 312 package rows, 156 family decisions, history and controls preserved.")


if __name__ == "__main__":
    main()
