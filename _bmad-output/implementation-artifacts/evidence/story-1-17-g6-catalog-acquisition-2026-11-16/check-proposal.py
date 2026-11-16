#!/usr/bin/env python3
"""Check and apply the owner proposal only in a disposable sparse source copy."""

import hashlib
import json
import os
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OWNER = ROOT.parent / "cicd"
binding = json.loads((HERE / "proposal.json").read_text())
source = ROOT / "references/Hexalith.Projects"
target = Path(os.environ["TMPDIR"]) / "proposal-check"


def git(root, *args):
    return subprocess.check_output(["git", "-c", "submodule.recurse=false", "-C", str(root), *map(str, args)])


subprocess.run(["git", "clone", "--quiet", "--shared", "--no-checkout", "--no-recurse-submodules",
                str(source), str(target)], check=True)
git(target, "read-tree", binding["projects_base"])
workflow = target / ".github/workflows/ci.yml"
workflow.parent.mkdir(parents=True)
workflow.write_bytes(git(source, "show", binding["projects_base"] + ":.github/workflows/ci.yml"))
before = {"head": git(target, "rev-parse", "HEAD").decode().strip(),
          "index_sha256": hashlib.sha256(git(target, "ls-files", "--stage", "-z")).hexdigest()}
patch = OWNER / "docs/proposals/projects-g6-catalog.patch"
git(target, "apply", "--check", patch)
git(target, "apply", patch)
if hashlib.sha256(workflow.read_bytes()).hexdigest() != binding["workflow_after_sha256"]:
    raise RuntimeError("Applied workflow differs from the proposal")
for path, expected in binding["owner_files"].items():
    if hashlib.sha256((target / path).read_bytes()).hexdigest() != expected:
        raise RuntimeError("Applied action differs from the owner source")
after = {"head": git(target, "rev-parse", "HEAD").decode().strip(),
         "index_sha256": hashlib.sha256(git(target, "ls-files", "--stage", "-z")).hexdigest()}
if before != after or before["head"] != binding["projects_base"]:
    raise RuntimeError("Source revision or index changed")
print(json.dumps({"scope": "disposable proposal applicability only; no owner delivery",
                  "base": binding["projects_base"], "git_apply_check_exit": 0,
                  "disposable_apply_exit": 0, "before": before, "after": after,
                  "exact_action_and_workflow_bytes": True, "live_module_applied": False}))
