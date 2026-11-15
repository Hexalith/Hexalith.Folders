"""Replay retained Story 1.17 evidence without network access or assertions."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(root, name):
    return json.loads((root / name).read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    receipt = read(root, "receipt.json")
    expected_controls = {"route_mode": "V1Only", "migration_t0": None,
                         "activation_authority": None, "staging_checks_executed": 0,
                         "parent_status": "in-progress", "tracker": "backlog"}
    require(receipt["controls"] == expected_controls, "Unsafe operational controls")
    require(receipt["status"] == "local-consumer-verified-operational-hold", "Unsupported qualification claim")
    require(receipt["catalog_committed"] is False and receipt["projects_local_version_override"] is False,
            "Unsupported catalog/source claim")
    require(receipt["hosted_projects_artifact"] is None, "Hosted artifact was not established")
    before_catalog = (root / "catalog.before.props").read_bytes()
    after_catalog = (root / "catalog.after.props").read_bytes()
    old_default = b'<HexalithFoldersVersion Condition="\'$(HexalithFoldersVersion)\' == \'\'">1.4.0</HexalithFoldersVersion>'
    require(before_catalog.count(old_default) == 1 and
            after_catalog == before_catalog.replace(old_default, old_default.replace(b"1.4.0", b"2.0.0")),
            "Catalog changed beyond the intended family default")
    manifest = read(root, "artifact-manifest.json")
    for relative, expected in manifest.items():
        path = root / relative
        require(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(root),
                "Missing or unsafe retained file: " + relative)
        require(digest(path) == expected, "Retained file changed: " + relative)
    commands = read(root / "final", "commands.json")
    index = {row["name"]: row for row in commands}
    require(len(index) == len(commands), "Duplicate command identity")
    required = {"catalog-validation", "catalog-authority", "catalog-platform", "catalog-exceptions",
                "projects-release-restore", "projects-release-build", "folders-release-restore",
                "folders-release-build", "folders-oracle-build", "folders-parity", "folders-governance",
                "folders-debug-restore", "folders-debug-build"} | {"projects-tests-" + str(n) for n in range(6)}
    for name in required:
        require(name in index and index[name]["exit_code"] == 0, "Required gate did not pass: " + name)
    for row in commands:
        expected = 1 if row["name"] in {"audit-precommit-refusal", "audit-current-refusal"} else 0
        require(row["exit_code"] == expected, "Unclassified command outcome")
    require("is dirty relative to generated-from revision" in (root / "final/audit-precommit-refusal.log").read_text(),
            "Audit refusal has an unexpected cause")
    require("no matching accepted audit selection" in (root / "final/audit-current-refusal.log").read_text(),
            "Audit drift was not retained")
    total = 0
    for ordinal, selector in enumerate(receipt["test_selectors"]):
        tests = ET.parse(root / "final" / ("projects-" + str(ordinal) + ".xml")).getroot().findall(".//test")
        require(tests and all(test.attrib.get("result") == "Pass" for test in tests), "Nonpassing/empty selector")
        require(all(test.attrib.get("type") == selector for test in tests), "Wrong test selector")
        require(len(tests) == receipt["test_counts"][selector], "Test count changed")
        total += len(tests)
    require(total == receipt["test_total"], "Aggregate test count changed")
    parity = read(root / "final", "parity-report.json")
    require(parity["status"] == "passed" and len(parity["results"]) == 12
            and all(row["status"] == "passed" for row in parity["results"]), "Parity did not pass all categories")
    packages = read(root / "final", "public-packages.json")
    publication = read(root / "final", "publication-verification.retained.json")
    require(publication["version"] == "2.0.0" and publication["status"] == "verified", "Invalid retained publication")
    expected_source = "209dacbdb42fcde728d9e190781b69945bccc93b"
    require(publication["source"] == expected_source, "Wrong release source")
    require(len(packages) == 5 and len({row["id"] for row in packages}) == 5, "Wrong public package set")
    package_index = {row["id"]: row for row in packages}
    for package in packages:
        path = root / "final" / package["archive"]
        require(digest(path) == package["sha256"], "Public archive changed")
        retained = next(row for row in publication["packages"] if row["id"] == package["id"])
        require(package["sha256"] == retained["public_archive_sha256"], "Public archive differs from publication")
        with zipfile.ZipFile(path) as archive:
            metadata = ET.fromstring(archive.read(package["id"] + ".nuspec")).find("{*}metadata")
            require(metadata.findtext("{*}id") == package["id"] and metadata.findtext("{*}version") == "2.0.0",
                    "Wrong package identity")
            require(metadata.find("{*}repository").attrib["commit"] == expected_source, "Wrong package source")
            require(".signature.p7s" in archive.namelist(), "Missing repository signature")
            require(hashlib.sha256(archive.read(package["payload_member"])).hexdigest() == package["payload_sha256"],
                    "Package payload changed")
    bindings = read(root / "final", "runtime-bindings.json")
    require(len(bindings) == 4 and {(row["project"], row["package"]) for row in bindings} == {
        (project, package + "/2.0.0") for project in ("server", "tests")
        for package in ("Hexalith.Folders.Client", "Hexalith.Folders.Contracts")}, "Incomplete consumer binding")
    for row in bindings:
        package = package_index[row["package"].split("/")[0]]
        require(row["archive_sha256"] == package["sha256"] and row["runtime_sha256"] == package["payload_sha256"],
                "Consumer runtime differs from public package")
    artifact = read(root / "final", "projects-local-artifact.json")
    require("unpublished local" in artifact["kind"], "Local build was mislabelled")
    require(digest(root / "final" / artifact["archive"]) == artifact["sha256"], "Local artifact changed")
    with zipfile.ZipFile(root / "final" / artifact["archive"]) as archive:
        require(set(archive.namelist()) == {row["path"] for row in artifact["members"]}, "Artifact member set changed")
        for member in artifact["members"]:
            require(hashlib.sha256(archive.read(member["path"])).hexdigest() == member["sha256"], "Artifact member changed")
        for package in ("Hexalith.Folders.Client", "Hexalith.Folders.Contracts"):
            require(hashlib.sha256(archive.read(package + ".dll")).hexdigest() == package_index[package]["payload_sha256"],
                    "Retained runtime bundle contains the wrong pair")
    proposal_root = root / "provisional-audit"
    proposal = read(proposal_root, "result.json")
    require(receipt["provisional_audit"]["temporary_source_commit"] == proposal["temporary_source_commit"],
            "Provisional source identity changed")
    require(proposal["status"] == "validated-provisional-only" and proposal["owning_head_or_index_changed"] is False
            and proposal["live_audit_replaced"] is False, "Unsupported owning audit claim")
    require(proposal["temporary_source_commit"] != proposal["owner_base"], "Provisional identity is not distinct")
    for row in read(proposal_root, "commands.json"):
        require(row["exit_code"] == 0, "Provisional audit command failed")
    require(digest(proposal_root / proposal["source_patch"]) == receipt["catalog_selection"]["patch_sha256"],
            "Provisional source patch differs from the qualified owning delta")
    require(digest(proposal_root / proposal["source_bundle"]) == proposal["source_bundle_sha256"], "Source bundle changed")
    require(digest(proposal_root / proposal["proposal"]) == proposal["proposal_sha256"], "Audit proposal changed")
    old_bytes = gzip.decompress((proposal_root / "package-version-audit.before.json.gz").read_bytes())
    require(hashlib.sha256(old_bytes).hexdigest() == proposal["prior_audit_sha256"], "Prior audit changed")
    old = json.loads(old_bytes)
    new = read(proposal_root, proposal["proposal"])
    require(new["generatedFromRevision"] == proposal["temporary_source_commit"]
            and new["snapshot"]["refreshedFamilies"] == ["hexalith-folders"], "Wrong provisional audit binding")
    for key, identity in (("packages", "id"), ("familyDecisions", "family")):
        previous = {row[identity]: row for row in old[key]}
        current = {row[identity]: row for row in new[key]}
        require(set(previous) == set(current), "Audit inventory changed")
        for name, row in previous.items():
            require(all(item in current[name]["historicalContext"] for item in row["historicalContext"]),
                    "Audit lost historical observations")
            if row["family"] != "hexalith-folders":
                require(current[name] == row, "Unrequested audit row changed")
            elif key == "packages":
                require(current[name]["selectedVersion"] == "2.0.0" and current[name]["listingState"] == "listed",
                        "Wrong provisional Folders selection")
    if args.workspace:
        workspace = args.workspace.resolve()
        before = read(root, "before.json")
        for relative, expected in before["protected"].items():
            require(digest(workspace / relative) == expected, "Protected input changed: " + relative)
        for relative, old in ((receipt["spec"], "spec.before.md"), (receipt["readiness"], "readiness.before.md"),
                              (receipt["migration"], "migration.before.md")):
            require((workspace / relative).read_bytes().startswith((root / old).read_bytes()), "Historical prefix changed")
        for relative, identity in before["repositories"].items():
            owner = workspace / relative
            current = subprocess.check_output(["git", "-C", str(owner), "rev-parse", "HEAD"], text=True).strip()
            require(current == identity["head"], "Owning HEAD changed: " + relative)
            index_bytes = subprocess.check_output(["git", "-C", str(owner), "ls-files", "--stage", "-z"])
            require(hashlib.sha256(index_bytes).hexdigest() == identity["index_sha256"], "Owning index changed")
        for owner, snapshot in read(root / "final", "source-inputs.json").items():
            for relative, expected in snapshot["files"].items():
                if owner == "." and relative in {receipt["spec"], receipt["readiness"], receipt["migration"]}:
                    continue
                require(digest(workspace / owner / relative) == expected, "Qualified input changed: " + owner + "/" + relative)
        inventory = (workspace / receipt["candidate_inventory"]).read_text()
        entries = re.findall(r'^  - path: ("[^\n]+")\n    kind: file\n    bytes: ([0-9]+)\n    sha256: ([0-9a-f]{64})$',
                             inventory, re.MULTILINE)
        require(len(entries) == receipt["candidate_artifacts"], "Candidate inventory coverage changed")
        material = []
        for encoded, size, expected in entries:
            relative = json.loads(encoded)
            path = workspace / relative
            require(path.resolve().is_relative_to(workspace) and not path.is_symlink(), "Unsafe candidate input")
            require(path.stat().st_size == int(size) and digest(path) == expected, "Candidate input changed: " + relative)
            material.append(relative + "\0" + expected + "\n")
        require(hashlib.sha256("".join(material).encode()).hexdigest() == receipt["candidate_set_sha256"],
                "Candidate ordered-set digest changed")
        require("approval_status: pending-a6b" in inventory and "production_exposure: disabled" in inventory
                and "production_routed: false" in inventory and "story_closure_claimed: false" in inventory,
                "Pending inventory acquired unsupported authority")
    print("PASS: retained packages, local consumer, all gates, audit refusal, inert controls" +
          (", protected/current workspace inputs" if args.workspace else ""))


if __name__ == "__main__":
    main()
