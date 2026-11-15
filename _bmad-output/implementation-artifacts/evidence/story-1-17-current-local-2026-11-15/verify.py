#!/usr/bin/env python3
"""Verify retained evidence; optionally rehash its source bindings in an owning workspace."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import zipfile


HERE = Path(__file__).resolve().parent
CONTROLS = {
    "routeMode": "V1Only", "migrationT0": None, "firstInstallActivation": None,
    "activationAuthority": None, "stagingChecksExecuted": 0, "parentStatus": "in-progress",
    "trackerStatus": "backlog", "storyClosureClaimed": False,
}
PENDING = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml"
EXPECTED_MANIFEST = "c715af7dde4f986adeb2098aa18243fe04216097fb366ea2d0f973e7bd8ff1f2"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_json(root: Path, name: str):
    return json.loads((root / name).read_text())


def verify_inventory(data: bytes) -> list[tuple[str, str, int]]:
    require(digest(data) == EXPECTED_MANIFEST, "Pending candidate bytes changed")
    lines = data.decode().splitlines()
    boundary = lines.index("artifacts:")
    header = dict(line.split(": ", 1) for line in lines[:boundary])
    expected = {
        "source_identity_qualification": "pending-after-qualified-source-pins", "approval_status": "pending-a6b",
        "production_exposure": "disabled", "story_closure_claimed": "false",
        "governance_status": "candidate-awaiting-a6b", "production_routed": "false",
    }
    require(all(header.get(k) == v for k, v in expected.items()), "Pending authority changed")
    rows = lines[boundary + 1:]
    require(len(rows) % 4 == 0, "Incomplete candidate row")
    entries = []
    for index in range(0, len(rows), 4):
        path = json.loads(rows[index].removeprefix("  - path: "))
        require(not PurePosixPath(path).is_absolute() and ".." not in PurePosixPath(path).parts, "Unsafe candidate path")
        require(rows[index + 1] == "    kind: file", "Candidate kind changed")
        entries.append((path, rows[index + 3].removeprefix("    sha256: "), int(rows[index + 2].removeprefix("    bytes: "))))
    require(len(entries) == 1635 and header["artifact_count"] == "1635", "Candidate count changed")
    paths = [path for path, _, _ in entries]
    require(paths == sorted(set(paths)), "Candidate paths are not sorted and unique")
    ordered = digest("".join(f"{path}\0{value}\n" for path, value, _ in entries).encode())
    require(ordered == header["candidate_set_sha256"], "Candidate aggregate changed")
    return entries


def verify(root: Path, workspace: Path | None = None) -> dict:
    manifest = read_json(root, "manifest.json")
    files = manifest["files"]
    actual = {str(path.relative_to(root)) for path in root.rglob("*") if path.is_file() and path != root / "manifest.json"}
    require(actual == set(files), "Evidence file inventory changed")
    for relative, expected in files.items():
        parts = PurePosixPath(relative)
        require(not parts.is_absolute() and ".." not in parts.parts, "Unsafe evidence path")
        path = root / relative
        require(not path.is_symlink() and not any(p.is_symlink() for p in path.parents if p != root.parent), "Evidence symlink refused")
        content = path.read_bytes()
        require(len(content) == expected["bytes"] and digest(content) == expected["sha256"], f"Evidence integrity: {relative}")
    canonical = json.dumps(CONTROLS, sort_keys=True)
    for name in ("handoff.json", "receipt.json"):
        value = read_json(root, name)
        require(json.dumps(value.get("operationalControls"), sort_keys=True) == canonical, f"Unsafe controls: {name}")
        require(value.get("qualification") == "HOLD", f"Unsafe qualification: {name}")
    entries = verify_inventory((root / "candidate-generated.yaml").read_bytes())
    require((root / "candidate-generated.yaml").read_bytes() == (root / "candidate-generated-second.yaml").read_bytes(), "Two generations differ")
    expected_success = {
        "source-commands.json": {"source-server-tests-behavior": [20], "source-integrationtests-behavior": [19],
            "source-contracts-tests-behavior": [18], "source-conformance": [21]},
        "final-commands.json": {"final-conformance": [21], "final-contract-behavior": [18], "final-governance": [217, 7, 3]},
    }
    for name, expected in expected_success.items():
        commands = {row["label"]: row for row in read_json(root, name)}
        for label, counts in expected.items():
            command = commands[label]
            require(command["exit_code"] == 0, f"Required local gate failed: {label}")
            log = (root / command["log"]).read_bytes()
            require(digest(log) == command["log_sha256"], f"Command log changed: {label}")
            summaries = re.findall(r"Total:\s*(\d+), Errors:\s*(\d+), Failed:\s*(\d+), Skipped:\s*(\d+), Not Run:\s*(\d+)", log.decode())
            require([int(row[0]) for row in summaries] == counts, f"Test counts changed: {label}")
            require(all(row[1:] == ("0", "0", "0", "0") for row in summaries), f"Nonpassing test result: {label}")
    release = {row["label"]: row for row in read_json(root, "release-commands.json")}
    require(release["folders-release-build"]["exit_code"] != 0, "Package failure converted into success")
    require(release["projects-restore"]["exit_code"] != 0, "Offline consumer failure converted into success")
    parity = read_json(root, "final-parity-report.json")
    require(parity["status"] == "failed" and len(parity["results"]) == 12, "Parity failure lost")
    require(sum(row["status"] == "passed" for row in parity["results"]) == 5, "Parity count changed")
    before = read_json(root, "source-before.json")
    after = read_json(root, "source-after.json")
    require(before["protected"] == after["protected"] and before["repositories"] == after["repositories"], "Source-run preservation failed")
    for package in read_json(root, "cached-package-identities.json"):
        path = root / package["archive"]
        require(digest(path.read_bytes()) == package["archive_sha256"], "Retained archive changed")
        with zipfile.ZipFile(path) as archive:
            for payload in package["dlls"]:
                require(digest(archive.read(payload["member"])) == payload["sha256"], "Retained payload changed")
    for index in ("source-runtime-artifacts.json", "final-contracts-artifacts.json"):
        for artifact in read_json(root, index):
            if artifact["retained"]:
                require(digest((root / artifact["retained"]).read_bytes()) == artifact["sha256"], "Diagnostic artifact changed")
    if workspace:
        require((workspace / "_bmad-output/implementation-artifacts/story-1-17-publication-handoff-2026-11-15.json").read_bytes() == (root / "handoff.json").read_bytes(), "Owning handoff differs")
        require((workspace / PENDING).read_bytes() == (root / "candidate-generated.yaml").read_bytes(), "Owning pending file differs")
        for relative, expected, size in entries:
            content = (workspace / relative).read_bytes()
            require(len(content) == size and digest(content) == expected, f"Current candidate drift: {relative}")
        for relative, expected in before["protected"].items():
            require(digest((workspace / relative).read_bytes()) == expected, f"Protected input drift: {relative}")
        for relative, expected in read_json(root, "source-copied-inputs.json").items():
            require(digest((workspace / relative).read_bytes()) == expected, f"Copied source drift: {relative}")
        for name in ("spec-1-17-complete-pd10-v2-migration-and-closure.md", "story-1-17-current-candidate-technical-readiness-2026-09-24.md"):
            require((workspace / "_bmad-output/implementation-artifacts" / name).read_bytes().startswith((root / (name + ".before")).read_bytes()), "Historical prefix changed")
    return {"status": "pass", "candidate_rows": len(entries), "evidence_files": len(files),
            "protected_inputs": len(before["protected"]), "workspace_rehashed": workspace is not None,
            "qualification": "HOLD", "interpretation": "retained evidence integrity and stated local results; no hosted acceptance"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path)
    arguments = parser.parse_args()
    try:
        print(json.dumps(verify(HERE, arguments.workspace), sort_keys=True))
    except (ValueError, KeyError, OSError, TypeError) as failure:
        raise SystemExit(f"FAIL: {failure}") from failure
