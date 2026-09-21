#!/usr/bin/env python3
"""Write the deterministic exact-path/SHA-256 inventory for the PD10 v2 candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
from pathlib import Path


OUTPUT_PATH = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml"
BASELINE_COMMIT = "3f1056d998ac4688f36eb869c516812c1a4ddb71"
MATRIX_PATH = "docs/contract/authorization-matrix.md"
HISTORICAL_V1_PATH = "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml"
APPROVAL_REGISTER_PATH = "_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml"
REQUIRED_CANDIDATE_PATHS = {
    ".github/workflows/ci.yml",
    ".github/workflows/contract-spine.yml",
    "docs/contract/authorization-matrix.md",
    "docs/operations/canonical-error-catalog.md",
    "docs/sdk/api-reference.md",
    "scripts/generate-pd10-v2-conformance-set.py",
    "scripts/generate-pd10-v2-contract.py",
    "scripts/generate-v2-conformance-set.ps1",
    "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
    "src/Hexalith.Folders.Client/nswag.json",
    "src/Hexalith.Folders.Client/Generation/Program.cs",
    "src/Hexalith.Folders.Client/Generation/GeneratedClientPostProcessor.cs",
    "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
    "src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs",
    "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml",
    "tests/fixtures/parity-contract.schema.json",
    "tests/fixtures/parity-contract.yaml",
    "tests/fixtures/previous-spine.yaml",
    "tests/tools/parity-oracle-generator/Program.cs",
    "tests/tools/run-consumer-docs-gates.ps1",
    "tests/tools/run-contract-parity-ci-gates.ps1",
    "tests/tools/run-contract-spine-gates.ps1",
    "tests/tools/run-governance-completeness-gates.ps1",
    "tests/tools/run-provider-error-docs-gates.ps1",
}


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    parser.add_argument("--baseline-commit", default=BASELINE_COMMIT)
    return parser.parse_args()


def git_nul_records(root: Path, *arguments: str) -> list[str]:
    result = subprocess.run(
        ["git", "-C", str(root), *arguments],
        check=True,
        capture_output=True,
    )
    return [os.fsdecode(record) for record in result.stdout.split(b"\0") if record]


def gitlinks(root: Path) -> dict[str, str]:
    links: dict[str, str] = {}
    for line in git_nul_records(root, "ls-files", "--stage", "-z"):
        metadata, relative_path = line.split("\t", 1)
        mode, object_id, _stage = metadata.split(" ", 2)
        if mode == "160000":
            links[relative_path.replace("\\", "/")] = object_id
    return links


def candidate_paths(root: Path, baseline_commit: str, output: Path) -> tuple[list[str], dict[str, str]]:
    tracked = git_nul_records(
        root,
        "diff",
        "--name-only",
        "--diff-filter=ACMRT",
        "-z",
        baseline_commit,
        "--",
    )
    untracked = git_nul_records(root, "ls-files", "--others", "--exclude-standard", "-z")
    root_gitlinks = gitlinks(root)
    resolved_output = output.resolve()
    try:
        output_relative = resolved_output.relative_to(root).as_posix()
    except ValueError:
        output_relative = None
    candidates: set[str] = set(REQUIRED_CANDIDATE_PATHS)
    for relative_path in tracked + untracked:
        relative_path = relative_path.replace("\\", "/")
        parts = Path(relative_path).parts
        if (
            relative_path in {OUTPUT_PATH, APPROVAL_REGISTER_PATH, output_relative}
            or relative_path.startswith("_bmad-output/gates/")
            or relative_path.startswith("_bmad-output/implementation-artifacts/")
            or relative_path.startswith("_bmad-output/planning-artifacts/")
            or "bin" in parts
            or "obj" in parts
            or (relative_path not in root_gitlinks and not (root / relative_path).is_file())
        ):
            continue
        candidates.add(relative_path)
    if not candidates:
        raise RuntimeError("The v2 conformance set cannot be empty.")
    return sorted(candidates), root_gitlinks


def artifact_entry(
    root: Path,
    relative_path: str,
    root_gitlinks: dict[str, str],
) -> tuple[str, str, int, str, str | None]:
    if relative_path in root_gitlinks:
        object_id = root_gitlinks[relative_path]
        content = object_id.encode("ascii")
        return relative_path, "gitlink", len(content), hashlib.sha256(content).hexdigest(), object_id
    content = (root / relative_path).read_bytes()
    return relative_path, "file", len(content), hashlib.sha256(content).hexdigest(), None


def main() -> int:
    arguments = parse_arguments()
    root = arguments.repository_root.resolve()
    output = arguments.output or root / OUTPUT_PATH
    if not output.is_absolute():
        output = root / output
    output = output.resolve()
    paths, root_gitlinks = candidate_paths(root, arguments.baseline_commit, output)
    entries = [artifact_entry(root, path, root_gitlinks) for path in paths]
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
