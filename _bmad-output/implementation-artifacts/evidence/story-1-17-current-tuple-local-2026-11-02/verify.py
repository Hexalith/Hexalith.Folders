#!/usr/bin/env python3
"""Replay the bounded Story 1.17 local evidence against its exact workspace tuple."""

import argparse
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

import yaml


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def git(root, *arguments):
    return subprocess.check_output(["git", "-C", str(root), *arguments], text=True).strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace-root", type=Path, default=Path(__file__).resolve().parents[4])
    arguments = parser.parse_args()
    root = arguments.workspace_root.resolve()
    capsule = Path(__file__).resolve().parent
    receipt = json.loads((capsule / "receipt.json").read_bytes())

    require(receipt["status"] == "HOLD", "unsafe receipt status")
    require(receipt["controls"] == {"routeMode": "V1Only", "t0": None, "activationAuthority": None, "tracker": "backlog", "stagingChecksExecuted": 0}, "unsafe operational controls")
    for name, metadata in receipt["members"].items():
        content = (capsule / name).read_bytes()
        require(len(content) == metadata["bytes"] and digest(content) == metadata["sha256"], "evidence member drift: " + name)

    tuple_data = json.loads((capsule / "source-tuple.json").read_bytes())
    require(git(root, "rev-parse", "HEAD") == tuple_data["foldersHead"], "Folders revision changed")
    for row in tuple_data["repositories"]:
        owner = root / "references" / row["repository"]
        tree = git(root, "ls-tree", "HEAD", "--", "references/" + row["repository"]).split()[2]
        require(tree == row["rootGitlink"] == git(owner, "rev-parse", "HEAD"), "owner revision changed: " + row["repository"])
    platform = root / "references/Hexalith.Platform"
    for name, revision in tuple_data["platformDirectGitlinks"].items():
        actual = git(platform, "ls-tree", "HEAD", "--", "references/" + name).split()[2]
        require(actual == revision, "Platform direct gitlink changed: " + name)
    require(not (platform / "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json").is_file(), "Platform nested declaration condition changed")

    for path, expected in receipt["protectedFiles"].items():
        require(digest((root / path).read_bytes()) == expected, "protected file changed: " + path)
    for path, prefix in receipt["preservedPrefixes"].items():
        content = (root / path).read_bytes()
        require(len(content) >= prefix["bytes"] and digest(content[:prefix["bytes"]]) == prefix["sha256"], "original document prefix changed: " + path)
    require("1-17-publish-the-pd10-v2-authorization-contract-spine: backlog" in (root / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text(), "tracker changed")
    require("null => FoldersApiRoutingMode.V1Only" in (root / "src/Hexalith.Folders.Server/FoldersApiRouting.cs").read_text(), "default routing changed")

    audit = json.loads((capsule / "candidate-audit.json").read_bytes())
    manifest = root / audit["manifestPath"]
    require(digest(manifest.read_bytes()) == audit["manifestSha256"], "pending inventory changed")
    data = yaml.safe_load(manifest.read_bytes())
    require(data["artifact_count"] == audit["recordedArtifactCount"] == len(data["artifacts"]), "candidate row count changed")
    mismatches = []
    for row in data["artifacts"]:
        content = (root / row["path"]).read_bytes()
        if len(content) != row["bytes"] or digest(content) != row["sha256"]:
            mismatches.append(row["path"])
    require(mismatches == [row["path"] for row in audit["mismatches"]], "candidate drift changed")
    allow = set((root / "scripts/pd10-v2-story-owned-paths.txt").read_text().splitlines())
    missing = sorted(str(path.relative_to(root)) for folder in (root / "scripts/pd10-v2-coverage-roots.txt").read_text().splitlines() for path in (root / folder).rglob("*") if path.suffix in (".cs", ".csproj") and not set(path.relative_to(root).parts) & {"bin", "obj"} and str(path.relative_to(root)) not in allow)
    require(missing == audit["missingCompileInputs"], "compile-input coverage changed")

    for part in ("Client", "Contracts"):
        archive = capsule / f"hexalith.folders.{part.lower()}.1.3.3.nupkg"
        runtime = (capsule / f"projects-runtime-Hexalith.Folders.{part}.dll").read_bytes()
        with zipfile.ZipFile(archive) as package:
            packaged = package.read(f"lib/net10.0/Hexalith.Folders.{part}.dll")
        require(runtime == packaged, "Projects runtime differs from cached package: " + part)

    print("PASS: exact local tuple, protected prefixes/controls, candidate drift and 1.3.3 runtime payloads")


if __name__ == "__main__":
    main()
