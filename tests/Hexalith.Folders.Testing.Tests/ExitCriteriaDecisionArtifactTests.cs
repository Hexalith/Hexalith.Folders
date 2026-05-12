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

    private static readonly string[] RequiredMetadataKeys =
    [
        "status",
        "decision owner",
        "approval authority",
        "source inputs",
        "last reviewed",
        "open questions"
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

    private static readonly Regex[] PlaceholderRegexes =
    [
        new(@"\bTBD\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bT\.B\.D\.?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bTBA\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bXXX\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bFIXME\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bTODO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"retain as needed", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"to be determined", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"<placeholder>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"<unknown>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];

    private static readonly string[] SecretSubstringDenylist =
    [
        "BEGIN PRIVATE KEY",
        "BEGIN RSA PRIVATE KEY",
        "BEGIN EC PRIVATE KEY",
        "BEGIN OPENSSH PRIVATE KEY",
        "BEGIN CERTIFICATE",
        "client_secret",
        "clientSecret",
        "password=",
        "aws_access_key_id",
        "diff --git"
    ];

    private static readonly Regex[] SecretRegexDenylist =
    [
        new(@"AKIA[A-Z0-9]{16}", RegexOptions.CultureInvariant),
        new(@"\bsk-[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant),
        new(@"\bsk_(?:live|test)_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bghp_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bgho_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bxox[abprs]-[A-Za-z0-9-]{20,}", RegexOptions.CultureInvariant)
    ];

    // Six data classes the Story 1.4 task explicitly enumerated for C3. Workshop expansions are
    // allowed, but removing any of these requires architecture follow-up, not a silent edit.
    private static readonly string[] MandatedC3Categories =
    [
        "Audit metadata",
        "Workspace status",
        "Provider correlation",
        "Read-model views",
        "Temporary working files",
        "Cleanup records"
    ];

    private static readonly string[] C6States =
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

    private static readonly string[] C6Events =
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

    [Fact]
    public void ExitCriteriaDecisionArtifactsExistWithRequiredDecisionShape()
    {
        string root = RepositoryRoot();

        foreach (string relativePath in RequiredArtifacts)
        {
            string path = Path.Combine(root, NormalizeForFileSystem(relativePath));
            File.Exists(path).ShouldBeTrue($"{relativePath} should exist.");

            string content = File.ReadAllText(path);

            foreach (string key in RequiredMetadataKeys)
            {
                Regex.IsMatch(content, $@"(?im)^\s*{Regex.Escape(key)}\s*:")
                    .ShouldBeTrue($"{relativePath} must declare front-matter key '{key}:' at the start of a line; prose substrings do not count.");
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

            foreach (Regex placeholder in PlaceholderRegexes)
            {
                placeholder.IsMatch(content).ShouldBeFalse(
                    $"{relativePath} must not contain generic placeholder matching /{placeholder}/.");
            }

            foreach (string token in SecretSubstringDenylist)
            {
                content.ShouldNotContain(token, Case.Insensitive,
                    $"{relativePath} must not contain credential material '{token}'.");
            }

            foreach (Regex secret in SecretRegexDenylist)
            {
                secret.IsMatch(content).ShouldBeFalse(
                    $"{relativePath} must not contain credential material matching /{secret}/.");
            }

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
            string content = File.ReadAllText(Path.Combine(root, NormalizeForFileSystem(relativePath)));
            string reviewedDate = ParseFrontMatterValue(content, "last reviewed")
                ?? throw new InvalidOperationException($"{relativePath} is missing 'last reviewed' front-matter value.");

            string[] decisionRows = ExtractDecisionTableRows(content);

            decisionRows.ShouldNotBeEmpty($"{relativePath} should include decision rows.");
            foreach (string row in decisionRows)
            {
                ApprovalStates.Any(state => row.Contains(state, StringComparison.OrdinalIgnoreCase))
                    .ShouldBeTrue($"{relativePath} row must declare one of the three approval states: {row}");
                row.ShouldContain("Architecture", Case.Insensitive, $"{relativePath} row must name provenance: {row}");
                row.ShouldContain(reviewedDate, Case.Sensitive, $"{relativePath} row must carry the front-matter review date '{reviewedDate}': {row}");
                Regex.IsMatch(row, @"\bStory\s+\d+(?:\.\d+)?", RegexOptions.IgnoreCase)
                    .ShouldBeTrue($"{relativePath} row must name a consuming future story matching 'Story <N>[.<M>]': {row}");
            }
        }
    }

    [Fact]
    public void C3CoversTheSixMandatedDataClasses()
    {
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "exit-criteria", "c3-retention.md"));

        // Check table rows only — prose paragraphs must not substitute for table coverage.
        // A row removal that leaves only a prose mention would otherwise pass silently.
        string tableContent = string.Join('\n', ExtractDecisionTableRows(content));
        tableContent.ShouldNotBeEmpty("c3-retention.md must contain at least one decision table row.");

        foreach (string category in MandatedC3Categories)
        {
            tableContent.ShouldContain(category, Case.Insensitive,
                $"c3-retention.md decision table must keep the task-mandated data class '{category}'. Workshop expansions are allowed; removals are not.");
        }
    }

    [Fact]
    public void C4RowsCarryEitherANumericValueOrASemanticFlag()
    {
        string root = RepositoryRoot();
        string content = File.ReadAllText(Path.Combine(root, "docs", "exit-criteria", "c4-input-limits.md"));
        string[] rows = ExtractDecisionTableRows(content);

        rows.ShouldNotBeEmpty("c4-input-limits.md must include decision rows.");
        foreach (string row in rows)
        {
            string[] cells = row.Trim('|').Split('|');
            cells.Length.ShouldBeGreaterThanOrEqualTo(3,
                $"c4-input-limits.md row must carry Limit + Numeric value + Semantic flag cells: {row}");

            string numericCell = cells[1].Trim();
            string semanticCell = cells[2].Trim();

            bool numericHasValue = Regex.IsMatch(numericCell, @"^\d");
            bool semanticHasValue = semanticCell.Length > 0 && !IsEmDashOrHyphen(semanticCell);
            bool numericIsBlank = IsEmDashOrHyphen(numericCell);

            (numericHasValue || semanticHasValue).ShouldBeTrue(
                $"c4-input-limits.md row must declare either a numeric value or a semantic flag: {row}");

            (numericHasValue && semanticHasValue).ShouldBeFalse(
                $"c4-input-limits.md row must not populate both Numeric value and Semantic flag in the same row; the unused column must be '—': {row}");

            if (numericHasValue)
            {
                semanticCell.ShouldSatisfyAllConditions(
                    () => IsEmDashOrHyphen(semanticCell).ShouldBeTrue(
                        $"c4-input-limits.md numeric row must use '—' in the Semantic flag column: {row}"));
            }
            else
            {
                numericIsBlank.ShouldBeTrue(
                    $"c4-input-limits.md semantic-flag row must use '—' in the Numeric value column: {row}");
            }
        }
    }

    [Fact]
    public void S2OidcArtifactPinsFrozenJwtBearerSettings()
    {
        string root = RepositoryRoot();
        string path = Path.Combine(root, "docs", "exit-criteria", "s2-oidc-validation.md");
        File.Exists(path).ShouldBeTrue("docs/exit-criteria/s2-oidc-validation.md must exist.");
        string content = File.ReadAllText(path);

        string[] jwtBearerSettings =
        [
            "ClockSkew = TimeSpan.FromSeconds(30)",
            "RequireExpirationTime = true",
            "RequireSignedTokens = true",
            "ValidateIssuer = true",
            "ValidateAudience = true",
            "ValidateLifetime = true",
            "ValidateIssuerSigningKey = true",
            "AutomaticRefreshInterval = TimeSpan.FromMinutes(10)",
            "RefreshInterval = TimeSpan.FromMinutes(1)"
        ];

        foreach (string setting in jwtBearerSettings)
        {
            content.ShouldContain(setting, Case.Sensitive,
                $"s2-oidc-validation.md must pin JwtBearer setting '{setting}'.");
        }
    }

    [Fact]
    public void S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders()
    {
        string root = RepositoryRoot();
        string path = Path.Combine(root, "docs", "exit-criteria", "s2-oidc-validation.md");
        File.Exists(path).ShouldBeTrue("docs/exit-criteria/s2-oidc-validation.md must exist.");
        string content = File.ReadAllText(path);

        string[] authoritativeClaims =
        [
            "eventstore:tenant",
            "eventstore:permission"
        ];

        foreach (string claim in authoritativeClaims)
        {
            content.ShouldContain(claim, Case.Sensitive,
                $"s2-oidc-validation.md must document authoritative claim '{claim}'.");
        }

        content.ShouldContain(".invalid", Case.Insensitive,
            "s2-oidc-validation.md must only use .invalid issuer placeholders.");
    }

    [Fact]
    public void C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally()
    {
        string root = RepositoryRoot();
        string artifact = File.ReadAllText(Path.Combine(root, "docs", "exit-criteria", "c6-transition-matrix-mapping.md"));
        string architecture = File.ReadAllText(Path.Combine(root, "_bmad-output", "planning-artifacts", "architecture.md"));

        foreach (string state in C6States)
        {
            artifact.ShouldContain($"`{state}`", Case.Sensitive,
                $"c6-transition-matrix-mapping.md must mirror state '{state}'.");
            architecture.ShouldContain($"`{state}`", Case.Sensitive,
                $"architecture.md must still declare state '{state}'; vocabulary drift between architecture and the C6 mapping artifact.");
        }

        foreach (string eventName in C6Events)
        {
            artifact.ShouldContain($"`{eventName}`", Case.Sensitive,
                $"c6-transition-matrix-mapping.md must mirror event '{eventName}'.");
            architecture.ShouldContain($"`{eventName}`", Case.Sensitive,
                $"architecture.md must still declare event '{eventName}'; vocabulary drift between architecture and the C6 mapping artifact.");
        }

        artifact.ShouldContain("state_transition_invalid", Case.Sensitive,
            "c6-transition-matrix-mapping.md must document the canonical default rejection category.");
        artifact.ShouldContain("src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs", Case.Sensitive,
            "c6-transition-matrix-mapping.md must name the future implementation target.");
    }

    private static string[] ExtractDecisionTableRows(string content)
    {
        var rows = new List<string>();
        string[] lines = content.Split('\n');
        bool sawSeparatorOnCurrentTable = false;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('|'))
            {
                // Only reset on non-blank non-pipe lines (section headings, paragraphs).
                // Blank lines between a separator and the first body row must not break collection.
                if (line.Length > 0)
                    sawSeparatorOnCurrentTable = false;
                continue;
            }

            // A GFM separator row has cells composed exclusively of hyphens (3+) with optional
            // alignment colons. Single-hyphen or em-dash data cells do not satisfy this pattern.
            if (Regex.IsMatch(line, @"^\|(?:\s*:?-{3,}:?\s*\|)+\s*$"))
            {
                sawSeparatorOnCurrentTable = true;
                continue;
            }

            if (sawSeparatorOnCurrentTable)
            {
                rows.Add(line);
            }
        }

        return rows.ToArray();
    }

    private static string? ParseFrontMatterValue(string content, string key)
    {
        // Constrain the search to the header block before the first section heading so that a
        // prose line matching "key: value" anywhere in the document body cannot satisfy the check.
        int firstHeading = content.IndexOf("\n## ", StringComparison.Ordinal);
        string block = firstHeading > 0 ? content[..firstHeading] : content;
        Match m = Regex.Match(block, $@"(?im)^\s*{Regex.Escape(key)}\s*:\s*(.+?)\s*$");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static bool IsEmDashOrHyphen(string cell) => cell is "—" or "–";

    private static Regex ProductionUrl() => new(
        @"https?://(?![a-z0-9.-]+\.invalid(?:[/:)#?[\s]|$))[^\s)]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Matches three base64url segments after the literal "eyJ" header prefix. To document a
    // forbidden JWT shape inside an artifact without tripping this assertion, write the example
    // as `eyJ<redacted>.<redacted>.<redacted>` — the `<` character is outside the alphabet so
    // the regex will not match.
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
        const int MaxAncestors = 12;
        string start = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(start);
        for (int i = 0; i < MaxAncestors && directory is not null; i++)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root was not found within {MaxAncestors} ancestors starting from '{start}'. " +
            "Expected an ancestor directory containing 'Hexalith.Folders.slnx'.");
    }

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
