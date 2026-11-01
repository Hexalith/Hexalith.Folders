"""Reproduce the current selective mapper failure without building or changing hosts."""

from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[3]
PREVIOUS = PACKET.parent / "story-12-2-current-consumer-20261031"
SERVER = ROOT / ".artifacts/story-12-2-current-consumer-20261031/bin/Hexalith.Folders.Server.Tests/debug/Hexalith.Folders.Server.Tests.dll"
PROBE = ROOT / ".artifacts/story-12-2-registration-probe-final-20261031/bin/registration-seam-probe/debug/registration-seam-probe.dll"


def now():
    return datetime.now(timezone.utc).isoformat()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def write(name, value):
    (PACKET / name).write_text(json.dumps(value, indent=2) + "\n")


def audit():
    inventories = []
    for name in ("sources-before.json", "binaries-before.json", "corrected-binaries-before.json"):
        inventory_path = PREVIOUS / name
        inventory = json.loads(inventory_path.read_text())
        differences = []
        for name, expected in inventory.items():
            actual = digest(Path(name))
            if actual != expected:
                differences.append({"path": name, "expected": expected, "actual": actual})
        inventories.append({"inventory": str(inventory_path.relative_to(ROOT)),
                            "sha256": digest(inventory_path), "count": len(inventory),
                            "differences": differences})
    return {"atUtc": now(), "inventories": inventories}


def run(name, argv):
    started = now()
    with (PACKET / (name + ".log")).open("wb") as log:
        result = subprocess.run(argv, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT,
                                timeout=60, check=False)
    receipt = {"argv": [str(arg) for arg in argv], "cwd": str(ROOT),
               "startedAtUtc": started, "finishedAtUtc": now(), "exitCode": result.returncode}
    write(name + ".json", receipt)
    return result.returncode


before = audit()
write("before-audit.json", before)
if any(item["differences"] for item in before["inventories"]):
    sys.exit("Source/runtime drift: no previously qualified output was executed.")

probe_before = {str(path): digest(path) for path in sorted(PROBE.parent.iterdir()) if path.is_file()}
write("probe-runtime-before.json", probe_before)
heads = {}
for repository in (ROOT, ROOT / "references/Hexalith.EventStore"):
    heads[str(repository)] = subprocess.check_output(
        ["git", "-C", str(repository), "rev-parse", "HEAD"], text=True).strip()
write("heads.json", heads)

source_paths = [
    "src/Hexalith.Folders.Server/FoldersServerHostComposition.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreHttpHostSecurityExtensions.cs",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreHttpEndpointInventory.cs",
    "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-5-14-authenticated-fallback-and-dapr-framework-route-protection.md",
    "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-5-5-internal-and-domain-service-trust-boundary.md",
    "_bmad-output/implementation-artifacts/proposal-12-2-acceptance-prerequisites.md",
]
write("integration-inputs.json", {name: digest(ROOT / name) for name in source_paths})

discovery = run("production-discovery", ["dotnet", str(SERVER), "-method",
    "Hexalith.Folders.Server.Tests.FoldersServerHostCompositionTests.ProtectedProductionDiscoveryAdvertisesOnlyProcessorBackedNamedAndSharedRoutes",
    "-result-xml", str(PACKET / "production-discovery.xml")])
original = run("registration-original", ["dotnet", str(PROBE)])
registered = run("registration-public-seam", ["dotnet", str(PROBE), "register-http"])
after = audit()
write("after-audit.json", after)
probe_drift = [name for name, expected in probe_before.items() if digest(Path(name)) != expected]
write("probe-runtime-after.json", {"count": len(probe_before), "differences": probe_drift})

assembly = ET.parse(PACKET / "production-discovery.xml").getroot().find("assembly")
probe = json.loads((PACKET / "registration-public-seam.log").read_text().splitlines()[-1])
summary = {"qualified": False, "newPassingConsumerCases": 0,
           "discoveryExitCode": discovery, "discovery": dict(assembly.attrib),
           "originalProbeExitCode": original, "publicRegistrationProbeExitCode": registered,
           "publicRegistrationInventoryFindings": len(probe["failures"]),
           "sourceOrRuntimeDrift": any(item["differences"] for item in after["inventories"]),
           "probeRuntimeDrift": probe_drift,
           "topologyStarted": False, "processCleanup": "All three synchronous child commands exited."}
write("summary.json", summary)
print(json.dumps(summary, indent=2))
if discovery != 1 or original != 1 or registered != 2 or len(probe["failures"]) != 58:
    sys.exit("The observed result differs from the retained blocker; inspect receipts.")
if summary["sourceOrRuntimeDrift"] or probe_drift:
    sys.exit("Execution drift: these observations cannot establish a stable candidate.")
