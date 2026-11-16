#!/usr/bin/env python3
"""Reproduce strict package boundaries in a disposable diagnostic snapshot.

Use run-with-temp and a NEW --output directory. No publication or Git mutations.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[4]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", required=True, type=Path)
OUTPUT = parser.parse_args().output.resolve()
OUTPUT.mkdir(parents=True, exist_ok=False)
PROJECTS = ROOT / "references/Hexalith.Projects"
BUILDS = ROOT / "references/Hexalith.Builds"
WORK = Path(os.environ["TMPDIR"]) / "Hexalith.Projects"
WORK.mkdir()


def git(owner, *args):
    return subprocess.check_output(["git", "-C", str(owner), *args])


source = git(PROJECTS, "rev-parse", "HEAD").decode().strip()
pin = git(PROJECTS, "rev-parse", "HEAD:references/Hexalith.Builds").decode().strip()
inputs = {}
for name in git(PROJECTS, "ls-files", "-z").decode().split("\0"):
    item = Path(name)
    if not name or len(item.parts) > 1 and item.parts[0] not in {"src", "tests", "tools", "scripts", "docs", "module"}:
        continue
    if not (PROJECTS / item).is_file():
        continue
    destination = WORK / item
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(PROJECTS / item, destination)
    inputs[name] = hashlib.sha256(destination.read_bytes()).hexdigest()
(OUTPUT / "input-manifest.json").write_text(json.dumps(inputs, indent=2) + "\n")
builds_snapshot = WORK / "references/Hexalith.Builds"
builds_snapshot.mkdir(parents=True)
with tarfile.open(fileobj=io.BytesIO(git(BUILDS, "archive", pin))) as archive:
    archive.extractall(builds_snapshot, filter="data")

environment = dict(os.environ, CI="true", PYTHONDONTWRITEBYTECODE="1", MSBUILDDISABLENODEREUSE="1")
results = []
# Consult existing source Git metadata read-only for the validator's exact source lookup.
# The disposable build bytes and uncommitted patch remain separately recorded.
source_git = git(PROJECTS, "rev-parse", "--absolute-git-dir").decode().strip()
consumer_environment = dict(environment, GIT_DIR=source_git, GIT_WORK_TREE=str(PROJECTS), GIT_OPTIONAL_LOCKS="0")


def run(label, command):
    log = OUTPUT / (label + ".log")
    start = datetime.now(timezone.utc).isoformat()
    with log.open("w") as output:
        completed = subprocess.run(command, cwd=WORK, env=consumer_environment if label == "package-api-consumers" else environment, stdout=output, stderr=subprocess.STDOUT)
    results.append({"label": label, "command": command, "exit_code": completed.returncode,
                    "started_at_utc": start, "completed_at_utc": datetime.now(timezone.utc).isoformat(),
                    "log": log.name, "sha256": hashlib.sha256(log.read_bytes()).hexdigest()})
    (OUTPUT / "results.json").write_text(json.dumps({
        "source": source, "builds_declared_pin": pin, "cwd": str(WORK),
        "input_manifest": "input-manifest.json", "input_manifest_sha256": hashlib.sha256((OUTPUT / "input-manifest.json").read_bytes()).hexdigest(),
        "scope": "Diagnostic candidates with explicit base source identity and separately hashed uncommitted inputs; no publication or retained host artifact",
        "consumer_provenance_lookup": "Existing Projects Git metadata read-only (GIT_OPTIONAL_LOCKS=0); not a new committed candidate",
        "restore_configuration": {"CI": "true", "configuration": "Release", "UseHexalithProjectReferences": False,
                                  "nuget_config_sha256": inputs["nuget.config"], "audit_suppression_flags": [], "dependency_version_overrides": []},
        "results": results,
    }, indent=2) + "\n")
    print(label, completed.returncode, flush=True)
    print("\n".join(log.read_text(errors="replace").splitlines()[-5:]), flush=True)
    return completed.returncode


run("sdk-info", ["dotnet", "--info"])
version = "91.92.93-ci.1"
properties = ["-p:UseHexalithProjectReferences=false", f"-p:MinVerVersionOverride={version}",
              f"-p:HexalithProjectsPackageVersion={version}", f"-p:RepositoryCommit={source}", "-m:1", "/nr:false"]
manifest = json.loads((WORK / "tools/release-packages.json").read_bytes())
candidates = WORK / "candidates"
candidates.mkdir()
packed = True
for package in manifest["packages"]:
    label = "package-" + package["id"].replace("Hexalith.Projects", "projects").replace(".", "-").lower()
    code = run(label, ["dotnet", "pack", package["project"], "--configuration", "Release", "--output", str(candidates), "-warnaserror", *properties])
    packed = packed and code == 0
if packed:
    validation = run("package-manifest-validation", ["python3", "scripts/validate-nuget-packages.py", str(candidates),
                     "--expected-version", version, "--expected-source-sha", source, "--normalize-internal-ranges"])
    # Retain the actual normalized nuspecs, including rejected graph/version data.
    nuspec_directory = OUTPUT / "nuspecs"
    nuspec_directory.mkdir()
    report = []
    for candidate in sorted(candidates.glob("*.nupkg")):
        with zipfile.ZipFile(candidate) as archive:
            name = next(name for name in archive.namelist() if name.endswith(".nuspec"))
            data = archive.read(name)
            (nuspec_directory / name).write_bytes(data)
            root = ET.fromstring(data)
            report.append({"archive": candidate.name, "archive_sha256": hashlib.sha256(candidate.read_bytes()).hexdigest(),
                           "nuspec": "nuspecs/" + name, "nuspec_sha256": hashlib.sha256(data).hexdigest(),
                           "dependencies": [dict(item.attrib) for item in root.iter() if item.tag.split("}")[-1] == "dependency"]})
    (OUTPUT / "dependency-report.json").write_text(json.dumps(report, indent=2) + "\n")
    if validation == 0:
        run("package-api-consumers", ["python3", "scripts/validate-consumer-package-references.py", str(candidates)])
