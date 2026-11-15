"""Reconstruct the provisional source from its local bundle and validate again."""
import json
import os
from pathlib import Path
import subprocess

packet = Path(__file__).resolve().parent
owner = packet.parents[3] / "references/Hexalith.Builds"
private = Path(os.environ["TMPDIR"]) / "builds"
summary = json.loads((packet / "provisional-audit/result.json").read_text())
steps = [
    ["git", "clone", "--quiet", "--shared", "--no-checkout", str(owner), str(private)],
    ["git", "-C", str(private), "fetch", "--quiet", str(packet / "provisional-audit/source.bundle"), "HEAD"],
    ["git", "-C", str(private), "checkout", "--quiet", "--detach", "FETCH_HEAD"],
    ["pwsh", "-NoLogo", "-NoProfile", "-File", "Tools/validate-package-version-audit.ps1",
     "-AuditPath", str(packet / "provisional-audit/package-version-audit.proposed.json")],
]
env = dict(os.environ)
env.update({"DOTNET_CLI_HOME": str(Path(os.environ["TMPDIR"]) / "dotnet-home"), "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
rows = []
for ordinal, command in enumerate(steps):
    result = subprocess.run(command, cwd=owner if ordinal == 0 else private, env=env,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    rows.append({"argv": [arg.replace(str(packet), "{packet}").replace(str(private), "{private-builds}")
                            .replace(str(owner), "{owning-builds}") for arg in command],
                 "exit_code": result.returncode,
                 "output": result.stdout.replace(str(private), "{private-builds}")})
    if result.returncode:
        raise RuntimeError("Provisional audit reconstruction failed")
actual = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=private, text=True).strip()
if actual != summary["temporary_source_commit"]:
    raise RuntimeError("Reconstructed a different provisional source")
(packet / "audit-proposal-replay.json").write_text(json.dumps({"source": actual, "commands": rows}, indent=2) + "\n")
print("PASS: source bundle reconstructs the exact provisional revision and its audit validates")
