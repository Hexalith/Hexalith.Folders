#!/usr/bin/env python3
"""Write the deterministic exact-path/SHA-256 inventory for the PD10 v2 candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


OUTPUT_PATH = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml"
BASELINE_COMMIT = "3f1056d998ac4688f36eb869c516812c1a4ddb71"
MATRIX_PATH = "docs/contract/authorization-matrix.md"
HISTORICAL_V1_PATH = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml"
STORY_OWNED_PATHS_PATH = "scripts/pd10-v2-story-owned-paths.txt"


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    parser.add_argument("--baseline-commit", default=BASELINE_COMMIT)
    return parser.parse_args()


def candidate_paths(root: Path, output: Path) -> list[str]:
    allowlist = root / STORY_OWNED_PATHS_PATH
    paths = [line.strip() for line in allowlist.read_text(encoding="utf-8").splitlines() if line.strip()]
    if paths != sorted(set(paths)):
        raise RuntimeError("The story-owned candidate path allowlist must be unique and ordinally sorted.")

    resolved_output = output.resolve()
    for relative_path in paths:
        candidate = root / relative_path
        if candidate.resolve() == resolved_output:
            raise RuntimeError("The conformance-set output must not alias a required candidate input.")
        if candidate.is_symlink() or not candidate.is_file():
            raise RuntimeError(f"Story-owned candidate path is not a regular file: {relative_path}")

    if not paths:
        raise RuntimeError("The v2 conformance set cannot be empty.")
    return paths


def artifact_entry(
    root: Path,
    relative_path: str,
) -> tuple[str, str, int, str, str | None]:
    content = (root / relative_path).read_bytes()
    return relative_path, "file", len(content), hashlib.sha256(content).hexdigest(), None


def main() -> int:
    arguments = parse_arguments()
    root = arguments.repository_root.resolve()
    output = arguments.output or root / OUTPUT_PATH
    if not output.is_absolute():
        output = root / output
    output = output.resolve()
    paths = candidate_paths(root, output)
    entries = [artifact_entry(root, path) for path in paths]
    entry_digests = {path: digest for path, _, _, digest, _ in entries}

    if MATRIX_PATH not in entry_digests:
        raise RuntimeError(f"The candidate set does not contain {MATRIX_PATH}.")

    digest_material = "".join(f"{path}\0{digest}\n" for path, _, _, digest, _ in entries).encode("utf-8")
    candidate_digest = hashlib.sha256(digest_material).hexdigest()
    historical_v1_bytes = (root / HISTORICAL_V1_PATH).read_bytes()
    historical_v1_digest = hashlib.sha256(historical_v1_bytes).hexdigest()
    lines = [
        "schema_version: '1.0.0'",
        "evidence_id: PD10-V2-CONFORMANCE-SET",
        "candidate_version: '2.0.0'",
        "generated_for_date: '2026-09-17'",
        f"baseline_commit: {json.dumps(arguments.baseline_commit)}",
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
    for relative_path, kind, byte_count, digest, git_object in entries:
        lines.extend(
            (
                f"  - path: {json.dumps(relative_path, ensure_ascii=False)}",
                f"    kind: {kind}",
                f"    bytes: {byte_count}",
                f"    sha256: {digest}",
            )
        )
        if git_object is not None:
            lines.append(f"    git_object: {git_object}")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
