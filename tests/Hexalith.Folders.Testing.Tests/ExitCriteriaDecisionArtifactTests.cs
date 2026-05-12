using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class ExitCriteriaDecisionArtifactTests
{
    private static readonly string[] RequiredArtifacts =
    [
        "docs/exit-criteria/c3-retention.md",
        "docs/exit-criteria/c4-input-limits.md",
        "docs/exit-criteria/s2-oidc-validation.md",
        "docs/exit-criteria/c6-transition-matrix-mapping.md"
    ];

    private static readonly string[] RequiredMetadata =
    [
        "status:",
        "decision owner:",
        "approval authority:",
        "source inputs:",
        "last reviewed:",
        "open questions:"
    ];

    private static readonly string[] RequiredSections =
    [
        "## Decision",
        "## Rationale",
        "## Verification impact",
        "## Deferred implementation"
    ];

    private static readonly string[] ApprovalStates =
    [
        "approved",
        "proposed workshop value",
        "needs human decision"
    ];

    [Fact]
    public void ExitCriteriaDecisionArtifactsExistWithRequiredDecisionShape()
    {
        string root = RepositoryRoot();

        foreach (string relativePath in RequiredArtifacts)
        {
            string path = Path.Combine(root, NormalizeForFileSystem(relativePath));
            File.Exists(path).ShouldBeTrue($"{relativePath} should exist.");

            string content = File.ReadAllText(path);
            foreach (string metadata in RequiredMetadata)
            {
                content.ShouldContain(metadata, Case.Insensitive, $"{relativePath} should expose {metadata}.");
            }

            foreach (string section in RequiredSections)
            {
                content.ShouldContain(section, Case.Sensitive, $"{relativePath} should contain {section}.");
            }
        }
    }

    [Fact]
    public void ExitCriteriaDecisionArtifactsAvoidPlaceholdersSecretsAndScopeLeakage()
    {
        string root = RepositoryRoot();

        foreach (string relativePath in RequiredArtifacts)
        {
            string content = File.ReadAllText(Path.Combine(root, NormalizeForFileSystem(relativePath)));

            content.ShouldNotContain("TBD", Case.Insensitive, $"{relativePath} should not use generic TBD placeholders.");
            content.ShouldNotContain("retain as needed", Case.Insensitive, $"{relativePath} should not use generic retention language.");
            content.ShouldNotContain("BEGIN PRIVATE KEY", Case.Insensitive, $"{relativePath} must not contain private key material.");
            content.ShouldNotContain("client_secret", Case.Insensitive, $"{relativePath} must not contain client secrets.");
            content.ShouldNotContain("password=", Case.Insensitive, $"{relativePath} must not contain credential material.");
            content.ShouldNotContain("diff --git", Case.Insensitive, $"{relativePath} must not contain diffs or file contents.");
            ProductionUrl().IsMatch(content).ShouldBeFalse($"{relativePath} should use non-production .invalid URL placeholders only.");
            RawJwt().IsMatch(content).ShouldBeFalse($"{relativePath} must not contain raw JWT examples.");
        }

        string spinePath = Path.Combine(root, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
        if (File.Exists(spinePath))
        {
            string spine = File.ReadAllText(spinePath);
            spine.ShouldContain("openapi: 3.1.0", Case.Sensitive, "Story 1.4 must not reshape the Contract Spine away from its OpenAPI 3.1 foundation; if the spine is present it must remain Story 1.6's foundation.");
            AssertNoDownstreamOperationGroups(spine);
        }

        File.Exists(Path.Combine(root, "src", "Hexalith.Folders", "Aggregates", "Folder", "FolderStateTransitions.cs"))
            .ShouldBeFalse("Story 1.4 documents the C6 mapping but must not implement FolderStateTransitions.cs.");
    }

    [Fact]
    public void C3AndC4RowsDeclareProvenanceApprovalConsumerAndReviewDate()
    {
        string root = RepositoryRoot();
        string[] relativePaths =
        [
            "docs/exit-criteria/c3-retention.md",
            "docs/exit-criteria/c4-input-limits.md"
        ];

        foreach (string relativePath in relativePaths)
        {
            string[] decisionRows = File.ReadAllLines(Path.Combine(root, NormalizeForFileSystem(relativePath)))
                .Where(IsDecisionTableRow)
                .ToArray();

            decisionRows.ShouldNotBeEmpty($"{relativePath} should include decision rows.");
            foreach (string row in decisionRows)
            {
                ApprovalStates.Any(state => row.Contains(state, StringComparison.OrdinalIgnoreCase))
                    .ShouldBeTrue($"{relativePath} row should declare approval state: {row}");
                row.ShouldContain("Architecture", Case.Insensitive, $"{relativePath} row should name provenance.");
                row.ShouldContain("2026-05-11", Case.Sensitive, $"{relativePath} row should carry review date.");
                row.ShouldContain("Story", Case.Insensitive, $"{relativePath} row should name a consuming future story/artifact.");
            }
        }
    }

    [Fact]
    public void S2OidcArtifactPinsFrozenValidationParametersWithSyntheticPlaceholders()
    {
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "exit-criteria", "s2-oidc-validation.md"));

        string[] requiredSettings =
        [
            "ClockSkew = TimeSpan.FromSeconds(30)",
            "RequireExpirationTime = true",
            "RequireSignedTokens = true",
            "ValidateIssuer = true",
            "ValidateAudience = true",
            "ValidateLifetime = true",
            "ValidateIssuerSigningKey = true",
            "AutomaticRefreshInterval = TimeSpan.FromMinutes(10)",
            "RefreshInterval = TimeSpan.FromMinutes(1)",
            "eventstore:tenant",
            "eventstore:permission"
        ];

        foreach (string setting in requiredSettings)
        {
            content.ShouldContain(setting, Case.Sensitive);
        }

        content.ShouldContain(".invalid", Case.Insensitive);
    }

    [Fact]
    public void C6MappingArtifactCarriesArchitectureVocabularyAndDefaultRejectionRule()
    {
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "exit-criteria", "c6-transition-matrix-mapping.md"));

        string[] states =
        [
            "requested",
            "preparing",
            "ready",
            "locked",
            "changes_staged",
            "dirty",
            "committed",
            "failed",
            "inaccessible",
            "unknown_provider_outcome",
            "reconciliation_required"
        ];

        string[] events =
        [
            "RepositoryBindingRequested",
            "RepositoryBound",
            "RepositoryBindingFailed",
            "ProviderOutcomeUnknown",
            "WorkspacePrepared",
            "WorkspacePreparationFailed",
            "WorkspaceLocked",
            "AuthRevocationDetected",
            "TenantRevoked",
            "RepositoryDeletedAtProvider",
            "ReconciliationRequested",
            "FileMutated",
            "WorkspaceLockReleased",
            "LockLeaseExpired",
            "CommitSucceeded",
            "CommitFailed",
            "OperatorDiscardRequested",
            "OperatorRetrySucceeded",
            "ProviderReadinessValidated",
            "ReconciliationCompletedClean",
            "ReconciliationCompletedDirty",
            "ReconciliationEscalated",
            "OperatorMarkedFailed"
        ];

        foreach (string state in states)
        {
            content.ShouldContain($"`{state}`", Case.Sensitive);
        }

        foreach (string eventName in events)
        {
            content.ShouldContain($"`{eventName}`", Case.Sensitive);
        }

        content.ShouldContain("state_transition_invalid", Case.Sensitive);
        content.ShouldContain("src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs", Case.Sensitive);
    }

    private static bool IsDecisionTableRow(string line) =>
        line.StartsWith('|')
        && !line.Contains("---", StringComparison.Ordinal)
        && !line.Contains("Data class", StringComparison.OrdinalIgnoreCase)
        && !line.Contains("Limit", StringComparison.OrdinalIgnoreCase)
        && ApprovalStates.Any(state => line.Contains(state, StringComparison.OrdinalIgnoreCase));

    private static Regex ProductionUrl() => new(
        @"https?://(?![a-z0-9.-]+\.invalid(?:[/:)\s]|$))[^\s)]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Regex RawJwt() => new(
        @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
        RegexOptions.CultureInvariant);

    private static void AssertNoDownstreamOperationGroups(string spine)
    {
        string[] forbiddenPaths =
        [
            "/api/v1/workspaces",
            "/api/v1/locks",
            "/api/v1/files",
            "/api/v1/context",
            "/api/v1/commits",
            "/api/v1/audit",
            "/api/v1/ops-console"
        ];

        foreach (string forbiddenPath in forbiddenPaths)
        {
            spine.ShouldNotContain(forbiddenPath, Case.Sensitive, $"{forbiddenPath} belongs to a downstream Contract Spine story.");
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
