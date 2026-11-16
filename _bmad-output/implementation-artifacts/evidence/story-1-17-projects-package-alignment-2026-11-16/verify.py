"""Verify the sealed alignment evidence and, by default, its current workspace."""

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[3]


def require(value, message):
    if not value:
        raise ValueError(message)


def read(path):
    return json.loads(path.read_bytes())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(owner, *arguments):
    return subprocess.check_output(["git", "-C", str(owner), *arguments])


def seal():
    manifest = PACKET / "SHA256SUMS"
    require(manifest.is_file(), "Missing SHA256SUMS; unsealed packet")
    members = set()
    for line in manifest.read_text().splitlines():
        expected, name = line.split("  ", 1)
        item = PACKET / name
        require(item.resolve().is_relative_to(PACKET) and not item.is_symlink() and name not in members
                and re.fullmatch(r"[a-f0-9]{64}", expected), "Invalid packet manifest")
        require(sha(item) == expected, "Packet member changed: " + name)
        members.add(name)
    actual = {str(item.relative_to(PACKET)) for item in PACKET.rglob("*") if item.is_file()
              and str(item.relative_to(PACKET)) not in {"SHA256SUMS", "verification.log"}}
    require(members == actual, "Incomplete packet manifest")


def results(directory, filename, hash_key):
    ledger = read(PACKET / directory / filename)
    for row in ledger["results"]:
        require(sha(PACKET / directory / row["log"]) == row[hash_key], "Changed command log")
    return ledger, {row["label"]: row for row in ledger["results"]}


def dependency_bytes(owner, info):
    data = git(owner, "archive", info["pin"], "--", *info.get("selected_paths", []))
    require(hashlib.sha256(data).hexdigest() == info["archive_sha256"], "Dependency archive differs")
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        files = {member.name: hashlib.sha256(archive.extractfile(member).read()).hexdigest()
                 for member in archive.getmembers() if member.isfile()}
    require(files == info["file_hashes"], "Dependency file inventory differs")


def verify(packet_only):
    seal()
    receipt = read(PACKET / "receipt.json")
    before = read(PACKET / receipt["prior_handoff_snapshot"])
    require(sha(PACKET / receipt["prior_handoff_snapshot"]) == receipt["prior_handoff_sha256"], "Prior handoff changed")
    require(sha(PACKET / receipt["source_patch"]) == receipt["source_patch_sha256"], "Source patch changed")
    if packet_only:
        print("PASS: sealed historical package-alignment evidence verified; no current workspace or operational acceptance claim.")
        return

    handoff = read(ROOT / receipt["handoff"])
    require(sha(ROOT / receipt["handoff"]) == receipt["handoff_sha256"], "Current handoff differs")
    require(sha(ROOT / receipt["owner_next_action"]) == receipt["owner_next_action_sha256"], "Current owner action differs")
    for field in ["controls", "tracking", "completed_do_not_repeat", "released_pair"]:
        require(before[field] == handoff[field] == receipt[field], "Protected field differs: " + field)
    require(handoff["controls"]["route_mode"] == "V1Only" and handoff["controls"]["migration_t0"] is None
            and handoff["controls"]["activation_authority"] is None and not handoff["story_closed"], "Unsafe controls")
    projects = ROOT / "references/Hexalith.Projects"
    patch = git(projects, "diff", "--binary", "--", "Directory.Packages.props", "tools/release-packages.json", "tests/tools/test_release_package_tools.py")
    require(patch == (PACKET / receipt["source_patch"]).read_bytes(), "Live source patch differs")

    historical = PACKET.parent / "story-1-17-closure-preparation-2026-11-16"
    old = read(historical / "before.json")
    appends = read(historical / "document-appends.json")
    for relative, expected in old["protected"].items():
        if relative in appends:
            expected = appends[relative]["final_sha256"]
        require(sha(ROOT / relative) == expected, "Protected historical file differs: " + relative)
    for relative, owner in old["repositories"].items():
        require(git(ROOT / relative, "rev-parse", "HEAD").decode().strip() == owner["head"], "HEAD changed")
        require(hashlib.sha256(git(ROOT / relative, "ls-files", "--stage", "-z")).hexdigest() == owner["index_sha256"], "Index changed")
    for packet_name in ["story-1-17-closure-preparation-2026-11-16", "story-1-17-projects-ci-repair-2026-11-16"]:
        prior = PACKET.parent / packet_name
        for line in (prior / "SHA256SUMS").read_text().splitlines():
            expected, name = line.split("  ", 1)
            require(sha(prior / name) == expected, "Historical sealed packet changed")
    stages = read(ROOT / "eng/story117-platform-qualification-current/staging-checks.json")
    require(len(stages["checks"]) == 14 and all(row["status"] == "unexecuted" for row in stages["checks"]), "Staging changed")

    product, unit = results("product", "product-results.json", "log_sha256")
    broad, broad_rows = results("product-qualified", "product-results.json", "log_sha256")
    integration, integration_rows = results("integration-qualified", "product-results.json", "log_sha256")
    packages, package_rows = results("packages-qualified", "results.json", "sha256")
    labels = {"sdk-info", "package-projects-contracts", "package-projects", "package-projects-client",
              "package-projects-testing", "package-projects-servicedefaults", "package-manifest-validation", "package-api-consumers"}
    require(set(package_rows) == labels and len(packages["results"]) == len(labels), "Missing/duplicate package command")
    require(broad_rows["product-solution-build"]["exit_code"] == 0, "Solution build failed")
    for label in ["openapi-fingerprint-gate", "frontcomposer-inspect-gate", "mcpcli-decoration-tests"]:
        require(broad_rows[label]["exit_code"] == 0, "Generated/compatibility gate failed")
    require(all(row["exit_code"] == 0 for row in packages["results"]), "Strict package/consumer stage failed")
    require("Validated 5 isolated package consumers." in (PACKET / "packages-qualified/package-api-consumers.log").read_text(), "Missing five-consumer result")
    require(packages["source"] == product["projects_head"] == receipt["source_revisions"]["references/Hexalith.Projects"]
            == handoff["projects_package_alignment"]["source_head"], "Source tuple differs")
    require(packages["builds_declared_pin"] == product["builds_declared_pin"] == broad["builds_declared_pin"]
            == integration["builds_declared_pin"], "Builds tuple differs")
    require(sha(PACKET / "packages-qualified" / packages["input_manifest"]) == packages["input_manifest_sha256"], "Package source manifest differs")
    require(read(PACKET / "packages-qualified" / packages["input_manifest"]) == product["snapshot_file_hashes"]
            == broad["snapshot_file_hashes"] == integration["snapshot_file_hashes"], "Separate source snapshots differ")
    for name, expected in product["snapshot_file_hashes"].items():
        require(sha(projects / name) == expected, "Current product source differs: " + name)
    for directory in ["product-qualified", "integration-qualified"]:
        for owner, info in read(PACKET / directory / "dependency-inputs.json").items():
            declared = git(projects, "rev-parse", "HEAD:references/" + owner).decode().strip()
            require(info["pin"] == declared, "Dependency source pin differs")
            dependency_bytes(ROOT if owner == "Hexalith.Folders" else ROOT / "references" / owner, info)
    builds = read(PACKET / "builds-input-reconstruction.json")
    require(builds["pin"] == product["builds_declared_pin"], "Reconstructed Builds pin differs")
    dependency_bytes(ROOT / "references/Hexalith.Builds", builds)

    summary = read(PACKET / receipt["test_summary"])
    for project in summary["projects"]:
        row = integration_rows["product-integration-tests"] if project["project"] == "integration" else unit["product-" + project["project"] + "-tests"]
        require(row["exit_code"] == 0, "Product test command failed")
        output = (PACKET / project["log"]).read_text()
        expected = f'Total: {project["total"]}, Errors: 0, Failed: {project["failed"]}, Skipped: {project["skipped"]}, Not Run: 0'
        require(expected in output, "Test denominator differs")
    require(summary["passed"] == sum(row["passed"] for row in summary["projects"]) == 2837
            and summary["skipped"] == 1 and summary["failed"] == 0, "Test totals differ")
    fixture = read(PACKET / "fixture-command.json")
    require(fixture["exit_code"] == 0 and sha(PACKET / fixture["log"]) == fixture["log_sha256"], "Fixture command changed")
    require(fixture["fixture_sha256"] == sha(projects / "tests/tools/test_release_package_tools.py")
            and fixture["manifest_sha256"] == sha(projects / "tools/release-packages.json"), "Fixture source binding differs")
    fixture_output = (PACKET / fixture["log"]).read_text()
    require("Ran 20 tests" in fixture_output and "\nOK\n" in fixture_output, "Fixtures did not pass")
    inspection = read(PACKET / "api-inspection-command.json")
    require(inspection["exit_code"] == 0 and sha(PACKET / inspection["output"]) == inspection["output_sha256"], "API inspection changed")
    api = read(PACKET / inspection["output"])["document"]
    require(api["before"]["isComplete"] and api["after"]["isComplete"] and api["summary"]["changedTypeCount"] == 0
            and api["summary"]["changedMemberCount"] == 0 and api["summary"]["breakingCount"] == 0, "API diff changed")
    report = read(PACKET / "packages-qualified/dependency-report.json")
    require(len(report) == 5, "Candidate inventory differs")
    expected_ids = {"Hexalith.Projects", "Hexalith.Projects.Contracts", "Hexalith.Projects.Client",
                    "Hexalith.Projects.Testing", "Hexalith.Projects.ServiceDefaults"}
    actual_ids = set()
    for row in report:
        require(sha(PACKET / "packages-qualified" / row["nuspec"]) == row["nuspec_sha256"], "Actual nuspec differs")
        parsed = ET.fromstring((PACKET / "packages-qualified" / row["nuspec"]).read_bytes())
        package_id = next(item.text for item in parsed.iter() if item.tag.split("}")[-1] == "id")
        require(package_id in expected_ids and package_id not in actual_ids, "Unexpected/duplicate candidate ID")
        actual_ids.add(package_id)
        actual_dependencies = {item.attrib["id"]: item.attrib["version"] for item in parsed.iter() if item.tag.split("}")[-1] == "dependency"}
        if package_id != "Hexalith.Projects.ServiceDefaults":
            require(actual_dependencies.get("Hexalith.McpCli.Abstractions") == "3.0.0"
                    and actual_dependencies.get("Hexalith.EventStore.Contracts") == "5.1.1", "Aligned dependency absent")
        for dependency in row["dependencies"]:
            if dependency["id"] in {"Hexalith.McpCli.Abstractions", "Hexalith.EventStore.Contracts"}:
                require(dependency["version"] == {"Hexalith.McpCli.Abstractions": "3.0.0", "Hexalith.EventStore.Contracts": "5.1.1"}[dependency["id"]], "Actual dependency mismatch")
    require(actual_ids == expected_ids, "Five-package identity set differs")
    payloads = read(PACKET / "product-qualified/server-folders-payload-binding.json")
    ids = {"Hexalith.Folders.Client", "Hexalith.Folders.Contracts"}
    require(len(payloads) == 2 and {row["id"] for row in payloads} == ids, "Payload pair incomplete")
    expected = {row["id"]: row for row in before["released_pair"]["retained_packages"] if row["id"] in ids}
    for row in payloads:
        require(row["version"] == expected[row["id"]]["version"] == "2.0.0" and row["matches_retained_payload"]
                and row["build_output_sha256"] == expected[row["id"]]["payload_sha256"], "Server Folders payload changed")
    print("PASS: 2837 product passes/one existing Dapr skip, 20 fixtures, five strict packages and consumers, generated/compatibility gates, exact Folders DLLs, preserved prior evidence/controls and source tuple verified. No hosted acceptance.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-only", action="store_true")
    args = parser.parse_args()
    try:
        verify(args.packet_only)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        sys.exit(1)
