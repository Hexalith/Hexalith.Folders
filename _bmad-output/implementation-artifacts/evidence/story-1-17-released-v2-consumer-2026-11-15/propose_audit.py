"""Prepare a revision-bound audit in a private, temporary committed snapshot."""
import datetime
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess

OUT = Path(__file__).resolve().parent / "provisional-audit"
ROOT = Path(__file__).resolve().parents[4]
OWNER = ROOT / "references/Hexalith.Builds"
SCRATCH = Path(os.environ["TMPDIR"])
PRIVATE = SCRATCH / "Hexalith.Builds"
OUT.mkdir()
COMMANDS = []
ENV = dict(os.environ)
ENV.update({"DOTNET_CLI_HOME": str(SCRATCH / "dotnet-home"), "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "npm_config_cache": str(SCRATCH / "npm-cache")})


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def safe(value):
    return str(value).replace(str(PRIVATE), "{private-builds}").replace(str(SCRATCH), "{temp}").replace(str(ROOT), "{workspace}")


def run(name, args, cwd=PRIVATE):
    print(name + " started", flush=True)
    process = subprocess.run(args, cwd=cwd, env=ENV, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (OUT / (name + ".log")).write_text(safe(process.stdout))
    COMMANDS.append({"name": name, "argv": [safe(arg) for arg in args], "cwd": safe(cwd),
                     "exit_code": process.returncode, "log": name + ".log"})
    (OUT / "commands.json").write_text(json.dumps(COMMANDS, indent=2) + "\n")
    require(process.returncode == 0, name + " failed")
    print(name + " passed", flush=True)
    return process.stdout


def history_preserved(old, new):
    return all(value in new for value in old)


base = subprocess.check_output(["git", "-C", str(OWNER), "rev-parse", "HEAD"], text=True).strip()
original_audit = OWNER / "Tools/package-version-audit.json"
old_hash = sha(original_audit)
old = json.loads(original_audit.read_text())
(OUT / "package-version-audit.before.json.gz").write_bytes(gzip.compress(original_audit.read_bytes(), mtime=0))
run("private-clone", ["git", "-c", "submodule.recurse=false", "clone", "--quiet", "--shared", "--no-checkout",
                      str(OWNER), str(PRIVATE)], cwd=ROOT)
run("private-read-tree", ["git", "read-tree", "HEAD"])
names = subprocess.check_output(["git", "-C", str(OWNER), "ls-files", "-z"]).split(b"\0")
files = [name.decode() for name in names if name and (OWNER / name.decode()).is_file()]
for offset in range(0, len(files), 150):
    subprocess.run(["cp", "--reflink=auto", "--parents", "--preserve=mode,timestamps",
                    "--target-directory=" + str(PRIVATE), "--", *files[offset:offset + 150]], cwd=OWNER, check=True)
os.symlink(OWNER / "node_modules", PRIVATE / "node_modules", target_is_directory=True)
require(json.loads((PRIVATE / "node_modules/@commitlint/cli/package.json").read_text())["version"] == "21.2.4",
        "Unexpected commitlint version")
message = SCRATCH / "temporary-source-message.txt"
message.write_text("build(deps): prepare the Folders 2.0.0 catalog\n\n"
                   "Temporary Story 1.17 verification snapshot only. This commit stays\n"
                   "in a disposable clone and supplies no owning-repository acceptance.\n")
run("private-commitlint", ["node", "node_modules/@commitlint/cli/cli.js", "--edit", str(message)])
run("private-stage", ["git", "add", "--", "Props/Directory.Packages.props", "Tools/test-authoritative-package-catalog.ps1"])
run("private-source-commit", ["git", "-c", "user.name=Story 1.17 verification", "-c",
                             "user.email=story117-verification@invalid", "commit", "--file", str(message)])
candidate = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=PRIVATE, text=True).strip()
(OUT / "source-commit-message.txt").write_bytes(message.read_bytes())
patch = subprocess.check_output(["git", "diff", "--binary", base, candidate], cwd=PRIVATE)
(OUT / "source.patch").write_bytes(patch)
run("private-source-bundle", ["git", "bundle", "create", str(OUT / "source.bundle"), "HEAD", "^" + base])
run("private-source-bundle-verification", ["git", "bundle", "verify", str(OUT / "source.bundle")], cwd=OWNER)
proposal = OUT / "package-version-audit.proposed.json"
run("provisional-audit-generation", ["pwsh", "-NoLogo", "-NoProfile", "-File", "Tools/audit-central-package-versions.ps1",
                                     "-ChangedFamily", "hexalith-folders", "-PriorAuditPath", "Tools/package-version-audit.json",
                                     "-OutputPath", str(proposal)])
run("provisional-audit-validation", ["pwsh", "-NoLogo", "-NoProfile", "-File", "Tools/validate-package-version-audit.ps1",
                                     "-AuditPath", str(proposal)])
new = json.loads(proposal.read_text())
require(new["generatedFromRevision"] == candidate, "Proposal did not bind the private source commit")
require(new["snapshot"]["refreshedFamilies"] == ["hexalith-folders"], "Unexpected family refresh")
old_packages = {row["id"]: row for row in old["packages"]}
new_packages = {row["id"]: row for row in new["packages"]}
require(set(old_packages) == set(new_packages), "Package inventory changed")
preserved_packages = 0
old_history = 0
for identity, row in old_packages.items():
    current = new_packages[identity]
    require(history_preserved(row["historicalContext"], current["historicalContext"]), "Package history lost")
    old_history += len(row["historicalContext"])
    if row["family"] == "hexalith-folders":
        require(current["selectedVersion"] == "2.0.0" and current["listingState"] == "listed", "Wrong family selection")
    else:
        require(current == row, "Unrequested package changed: " + identity)
        preserved_packages += 1
old_families = {row["family"]: row for row in old["familyDecisions"]}
new_families = {row["family"]: row for row in new["familyDecisions"]}
require(set(old_families) == set(new_families), "Family inventory changed")
for family, row in old_families.items():
    require(history_preserved(row["historicalContext"], new_families[family]["historicalContext"]), "Family history lost")
    old_history += len(row["historicalContext"])
    if family != "hexalith-folders":
        require(new_families[family] == row, "Unrequested family changed: " + family)
require(sha(original_audit) == old_hash, "Owning audit changed")
require(subprocess.check_output(["git", "-C", str(OWNER), "rev-parse", "HEAD"], text=True).strip() == base,
        "Owning HEAD changed")
summary = {"status": "validated-provisional-only", "recorded_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
           "owner_base": base, "temporary_source_commit": candidate,
           "owning_head_or_index_changed": False, "live_audit_replaced": False,
           "proposal": proposal.name, "proposal_sha256": sha(proposal), "source_bundle": "source.bundle",
           "source_bundle_sha256": sha(OUT / "source.bundle"), "source_patch": "source.patch",
           "source_patch_sha256": sha(OUT / "source.patch"),
           "catalog_sha256": sha(PRIVATE / "Props/Directory.Packages.props"),
           "prior_audit_sha256": old_hash, "refreshed_packages": 5, "preserved_packages": preserved_packages,
           "preserved_families": len(old_families) - 1, "prior_historical_observations_preserved": old_history,
           "meaning": "Offline-review proposal bound to a disposable source commit. Regenerate after actual owning catalog landing; this is no current owner audit acceptance."}
(OUT / "result.json").write_text(json.dumps(summary, indent=2) + "\n")
print("Provisional audit passes; all unrelated rows and historical observations are preserved.", flush=True)
