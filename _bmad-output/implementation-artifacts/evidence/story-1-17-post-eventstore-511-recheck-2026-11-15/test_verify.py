#!/usr/bin/env python3
"""Corruption tests for verify.py. Run as ``run-with-temp python3 -I test_verify.py``.

Each case copies the evidence directory into TMPDIR, mutates the copy, rebinds the manifest when the
case targets a semantic rule rather than byte integrity, and requires verify.py to refuse it under
both normal and optimized Python. The retained evidence directory is never modified.
"""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent


def rebind(root: Path) -> None:
    manifest = json.loads((root / "manifest.json").read_text())
    for relative in manifest["files"]:
        data = (root / relative).read_bytes()
        manifest["files"][relative] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n")


def edit_json(path: Path, change) -> None:
    value = json.loads(path.read_text())
    change(value)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def run(root: Path, optimized: bool) -> int:
    arguments = [sys.executable, "-I"] + (["-O"] if optimized else []) + [str(HERE / "verify.py"), "--root", str(root)]
    return subprocess.run(arguments, capture_output=True, text=True, check=False).returncode


CASES = {
    "pristine": (None, False, 0),
    "corrupted-log": (lambda r: (r / "final/source-routing.log").write_text("tampered\n"), False, 1),
    "unlisted-file": (lambda r: (r / "final/extra.txt").write_text("x\n"), False, 1),
    "rebound-activation-authority": (lambda r: edit_json(r / "handoff.json",
        lambda v: v["operationalControls"].__setitem__("activationAuthority", "fabricated")), True, 1),
    "rebound-route-mode": (lambda r: edit_json(r / "receipt.json",
        lambda v: v["operationalControls"].__setitem__("routeMode", "Coexistence")), True, 1),
    "rebound-overclaimed-count": (lambda r: edit_json(r / "receipt.json",
        lambda v: v["attempts"]["final"]["counts"]["source-routing"].__setitem__("passed", 999)), True, 1),
    "rebound-hidden-attempt-failure": (lambda r: edit_json(r / "receipt.json",
        lambda v: v["attempts"]["attempt-1"]["exit_codes"].__setitem__("source-native-authority", 0)), True, 1),
    "rebound-solution-result": (lambda r: edit_json(r / "receipt.json",
        lambda v: v["solutionRerun"]["result"].__setitem__("build", 12345)), True, 1),
    "rebound-root-cause": (lambda r: edit_json(r / "receipt.json",
        lambda v: v["rootCause"]["assemblyVersions"].__setitem__("Hexalith.EventStore.Client", "5.1.1.0")), True, 1),
}


def main() -> int:
    scratch = Path(os.environ["TMPDIR"])
    if "hexalith-job." not in str(scratch):
        raise SystemExit("Run under run-with-temp")
    failures = []
    for name, (mutate, rebound, expected) in CASES.items():
        root = scratch / name
        shutil.copytree(HERE, root, ignore=shutil.ignore_patterns("__pycache__"))
        if mutate is not None:
            mutate(root)
        if rebound:
            rebind(root)
        for optimized in (False, True):
            code = run(root, optimized)
            if (code == 0) != (expected == 0):
                failures.append(f"{name} optimized={optimized} exit={code}")
    print(json.dumps({"cases": len(CASES), "modes": 2, "failures": failures}))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
