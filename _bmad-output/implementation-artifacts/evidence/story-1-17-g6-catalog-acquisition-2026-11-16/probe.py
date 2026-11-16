#!/usr/bin/env python3
"""Exercise the actual CiCd helper in a private depth-one Builds object store."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

WORKSPACE = Path(__file__).resolve().parents[4]
OWNER = WORKSPACE.parent / "cicd"
SCRIPT = OWNER / ".github/actions/prepare-g6-catalog/prepare.py"
ORIGIN = "https://github.com/Hexalith/Hexalith.Builds.git"
CATALOG = "Props/Directory.Packages.props"


def run(arguments, cwd=None, check=True, input=None):
    environment = os.environ.copy()
    for key in tuple(environment):
        if key.startswith("GIT_"):
            environment.pop(key)
    environment.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
                       GIT_TERMINAL_PROMPT="0", GIT_NO_LAZY_FETCH="1", GIT_NO_REPLACE_OBJECTS="1")
    result = subprocess.run(list(map(str, arguments)), cwd=cwd, input=input, capture_output=True,
                            env=environment, timeout=180, check=False)
    if check and result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {arguments[0]}: {result.stderr.decode(errors='replace')}")
    return result


def git(root, *args):
    return run(["git", "-c", "submodule.recurse=false", "-C", root, *args]).stdout


def sha(root):
    return git(root, "rev-parse", "HEAD").decode().strip()


def pin(root, revision, name):
    line = git(root, "ls-tree", revision, "--", "references/" + name).decode().split()
    if line[:2] != ["160000", "commit"]:
        raise RuntimeError("Missing required source gitlink")
    return line[2]


def snapshot(root):
    index = Path(git(root, "rev-parse", "--path-format=absolute", "--git-path", "index").decode().strip())
    return {"head": sha(root),
            "index_file_sha256": hashlib.sha256(index.read_bytes()).hexdigest(),
            "index_sha256": hashlib.sha256(git(root, "ls-files", "--stage", "-z")).hexdigest(),
            "gitlinks": [line for line in git(root, "ls-tree", "-r", "HEAD").decode().splitlines()
                         if line.startswith("160000 ")],
            "refs": git(root, "for-each-ref", "--format=%(objectname) %(refname)").decode().splitlines()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise RuntimeError("Refusing to overwrite retained proof")
    scratch = Path(os.environ["TMPDIR"])
    root = scratch / "projects-g6-proof"
    source = WORKSPACE / "references/Hexalith.Projects"
    projects_revision = sha(source)
    platform_revision = pin(source, projects_revision, "Hexalith.Platform")
    builds_revision = pin(source, projects_revision, "Hexalith.Builds")
    platform_source = WORKSPACE / "references/Hexalith.Platform"
    selected_catalog = pin(platform_source, platform_revision, "Hexalith.Builds")
    expected_bytes = git(WORKSPACE / "references/Hexalith.Builds", "show", selected_catalog + ":" + CATALOG)
    run(["git", "clone", "--quiet", "--shared", "--sparse", "--no-recurse-submodules", source, root])
    git(root, "checkout", "--detach", projects_revision)
    builds = root / "references/Hexalith.Builds"
    builds.mkdir(parents=True, exist_ok=True)
    git(builds, "init", "--quiet")
    git(builds, "remote", "add", "origin", ORIGIN)
    git(builds, "fetch", "--quiet", "--no-recurse-submodules", "--no-tags", "--depth=1", "origin", builds_revision)
    git(builds, "sparse-checkout", "init", "--cone")
    git(builds, "checkout", "--detach", "FETCH_HEAD")
    platform = root / "references/Hexalith.Platform"
    run(["git", "clone", "--quiet", "--shared", "--sparse", "--no-recurse-submodules", platform_source, platform])
    git(platform, "checkout", "--detach", platform_revision)
    repositories = {"workspace": root, "builds": builds, "platform": platform}
    before = {name: snapshot(repository) for name, repository in repositories.items()}
    if git(builds, "rev-parse", "--is-shallow-repository") != b"true\n":
        raise RuntimeError("Builds proof must be shallow")
    depth = int(git(builds, "rev-list", "--count", "HEAD").decode())
    if depth != 1:
        raise RuntimeError("Initial Builds history must have depth one")
    missing = run(["git", "-C", builds, "cat-file", "--batch-check"], input=(selected_catalog + "\n").encode())
    if missing.stdout != (selected_catalog + " missing\n").encode():
        raise RuntimeError("Selected catalog must be absent before acquisition")
    failed = run(["git", "-C", builds, "show", selected_catalog + ":" + CATALOG], check=False)
    if failed.returncode != 128:
        raise RuntimeError("Expected the exact missing-object read to fail with 128")
    prepared = run([sys.executable, "-B", SCRIPT, "--workspace", root, "--platform-revision", platform_revision])
    result = json.loads(prepared.stdout)
    if result["status"] != "prepared" or not result["acquired"] or result["catalog_commit"] != selected_catalog:
        raise RuntimeError("Acquisition did not prepare the selected catalog")
    actual_bytes = git(builds, "show", selected_catalog + ":" + CATALOG)
    if actual_bytes != expected_bytes:
        raise RuntimeError("Catalog bytes differ from selected source")
    after = {name: snapshot(repository) for name, repository in repositories.items()}
    if before != after:
        raise RuntimeError("HEAD/index/gitlinks/refs changed")
    repeated = json.loads(run([sys.executable, "-B", SCRIPT, "--workspace", root,
                               "--platform-revision", platform_revision]).stdout)
    if repeated["acquired"]:
        raise RuntimeError("Available catalog should not be reacquired")
    if before != {name: snapshot(repository) for name, repository in repositories.items()}:
        raise RuntimeError("Second acquisition changed source state")
    if any(path.name == ".git" for repository in (builds, platform)
           for path in (repository / "references").glob("*/.git")):
        raise RuntimeError("Nested submodules must stay uninitialized")
    report = {"observed_utc": datetime.now(timezone.utc).isoformat(), "projects_revision": projects_revision,
              "platform_revision": platform_revision, "builds_revision": builds_revision,
              "catalog_commit": selected_catalog, "catalog_sha256": hashlib.sha256(actual_bytes).hexdigest(),
              "catalog_bytes": len(actual_bytes), "initial_builds_history_depth": depth,
              "helper_sha256": hashlib.sha256(SCRIPT.read_bytes()).hexdigest(),
              "before": before, "after": after, "missing_batch_check": missing.stdout.decode().strip(),
              "before_read_exit": failed.returncode, "before_read_stderr": failed.stderr.decode(),
              "after_read_exit": 0, "exact_catalog_bytes": True, "acquisition": result,
              "second_acquisition": repeated, "nested_initialized": False,
              "scope": "Git object acquisition only; no mapper, restore, runtime or hosted qualification"}
    args.output.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({key: report[key] for key in ("catalog_commit", "catalog_sha256", "before_read_exit", "after_read_exit", "exact_catalog_bytes")}))


if __name__ == "__main__":
    main()
