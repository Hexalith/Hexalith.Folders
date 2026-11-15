#!/usr/bin/env python3
"""Compare the current owning checkouts with the source snapshots of the public 5.1.1 qualification.

Read-only. A dated public-feed Release/package result is reusable only for owners whose HEAD and
changed tracked bytes are identical, except files proven not to be compiled or gate inputs.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
PUBLIC = ROOT / "_bmad-output/implementation-artifacts/evidence/story-1-17-published-release-2026-11-15"
# Story narrative files are not compiled, packed, generated into the candidate inventory or read by any
# test/script/workflow (checked with grep over tests/, src/, scripts/, tools/ and .github/).
NON_INPUT = {"_bmad-output/implementation-artifacts/spec-1-17-complete-pd10-v2-migration-and-closure.md",
             "_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md",
             "_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md"}
# This run's Directory.Build.targets edit is evaluated, per CI-solution project, as inert in the Release/package
# profile (profile-evaluation.json). It is material for Debug/source, which this run executes fresh.
PACKAGE_INERT = {"Directory.Build.targets"}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    snapshots = json.loads((PUBLIC / "source-snapshots.json").read_text())
    evaluation = json.loads((HERE / "profile-evaluation.json").read_text())
    package_inert = evaluation.get("status") == "pass" and evaluation.get("package_inert") is True
    rows = []
    reusable = True
    for item in snapshots:
        owner = Path(item["owner"])
        relative = "." if owner == ROOT else str(owner.relative_to(ROOT))
        head = subprocess.check_output(["git", "-C", str(owner), "rev-parse", "HEAD"], text=True).strip()
        changed = subprocess.check_output(["git", "-C", str(owner), "diff", "HEAD", "--name-only",
                                           "--ignore-submodules=all"], text=True).split()
        current = {name: sha(owner / name) for name in changed if (owner / name).is_file()}
        differences = sorted(set(current) ^ set(item["changed_files"]) |
                             {name for name in set(current) & set(item["changed_files"])
                              if current[name] != item["changed_files"][name]})
        material = [name for name in differences if name not in NON_INPUT
                    and not (relative == "." and name in PACKAGE_INERT and package_inert)]
        same = head == item["head"] and not material
        reusable &= same
        rows.append({"owner": relative, "public_head": item["head"], "current_head": head,
                     "public_changed_files": item["changed_files"], "current_changed_files": current,
                     "differences": differences, "material_differences": material, "package_profile_inputs_equivalent": same})
    result = {"public_source_snapshots_sha256": sha(PUBLIC / "source-snapshots.json"),
              "public_result_sha256": sha(PUBLIC / "result.json"),
              "public_completed_commands_sha256": sha(PUBLIC / "completed-commands.json"),
              "non_input_exclusions": sorted(NON_INPUT), "package_inert_exclusions": sorted(PACKAGE_INERT),
              "profile_evaluation_sha256": sha(HERE / "profile-evaluation.json"), "package_inert_proven": package_inert,
              "owners": rows, "reusable_for_release_package_profile_only": reusable, "reusable": reusable}
    (HERE / "reuse-identity.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print(json.dumps({"owners": len(rows), "reusable": reusable,
                      "material": {row["owner"]: row["material_differences"] for row in rows if row["material_differences"]},
                      "nonmaterial": {row["owner"]: row["differences"] for row in rows if row["differences"] and not row["material_differences"]}}))
    return 0 if reusable else 1


if __name__ == "__main__":
    sys.exit(main())
