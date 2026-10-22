"""Independently audit the selected-runtime strict qualification packet."""

import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

packet = Path(__file__).resolve().parent
workspace = packet.parents[3]
freeze = json.loads((packet / "executed-freeze.json").read_text())
errors = []
counts = {}
for group, entries in freeze.items():
    counts[group] = len(entries)
    for name, expected in entries.items():
        path = Path(name)
        actual = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
        if actual != expected:
            errors.append({"group": group, "path": name, "expected": expected, "actual": actual})

observed = json.loads((packet / "strict-observed-observations.json").read_text())
for module in observed["modules"]:
    actual = hashlib.sha256(Path(module["path"]).read_bytes()).hexdigest()
    if not module["matches"] or actual != module["sha256"]:
        errors.append({"module": module["path"], "actual": actual})
for sidecar in observed["sidecars"]:
    argv = sidecar["argv"]
    configuration = Path(argv[argv.index("--config") + 1])
    resources = Path(argv[argv.index("--resources-path") + 1])
    if configuration.is_relative_to(resources):
        errors.append({"colocated_configuration_sidecar_pid": sidecar["pid"]})

results = {}
for name in ("fixture-helpers", "strict-live", "strict-selected-live", "strict-observed-live"):
    assemblies = ET.parse(packet / (name + ".xml")).getroot().findall(".//assembly")
    results[name] = [{key: entry.attrib.get(key) for key in
                      ("total", "passed", "failed", "skipped", "errors", "time")}
                     for entry in assemblies]
for name, expected in (("fixture-helpers", "22"), ("strict-observed-live", "1")):
    if (len(results[name]) != 1 or results[name][0]["passed"] != expected
            or any(results[name][0][key] != "0" for key in ("failed", "skipped", "errors"))):
        errors.append({"test_result": name, "actual": results[name]})

fixture = "tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs"
before = subprocess.check_output(["git", "show", "HEAD:" + fixture], cwd=workspace, text=True)
after = (workspace / fixture).read_text()
start = "    private async Task StartAsync()"
end = "    private async Task BootstrapAsync()"
outside_startup_unchanged = (before[:before.index(start)] == after[:after.index(start)]
                           and before[before.index(end):] == after[after.index(end):])
if not outside_startup_unchanged:
    errors.append({"fixture_changes_outside_startup": True})
diff = subprocess.check_output(["git", "diff", "--", fixture], cwd=workspace)
(packet / "parent-fixture-diff.patch").write_bytes(diff)

fixture_ids = observed["fixture_ids"]
owned_processes = []
for process in Path("/proc").glob("[0-9]*"):
    try:
        if process.stat().st_uid != os.getuid():
            continue
        environment = (process / "environ").read_bytes()
        if any(identity.encode() in environment for identity in fixture_ids):
            owned_processes.append(int(process.name))
    except (OSError, PermissionError):
        pass
container_names = subprocess.check_output(["docker", "ps", "-a", "--format", "{{.Names}}"], text=True).splitlines()
owned_containers = [name for name in container_names if any(identity in name for identity in fixture_ids)]
if owned_processes or owned_containers:
    errors.append({"remaining_processes": owned_processes, "remaining_containers": owned_containers})

report = {
    "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "scope": "Selected qualified production/SDK runtime plus startup-path-only fixture correction; no current external SDK-source qualification.",
    "counts": counts,
    "observed_modules": len(observed["modules"]),
    "sidecars_with_separate_configuration": len(observed["sidecars"]),
    "fixture_methods_outside_startup_unchanged": outside_startup_unchanged,
    "results": results,
    "reused_capacity": "Prior byte-bound 10,001/32,769 component and 10,001 authenticated capacity/SDK process evidence; not newly executed here.",
    "remaining_fixture_processes": owned_processes,
    "remaining_fixture_containers": owned_containers,
    "errors": errors,
    "passed": not errors,
    "whole_story_complete": False,
    "open_gates": ["Package/PD10", "SM4 acknowledgement performance", "SDK broad-regression/current external-source disposition", "Independent whole-story review"],
}
(packet / "parent-audit.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
raise SystemExit(0 if report["passed"] else 1)
