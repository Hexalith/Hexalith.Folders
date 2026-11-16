#!/usr/bin/env python3
"""Run package-mode product checks in a wrapper-owned disposable snapshot.

No Git mutations, dependency updates, publishing, or service orchestration.
The Projects-declared Builds tree is read from the existing root dependency's
object database, without initializing a nested submodule. Outputs are diagnostic.
"""

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
from datetime import datetime, timezone

SOURCE_PACKET = Path(__file__).resolve().parent
ROOT = SOURCE_PACKET.parents[3]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", required=True, type=Path, help="New evidence directory; an existing directory is refused.")
PACKET = parser.parse_args().output.resolve()
PACKET.mkdir(parents=True, exist_ok=False)
PROJECTS = ROOT / "references/Hexalith.Projects"
BUILDS = ROOT / "references/Hexalith.Builds"
SCRATCH = Path(os.environ["TMPDIR"])
WORK = SCRATCH / "Hexalith.Projects"
WORK.mkdir()


def git(directory, *arguments):
    return subprocess.check_output(["git", "-C", str(directory), *arguments])


source = git(PROJECTS, "rev-parse", "HEAD").decode().strip()
builds_pin = git(PROJECTS, "rev-parse", "HEAD:references/Hexalith.Builds").decode().strip()
copied = {}
for name in git(PROJECTS, "ls-files", "-z").decode().split("\0"):
    if not name:
        continue
    item = Path(name)
    if len(item.parts) > 1 and item.parts[0] not in {"src", "tests", "tools", "scripts", "docs", "module"}:
        continue
    origin = PROJECTS / item
    if origin.is_file():
        destination = WORK / item
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(origin, destination)
        copied[name] = hashlib.sha256(origin.read_bytes()).hexdigest()
builds_snapshot = WORK / "references/Hexalith.Builds"
builds_snapshot.mkdir(parents=True)
with tarfile.open(fileobj=io.BytesIO(git(BUILDS, "archive", builds_pin))) as archive:
    archive.extractall(builds_snapshot, filter="data")

environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", CI="true", MSBUILDDISABLENODEREUSE="1")
records = []


def run(label, command):
    log = PACKET / (label + ".log")
    started = datetime.now(timezone.utc).isoformat()
    with log.open("w") as output:
        completed = subprocess.run(command, cwd=WORK, env=environment, stdout=output, stderr=subprocess.STDOUT)
    records.append({"label": label, "command": command, "cwd": str(WORK), "exit_code": completed.returncode,
                    "started_at_utc": started, "completed_at_utc": datetime.now(timezone.utc).isoformat(),
                    "log": log.name, "log_sha256": hashlib.sha256(log.read_bytes()).hexdigest()})
    (PACKET / "product-results.json").write_text(json.dumps({
        "projects_head": source, "fixture_sha256": copied["tests/tools/test_release_package_tools.py"],
        "builds_declared_pin": builds_pin, "builds_archive_materialization": "Existing root Builds git archive; no nested submodule initialized",
        "snapshot_scope": "Tracked root build files plus src/tests/tools/scripts/docs/module; no .git or historical evidence",
        "snapshot_file_hashes": copied, "results": records,
        "qualification_limit": "Local diagnostic snapshot with uncommitted fixture repair, not committed-source CI, source mapping or hosted qualification",
    }, indent=2) + "\n")
    print(label, "exit", completed.returncode, "log", log, flush=True)
    print("\n".join(log.read_text(errors="replace").splitlines()[-12:]), flush=True)
    return completed.returncode


properties = ["-p:UseHexalithProjectReferences=false", "-p:MinVerVersionOverride=1.0.0", "-m:1", "/nr:false"]
run("product-sdk-info", ["dotnet", "--info"])
restore = run("product-solution-restore", ["dotnet", "restore", "Hexalith.Projects.CI.slnx", "-p:Configuration=Release", *properties])
if restore == 0:
    run("product-solution-build", ["dotnet", "build", "Hexalith.Projects.CI.slnx", "--configuration", "Release", "--no-restore", "-warnaserror", *properties])

# Individual project fallbacks remain useful even when an unrelated solution edge fails.
for component in ["Contracts", "Client", "", "Server", "UI", "Mcp", "Cli"]:
    project_name = "Hexalith.Projects" + ("." + component if component else "") + ".Tests"
    project = f"tests/{project_name}/{project_name}.csproj"
    label = "product-" + (component.lower() if component else "core")
    code = run(label + "-build", ["dotnet", "build", project, "--configuration", "Release", "-warnaserror", *properties])
    if code == 0:
        assembly = WORK / f"tests/{project_name}/bin/Release/net11.0/{project_name}.dll"
        run(label + "-tests", ["dotnet", str(assembly), "-noLogo"])
