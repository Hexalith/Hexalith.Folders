"""Verify the exact public evidence files; no service or private configuration access."""
from pathlib import Path
import hashlib
import json

root = Path(__file__).resolve().parent
manifest = json.loads((root / "seal.json").read_text())
actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file() and p.name != "seal.json" and "__pycache__" not in p.parts}
assert actual == set(manifest["files"]), "Artifact inventory changed"
for name, entry in manifest["files"].items():
    path = root / name
    assert path.is_file() and not path.is_symlink(), name
    assert path.stat().st_size == entry["bytes"], name
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"], name
summary = json.loads((root / "consumer-summary.json").read_text())
assert summary["totals"] == {"passed":5545,"failed":0,"skipped":2,"errors":0,"total":5547}
audit = json.loads((root / "change-audit.json").read_text())
assert audit["frozen_intent_unchanged"] and all(audit["strict_bodies_unchanged"].values())
assert audit["immutable_historical_conformance_sha256"] == "6289c143af409f72e53aff555310d37faa25da0bb852488fe9a2f5ab7196a286"
for row in json.loads((root / "matrix-audit.json").read_text()).values():
    assert row["component_pass"] and not row["strict_live_qualified"]
for attempt in range(2,5):
    receipt = json.loads((root / "commands" / f"strict-operational-{attempt}-receipt.json").read_text())
    assert receipt["exit_code"] == 1 and not receipt["source_or_head_drift"] and not receipt["runtime_file_drift"]
    cleanup = receipt["cleanup"]
    assert not cleanup["remaining_observed_descendants"] and not cleanup["remaining_owned_containers"] and not cleanup["remaining_observed_directories"]
assert all(not state["exists"] for row in json.loads((root / "mtls-cleanup-audit.json").read_text()) for key,state in row.items() if key != "attempt")
print(json.dumps({"sealed_files":len(actual),"consumer":summary["totals"],"strict_live_qualified":False,"owner_decision_required":True}))

