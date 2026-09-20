#!/usr/bin/env python3
"""Write the deterministic exact-path/SHA-256 inventory for the PD10 v2 candidate."""

from __future__ import annotations

import argparse
import hashlib
import subprocess
from pathlib import Path


OUTPUT_PATH = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml"
BASELINE_COMMIT = "3f1056d998ac4688f36eb869c516812c1a4ddb71"
MATRIX_PATH = "docs/contract/authorization-matrix.md"
HISTORICAL_V1_PATH = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml"
APPROVAL_REGISTER_PATH = "_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml"


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    parser.add_argument("--baseline-commit", default=BASELINE_COMMIT)
    return parser.parse_args()


def git_lines(root: Path, *arguments: str) -> list[str]:
    result = subprocess.run(
        ["git", "-C", str(root), *arguments],
        check=True,
        capture_output=True,
        text=True,
    )
    return [line for line in result.stdout.splitlines() if line]


def candidate_paths(root: Path, baseline_commit: str) -> list[str]:
    tracked = git_lines(
        root,
        "diff",
        "--name-only",
        "--diff-filter=ACMRT",
        baseline_commit,
        "--",
    )
    untracked = git_lines(root, "ls-files", "--others", "--exclude-standard")
    candidates: set[str] = set()
    for relative_path in tracked + untracked:
        parts = Path(relative_path).parts
        if (
            relative_path in {OUTPUT_PATH, APPROVAL_REGISTER_PATH}
            or relative_path.startswith("_bmad-output/implementation-artifacts/spec-")
            or "bin" in parts
            or "obj" in parts
            or not (root / relative_path).is_file()
        ):
            continue
        candidates.add(relative_path.replace("\\", "/"))
    if not candidates:
        raise RuntimeError("The v2 conformance set cannot be empty.")
    return sorted(candidates)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    arguments = parse_arguments()
    root = arguments.repository_root.resolve()
    output = arguments.output or root / OUTPUT_PATH
    paths = candidate_paths(root, arguments.baseline_commit)
    entries = [(path, (root / path).stat().st_size, sha256(root / path)) for path in paths]
    entry_digests = {path: digest for path, _, digest in entries}

    if MATRIX_PATH not in entry_digests:
        raise RuntimeError(f"The candidate set does not contain {MATRIX_PATH}.")

    digest_material = "".join(f"{path}\0{digest}\n" for path, _, digest in entries).encode("utf-8")
    candidate_digest = hashlib.sha256(digest_material).hexdigest()
    historical_v1_digest = sha256(root / HISTORICAL_V1_PATH)
    lines = [
        "schema_version: '1.0.0'",
        "evidence_id: PD10-V2-CONFORMANCE-SET",
        "candidate_version: '2.0.0'",
        "generated_for_date: '2026-09-17'",
        f"baseline_commit: '{arguments.baseline_commit}'",
        f"historical_v1_path: {HISTORICAL_V1_PATH}",
        f"historical_v1_sha256: {historical_v1_digest}",
        f"authorization_matrix_path: {MATRIX_PATH}",
        f"authorization_matrix_sha256: {entry_digests[MATRIX_PATH]}",
        "approval_status: pending-a6b",
        "production_exposure: disabled",
        "story_closure_claimed: false",
        "governance_status: candidate-awaiting-a6b",
        "production_routed: false",
        "hash_algorithm: SHA-256",
        f"candidate_set_sha256: {candidate_digest}",
        f"artifact_count: {len(entries)}",
        "artifacts:",
    ]
    for relative_path, byte_count, digest in entries:
        lines.extend(
            (
                f"  - path: {relative_path}",
                f"    bytes: {byte_count}",
                f"    sha256: {digest}",
            )
        )

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
