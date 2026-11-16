"""Qualify the landed tuple locally; never refresh feeds or mutate owner Git state."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time


ROOT = Path(__file__).resolve().parents[4]
BASE = Path(__file__).resolve().parent
OUT = BASE
if len(sys.argv) > 1:
    require_name = len(sys.argv) == 2 and re.fullmatch(r"[a-z0-9][a-z0-9-]*", sys.argv[1])
    if not require_name:
        raise SystemExit("Use one safe relative attempt name")
    OUT = BASE / sys.argv[1]
    OUT.mkdir()
PRIOR = BASE.parent / "story-1-17-released-v2-consumer-2026-11-15"
SCRATCH = Path(os.environ["TMPDIR"]).resolve()
COMMANDS = []
ENV = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1",
           DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1")
ENV["PATH"] += ":" + str(Path.home() / ".dotnet/tools")


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + "\n")


def git(owner, *arguments):
    return subprocess.check_output(["git", "-C", str(owner), *arguments], text=True).strip()


def run(name, arguments, cwd=ROOT, expected=0):
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    begin = time.monotonic()
    result = subprocess.run(arguments, cwd=cwd, env=ENV, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True)
    def safe(text):
        return str(text).replace(str(SCRATCH), "{private-temp}").replace(str(ROOT), "{workspace}")
    (OUT / (name + ".log")).write_text(safe(result.stdout))
    COMMANDS.append({"name": name, "argv": [safe(arg) for arg in arguments],
                     "cwd": safe(cwd), "started_utc": started,
                     "elapsed_seconds": round(time.monotonic() - begin, 3),
                     "exit_code": result.returncode, "expected_exit_code": expected,
                     "log": name + ".log"})
    write("commands.json", COMMANDS)
    print(name + " exit " + str(result.returncode), flush=True)
    require(result.returncode == expected, "Unexpected result for " + name)


def snapshot():
    prior = json.loads((PRIOR / "before.json").read_text())
    repositories = {}
    for relative in prior["repositories"]:
        owner = ROOT / relative
        index = subprocess.check_output(["git", "-C", str(owner), "ls-files", "--stage", "-z"])
        repositories[relative] = {"head": git(owner, "rev-parse", "HEAD"),
                                  "index_sha256": hashlib.sha256(index).hexdigest(),
                                  "status": git(owner, "status", "--porcelain=v1"),
                                  "root_gitlink": None if relative == "." else
                                      git(ROOT, "rev-parse", "HEAD:" + relative)}
    return {"repositories": repositories,
            "protected": {name: sha(ROOT / name) for name in prior["protected"]}}


def main():
    require(not (OUT / "commands.json").exists(), "Use a separate evidence directory for another run")
    require(SCRATCH != ROOT and not SCRATCH.is_relative_to(ROOT), "Private TMPDIR required")
    before = snapshot()
    write("before.json", before)
    for name, source in (("spec.before.md", "spec-1-17-complete-pd10-v2-migration-and-closure.md"),
                         ("readiness.before.md", "story-1-17-current-candidate-technical-readiness-2026-09-24.md"),
                         ("migration.before.md", "story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md")):
        shutil.copyfile(BASE.parents[1] / source, OUT / name)
    prior_inputs = json.loads((PRIOR / "final/source-inputs.json").read_text())
    comparisons = {}
    for relative, record in prior_inputs.items():
        owner = ROOT / relative
        changed = []
        for name, expected in record["files"].items():
            path = owner / name
            actual = sha(path) if path.is_file() else None
            if actual != expected:
                changed.append({"path": name, "before": expected, "after": actual})
        names = subprocess.check_output(["git", "-C", str(owner), "ls-files", "-z"]).split(b"\0")
        current = {name.decode() for name in names if name and (owner / name.decode()).is_file()
                   and not name.decode().startswith(("_bmad-output/implementation-artifacts/evidence/",
                                                     "references/", ".agents/", "_bmad/"))}
        comparisons[relative] = {"prior_head": record["head"], "head": git(owner, "rev-parse", "HEAD"),
                                "compared_files": len(record["files"]), "changed": changed,
                                "added": sorted(current - record["files"].keys()),
                                "removed": sorted(record["files"].keys() - current)}
    allowed = {"_bmad-output/implementation-artifacts/spec-1-17-complete-pd10-v2-migration-and-closure.md",
               "_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md",
               "_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md"}
    for relative, comparison in comparisons.items():
        require(not comparison["added"] and not comparison["removed"], "Source path set changed: " + relative)
        require({row["path"] for row in comparison["changed"]} <= (allowed if relative == "." else set()),
                "Qualification source changed: " + relative)
    write("source-comparison.json", {"prior_manifest": str((PRIOR / "final/source-inputs.json").relative_to(ROOT)),
                                      "prior_manifest_sha256": sha(PRIOR / "final/source-inputs.json"),
                                      "owners": comparisons})
    prior_receipt = json.loads((PRIOR / "receipt.json").read_text())
    dependencies = {}
    for relative, old in prior_receipt["source_revisions"].items():
        if relative in comparisons:
            continue
        owner = ROOT / relative
        new = git(owner, "rev-parse", "HEAD")
        changes = git(owner, "diff", "--name-only", old, new).splitlines() if old != new else []
        if relative.endswith("Hexalith.AI.Tools"):
            require(changes == ["hexalith-llm-instructions.md"], "Unexpected instruction dependency delta")
        elif relative.endswith("Hexalith.McpCli"):
            require(all(name.startswith("_bmad-output/") for name in changes), "Compiled McpCli source changed")
        else:
            require(not changes, "Other dependency changed: " + relative)
        dependencies[relative] = {"prior_head": old, "head": new, "changed_paths": changes}
    write("dependency-comparison.json", dependencies)
    psh = ["pwsh", "-NoLogo", "-NoProfile", "-File"]
    builds = ROOT / "references/Hexalith.Builds"
    run("retained-evidence-replay", ["python3", "-O", str(PRIOR / "verify.py")])
    for name, script in (("catalog-validation", "validate-central-package-versions.ps1"),
                         ("catalog-authority", "test-authoritative-package-catalog.ps1"),
                         ("catalog-platform", "validate-platform-version-catalog.ps1")):
        run(name, psh + ["Tools/" + script], builds)
    run("catalog-exceptions", psh + ["Tools/validate-package-version-exceptions.ps1", "-InventoryPath",
                                     "Tools/package-version-exceptions.json", "-CatalogPath",
                                     "Props/Directory.Packages.props"], builds)
    run("current-audit-refusal", psh + ["Tools/validate-package-version-audit.ps1"], builds, expected=1)
    refusal = (OUT / "current-audit-refusal.log").read_text()
    require("no matching accepted audit selection" in refusal, "Unexpected audit blocker")
    run("current-source-proposal-refusal", ["python3", "eng/story117-platform-qualification-current/verify.py"],
        expected=1)
    inventory = ROOT / prior_receipt["candidate_inventory"]
    rows = re.findall(r'  - path: "([^"\n]+)"\n    kind: file\n    bytes: (\d+)\n    sha256: ([a-f0-9]{64})', inventory.read_text())
    require(len(rows) == 1635, "Wrong candidate denominator")
    for name, size, expected in rows:
        path = ROOT / name
        require(path.is_file() and path.stat().st_size == int(size) and sha(path) == expected,
                "Candidate drift: " + name)
    for ordinal in (1, 2):
        target = SCRATCH / ("candidate-" + str(ordinal) + ".yaml")
        run("candidate-generation-" + str(ordinal),
            ["python3", "scripts/generate-pd10-v2-conformance-set.py", "--output", str(target)])
        require(target.read_bytes() == inventory.read_bytes(), "Candidate generation differs")
    write("candidate-verification.json", {"path": str(inventory.relative_to(ROOT)),
                                           "sha256": sha(inventory), "entries_rehashed": len(rows),
                                           "two_generations_equal_current": True})
    after = snapshot()
    require(before == after, "Owner revisions, indices, status or protected inputs changed during checks")
    write("checks-after.json", after)
    print("Landed tuple checks completed; audit refresh and hosted operational gates remain HOLD.")


if __name__ == "__main__":
    main()
