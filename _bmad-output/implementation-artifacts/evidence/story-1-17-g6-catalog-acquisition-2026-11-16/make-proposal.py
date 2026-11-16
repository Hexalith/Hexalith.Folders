#!/usr/bin/env python3
"""Build an unapplied, exact-source owner integration patch; never write Projects."""

import difflib
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
OWNER = ROOT.parent / "cicd"
PROJECTS = ROOT / "references/Hexalith.Projects"
BASE = "981ceef13a1abc88219925e73c51a23eb9ac50c2"
WORKFLOW = ".github/workflows/ci.yml"
FILES = (".github/actions/prepare-g6-catalog/action.yml", ".github/actions/prepare-g6-catalog/prepare.py")

original = subprocess.check_output(["git", "-C", str(PROJECTS), "show", BASE + ":" + WORKFLOW]).decode()
anchor = "      - name: Restore effective G-6 package graphs\n"
if original.count(anchor) != 1:
    raise RuntimeError("Expected one exact G-6 restore step")
addition = '''      - name: Resolve reviewed direct Platform revision
        id: g6-platform
        shell: bash
        run: |
          set -euo pipefail
          platform_revision="$(git rev-parse HEAD:references/Hexalith.Platform)"
          [[ "$platform_revision" =~ ^[0-9a-f]{40}$ ]]
          printf 'revision=%s\\n' "$platform_revision" >> "$GITHUB_OUTPUT"

      - name: Acquire reviewed G-6 catalog
        uses: ./.github/actions/prepare-g6-catalog
        with:
          platform-revision: ${{ steps.g6-platform.outputs.revision }}
          diagnostics: ${{ env.G6_OUTPUT }}/source-mapping

'''
updated = original.replace(anchor, addition + anchor)
patch = "diff --git a/" + WORKFLOW + " b/" + WORKFLOW + "\n"
patch += "".join(difflib.unified_diff(original.splitlines(keepends=True), updated.splitlines(keepends=True),
                                     fromfile="a/" + WORKFLOW, tofile="b/" + WORKFLOW))
for path in FILES:
    content = (OWNER / path).read_text()
    patch += f"diff --git a/{path} b/{path}\nnew file mode 100644\n"
    patch += "".join(difflib.unified_diff([], content.splitlines(keepends=True), fromfile="/dev/null", tofile="b/" + path))
destination = OWNER / "docs/proposals/projects-g6-catalog.patch"
destination.parent.mkdir(exist_ok=True)
destination.write_text(patch)
(Path(__file__).parent / "owner-proposal.patch").write_text(patch)
manifest = {"projects_base": BASE, "workflow_before_sha256": hashlib.sha256(original.encode()).hexdigest(),
            "workflow_after_sha256": hashlib.sha256(updated.encode()).hexdigest(),
            "owner_files": {path: hashlib.sha256((OWNER / path).read_bytes()).hexdigest() for path in FILES},
            "patch_sha256": hashlib.sha256(patch.encode()).hexdigest(),
            "patch": str(destination), "applied": False, "delivery": "blocked-no-supported-pre-adoption-lane"}
(Path(__file__).parent / "proposal.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps(manifest, sort_keys=True))
