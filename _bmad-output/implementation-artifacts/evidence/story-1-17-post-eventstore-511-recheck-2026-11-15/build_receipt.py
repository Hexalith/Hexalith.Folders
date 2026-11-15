#!/usr/bin/env python3
"""Derive receipt.json from the retained run outputs so stated results cannot drift from evidence."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
ATTEMPTS = ("attempt-1", "attempt-2-private-fix-trial", "final")
MIGRATION = "_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md"
PUBLIC = "_bmad-output/implementation-artifacts/evidence/story-1-17-published-release-2026-11-15"
PROJECTS_PUBLIC = "references/Hexalith.Projects/_bmad-output/implementation-artifacts/evidence/story-1-17-published-release-2026-11-15"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> object:
    return json.loads(path.read_text())


def attempt(name: str) -> dict:
    directory = HERE / name
    commands = load(directory / "commands.json")
    counts, verdicts = {}, {}
    for path in sorted(directory.glob("*-counts.json")):
        label = path.name.removesuffix("-counts.json")
        value = load(path)
        counts[label] = value["totals"]
        verdicts[label] = value["every_selector_passed_unskipped"]
    result = {"exit_codes": {entry["label"]: entry["exit_code"] for entry in commands},
              "counts": counts, "selectors_pass": verdicts, "generation": load(directory / "generation.json"),
              "started_utc": commands[0]["started_utc"], "finished_utc": commands[-1]["finished_utc"]}
    if (directory / "source-solution.json").is_file():
        result["solution"] = load(directory / "source-solution.json")
    return result


def main() -> int:
    header = json.loads(Path(sys.argv[1]).read_text())
    diagnosis = load(HERE / "attempt-1/diagnosis/diagnose-source-binding.json")
    final_before = load(HERE / "final/before.json")
    rerun = load(HERE / "final-solution-with-pwsh-path/source-solution.json")
    rerun_commands = load(HERE / "final-solution-with-pwsh-path/commands.json")
    receipt = {
        "schema": "hexalith.folders.story117.post-eventstore-511-recheck.v2026-11-15",
        "qualification": "HOLD",
        "scope": "Local Debug/source recheck and Folders source-graph identity repair after EventStore 5.1.1; "
                 "Release/package results are reused from the same-day public-feed qualification.",
        "sourceTuple": final_before["repositories"],
        "attempts": {name: attempt(name) for name in ATTEMPTS},
        "solutionRerun": {"result": rerun, "commands": rerun_commands,
                          "reason": "final/ solution build stopped when Hexalith.Builds.Tooling ran pwsh, absent from the "
                                    "agent PATH (exit 127); only ~/.dotnet/tools was prepended."},
        "rootCause": {
            "assemblyVersions": {key.split("/")[0]: next(iter(value["runtime"].values()))["assemblyVersion"]
                                 for key, value in diagnosis["deps_targets"].items()},
            "explanation": "Builds selects EventStore 5.1.1 while Tenants keeps a local 5.1.0 package-cohort default and "
                           "carries it on its EventStore source edges, so EventStore Client/Contracts were also built as "
                           "5.1.0.0 into the shared source output that Folders and EventStore Server reference as 5.1.1.0.",
        },
        "fix": {"path": "Directory.Build.targets",
                "patchSha256": sha(HERE / "attempt-2-private-fix-trial/private-copy.patch"),
                "resultSha256": sha(ROOT / "Directory.Build.targets"),
                "baseSha256": subprocess.check_output(["git", "-C", str(ROOT), "rev-parse", "HEAD:Directory.Build.targets"],
                                                      text=True).strip(),
                "profileEvaluationSha256": sha(HERE / "profile-evaluation.json")},
        "reusedReleasePackageEvidence": {
            "qualifiedAtUtc": "2026-11-15T14:49Z",
            "files": {relative: sha(ROOT / relative) for relative in (
                f"{PUBLIC}/result.json", f"{PUBLIC}/completed-commands.json", f"{PUBLIC}/source-snapshots.json",
                f"{PUBLIC}/runtime-package-bindings.json", f"{PUBLIC}/parity-report.json",
                f"{PUBLIC}/native-authority-counts.json", f"{PUBLIC}/routing-counts.json",
                f"{PROJECTS_PUBLIC}/conversations-folders-adapters-counts.json",
                "_bmad-output/implementation-artifacts/evidence/story-1-17-approved-release-2026-11-15/RELEASE.json")},
            "results": {"foldersReleaseSolution": "0 warnings, 0 errors", "projectsReleaseSolution": "0 warnings, 0 errors",
                        "projectsAdapters": 221, "foldersNativeAuthority": 64, "foldersRouting": 20,
                        "parityCategories": "12/12", "governance": "217/217 + 7/7 + 3/3", "runtimePackageBindings": 32},
            "reuseIdentitySha256": sha(HERE / "reuse-identity.json"),
        },
        "pendingInventory": {"path": "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml",
                             "rows": 1635, "rawSha256": "c715af7dde4f986adeb2098aa18243fe04216097fb366ea2d0f973e7bd8ff1f2",
                             "orderedSetSha256": "6084b780cb14749d79930766833ce7cc82e33cb49472a17e46701007f47f1022",
                             "resealed": False},
        "intentionalProtectedEdits": [MIGRATION],
        "migrationHeaderCorrection": header,
        "operationalControls": {"routeMode": "V1Only", "migrationT0": None, "firstInstallActivation": None,
                                "activationAuthority": None, "stagingChecksExecuted": 0, "trackerStatus": "backlog",
                                "parentStatus": "in-progress", "storyClosureClaimed": False},
        "notPerformed": ["stage", "commit", "push", "publish", "deploy", "dependency or pin change",
                         "nested submodule initialization", "remote request", "tracker transition", "route activation"],
    }
    (HERE / "receipt.json").write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n")
    print(json.dumps({"attempts": list(receipt["attempts"]), "solution_rerun": rerun}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
