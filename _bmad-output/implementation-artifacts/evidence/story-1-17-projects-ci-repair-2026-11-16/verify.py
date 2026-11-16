"""Read-only integrity, result and preservation checks for this repair packet."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

sys.dont_write_bytecode = True
PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[3]
PRIOR = PACKET.parent / "story-1-17-closure-preparation-2026-11-16"


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_bytes())


def git(owner, *arguments):
    return subprocess.check_output(["git", "-C", str(owner), *arguments])


def packet_integrity():
    manifest = PACKET / "SHA256SUMS"
    require(manifest.is_file(), "Missing SHA256SUMS; packet is unsealed")
    names = set()
    for row in manifest.read_text().splitlines():
        expected, name = row.split("  ", 1)
        member = PACKET / name
        require(member.resolve().is_relative_to(PACKET) and not member.is_symlink()
                and name not in names and re.fullmatch(r"[a-f0-9]{64}", expected), "Invalid packet manifest")
        require(sha(member) == expected, "Packet member changed: " + name)
        names.add(name)
    actual = {str(item.relative_to(PACKET)) for item in PACKET.rglob("*") if item.is_file()
              and str(item.relative_to(PACKET)) not in {"SHA256SUMS", "verification.log"}}
    require(names == actual, "Packet manifest incomplete")


def verify(packet_only=False):
    packet_integrity()
    receipt = read(PACKET / "receipt.json")
    before = read(PACKET / receipt["previous_handoff_snapshot"])
    require(sha(PACKET / receipt["previous_handoff_snapshot"]) == receipt["previous_handoff_sha256"], "Prior handoff changed")
    if packet_only:
        print("PASS: sealed historical repair packet verified; current workspace, source diff and operational state not evaluated.")
        return
    handoff = read(ROOT / receipt["handoff"])
    require(sha(ROOT / receipt["handoff"]) == receipt["handoff_sha256"], "Handoff changed")
    old_receipt = read(PRIOR / "receipt.json")
    require(receipt["previous_handoff_sha256"] == old_receipt["new_handoff_sha256"], "Historical handoff binding differs")
    require(sha(ROOT / receipt["owner_next_action"]) == receipt["owner_next_action_sha256"], "Owner action changed")
    fixture = ROOT / "references/Hexalith.Projects/tests/tools/test_release_package_tools.py"
    require(sha(fixture) == receipt["fixture_sha256"], "Fixture changed")
    require(sha(PACKET / receipt["projects_fixture_patch"]) == receipt["fixture_patch_sha256"], "Fixture patch changed")
    patch = git(ROOT / "references/Hexalith.Projects", "diff", "--binary", "--", "tests/tools/test_release_package_tools.py")
    require(patch == (PACKET / receipt["projects_fixture_patch"]).read_bytes(), "Worktree fixture diff differs")
    for key in ("controls", "tracking", "completed_do_not_repeat", "released_pair"):
        require(handoff[key] == before[key], "Protected handoff field changed: " + key)
    require(handoff["controls"]["route_mode"] == "V1Only" and handoff["controls"]["migration_t0"] is None
            and handoff["controls"]["activation_authority"] is None and not handoff["story_closed"], "Unsafe controls")
    require(not handoff["tracking"]["transition_authorized"] and not handoff["tracking"]["applied_delta"], "Tracking changed")
    stages = read(ROOT / "eng/story117-platform-qualification-current/staging-checks.json")
    require(len(stages["checks"]) == 14 and all(row["status"] == "unexecuted" for row in stages["checks"]), "Staging changed")

    historical = read(PRIOR / "before.json")
    appends = read(PRIOR / "document-appends.json")
    for relative, expected in historical["protected"].items():
        if relative in appends:
            expected = appends[relative]["final_sha256"]
        require(sha(ROOT / relative) == expected, "Historical protected bytes changed: " + relative)
    for relative, owner in historical["repositories"].items():
        require(git(ROOT / relative, "rev-parse", "HEAD").decode().strip() == owner["head"], "HEAD changed: " + relative)
        require(hashlib.sha256(git(ROOT / relative, "ls-files", "--stage", "-z")).hexdigest() == owner["index_sha256"], "Index changed: " + relative)
    for row in (PRIOR / "SHA256SUMS").read_text().splitlines():
        expected, name = row.split("  ", 1)
        require(Path(name).name == name and sha(PRIOR / name) == expected, "Historical packet changed: " + name)

    product = read(PACKET / "product-results.json")
    require(product["projects_head"] == receipt["source_revisions"]["references/Hexalith.Projects"]
            == handoff["projects_ci_repair"]["source_head"], "Product source identities differ")
    require(product["builds_declared_pin"] == handoff["projects_ci_repair"]["builds_declared_pin"], "Product Builds pin differs")
    require(product["fixture_sha256"] == receipt["fixture_sha256"], "Product fixture identity differs")
    for name, expected in product["snapshot_file_hashes"].items():
        require(sha(ROOT / "references/Hexalith.Projects" / name) == expected, "Product snapshot drift: " + name)
    rows = {row["label"]: row for row in product["results"]}
    require(rows["product-solution-restore"]["exit_code"] == 1 and rows["product-server-build"]["exit_code"] == 1, "Product blocker outcomes differ")
    expected_tests = {"contracts": 207, "client": 125, "core": 691, "ui": 277, "mcp": 35, "cli": 22}
    for project, count in expected_tests.items():
        result = rows["product-" + project + "-tests"]
        require(result["exit_code"] == 0, "Product test failed: " + project)
        output = (PACKET / result["log"]).read_text()
        require(f"Total: {count}, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0" in output, "Product test denominator changed")
    require(sum(expected_tests.values()) == 1357, "Wrong total")
    require(handoff["projects_ci_repair"]["local_product_tests"]["passed"] == sum(expected_tests.values())
            and handoff["projects_ci_repair"]["local_product_tests"]["failed"] == 0
            and handoff["projects_ci_repair"]["local_product_tests"]["skipped"] == 0, "Handoff product totals differ")
    for row in product["results"]:
        require(sha(PACKET / row["log"]) == row["log_sha256"], "Product log changed")
    packages = read(PACKET / "package-results.json")
    require(packages["source"] == product["projects_head"] and packages["builds_declared_pin"] == product["builds_declared_pin"], "Original package tuple differs")
    require(len(packages["results"]) == 6 and all(row["exit_code"] == 0 for row in packages["results"][:5]), "Pack outcomes changed")
    require(packages["results"][-1]["label"] == "package-manifest-validation" and packages["results"][-1]["exit_code"] == 1, "Manifest outcome changed")
    for row in packages["results"]:
        require(sha(PACKET / row["log"]) == row["sha256"], "Package log changed")
    require("wrong_versions=['Hexalith.EventStore.Contracts']" in (PACKET / "package-manifest-validation.log").read_text(), "Manifest blocker changed")
    require("error NU1109" in (PACKET / "product-server-build.log").read_text(), "Host dependency blocker missing")
    require("KeyError: 'Hexalith.Conversations.Contracts'" in (PACKET / "fixture-before.log").read_text(), "Failure reproduction missing")
    after = (PACKET / "fixtures-after.log").read_text()
    require("Ran 20 tests" in after and "\nOK\n" in after, "Focused fixture result missing")
    require(handoff["projects_ci_repair"]["focused_fixtures"] == {"passed": 20, "failed": 0}, "Handoff fixture totals differ")

    replay = PACKET / "package-replay"
    replay_results = read(replay / "results.json")
    require(replay_results["source"] == product["projects_head"] and replay_results["builds_declared_pin"] == product["builds_declared_pin"], "Package replay tuple differs")
    require(sha(replay / replay_results["input_manifest"]) == replay_results["input_manifest_sha256"], "Package input manifest changed")
    require(read(replay / replay_results["input_manifest"]) == product["snapshot_file_hashes"], "Separate package snapshot differs from product inputs")
    for row in replay_results["results"]:
        require(sha(replay / row["log"]) == row["sha256"], "Package replay log changed")
    require(replay_results["results"][-1]["label"] == "package-manifest-validation"
            and replay_results["results"][-1]["exit_code"] == 1, "Package replay did not reproduce rejection")
    report = read(replay / "dependency-report.json")
    require(len(report) == 5, "Missing candidate metadata")
    for row in report:
        require(sha(replay / row["nuspec"]) == row["nuspec_sha256"], "Candidate nuspec changed")
    core = next(row for row in report if row["archive"].startswith("Hexalith.Projects.91."))
    actual = next(row["version"] for row in core["dependencies"] if row["id"] == "Hexalith.EventStore.Contracts")
    require(actual == "5.1.1", "Actual EventStore dependency differs")

    payloads = read(PACKET / "folders-payload-binding.json")["packages"]
    ids = {"Hexalith.Folders.Client", "Hexalith.Folders.Contracts"}
    require(len(payloads) == 2 and {row["id"] for row in payloads} == ids, "Missing or duplicate Folders payload binding")
    expected_pair = {row["id"]: row for row in before["released_pair"]["retained_packages"] if row["id"] in ids}
    for package in payloads:
        for key, expected in expected_pair[package["id"]].items():
            require(package[key] == expected, "Folders pair differs from protected handoff: " + key)
        archive = ROOT / package["retained_path"]
        require(sha(archive) == package["sha256"], "Folders archive changed")
        with zipfile.ZipFile(archive) as contents:
            require(hashlib.sha256(contents.read(package["payload_member"])).hexdigest() == package["payload_sha256"], "Folders DLL changed")

    print("PASS: 20 fixtures, 1357 product tests, both explicit product blockers, source snapshot, exact Folders pair, 200 protected files, 13 HEADs/indices, prior packet and inert controls verified. No operational acceptance.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-only", action="store_true", help="Verify sealed historical bytes without requiring the original live unstaged diff.")
    args = parser.parse_args()
    try:
        verify(args.packet_only)
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        sys.exit(1)
