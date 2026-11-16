"""Reproduce catalog-object acquisition in a disposable bare repository only."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true", help="Explicitly write a new retained result while authoring this packet")
    options = parser.parse_args()
    packet = Path(__file__).resolve().parent
    root = packet.parents[3]
    before = json.loads((packet / "before.json").read_text())
    bindings = json.loads((packet / "source-bindings.json").read_text())
    builds = root / "references/Hexalith.Builds"
    selected = before["repositories"]["references/Hexalith.Builds"]["head"]
    catalog = bindings["selected_catalog"]["builds_revision"]
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}

    def git(repository, *arguments, input_data=None):
        return subprocess.run(
            ["git", "-c", "submodule.recurse=false", "-C", str(repository), *arguments],
            env=environment, input=input_data, capture_output=True, check=False,
        )

    def require(condition, message):
        if not condition:
            raise SystemExit(message)

    require(bool(os.environ.get("TMPDIR")), "Run this probe through run-with-temp")
    require(git(builds, "rev-parse", "HEAD").stdout.decode().strip() == selected,
            "Source Builds revision changed; this is a historical probe")
    original = git(builds, "show", catalog + ":Props/Directory.Packages.props")
    require(original.returncode == 0, "Selected catalog unavailable in source object database")
    with tempfile.TemporaryDirectory(prefix="story117-g6-", dir=os.environ["TMPDIR"]) as scratch:
        repository = Path(scratch) / "builds.git"
        repository.mkdir()
        require(git(repository, "init", "--bare", "--quiet").returncode == 0, "Bare init failed")
        fetch = ("fetch", "--no-recurse-submodules", "--no-tags", "--depth=1", builds.as_uri())
        require(git(repository, *fetch, selected).returncode == 0, "Depth-one source acquisition failed")
        require(git(repository, "update-ref", "HEAD", selected).returncode == 0, "Scratch HEAD setup failed")
        object_check = git(repository, "cat-file", "--batch-check", input_data=(catalog + "\n").encode())
        require(object_check.returncode == 0 and object_check.stdout == (catalog + " missing\n").encode(),
                "Catalog commit was not explicitly reported missing")
        missing = git(repository, "show", catalog + ":Props/Directory.Packages.props")
        require(missing.returncode != 0, "Depth-one repository unexpectedly contains catalog ancestor")
        require(git(repository, *fetch, catalog).returncode == 0, "Exact catalog acquisition failed")
        acquired = git(repository, "show", catalog + ":Props/Directory.Packages.props")
        require(acquired.returncode == 0 and acquired.stdout == original.stdout,
                "Acquired catalog differs from selected pinned bytes")
        head_after = git(repository, "rev-parse", "HEAD").stdout.decode().strip()
        require(head_after == selected, "Catalog acquisition moved scratch Builds HEAD")
        result = {
            "scope": "Git-object prerequisite reproduction only; no mapper, build or runtime qualification",
            "source_builds": selected,
            "catalog_revision": catalog,
            "shallow_read_exit": missing.returncode,
            "catalog_object_before_fetch": object_check.stdout.decode().strip(),
            "shallow_read_stderr": missing.stderr.decode().strip(),
            "exact_fetch_read_exit": acquired.returncode,
            "catalog_sha256": hashlib.sha256(acquired.stdout).hexdigest(),
            "head_after_acquisition": head_after,
            "fetch_arguments": ["fetch", "--no-recurse-submodules", "--no-tags", "--depth=1",
                                "<local-root-declared-Builds-file-URI>", catalog],
            "repository_location": "$TMPDIR/story117-g6-*/builds.git",
            "scratch_cleanup": "completed on context exit",
            "workspace_checkout_or_object_database_modified": False,
        }
    retained = packet / "probe-result.json"
    if options.record:
        retained.write_text(json.dumps(result, indent=2) + "\n")
    else:
        require(json.loads(retained.read_text()) == result, "Fresh probe disagrees with the retained result")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
