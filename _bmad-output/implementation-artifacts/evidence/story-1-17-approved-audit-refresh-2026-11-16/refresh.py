"""Run the owner audit generator, qualify its scope, then install and validate it."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess


evidence = Path(__file__).resolve().parent
workspace = evidence.parents[3]
owner = workspace / "references/Hexalith.Builds"
audit = owner / "Tools/package-version-audit.json"
scratch = Path(os.environ["TMPDIR"])
candidate = scratch / "package-version-audit.json"
ledger = []


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(name, arguments):
    started = datetime.datetime.now(datetime.timezone.utc)
    with (evidence / (name + ".log")).open("wb") as output:
        result = subprocess.run(arguments, cwd=owner, stdout=output, stderr=subprocess.STDOUT)
    ledger.append({"name": name, "argv": [str(a).replace(str(scratch), "{TMPDIR}") for a in arguments],
                   "cwd": "references/Hexalith.Builds", "started_utc": started.isoformat(),
                   "elapsed_seconds": (datetime.datetime.now(datetime.timezone.utc) - started).total_seconds(),
                   "exit_code": result.returncode, "log": name + ".log"})
    (evidence / "commands.json").write_text(json.dumps(ledger, indent=2) + "\n")
    print(name, "exit", result.returncode, flush=True)
    require(result.returncode == 0, name + " failed; see retained log")


before = json.loads((evidence / "before.json").read_text())
require(sha(audit) == before["audit_before_sha256"], "Owner audit changed since snapshot")
head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=owner, text=True).strip()
require(head == before["repositories"]["references/Hexalith.Builds"]["head"], "Owner revision changed")
run("generate", ["pwsh", "-NoProfile", "-File", "Tools/audit-central-package-versions.ps1",
                 "-ChangedFamily", "hexalith-folders", "-PriorAuditPath", str(evidence / "audit.before.json"),
                 "-OutputPath", str(candidate)])
prior = json.loads((evidence / "audit.before.json").read_text())
current = json.loads(candidate.read_text())
require(current["generatedFromRevision"] == head, "Audit does not bind the owner revision")
require(current["snapshot"]["mode"] == "incremental" and
        current["snapshot"]["refreshedFamilies"] == ["hexalith-folders"], "Wrong refresh scope")
old_packages = {row["id"]: row for row in prior["packages"]}
new_packages = {row["id"]: row for row in current["packages"]}
require(old_packages.keys() == new_packages.keys() and len(new_packages) == 317, "Package denominator changed")
changed = [name for name in old_packages if old_packages[name] != new_packages[name]]
expected = sorted(name for name, row in old_packages.items() if row["family"] == "hexalith-folders")
require(sorted(changed) == expected and len(expected) == 5, "Unrelated package rows changed")
for name in expected:
    row = new_packages[name]
    require(row["auditedVersion"] == row["selectedVersion"] == "2.0.0" and row["listingState"] == "listed",
            "Selected package is not observed listed at 2.0.0: " + name)
    require(row["historicalContext"][:len(old_packages[name]["historicalContext"])] ==
            old_packages[name]["historicalContext"], "Historical package observations changed")
old_families = {row["family"]: row for row in prior["familyDecisions"]}
new_families = {row["family"]: row for row in current["familyDecisions"]}
require(old_families.keys() == new_families.keys() and len(new_families) == 157, "Family denominator changed")
require([name for name in old_families if old_families[name] != new_families[name]] == ["hexalith-folders"],
        "Unrelated family decisions changed")
history_before = sum(len(row["historicalContext"]) for row in prior["packages"]) + sum(
    len(row["historicalContext"]) for row in prior["familyDecisions"])
history_after = sum(len(row["historicalContext"]) for row in current["packages"]) + sum(
    len(row["historicalContext"]) for row in current["familyDecisions"])
run("validate-candidate", ["pwsh", "-NoProfile", "-File", "Tools/validate-package-version-audit.ps1",
                           "-AuditPath", str(candidate)])
require(sha(audit) == before["audit_before_sha256"], "Owner audit changed before installation")
installation = audit.with_name(".package-version-audit.story117-refresh.tmp")
try:
    with installation.open("xb") as output:
        output.write(candidate.read_bytes())
    os.replace(installation, audit)
finally:
    installation.unlink(missing_ok=True)
run("validate-owner", ["pwsh", "-NoProfile", "-File", "Tools/validate-package-version-audit.ps1"])
summary = {"generated_from_revision": head, "audit_sha256": sha(audit),
           "audited_at_utc": current["snapshot"]["auditedAtUtc"], "source_results": current["sources"],
           "refreshed_packages": expected, "preserved_package_rows": 312, "preserved_family_decisions": 156,
           "historical_observations_before": history_before, "historical_observations_after": history_after,
           "folders_rows": [{key: new_packages[name][key] for key in
                             ["id", "auditedVersion", "selectedVersion", "latestStable", "listingState"]}
                            for name in expected], "family_disposition": new_families["hexalith-folders"]["disposition"]}
(evidence / "result.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary, indent=2), flush=True)
