#!/usr/bin/env python3
"""Exercise integrity and rebound authority refusals under optimized Python."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


HERE = Path(__file__).resolve().parent


class ReplayTests(unittest.TestCase):
    def replay(self, root: Path) -> subprocess.CompletedProcess:
        return subprocess.run([sys.executable, "-O", str(root / "verify.py")], capture_output=True, text=True, check=False)

    def mutate(self, name: str, callback, rebound: bool) -> str:
        with tempfile.TemporaryDirectory() as temporary:
            copy = Path(temporary) / "evidence"
            shutil.copytree(HERE, copy)
            path = copy / name
            callback(path)
            if rebound:
                manifest = json.loads((copy / "manifest.json").read_text())
                manifest["files"][name] = {"sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "bytes": path.stat().st_size}
                (copy / "manifest.json").write_text(json.dumps(manifest))
            result = self.replay(copy)
            self.assertNotEqual(0, result.returncode, result.stdout)
            return result.stderr

    def test_retained_evidence_passes_optimized_replay(self):
        result = self.replay(HERE)
        self.assertEqual(0, result.returncode, result.stderr)

    def test_corruption_is_refused(self):
        error = self.mutate("source-server-tests-behavior.log", lambda path: path.write_bytes(path.read_bytes() + b"corrupted"), False)
        self.assertIn("Evidence integrity", error)

    def test_rebound_handoff_authority_is_refused(self):
        def change(path):
            value = json.loads(path.read_text())
            value["operationalControls"]["activationAuthority"] = "fabricated"
            path.write_text(json.dumps(value))
        self.assertIn("Unsafe controls", self.mutate("handoff.json", change, True))

    def test_rebound_receipt_qualification_is_refused(self):
        def change(path):
            value = json.loads(path.read_text())
            value["qualification"] = "accepted"
            path.write_text(json.dumps(value))
        self.assertIn("Unsafe qualification", self.mutate("receipt.json", change, True))

    def test_rebound_failed_gate_cannot_claim_success(self):
        def change(path):
            commands = json.loads(path.read_text())
            next(row for row in commands if row["label"] == "final-conformance")["exit_code"] = 1
            path.write_text(json.dumps(commands))
        self.assertIn("Required local gate failed", self.mutate("final-commands.json", change, True))

    def test_rebound_candidate_exposure_is_refused(self):
        self.assertIn("Pending candidate bytes changed", self.mutate("candidate-generated.yaml", lambda path:
            path.write_bytes(path.read_bytes().replace(b"production_exposure: disabled", b"production_exposure: enabled")), True))


if __name__ == "__main__":
    unittest.main(verbosity=2)
