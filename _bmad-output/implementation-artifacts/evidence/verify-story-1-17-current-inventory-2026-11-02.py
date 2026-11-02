#!/usr/bin/env python3
"""Independently verify the pending PD10 inventory against current workspace bytes."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MANIFEST = "_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending.yaml"
MANIFEST_SHA256 = "2c872d769456c344f8307ecf287116819ef1f962c1992ac0b8b11e71e90783fc"
PROTECTED = {
    ".gitmodules": "c9daa495deb0a5903771cbbfeef10f6d376a3e521c8f6ad7447aae689c5bcf37",
    "_bmad-output/implementation-artifacts/sprint-status.yaml": "7369c8a90ac5f1514fdf5ab5eab5e900759913850f3fe8730f984036d9e057f4",
    "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml": "6289c143af409f72e53aff555310d37faa25da0bb852488fe9a2f5ab7196a286",
    "_bmad-output/planning-artifacts/planning-story-manifest.yaml": "ba04ee232dcf74da85127af481336c8a17539cf7b0a17e4b1e95bba7aab84837",
    "docs/contract/authorization-matrix.md": "d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420",
    "src/Hexalith.Folders.Server/FoldersApiRouting.cs": "5eaf1a36aaebd3039cba6279bb3d94396b2202801407036d2b3bd5caba0d89ce",
    "tools/release-packages.json": "f220b06f663b74a3d07af21d2ab5f513f6924677ea843a40d2303084e6d8cce2",
    ".github/workflows/release.yml": "d6a69c9be2a657e02a35ee2d8109d8d3658299102ae157e816ff08ceac6d2345",
}
CURRENT_CI = {
    ".github/workflows/contract-spine.yml": "d8ea375bacba39fd5435100a2311f91a6f089773aca2c31e04782dced12bd705",
    "scripts/tests/test_prepare_ci_topology_projects.py": "fca6284205fb5d8770adcbe66085f52690a86fe416a178a9da5c46cc1c142779",
    "tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineCiGateTests.cs": "015dbd2bddd53bc2f236f8bca6d654b3dc21eea4c2d070105bd28011dd900a32",
}
CONTROLS = {
    "source_identity_qualification": "pending-after-approved-v1-addition",
    "approval_status": "pending-a6b",
    "production_exposure": "disabled",
    "story_closure_claimed": "false",
    "governance_status": "candidate-awaiting-a6b",
    "production_routed": "false",
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def verify() -> dict[str, object]:
    manifest = (ROOT / MANIFEST).read_bytes()
    if digest(manifest) != MANIFEST_SHA256:
        raise ValueError("Pending manifest bytes changed")
    lines = manifest.decode("utf-8").splitlines()
    header_end = lines.index("artifacts:")
    header = dict(line.split(": ", 1) for line in lines[:header_end])
    for key, value in CONTROLS.items():
        if header.get(key) != value:
            raise ValueError(f"Pending control changed: {key}")
    for relative, expected in (PROTECTED | CURRENT_CI).items():
        if digest((ROOT / relative).read_bytes()) != expected:
            raise ValueError(f"Protected/current input changed: {relative}")

    rows = lines[header_end + 1 :]
    if len(rows) % 4:
        raise ValueError("Incomplete inventory row")
    entries: list[tuple[str, str]] = []
    for offset in range(0, len(rows), 4):
        path = json.loads(rows[offset].removeprefix("  - path: "))
        if not isinstance(path, str) or rows[offset + 1] != "    kind: file":
            raise ValueError("Invalid inventory row")
        size = int(rows[offset + 2].removeprefix("    bytes: "))
        recorded = rows[offset + 3].removeprefix("    sha256: ")
        actual = (ROOT / path).read_bytes()
        if len(actual) != size or digest(actual) != recorded:
            raise ValueError(f"Candidate row drift: {path}")
        entries.append((path, recorded))
    paths = [path for path, _ in entries]
    if paths != sorted(set(paths)) or paths != (ROOT / "scripts/pd10-v2-story-owned-paths.txt").read_text().splitlines():
        raise ValueError("Candidate paths are not the sorted allowlist")
    material = "".join(f"{path}\0{recorded}\n" for path, recorded in entries).encode()
    set_digest = digest(material)
    if header.get("candidate_set_sha256") != set_digest or header.get("artifact_count") != str(len(entries)):
        raise ValueError("Candidate aggregate does not match its rows")
    if len(entries) != 1490:
        raise ValueError("Unexpected candidate count")
    return {"status": "pass", "rows": len(entries), "manifest_sha256": digest(manifest),
            "candidate_set_sha256": set_digest, "protected_inputs": len(PROTECTED), "current_ci_inputs": len(CURRENT_CI)}


if __name__ == "__main__":
    print(json.dumps(verify(), sort_keys=True))
