#!/usr/bin/env python3
"""Verify the retained acquisition proof and, optionally, current source preservation."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(name):
    return json.loads((HERE / name).read_text())


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--packet-only", action="store_true")
    args = parser.parse_args()
    seal = read("manifest.json")
    require(set(seal) == {p.name for p in HERE.iterdir() if p.is_file() and p.name != "manifest.json"},
            "Packet membership differs from the seal")
    for name, expected in seal.items():
        require(digest(HERE / name) == expected, "Packet hash mismatch: " + name)
    proof = read("probe-result.final.json")
    proposal = read("proposal.json")
    require(digest(HERE / "owner-proposal.patch") == proposal["patch_sha256"], "Retained owner proposal changed")
    require(proof["helper_sha256"] == proposal["owner_files"][".github/actions/prepare-g6-catalog/prepare.py"],
            "Proposed helper differs from the tested helper")
    require(proof["before"] == proof["after"], "Acquisition changed source state")
    require(proof["before_read_exit"] == 128 and proof["after_read_exit"] == 0,
            "Missing-then-acquired read proof is inconsistent")
    require(proof["initial_builds_history_depth"] == 1, "Initial Builds history was not depth one")
    require(proof["missing_batch_check"] == proof["catalog_commit"] + " missing", "Missing object proof differs")
    require(proof["exact_catalog_bytes"] and not proof["nested_initialized"], "Exact-byte or nested-state proof failed")
    require(digest(HERE / "catalog.props") == proof["catalog_sha256"]
            and (HERE / "catalog.props").stat().st_size == proof["catalog_bytes"], "Retained catalog bytes differ")
    for name, revision in (("workspace", proof["projects_revision"]), ("builds", proof["builds_revision"]),
                           ("platform", proof["platform_revision"])):
        require(proof["before"][name]["head"] == revision, "Proof checkout subject differs: " + name)
    for result in (proof["acquisition"], proof["second_acquisition"]):
        require(result["status"] == "prepared", "Acquisition status differs")
        for field in ("catalog_commit", "catalog_sha256", "catalog_bytes", "platform_revision", "builds_revision"):
            require(result[field] == proof[field], "Acquisition subject differs: " + field)
        require(result["source_revision"] == proof["projects_revision"], "Projects source differs")
        for name, state in proof["before"].items():
            for field in ("head", "index_file_sha256", "index_sha256", "refs"):
                require(result["preserved"][name][field] == state[field], "Helper preservation subject differs")
    require(proof["acquisition"]["acquired"] and not proof["second_acquisition"]["acquired"], "Idempotence proof differs")
    require(proof["projects_revision"] == proposal["projects_base"] and not proposal["applied"], "Proposal subject differs")
    check = read("proposal-check.json")
    require(check["before"] == check["after"] and check["git_apply_check_exit"] == 0
            and check["disposable_apply_exit"] == 0 and check["exact_action_and_workflow_bytes"]
            and not check["live_module_applied"], "Disposable proposal check failed")
    blocker = read("owner-blocker.json")
    require(blocker["delivery_status"] == "blocked" and not blocker["delivery_attempted"]
            and not blocker["live_module_patch_applied"] and not blocker["workflow_dispatch_attempted"],
            "Delivery limit differs")
    if not args.packet_only:
        ROOT = HERE.parents[3]
        OWNER = ROOT.parent / "cicd"
        before = read("before.json")
        concurrent = read("concurrent-work.json")
        for path, state in before.items():
            if path == "protected":
                continue
            expected_head, expected_index = state["head"], state["index_sha256"]
            if path == concurrent["repository"]:
                require(concurrent["base"] == state["head"], "Concurrent source baseline differs")
                expected_head, expected_index = concurrent["head"], concurrent["index_sha256"]
                require(git(path, "diff", "--name-status", state["head"], expected_head).decode().splitlines()
                        == concurrent["committed_change_rows"], "Concurrent committed changes differ")
                for changed, expected in concurrent["committed_paths_sha256"].items():
                    require(changed.startswith("_bmad-output/") and digest(Path(path) / changed) == expected,
                            "Concurrent advance differs from the documented observation")
            require(git(path, "rev-parse", "HEAD").decode().strip() == expected_head, "Source HEAD changed: " + path)
            require(hashlib.sha256(git(path, "ls-files", "--stage", "-z")).hexdigest() == expected_index,
                    "Source index changed: " + path)
            links = [line for line in git(path, "ls-tree", "-r", "HEAD").decode().splitlines() if line.startswith("160000 ")]
            require(links == state["gitlinks"], "Source gitlinks changed: " + path)
            old_tracked = [line for line in state["status"].splitlines() if not line.startswith("?? ")]
            tracked = git(path, "status", "--porcelain", "--untracked-files=no", "--ignore-submodules=all").decode().splitlines()
            if path == concurrent["repository"]:
                for changed, expected in concurrent["paths"].items():
                    require(digest(Path(path) / changed) == expected, "Concurrent work changed after observation: " + changed)
                tracked = [line for line in tracked if line[3:] not in concurrent["paths"]]
            require(tracked == old_tracked, "Tracked worktree changed: " + path)
        for path, expected in before["protected"].items():
            require(digest(ROOT / path) == expected, "Protected tracking bytes changed: " + path)
        for path, expected in proposal["owner_files"].items():
            require(digest(OWNER / path) == expected, "Owner implementation changed: " + path)
        require(digest(OWNER / "docs/proposals/projects-g6-catalog.patch") == proposal["patch_sha256"], "Owner proposal changed")
        require(digest(OWNER / ".github/actions/prepare-g6-catalog/prepare.py") == proof["helper_sha256"], "Tested helper changed")
        for path, expected in blocker["owner_source_sha256"].items():
            require(digest(OWNER / path) == expected, "Owner delivery source changed: " + path)
        for path, expected in read("qualification-sources.json")["owner_files"].items():
            require(digest(OWNER / path) == expected, "Qualification source changed: " + path)
        projects = ROOT / "references/Hexalith.Projects"
        platform = ROOT / "references/Hexalith.Platform"
        for repository, revision, path, expected in (
                (projects, proof["projects_revision"], "references/Hexalith.Platform", proof["platform_revision"]),
                (projects, proof["projects_revision"], "references/Hexalith.Builds", proof["builds_revision"]),
                (platform, proof["platform_revision"], "references/Hexalith.Builds", proof["catalog_commit"])):
            row = git(repository, "ls-tree", revision, "--", path).decode().split()
            require(row == ["160000", "commit", expected, path], "Committed source selection chain differs")
        require(git(ROOT / "references/Hexalith.Builds", "show", proof["catalog_commit"] + ":Props/Directory.Packages.props")
                == (HERE / "catalog.props").read_bytes(), "Retained catalog differs from selected Git bytes")
    print("PASS: retained acquisition proof" + (" (packet only)" if args.packet_only else " and current source preservation")
          + "; owner delivery and fresh hosted CI/G-6 remain blocked")


if __name__ == "__main__":
    main()
