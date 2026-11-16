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
import sys
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
builds_archive = git(BUILDS, "archive", builds_pin)
with tarfile.open(fileobj=io.BytesIO(builds_archive)) as archive:
    archive.extractall(builds_snapshot, filter="data")
(PACKET / "builds-inputs.json").write_text(json.dumps({
    "pin": builds_pin, "archive_sha256": hashlib.sha256(builds_archive).hexdigest(),
    "file_hashes": {str(item.relative_to(builds_snapshot)): hashlib.sha256(item.read_bytes()).hexdigest()
                    for item in builds_snapshot.rglob("*") if item.is_file()}
}, indent=2) + "\n")

# Materialize only ordinary build sources from the permitted root owners at Projects' declared pins.
dependencies = {}
for owner in ["Hexalith.EventStore", "Hexalith.Tenants", "Hexalith.FrontComposer", "Hexalith.Platform", "Hexalith.Folders"]:
    dependency_pin = git(PROJECTS, "rev-parse", "HEAD:references/" + owner).decode().strip()
    permitted = ROOT if owner == "Hexalith.Folders" else ROOT / "references" / owner
    entries = git(permitted, "ls-tree", dependency_pin).decode().splitlines()
    selected = [line.split("\t", 1)[1] for line in entries if line.startswith("100")]
    if owner != "Hexalith.Platform":
        selected.append("src")
    data = git(permitted, "archive", dependency_pin, "--", *selected)
    target = WORK / "references" / owner
    target.mkdir(parents=True)
    with tarfile.open(fileobj=io.BytesIO(data)) as archive:
        archive.extractall(target, filter="data")
    dependencies[owner] = {"pin": dependency_pin, "selected_paths": selected,
                           "archive_sha256": hashlib.sha256(data).hexdigest(),
                           "file_hashes": {str(file.relative_to(target)): hashlib.sha256(file.read_bytes()).hexdigest()
                                           for file in target.rglob("*") if file.is_file()}}
(PACKET / "dependency-inputs.json").write_text(json.dumps(dependencies, indent=2) + "\n")

environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", CI="true", MSBUILDDISABLENODEREUSE="1")
records = []
# Record relevant inherited properties in future reproductions. Fail before checks if inherited
# settings would suppress audits/diagnostics or select a different external dependency tuple.
property_names = {"NuGetAudit", "NuGetAuditMode", "NoWarn", "WarningsNotAsErrors", "TreatWarningsAsErrors",
                  "UseHexalithProjectReferences", "HexalithMcpCliVersion", "HexalithEventStoreVersion", "HexalithFoldersVersion"}
effective_environment = {key: value for key, value in os.environ.items() if key.casefold() in {name.casefold() for name in property_names}}
(PACKET / "inherited-build-environment.json").write_text(json.dumps(effective_environment, indent=2) + "\n")
for key, value in effective_environment.items():
    expected_properties = {"treatwarningsaserrors": "true", "usehexalithprojectreferences": "false",
                           "hexalithmcpcliversion": "3.0.0", "hexalitheventstoreversion": "5.1.1", "hexalithfoldersversion": "2.0.0"}
    if key.casefold() in expected_properties and value and value.casefold() != expected_properties[key.casefold()]:
        raise ValueError("Conflicting inherited qualification property: " + key)
    if key.casefold() == "nugetaudit" and value.casefold() not in {"", "true"}:
        raise ValueError("Conflicting inherited NuGetAudit setting")
    if key.casefold() in {"nowarn", "warningsnotaserrors"} and value:
        raise ValueError("Inherited diagnostic suppression is not a qualification input")



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
        "qualification_limit": "Local diagnostic snapshot with uncommitted package alignment and fixture repair, not committed-source CI, source mapping or hosted qualification",
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
for component in ["Integration"]:
    project_name = "Hexalith.Projects" + ("." + component if component else "") + ".Tests"
    project = f"tests/{project_name}/{project_name}.csproj"
    label = "product-" + (component.lower() if component else "core")
    code = run(label + "-build", ["dotnet", "build", project, "--configuration", "Release", "-warnaserror", *properties])
    if code == 0:
        assembly = WORK / f"tests/{project_name}/bin/Release/net11.0/{project_name}.dll"
        run(label + "-tests", ["dotnet", str(assembly), "-noLogo"])

# The unchanged scripted fingerprint gate builds/runs its actual focused tests.
run("openapi-fingerprint-gate", ["pwsh", "-NoProfile", "-File", "tests/tools/run-openapi-fingerprint-gate.ps1"])
run("frontcomposer-inspect-gate", ["pwsh", "-NoProfile", "-File", "tests/tools/run-frontcomposer-inspect-gate.ps1"])
# Focused decoration compatibility is explicit; source-only gateway fixtures remain unqualified.
run("mcpcli-decoration-tests", ["dotnet", str(WORK / "tests/Hexalith.Projects.Contracts.Tests/bin/Release/net11.0/Hexalith.Projects.Contracts.Tests.dll"),
                                "-class", "Hexalith.Projects.Contracts.Tests.Queries.ProjectMcpCliDecorationTests"])
# Verify restored/build-output Folders payloads without claiming an immutable image or hosted load.
handoff = json.loads((SOURCE_PACKET / "closure-handoff.before.json").read_bytes())
server = WORK / "src/Hexalith.Projects.Server"
bindings = []
for package in handoff["released_pair"]["retained_packages"]:
    if package["id"] not in {"Hexalith.Folders.Client", "Hexalith.Folders.Contracts"}:
        continue
    output = server / "bin/Release/net11.0" / (package["id"] + ".dll")
    actual = hashlib.sha256(output.read_bytes()).hexdigest() if output.is_file() else None
    bindings.append({"id": package["id"], "version": package["version"], "expected_payload_sha256": package["payload_sha256"],
                     "build_output_sha256": actual, "matches_retained_payload": actual == package["payload_sha256"]})
(PACKET / "server-folders-payload-binding.json").write_text(json.dumps(bindings, indent=2) + "\n")

# Reflect all required subprocess outcomes in future reproductions.
sys.exit(1 if any(row["exit_code"] != 0 for row in records) else 0)
