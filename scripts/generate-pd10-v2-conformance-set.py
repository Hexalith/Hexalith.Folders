#!/usr/bin/env python3
"""Write the deterministic exact-path/SHA-256 inventory for the PD10 v2 candidate."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


OUTPUT_PATH = "_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml"
# Run-local gate reports are operational evidence that mutate while the gate executes;
# the A6b candidate set binds deterministic candidate inputs and outputs instead.
ARTIFACT_PATHS = sorted(
    {
        "docs/contract/authorization-matrix.md",
        "docs/contract/contract-parity-ci-gates.md",
        "docs/contract/contract-spine-foundation.md",
        "docs/contract/parity-oracle-generator.md",
        "docs/contract/pd10-v2-candidate.md",
        "docs/contract/sdk-generation-and-idempotency-helpers.md",
        "docs/operations/canonical-error-catalog.md",
        "docs/sdk/api-reference.md",
        "docs/sdk/cli-reference.md",
        "docs/sdk/mcp-reference.md",
        "docs/sdk/quickstart.md",
        "scripts/generate-pd10-v2-conformance-set.py",
        "scripts/generate-pd10-v2-contract.py",
        "samples/Hexalith.Folders.Sample/FolderLifecycleSample.cs",
        "src/Hexalith.Folders.Cli/Commands/Commit/CommitCommand.cs",
        "src/Hexalith.Folders.Cli/Commands/Folder/FolderCommand.cs",
        "src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs",
        "src/Hexalith.Folders.Cli/FoldersExitCodes.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs",
        "src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs",
        "src/Hexalith.Folders.Client/Generation/Program.cs",
        "src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj",
        "src/Hexalith.Folders.Client/nswag.json",
        "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml",
        "src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs",
        "src/Hexalith.Folders.Mcp/Tools/CommitTools.cs",
        "src/Hexalith.Folders.Mcp/Tools/DiagnosticsTools.cs",
        "src/Hexalith.Folders.Mcp/Tools/FolderTools.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10AuthorityEvidenceState.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10AuthorizationContext.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10AuthorizationOutcome.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationExecutor.cs",
        "src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationResult.cs",
        "src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs",
        "src/Hexalith.Folders.UI/Components/ConsoleErrorPanel.razor",
        "src/Hexalith.Folders.UI/Components/Models/ConsoleErrorView.cs",
        "src/Hexalith.Folders.UI/Components/Pages/AuditTrail.razor.cs",
        "src/Hexalith.Folders.UI/Components/Pages/FolderDetail.razor",
        "src/Hexalith.Folders.UI/Components/Pages/IncidentStream.razor.cs",
        "src/Hexalith.Folders.UI/Components/Pages/OperationTimeline.razor.cs",
        "src/Hexalith.Folders.UI/Components/Pages/Provider.razor.cs",
        "src/Hexalith.Folders.UI/Components/Pages/Workspace.razor.cs",
        "src/Hexalith.Folders.UI/Services/ConsoleErrorDisposition.cs",
        "src/Hexalith.Folders.UI/Services/ConsoleErrorPresenter.cs",
        "src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs",
        "tests/Hexalith.Folders.Cli.Tests/ErrorProjectionTests.cs",
        "tests/Hexalith.Folders.Cli.Tests/ExitCodeWiringTests.cs",
        "tests/Hexalith.Folders.Cli.Tests/ParityOracleConformanceTests.cs",
        "tests/Hexalith.Folders.Client.Tests/ArchiveFolderClientConformanceTests.cs",
        "tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10ConformanceSetTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10V2CandidateContractTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/Deployment/ContractParityCiWorkflowConformanceTests.cs",
        "tests/Hexalith.Folders.Contracts.Tests/Deployment/ProviderErrorDocsConformanceTests.cs",
        "tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs",
        "tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs",
        "tests/Hexalith.Folders.IntegrationTests/MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs",
        "tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs",
        "tests/Hexalith.Folders.Mcp.Tests/SourcingTests.cs",
        "tests/Hexalith.Folders.Server.Tests/Pd10ProtectedOperationExecutorTests.cs",
        "tests/Hexalith.Folders.UI.Tests/AuditTrailPageTests.cs",
        "tests/Hexalith.Folders.UI.Tests/ConsoleErrorPresenterTests.cs",
        "tests/Hexalith.Folders.UI.Tests/FolderDetailPageTests.cs",
        "tests/Hexalith.Folders.UI.Tests/IncidentStreamPageTests.cs",
        "tests/Hexalith.Folders.UI.Tests/OperationTimelinePageTests.cs",
        "tests/fixtures/parity-contract.schema.json",
        "tests/fixtures/parity-contract.yaml",
        "tests/fixtures/previous-spine.yaml",
        "tests/tools/parity-oracle-generator/Program.cs",
        "tests/tools/run-contract-parity-ci-gates.ps1",
        "tests/tools/run-contract-spine-gates.ps1",
    }
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path)
    return parser.parse_args()


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    arguments = parse_arguments()
    root = arguments.repository_root.resolve()
    output = arguments.output or root / OUTPUT_PATH
    entries: list[tuple[str, str]] = []
    for relative_path in ARTIFACT_PATHS:
        path = root / relative_path
        if not path.is_file():
            raise FileNotFoundError(f"Candidate artifact is missing: {relative_path}")
        entries.append((relative_path, sha256(path)))

    digest_material = "".join(f"{path}\0{digest}\n" for path, digest in entries).encode("utf-8")
    candidate_digest = hashlib.sha256(digest_material).hexdigest()
    matrix_digest = dict(entries)["docs/contract/authorization-matrix.md"]
    lines = [
        "schema_version: 1",
        "candidate: 1.17-GENERATE",
        "matrix_version: 2.0.0",
        "governance_status: candidate-awaiting-a6b",
        "production_routed: false",
        "hash_algorithm: SHA-256",
        f"authorization_matrix_sha256: {matrix_digest}",
        f"candidate_set_sha256: {candidate_digest}",
        f"artifact_count: {len(entries)}",
        "artifacts:",
    ]
    for relative_path, digest in entries:
        lines.extend((f"  - path: {relative_path}", f"    sha256: {digest}"))

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
