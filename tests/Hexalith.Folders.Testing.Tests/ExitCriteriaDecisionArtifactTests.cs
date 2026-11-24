using System.Globalization;
using System.Text.RegularExpressions;
using Hexalith.Folders.Testing.Tests.Helpers;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class ExitCriteriaDecisionArtifactTests
{
    private static readonly MarkdownPipeline _markdown = new MarkdownPipelineBuilder().UsePreciseSourceLocation().Build();
    private static readonly Regex _httpUrl = new(
        """https?://[^\s<>()`"']+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredArtifacts =
    [
        "docs/exit-criteria/c3-retention.md",
        "docs/exit-criteria/c4-input-limits.md",
        "docs/exit-criteria/s2-oidc-validation.md",
        "docs/exit-criteria/c6-transition-matrix-mapping.md",
        "docs/exit-criteria/c7-lock-authorization-timing.md"
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
        "aws_access_key_id"
    ];

    private static readonly Regex[] SecretRegexDenylist =
    [
        new(@"AKIA[A-Z0-9]{16}", RegexOptions.CultureInvariant),
        new(@"\bsk-[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant),
        new(@"\bsk_(?:live|test)_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bghp_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bgho_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
        new(@"\bgithub_pat_[A-Za-z0-9_]{30,}", RegexOptions.CultureInvariant),
        new(@"\bv[1-4]\.(?:local|public)\.[A-Za-z0-9_-]{32,}", RegexOptions.CultureInvariant),
        new(@"\b(?:Macaroon\s+|macaroon[""']?\s*[:=]\s*[""']?|L402\s+)[A-Za-z0-9+/_-]{32,}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.CultureInvariant),
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
        "LockLeaseBecameStale",
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
        string root = RepositoryArtifacts.FindRoot();

        foreach (string relativePath in RequiredArtifacts)
        {
            string path = Path.Combine(root, NormalizeForFileSystem(relativePath));
            File.Exists(path).ShouldBeTrue($"{relativePath} should exist.");

            string content = RepositoryArtifacts.ReadText(path);

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
        string root = RepositoryArtifacts.FindRoot();

        foreach (string relativePath in RequiredArtifacts)
        {
            string content = RepositoryArtifacts.ReadText(Path.Combine(root, NormalizeForFileSystem(relativePath)));

            foreach (Regex placeholder in PlaceholderRegexes)
            {
                placeholder.IsMatch(content).ShouldBeFalse(
                    $"{relativePath} must not contain generic placeholder matching /{placeholder}/.");
            }

            ContainsGitPatch(content).ShouldBeFalse($"{relativePath} must not contain Git patch content.");

            foreach (string token in SecretSubstringDenylist)
            {
                content.Contains(token, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                    $"{relativePath} must not contain credential material '{token}'.");
            }

            ContainsCredentialToken(content).ShouldBeFalse($"{relativePath} must not contain serialized credentials.");
            ContainsDisallowedUrl(content).ShouldBeFalse(
                $"{relativePath} should use synthetic .invalid URLs for examples. Microsoft Learn citations should use HTTPS Markdown links.");
        }

        string spinePath = Path.Combine(root, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
        if (File.Exists(spinePath))
        {
            string spine = RepositoryArtifacts.ReadText(spinePath);
            spine.ShouldContain("openapi: 3.1.0", Case.Sensitive, "Story 1.4 must not reshape the Contract Spine away from its OpenAPI 3.1 foundation; if the spine is present it must remain Story 1.6's foundation.");
            SpineContractAssertions.AssertNoDownstreamOperationGroups(spine);
        }

        string c6Mapping = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "exit-criteria", "c6-transition-matrix-mapping.md"));
        c6Mapping.ShouldContain(
            "FolderStateTransitions.cs",
            Case.Sensitive,
            "The C6 mapping document must keep pointing implementers to the aggregate transition matrix source.");
    }

    [Theory]
    [InlineData("github_pat_012345678901234567890123456789_000", true)]
    [InlineData("v4.local.01234567890123456789012345678900", true)]
    [InlineData("v2.public.01234567890123456789012345678900.fake", true)]
    [InlineData("Authorization: Macaroon 01234567890123456789012345678900", true)]
    [InlineData("\"macaroon\":\"01234567890123456789012345678900\"", true)]
    [InlineData("Authorization: L402 01234567890123456789012345678900:example", true)]
    [InlineData("Use github_pat_<redacted> or v4.public.<redacted>.", false)]
    [InlineData("The Macaroon authorization scheme uses caveats.", false)]
    public void CredentialScannerRecognizesSerializedTokensAndAllowsPlaceholders(string text, bool containsCredential)
        => ContainsCredentialToken(text).ShouldBe(containsCredential);

    [Fact]
    public void C3AndC4RowsDeclareProvenanceApprovalConsumerAndReviewDate()
    {
        string root = RepositoryArtifacts.FindRoot();
        string[] relativePaths =
        [
            "docs/exit-criteria/c3-retention.md",
            "docs/exit-criteria/c4-input-limits.md"
        ];

        foreach (string relativePath in relativePaths)
        {
            string content = RepositoryArtifacts.ReadText(Path.Combine(root, NormalizeForFileSystem(relativePath)));
            string[] decisionRows = ExtractDecisionTableRows(content);
            AssertDecisionReviewDates(content, relativePath, decisionRows);

            foreach (string row in decisionRows)
            {
                ApprovalStates.Any(state => row.Contains(state, StringComparison.OrdinalIgnoreCase))
                    .ShouldBeTrue($"{relativePath} row must declare one of the three approval states: {row}");
                row.ShouldContain("Architecture", Case.Insensitive, $"{relativePath} row must name provenance: {row}");
                Regex.IsMatch(row, @"\bStory\s+\d+(?:\.\d+)?", RegexOptions.IgnoreCase)
                    .ShouldBeTrue($"{relativePath} row must name a consuming future story matching 'Story <N>[.<M>]': {row}");
            }
        }
    }

    [Fact]
    public void DecisionReviewDateCheckAllowsHistoricalRowsAlongsideTheCurrentReview()
    {
        const string content = """
            last reviewed: 2026-09-17

            ## Decision
            | Class | Review date |
            | --- | --- |
            | Historical | 2026-05-11 |
            | Reviewed | 2026-09-17 |
            """;
        AssertDecisionReviewDates(content, "decision.md", ExtractDecisionTableRows(content));
    }

    [Theory]
    [InlineData("2026-09-17", "| Mentioned on 2026-09-17 | 2026-05-11 |", "current review date")]
    [InlineData("2026-02-30", "| Reviewed | 2026-02-28 |", "'last reviewed'")]
    [InlineData("2026-09-17", "| Reviewed | 2026-09-31 |", "row must end")]
    public void DecisionReviewDateCheckReportsInvalidOrMissingRowEvidence(string header, string row, string diagnostic)
    {
        string content = $"last reviewed: {header}\n\n## Decision\n| Class | Review date |\n| --- | --- |\n{row}";
        Should.Throw<ShouldAssertException>(() => AssertDecisionReviewDates(content, "decision.md", ExtractDecisionTableRows(content)))
            .Message.ShouldContain(diagnostic);
    }

    [Fact]
    public void SupplementaryTableCannotSupplyTheCurrentDecisionReviewDate()
    {
        const string content = """
            last reviewed: 2026-09-17

            ## Decision
            | Class | Review date |
            | --- | --- |
            | Temporary files | 2026-05-11 |

            ## Machine-Validated Policy Source
            | Class | Review date |
            | --- | --- |
            | Temporary files | 2026-09-17 |
            """;
        Should.Throw<ShouldAssertException>(
            () => AssertDecisionReviewDates(content, "decision.md", ExtractDecisionTableRows(content)))
            .Message.ShouldContain("current review date");
    }

    private static void AssertDecisionReviewDates(string content, string relativePath, string[] decisionRows)
    {
        HashSet<string> primaryRows = ExtractDecisionTableRows(string.Join('\n', Section(content, "## Decision")))
            .ToHashSet(StringComparer.Ordinal);
        primaryRows.ShouldNotBeEmpty($"{relativePath} should include a table with decision rows under '## Decision'.");

        string? reviewedDate = ParseFrontMatterValue(content, "last reviewed");
        DateOnly.TryParseExact(reviewedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly currentReview)
            .ShouldBeTrue($"{relativePath} must declare a valid ISO 'last reviewed' date.");

        // A partial review updates the affected rows. Historical rows keep their original dates.
        bool containsCurrentReviewDate = false;
        foreach (string row in decisionRows)
        {
            Match terminalCell = Regex.Match(row, @"\|[ \t]*(\d{4}-\d{2}-\d{2})[ \t]*\|[ \t]*$");
            DateOnly.TryParseExact(terminalCell.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly rowReview)
                .ShouldBeTrue($"{relativePath} row must end with a valid ISO review date: {row}");
            containsCurrentReviewDate |= rowReview == currentReview && primaryRows.Contains(row);
        }

        containsCurrentReviewDate.ShouldBeTrue(
            $"{relativePath} must have at least one decision row reviewed on the document's current review date '{reviewedDate}'.");
    }

    [Fact]
    public void C3CoversTheSixMandatedDataClasses()
    {
        string root = RepositoryArtifacts.FindRoot();
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "exit-criteria", "c3-retention.md"));

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
        string root = RepositoryArtifacts.FindRoot();
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "exit-criteria", "c4-input-limits.md"));
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
    public void C7ArtifactPinsApprovedProfileBoundariesOverridesAndProvenance()
    {
        string root = RepositoryArtifacts.FindRoot();
        string relativePath = "docs/exit-criteria/c7-lock-authorization-timing.md";
        string content = RepositoryArtifacts.ReadText(Path.Combine(root, NormalizeForFileSystem(relativePath)));

        Dictionary<string, string> expectedMetadata = new(StringComparer.Ordinal)
        {
            ["status"] = "approved",
            ["decision owner"] = "Architecture",
            ["approval authority"] = "Architecture + Security",
            ["decision version"] = "1.0.0",
            ["approved on"] = "2026-09-12",
            ["approved by"] = "Administrator (Architecture); Administrator (Security)",
            ["lock renewal interval seconds"] = "30",
            ["authorization revalidation interval seconds"] = "15",
            ["revocation effect SLO seconds"] = "60",
            ["expired to stale threshold seconds"] = "60",
        };

        foreach (KeyValuePair<string, string> expected in expectedMetadata)
        {
            ParseFrontMatterValue(content, expected.Key).ShouldBe(expected.Value, expected.Key);
        }

        foreach (string statement in new[]
        {
            "renewalAnchorAt = effectiveAt = acquiredAt",
            "now >= renewalAnchorAt + effectiveRenewalIntervalSeconds",
            "valid tenant override or otherwise 30 seconds",
            "now >= lastSuccessfulAuthorizationValidationAt + 15 seconds",
            "revocationEffectiveAt + 60 seconds",
            "authoritative upstream authority source's revocation-effective timestamp",
            "shared UTC clock domain",
            "local receipt or observation time is not a substitute",
            "now >= expiresAt",
            "now < expiresAt + 60 seconds",
            "now >= expiresAt + 60 seconds",
            "Positive whole seconds no greater than 30.",
            "Positive whole seconds no greater than 15.",
            "Positive whole seconds no greater than 60.",
            "caller-requested lease shorter than the effective renewal interval",
            "Under a tenant override of 10 seconds",
            "a 5-second",
            "lease expires before renewal",
            "Neither profile rounds",
            "Only the task that owns the lock may renew it",
            "Stale, unavailable,",
            "unknown, or revoked authority fails closed",
        })
        {
            content.ShouldContain(statement, Case.Sensitive);
        }

        Regex.IsMatch(
            content,
            @"Any artifact-content change,\s+timing-value\s+change, version change, or digest mismatch reopens OQ1",
            RegexOptions.CultureInvariant).ShouldBeTrue("C7 artifact drift must reopen OQ1.");
    }

    [Fact]
    public void C7GovernanceApprovalKeepsRuntimeEvidenceReferencePending()
    {
        string root = RepositoryArtifacts.FindRoot();
        string traceability = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "exit-criteria", "nfr-traceability.md"));
        string[] rows = traceability.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string nfr in new[] { "NFR7", "NFR21" })
        {
            string row = rows.Single(line => line.StartsWith($"| {nfr} |", StringComparison.Ordinal));
            row.ShouldContain("| reference-pending |", Case.Sensitive, $"{nfr} must retain its runtime evidence gap.");
            row.ShouldContain("| `C7` |", Case.Sensitive, $"{nfr} must remain linked to C7.");
            row.ShouldContain("executable evidence remains separately deferred", Case.Sensitive,
                $"{nfr} must not claim runtime coverage from governance approval.");
        }
    }

    [Fact]
    public void S2OidcArtifactPinsValidationSettingsAndAuthoritativeClaimSources()
    {
        string root = RepositoryArtifacts.FindRoot();
        string path = Path.Combine(root, "docs", "exit-criteria", "s2-oidc-validation.md");
        File.Exists(path).ShouldBeTrue("docs/exit-criteria/s2-oidc-validation.md must exist.");
        string content = RepositoryArtifacts.ReadText(path);

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

        AssertS2EndpointPlaceholders(content);
    }

    [Theory]
    [InlineData("[Docs](https://learn.microsoft.com/docs)", "https://issuer.invalid/discovery", "Issuer placeholder")]
    [InlineData("https://issuer.invalid/realm", "[Docs](https://learn.microsoft.com/docs)", "JWKS discovery placeholder")]
    public void S2EndpointExamplesRequireSyntheticUrlsEvenWhenFormattedAsLinks(string issuer, string discovery, string diagnostic)
    {
        string content = $"""
            ## Decision
            | Environment placeholder | Issuer placeholder | Audience placeholder | JWKS discovery placeholder |
            | --- | --- | --- | --- |
            | local | {issuer} | api://audience.invalid | {discovery} |
            """;
        Should.Throw<ShouldAssertException>(() => AssertS2EndpointPlaceholders(content))
            .Message.ShouldContain(diagnostic);
    }

    private static void AssertS2EndpointPlaceholders(string content)
    {
        string[] section = Section(content, "## Decision");
        foreach ((string header, int column) in new[] { ("Issuer placeholder", 1), ("JWKS discovery placeholder", 3) })
        {
            foreach (string cell in TableColumn(section, header, column))
            {
                MatchCollection urls = _httpUrl.Matches(cell);
                urls.Count.ShouldBeGreaterThan(0, $"S2 {header} must include a synthetic HTTPS URL.");
                urls.All(url => url.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && IsSyntheticUrl(url.Value))
                    .ShouldBeTrue($"S2 {header} must use a synthetic HTTPS .invalid URL.");
            }
        }
    }

    [Fact]
    public void C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally()
    {
        string root = RepositoryArtifacts.FindRoot();
        string artifact = RepositoryArtifacts.ReadText(Path.Combine(root, "docs", "exit-criteria", "c6-transition-matrix-mapping.md"));
        string architecture = RepositoryArtifacts.ReadText(Path.Combine(root, "_bmad-output", "planning-artifacts", "architecture.md"));

        AssertC6Vocabulary(architecture, artifact, C6States, C6Events);

        artifact.ShouldContain("state_transition_invalid", Case.Sensitive,
            "c6-transition-matrix-mapping.md must document the canonical default rejection category.");
        artifact.ShouldContain("src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs", Case.Sensitive,
            "c6-transition-matrix-mapping.md must name the future implementation target.");
    }

    [Theory]
    [InlineData("NewEvent", "ready", "ReservedEvent", "active events")]
    [InlineData("ActiveEvent", "changed", "ReservedEvent", "state catalogs")]
    [InlineData("ActiveEvent", "ready", "OtherReserved", "reserved events")]
    public void C6VocabularyDetectsDriftInDeclarationsDespiteProseMentions(
        string mappedEvent, string mappedState, string mappedReserved, string diagnostic)
    {
        const string architecture = """
            ### Workspace State Transition Matrix (C6 — Enumerated)
            | State     | Description |
            | --- | --- |
            | ready | state |

            | From → To    | Triggering Event    | Side Effect |
            | --- | --- | --- |
            | ready → ready | ActiveEvent / `SecondEvent` (clean/dirty) | none |

            The reserved post-MVP events `ReservedEvent` are excluded.
            """;
        string artifact = $"""
            ### State Catalog
            | State | Notes |
            | --- | --- |
            | {mappedState} | ready remains in this incidental mention. |

            ### Event Vocabulary
            The active MVP architecture event vocabulary copied for drift checking is:

            `{mappedEvent}`, `SecondEvent`.
            Reserved post-MVP names `{mappedReserved}` are excluded.
            Prose mentions `ActiveEvent` and `ReservedEvent`.
            """;

        Should.Throw<ShouldAssertException>(() => AssertC6Vocabulary(architecture, artifact, ["ready"], ["ActiveEvent"]))
            .Message.ShouldContain(diagnostic);
    }

    [Theory]
    [InlineData("OneEvent / `OtherEvent` (clean/dirty)")]
    [InlineData("`OneEvent`/OtherEvent by the owner/operator")]
    public void C6TriggerColumnSeparatesNamesFromGuardDescriptions(string cell)
        => TriggerNames(cell).ShouldBe(["OneEvent", "OtherEvent"]);

    private static void AssertC6Vocabulary(string architecture, string artifact, string[] requiredStates, string[] requiredEvents)
    {
        string[] matrix = Section(architecture, "### Workspace State Transition Matrix (C6 — Enumerated)");
        string[] catalog = Section(artifact, "### State Catalog");
        string[] vocabulary = Section(artifact, "### Event Vocabulary");

        string[] architectureStates = TableColumn(matrix, "State", 0).Select(state => state.Trim('`')).ToArray();
        string[] mappedStates = TableColumn(catalog, "State", 0).Select(state => state.Trim('`')).ToArray();
        SortedNames(mappedStates).ShouldBe(SortedNames(architectureStates), "C6 state catalogs must agree.");
        requiredStates.Except(architectureStates, StringComparer.Ordinal).ShouldBeEmpty("Required C6 states are missing.");

        IEnumerable<string> triggers = TableColumn(matrix, "Triggering Event", 1);
        string[] active = triggers.SelectMany(TriggerNames).Distinct(StringComparer.Ordinal).ToArray();
        string[] declaredActive = Declaration(vocabulary, "The active MVP architecture event vocabulary copied for drift checking is:", ".");
        SortedNames(declaredActive).ShouldBe(SortedNames(active), "C6 active events must match the positive-edge table.");

        string[] reserved = Declaration(matrix, "The reserved post-MVP events", "are ");
        string[] declaredReserved = Declaration(vocabulary, "Reserved post-MVP names", "are ");
        SortedNames(declaredReserved).ShouldBe(SortedNames(reserved), "C6 reserved events must match their declaration.");
        active.Intersect(reserved, StringComparer.Ordinal).ShouldBeEmpty("Reserved events cannot be positive edges.");
        requiredEvents.Except(active.Concat(reserved), StringComparer.Ordinal).ShouldBeEmpty("Required C6 events are missing.");
    }

    private static string[] Section(string markdown, string heading)
    {
        string[] lines = markdown.ReplaceLineEndings("\n").Split('\n');
        int start = Array.IndexOf(lines, heading);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing section '{heading}'.");
        return lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^#{1,3} ")).ToArray();
    }

    private static IEnumerable<string> TableColumn(string[] section, string header, int column)
    {
        int start = Array.FindIndex(section, line => line.StartsWith('|')
            && line.Trim('|').Split('|', StringSplitOptions.TrimEntries).ElementAtOrDefault(column) == header);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing table '{header}'.");
        string table = string.Join('\n', section.Skip(start).TakeWhile(line => line.StartsWith('|')));
        string[] rows = ExtractDecisionTableRows(table);
        rows.ShouldNotBeEmpty($"Expected rows under '{header}'.");
        return rows.Select(row => row.Trim('|').Split('|')[column].Trim());
    }

    private static IEnumerable<string> TriggerNames(string cell)
    {
        // The triggering names precede the optional guard description in this table cell.
        const string Trigger = @"`?(?<name>[A-Z][A-Za-z0-9]*)`?";
        Match list = Regex.Match(cell, $@"^{Trigger}(?:\s*/\s*{Trigger})*(?:$|\s+(?![\s/]))");
        list.Success.ShouldBeTrue($"Expected a C6 event name in '{cell}'.");
        return list.Groups["name"].Captures.Select(capture => capture.Value);
    }

    private static string[] Declaration(string[] section, string introduction, string terminator)
    {
        int start = Array.FindIndex(section, line => line.StartsWith(introduction, StringComparison.Ordinal));
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing event declaration '{introduction}'.");
        string declaration = string.Join(' ', section.Skip(start))[introduction.Length..].TrimStart();
        int end = declaration.IndexOf(terminator, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(0, $"Incomplete event declaration '{introduction}'.");
        return declaration[..end].Split(',', StringSplitOptions.TrimEntries)
            .Select(item =>
            {
                string name = (item.StartsWith("and ", StringComparison.Ordinal) ? item[4..] : item).Trim().Trim('`');
                Regex.IsMatch(name, @"\A[A-Z][A-Za-z0-9]*\z").ShouldBeTrue($"Invalid event in '{introduction}'.");
                return name;
            }).ToArray();
    }

    private static string[] SortedNames(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToArray();

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

    [Theory]
    [InlineData("A patch starts with `diff --git`.", false)]
    [InlineData("The example uses diff --git a/example b/example.", false)]
    [InlineData("## Example\r\n```diff\r\ndiff --git a/old file b/new file\r\n```", true)]
    [InlineData("    diff --git \"a/old file\" \"b/new file\"", true)]
    [InlineData("diff --git old.txt new.txt\nindex 1111111..2222222 100644\n--- old.txt\n+++ new.txt\n@@ -1 +1 @@\n-old\n+new", true)]
    [InlineData("diff --git describes the patch format.\nThis paragraph explains it.", false)]
    public void DecisionDocumentPatchCheckRecognizesGitHeaders(string text, bool expected)
        => ContainsGitPatch(text).ShouldBe(expected);

    private static bool ContainsGitPatch(string text)
    {
        // Git can omit or customize path prefixes. In that form the next metadata line
        // distinguishes the patch from prose that happens to start with the command.
        const string header = @"(?m)^[ \t]*diff --git[ \t]+";
        const string standardPaths = """["]?a/[^\r\n]+[ \t]+"?b/[^\r\n]+""";
        const string pathsAndMetadata = @"[^\r\n]+[ \t]+[^\r\n]+\r?\n[ \t]*(?:index |--- |old mode |new file mode |deleted file mode |similarity index |dissimilarity index |Binary files |GIT binary patch)";
        return Regex.IsMatch(text, header + "(?:" + standardPaths + "|" + pathsAndMetadata + ")", RegexOptions.CultureInvariant);
    }

    [Theory]
    [InlineData("See [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/).", false)]
    [InlineData("See [Microsoft Learn](<https://learn.microsoft.com/docs> \"Documentation\").", false)]
    [InlineData("See [Microsoft Learn][reference].\n\n[reference]: https://learn.microsoft.com/docs", false)]
    [InlineData("Issuer: `https://oidc.local.invalid:8443/realm`.", false)]
    [InlineData("Issuer: `https://issuer.example/realm`.", true)]
    [InlineData("Issuer: `https://learn.microsoft.com/en-us/aspnet/core/`.", true)]
    [InlineData("[Docs](http://learn.microsoft.com/en-us/aspnet/core/)", true)]
    [InlineData("[Docs](https://learn.microsoft.com.example/docs)", true)]
    [InlineData("[Docs](https://learn.microsoft.com:8443/docs)", true)]
    [InlineData("[Docs](https://placeholder@learn.microsoft.com/docs)", true)]
    [InlineData("[Docs](https://learn.microsoft.com/docs) or https://learn.microsoft.com/docs.", true)]
    [InlineData("![Image](https://learn.microsoft.com/docs)", true)]
    [InlineData("Issuer: `[Docs](https://learn.microsoft.com/docs)`", true)]
    [InlineData("Issuer: `\n[Docs](https://learn.microsoft.com/docs)`", true)]
    [InlineData("```md\n[Docs](https://learn.microsoft.com/docs)\n```", true)]
    [InlineData("~~~md\n[Docs](https://learn.microsoft.com/docs)\n~~~", true)]
    [InlineData("> ```md\n> [Docs](https://learn.microsoft.com/docs)\n> ```", true)]
    [InlineData("- Example\n\n  ```md\n  [Docs](https://learn.microsoft.com/docs)\n  ```", true)]
    public void DecisionDocumentUrlCheckAllowsOnlySyntheticExamplesAndApprovedCitations(string text, bool expected)
    {
        ArgumentNullException.ThrowIfNull(text);
        ContainsDisallowedUrl(text).ShouldBe(expected);
    }

    private static bool ContainsDisallowedUrl(string content)
    {
        // Use source offsets so the same URL elsewhere in the document is still checked.
        HashSet<int> citationOffsets = Markdown.Parse(content, _markdown).Descendants<LinkInline>()
            .Where(link => !link.IsImage && IsMicrosoftLearnCitation(link.Url))
            .Select(link => link.UrlSpan.Start)
            .Where(start => start >= 0 && start < content.Length)
            .Select(start => content[start] == '<' ? start + 1 : start)
            .ToHashSet();
        return _httpUrl.Matches(content).Any(url => !citationOffsets.Contains(url.Index) && !IsSyntheticUrl(url.Value));
    }

    private static bool IsMicrosoftLearnCitation(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.Host.Equals("learn.microsoft.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsSyntheticUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && uri.UserInfo.Length == 0
        && uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCredentialToken(string content) =>
        SecretRegexDenylist.Any(pattern => pattern.IsMatch(content));

    private static string NormalizeForFileSystem(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
