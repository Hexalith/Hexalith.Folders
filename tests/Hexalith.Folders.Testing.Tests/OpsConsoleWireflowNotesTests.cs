using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

/// <summary>
/// Content/structure validation gate for the Story 6.5 deliverable
/// <c>docs/ux/ops-console-wireflows.md</c>. Story 6.5 is a documentation-authoring story: it ships
/// no compiled artifact, so its acceptance criteria are content/structure checks against the
/// authored Markdown. These tests are the executable form of the AC-7 gate self-review and protect
/// the reviewed contract that blocks Stories 6.6–6.10 from drifting. They never require build
/// output, sidecars, credentials, or network access — only the repository tree.
/// </summary>
public sealed class OpsConsoleWireflowNotesTests
{
    private const string DocRelativePath = "docs/ux/ops-console-wireflows.md";

    // AC #1 — the required document skeleton.
    private static readonly string[] RequiredTopLevelSections =
    [
        "## Ownership Metadata",
        "## Scope and Boundary",
        "## Downstream Gate",
        "## References"
    ];

    // AC #1 — Ownership-Metadata house convention (docs/adrs/0000-template.md + exit-criteria/_template.md).
    private static readonly string[] RequiredOwnershipMetadataKeys =
    [
        "owner_workstream",
        "future_test_use",
        "known_omissions",
        "mutation_rules",
        "non_policy_placeholder",
        "synthetic_data_only"
    ];

    // AC #1 — the four review sources the AC-7 gate reviews against (paths must be cited verbatim).
    private static readonly string[] FourReviewSourcePaths =
    [
        "_bmad-output/planning-artifacts/prd.md",
        "_bmad-output/planning-artifacts/architecture.md",
        "_bmad-output/planning-artifacts/ux-design-specification.md",
        "_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md"
    ];

    // AC #1 / Downstream Gate — the five blocked stories.
    private static readonly string[] BlockedDownstreamStories =
    [
        "6.6",
        "6.7",
        "6.8",
        "6.9",
        "6.10"
    ];

    // AC #3 — the nine required per-view state-sets and their owning-story annotation fragment.
    private static readonly (string Heading, string OwningStory)[] NineStateSets =
    [
        ("### 3.1 Folder view", "6.6"),
        ("### 3.2 Workspace view", "6.6"),
        ("### 3.3 Provider view", "6.7"),
        ("### 3.4 Audit view", "6.8"),
        ("### 3.5 Incident-mode view", "6.9"),
        ("### 3.6 Redaction state", "6.4"),
        ("### 3.7 Loading state", "6.10"),
        ("### 3.8 Empty state", "all pages"),
        ("### 3.9 Error state", "all pages")
    ];

    // AC #4 — the twelve epic taxonomy terms (epics.md line 1380) the §2 table must define.
    private static readonly string[] TwelveEpicTaxonomyTerms =
    [
        "readiness",
        "locked",
        "prepared",
        "dirty",
        "committed",
        "audited",
        "failed",
        "stale",
        "unavailable",
        "inaccessible",
        "redacted",
        "unknown"
    ];

    // AC #4 — the five operator dispositions (C6 disposition column / DispositionLabelMapper).
    private static readonly string[] FiveOperatorDispositions =
    [
        "auto-recovering",
        "available",
        "degraded-but-serving",
        "awaiting-human",
        "terminal-until-intervention"
    ];

    // AC #4 — the eleven C6 technical lifecycle states.
    private static readonly string[] ElevenC6States =
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

    // AC #4 — the four FieldDisclosure members (Story 6.4).
    private static readonly string[] FourFieldDisclosureMembers =
    [
        "Visible",
        "Redacted",
        "Unknown",
        "Missing"
    ];

    // AC #5 — the explicit accessibility-cluster expectations (§4.1).
    private static readonly string[] AccessibilityClusterMarkers =
    [
        "Keyboard navigation",
        "focus restore",
        "Non-color-only",
        "125%",
        "150%",
        "200%",
        "Responsive fallback",
        "Redaction-vs-missing-vs-unknown"
    ];

    // AC #7 — the five trust questions every journey must answer.
    private static readonly string[] FiveTrustQuestions =
    [
        "**What happened**",
        "**Who/what caused it**",
        "**When**",
        "**From which surface**",
        "**Evidence trusted?**"
    ];

    // AC #8 — the reference-pending inputs the pages must not invent.
    private static readonly string[] PendingDeferredInputs =
    [
        "C2 status-freshness",
        "C3 retention",
        "C4 metadata-filter",
        "ProjectionAvailability",
        "French localization"
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

    [Fact]
    public void Ac1_DeliverableExistsWithRequiredSkeleton()
    {
        string content = ReadDoc();

        Regex.IsMatch(content, @"(?m)^# Operations Console Diagnostic Wireflow Notes\s*$")
            .ShouldBeTrue("The deliverable must open with the canonical H1 title '# Operations Console Diagnostic Wireflow Notes'.");

        foreach (string section in RequiredTopLevelSections)
        {
            content.ShouldContain(section, Case.Sensitive, $"The deliverable must contain the '{section}' section.");
        }

        string ownership = Section(content, "## Ownership Metadata", "## Scope and Boundary");
        foreach (string key in RequiredOwnershipMetadataKeys)
        {
            Regex.IsMatch(ownership, $@"(?im)^\s*-?\s*{Regex.Escape(key)}\s*:")
                .ShouldBeTrue($"Ownership Metadata must declare the house-convention key '{key}:'.");
        }

        Regex.IsMatch(ownership, @"(?im)^\s*-?\s*synthetic_data_only\s*:\s*true\b")
            .ShouldBeTrue("Ownership Metadata must set 'synthetic_data_only: true' — the doc contains only synthetic example data.");
    }

    [Fact]
    public void Ac1And9_ScopeAndBoundaryDeclaresReadOnlyProjectionBackedMetadataOnlyMvp()
    {
        string scope = Section(ReadDoc(), "## Scope and Boundary", "## Downstream Gate");

        scope.ShouldContain("Read-only", Case.Insensitive, "Scope and Boundary must declare the console read-only.");
        scope.ShouldContain("Projection-backed", Case.Insensitive, "Scope and Boundary must declare the console projection-backed.");
        scope.ShouldContain("Metadata-only", Case.Insensitive, "Scope and Boundary must declare the console metadata-only.");
        scope.ShouldContain("MVP", Case.Sensitive, "Scope and Boundary must declare the MVP boundary.");
        scope.ShouldContain("reviewed contract", Case.Insensitive, "Scope and Boundary must state the notes are a reviewed contract for Stories 6.6–6.10.");
    }

    [Fact]
    public void Ac1_DownstreamGateBlocksStories66Through610UntilReviewedAgainstFourSources()
    {
        string gate = Section(ReadDoc(), "## Downstream Gate", "## 1.");

        gate.ShouldContain(DocRelativePath, Case.Sensitive, "The gate must name the deliverable path that must exist before downstream work starts.");

        foreach (string story in BlockedDownstreamStories)
        {
            gate.ShouldContain(story, Case.Sensitive, $"The Downstream Gate must name blocked Story {story}.");
        }

        gate.ShouldContain("reviewed", Case.Insensitive, "The gate must state the doc has to be reviewed against the four sources before downstream stories begin.");
    }

    [Fact]
    public void Ac1_ReferencesCiteTheFourReviewSourcesWithPaths()
    {
        string references = Section(ReadDoc(), "## References", endHeading: null);

        foreach (string path in FourReviewSourcePaths)
        {
            references.ShouldContain(path, Case.Sensitive, $"References must cite the review source '{path}'.");
        }
    }

    [Fact]
    public void Ac2_FrontComposerHostingModelIsDocumented()
    {
        string hosting = Section(ReadDoc(), "## 1.", "## 2.");

        // Shell layout — as-built term, never the looser "Blazor Server" shorthand.
        hosting.ShouldContain("FrontComposerShell", Case.Sensitive, "§1 must name FrontComposerShell as the sole layout.");
        hosting.ShouldContain("Interactive Server", Case.Sensitive, "§1 must use the as-built 'Blazor Web App + Interactive Server' rendering term.");

        // Navigation — the as-built D2 route convention confirmed in the submodule.
        hosting.ShouldContain("BuildRoute", Case.Sensitive, "§1 must cite the as-built FrontComposerNavigation.BuildRoute route convention.");

        // Projection-view composition + the real attribute surface.
        hosting.ShouldContain("[ProjectionTemplate]", Case.Sensitive, "§1 must document the real [ProjectionTemplate] override attribute.");

        // Data path — IQueryService over the SDK returning QueryResult<T> with exactly these members.
        hosting.ShouldContain("IQueryService", Case.Sensitive, "§1 must describe the IQueryService data-path adapter.");
        hosting.ShouldContain("QueryResult", Case.Sensitive, "§1 must describe the QueryResult<T> read contract.");
        foreach (string member in new[] { "Items", "TotalCount", "ETag", "IsNotModified" })
        {
            hosting.ShouldContain(member, Case.Sensitive, $"§1 must name the real QueryResult<T> member '{member}'.");
        }

        hosting.ShouldContain("AddHexalithEventStore", Case.Sensitive, "§1 must record that AddHexalithEventStore is deferred in MVP.");

        // Tenant/user context fail-closed bridge.
        hosting.ShouldContain("NullUserContextAccessor", Case.Sensitive, "§1 must describe the fail-closed default user-context accessor.");
        hosting.ShouldContain("FoldersUserContextAccessor", Case.Sensitive, "§1 must describe replacing it with FoldersUserContextAccessor.");
        hosting.ShouldContain("Services.Replace", Case.Sensitive, "§1 must document the Services.Replace registration swap.");

        // Read-only command suppression.
        hosting.ShouldContain("[Command]", Case.Sensitive, "§1 must describe read-only command suppression via not defining [Command] projections.");
    }

    [Fact]
    public void Ac3_NinePerViewStateSetsPresentEachMappedToItsOwningStory()
    {
        string content = ReadDoc();
        string perView = Section(content, "## 3.", "## 4.");

        foreach ((string heading, string owningStory) in NineStateSets)
        {
            perView.ShouldContain(heading, Case.Sensitive, $"§3 must contain the per-view subsection '{heading}'.");

            string headingLine = HeadingLine(content, heading);
            headingLine.ShouldContain(owningStory, Case.Insensitive,
                $"The '{heading}' subsection must name its owning downstream story ('{owningStory}'): {headingLine}");
        }
    }

    [Fact]
    public void Ac4_TaxonomyDefinesAllTwelveEpicTerms()
    {
        string taxonomy = Section(ReadDoc(), "## 2.", "## 3.");

        foreach (string term in TwelveEpicTaxonomyTerms)
        {
            taxonomy.ShouldContain(term, Case.Insensitive,
                $"The §2 shared status taxonomy must define the epic term '{term}'.");
        }
    }

    [Fact]
    public void Ac4_TaxonomyReconcilesTheFourSourceVocabularies()
    {
        string taxonomy = Section(ReadDoc(), "## 2.", "## 3.");

        foreach (string disposition in FiveOperatorDispositions)
        {
            taxonomy.ShouldContain($"`{disposition}`", Case.Sensitive,
                $"§2 must reconcile the operator disposition '{disposition}'.");
        }

        foreach (string state in ElevenC6States)
        {
            taxonomy.ShouldContain($"`{state}`", Case.Sensitive,
                $"§2 must reconcile the C6 technical state '{state}'.");
        }

        foreach (string member in FourFieldDisclosureMembers)
        {
            taxonomy.ShouldContain($"`{member}`", Case.Sensitive,
                $"§2 must reconcile the FieldDisclosure member '{member}'.");
        }

        // ready -> available is conditional on projection-lag evidence; must not be documented as unconditional.
        taxonomy.ShouldContain("hasProjectionLagEvidence", Case.Sensitive,
            "§2 must document that `ready` maps to `available` only when no projection-lag evidence is present.");
    }

    [Fact]
    public void Ac4_TaxonomyCallsOutTheDeliberateDistinctions()
    {
        string taxonomy = Section(ReadDoc(), "## 2.", "## 3.");

        taxonomy.ShouldContain("`redacted` ≠ `unknown` ≠ `missing`", Case.Sensitive,
            "§2 must call out that redacted, unknown, and missing are distinct.");
        taxonomy.ShouldContain("`denied` ≠ `inaccessible`", Case.Sensitive,
            "§2 must call out the safe-denial distinction between denied and inaccessible.");
        taxonomy.ShouldContain("`stale`/`delayed`", Case.Sensitive,
            "§2 must call out that stale/delayed (freshness) differs from unavailable (read-model down).");
    }

    [Fact]
    public void Ac5_ConsoleExpectationsCoverUxDr1ThroughUxDr30()
    {
        string expectations = Section(ReadDoc(), "## 4.", "## 5.");
        int[] ids = TableRowIds(expectations);

        for (int n = 1; n <= 30; n++)
        {
            ids.ShouldContain(n, $"§4 must record a console implementation expectation row for UX-DR{n}.");
        }

        ids.Length.ShouldBe(30, "§4 must record exactly UX-DR1..UX-DR30 (30 rows); IDs preserved verbatim, none renumbered.");
        ids.ShouldNotContain(31, "UX-DR31 is release-verified (Story 6.11 + Workstream 7), not a §4 console-implementation row.");
        ids.ShouldNotContain(32, "UX-DR32 is release-verified (Story 6.11 + Workstream 7), not a §4 console-implementation row.");
    }

    [Fact]
    public void Ac5_AccessibilityClusterIsExplicitlyCovered()
    {
        string expectations = Section(ReadDoc(), "## 4.", "## 5.");

        foreach (string marker in AccessibilityClusterMarkers)
        {
            expectations.ShouldContain(marker, Case.Insensitive,
                $"The §4 accessibility cluster must explicitly cover '{marker}'.");
        }
    }

    [Fact]
    public void Ac6_TraceabilityMapCoversUxDr1ThroughUxDr32ExactlyOnce()
    {
        string trace = Section(ReadDoc(), "## 5.", "## 6.");
        int[] ids = TableRowIds(trace);

        ids.Length.ShouldBe(32, "§5 traceability table must have exactly 32 rows (UX-DR1..UX-DR32).");
        ids.Distinct().Count().ShouldBe(32, "§5 traceability rows must be unique — every ID appears exactly once.");

        for (int n = 1; n <= 32; n++)
        {
            ids.ShouldContain(n, $"§5 traceability map must include a row for UX-DR{n}.");
        }

        // UX-DR31/UX-DR32 are validated (not built) by 6.2–6.10.
        trace.ShouldContain("release-verified", Case.Insensitive,
            "§5 must flag UX-DR31/UX-DR32 as release-verified via Story 6.11 + Workstream 7.");
    }

    [Fact]
    public void Ac6_CrossSurfaceRowsNameUpstreamOwnerAndReleaseVerifiedRowsAreFlagged()
    {
        string trace = Section(ReadDoc(), "## 5.", "## 6.");
        string[][] rows = DataRows(trace);

        foreach (string[] cells in rows)
        {
            cells.Length.ShouldBe(5,
                $"Each §5 traceability row must carry ID + owning + supporting + scope-flag + upstream-owner cells: {string.Join(" | ", cells)}");

            string scopeFlag = cells[3].Trim();
            string upstreamOwner = cells[4].Trim();

            if (scopeFlag.Contains("cross-surface", StringComparison.OrdinalIgnoreCase))
            {
                (upstreamOwner.Length > 0 && upstreamOwner is not ("—" or "–"))
                    .ShouldBeTrue($"Cross-surface row {cells[0].Trim()} must name an upstream semantic owner; the column must not be blank: {string.Join(" | ", cells)}");
            }
        }

        foreach (string id in new[] { "UX-DR31", "UX-DR32" })
        {
            string[] row = rows.Single(r => r[0].Trim() == id);
            row[1].ShouldContain("6.11", Case.Sensitive, $"{id} must be owned by Story 6.11 (release-verified).");
            row[1].ShouldContain("Workstream 7", Case.Sensitive, $"{id} must be release-evidenced through Workstream 7.");
            row[3].ShouldContain("release-verified", Case.Insensitive, $"{id} must carry the release-verified scope flag (not implemented by 6.2–6.10).");
        }
    }

    [Fact]
    public void Ac7_ThreeDiagnosticJourneysEachAnswerTheFiveTrustQuestions()
    {
        string content = ReadDoc();
        string journeys = Section(content, "## 6.", "## 7.");

        CountOccurrences(journeys, "```mermaid").ShouldBe(3,
            "§6 must document the three critical journeys as three Mermaid flowcharts.");

        journeys.ShouldContain("three critical journeys", Case.Insensitive,
            "§6 must name the journeys as UX-DR32's 'three critical journeys'.");

        (string Heading, string EndHeading)[] journeyBounds =
        [
            ("### Journey 1", "### Journey 2"),
            ("### Journey 2", "### Journey 3"),
            ("### Journey 3", "## 7.")
        ];

        foreach ((string heading, string endHeading) in journeyBounds)
        {
            string journey = Section(content, heading, endHeading);
            journey.ShouldNotBeEmpty($"§6 must contain the '{heading}' subsection.");

            foreach (string question in FiveTrustQuestions)
            {
                journey.ShouldContain(question, Case.Sensitive,
                    $"'{heading}' must answer the trust question {question}.");
            }
        }
    }

    [Fact]
    public void Ac8_PendingAndDeferredInputsAreEnumeratedAsNotResolvedHere()
    {
        string pending = Section(ReadDoc(), "## 7.", "## 8.");

        foreach (string input in PendingDeferredInputs)
        {
            pending.ShouldContain(input, Case.Insensitive,
                $"§7 must list '{input}' as a reference-pending input the downstream pages must not invent.");
        }
    }

    [Fact]
    public void Ac9And10_DocumentIsMetadataOnlyWithNoSecretsOrRealData()
    {
        string content = ReadDoc();

        foreach (string token in SecretSubstringDenylist)
        {
            content.ShouldNotContain(token, Case.Insensitive,
                $"The wireflow notes must not contain credential material '{token}' (metadata-only, synthetic data only).");
        }

        foreach (Regex secret in SecretRegexDenylist)
        {
            secret.IsMatch(content).ShouldBeFalse(
                $"The wireflow notes must not contain credential material matching /{secret}/.");
        }

        Regex.IsMatch(content, @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+")
            .ShouldBeFalse("The wireflow notes must not contain raw JWT examples.");

        // The doc must positively assert the read-only / no-credential-reveal boundary it prescribes.
        content.ShouldContain("read-only", Case.Insensitive, "The doc must prescribe the read-only boundary (UX-DR11).");
        content.ShouldContain("credential", Case.Insensitive, "The doc must address that credential values are never revealed.");
    }

    [Fact]
    public void Ac10_MarkdownFencesAreBalancedWithExactlyThreeMermaidBlocks()
    {
        string content = ReadDoc();

        (CountOccurrences(content, "```") % 2).ShouldBe(0,
            "All Markdown code fences must be balanced (an even number of ``` markers).");

        CountOccurrences(content, "```mermaid").ShouldBe(3,
            "The doc must contain exactly three Mermaid flowcharts (the three diagnostic journeys).");
    }

    private static string ReadDoc()
    {
        string path = Path.Combine(RepositoryRoot(), NormalizeForFileSystem(DocRelativePath));
        File.Exists(path).ShouldBeTrue($"{DocRelativePath} must exist — it is the single Story 6.5 deliverable and the gate for Stories 6.6–6.10.");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the slice of <paramref name="content"/> between <paramref name="startHeading"/>
    /// (exclusive) and <paramref name="endHeading"/> (exclusive). Heading anchors such as "## 4."
    /// are matched after a newline so that deeper subsections (### 4.1) do not satisfy a top-level
    /// anchor. A null <paramref name="endHeading"/> returns the remainder of the document.
    /// </summary>
    private static string Section(string content, string startHeading, string? endHeading)
    {
        string anchored = "\n" + content;
        int start = anchored.IndexOf("\n" + startHeading, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"The document must contain the heading '{startHeading}'.");
        int from = start + 1 + startHeading.Length;

        if (endHeading is null)
        {
            return anchored[from..];
        }

        int end = anchored.IndexOf("\n" + endHeading, from, StringComparison.Ordinal);
        return end < 0 ? anchored[from..] : anchored[from..end];
    }

    private static string HeadingLine(string content, string headingFragment)
    {
        int index = content.IndexOf(headingFragment, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"The document must contain the heading '{headingFragment}'.");
        int lineEnd = content.IndexOf('\n', index);
        return lineEnd < 0 ? content[index..] : content[index..lineEnd];
    }

    /// <summary>
    /// Returns the ascending, distinct-by-position UX-DR numbers that appear as the first cell of a
    /// GFM table row in <paramref name="section"/>. A row such as <c>| UX-DR7 | ... |</c> contributes
    /// 7. Header and separator rows are ignored. Duplicates are preserved so callers can assert
    /// "exactly once".
    /// </summary>
    private static int[] TableRowIds(string section)
    {
        var ids = new List<int>();
        foreach (string raw in section.Split('\n'))
        {
            string line = raw.TrimEnd('\r').TrimStart();
            if (!line.StartsWith('|'))
            {
                continue;
            }

            string firstCell = line.Trim('|').Split('|')[0]
                .Replace("*", string.Empty)
                .Replace("`", string.Empty)
                .Trim();

            Match m = Regex.Match(firstCell, @"^UX-DR(\d+)$");
            if (m.Success)
            {
                ids.Add(int.Parse(m.Groups[1].Value));
            }
        }

        return [.. ids];
    }

    /// <summary>
    /// Returns the GFM table rows in <paramref name="section"/> whose first cell is a UX-DR id, each
    /// split into its pipe-delimited cells (outer pipes trimmed). Header/separator/non-UX-DR rows are
    /// skipped. Cell values never contain a literal pipe, so a naive split is safe here.
    /// </summary>
    private static string[][] DataRows(string section)
    {
        var rows = new List<string[]>();
        foreach (string raw in section.Split('\n'))
        {
            string line = raw.TrimEnd('\r').TrimStart();
            if (!line.StartsWith('|'))
            {
                continue;
            }

            string[] cells = line.Trim().Trim('|').Split('|');
            string firstCell = cells[0].Replace("*", string.Empty).Replace("`", string.Empty).Trim();
            if (Regex.IsMatch(firstCell, @"^UX-DR\d+$"))
            {
                rows.Add(cells);
            }
        }

        return [.. rows];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
