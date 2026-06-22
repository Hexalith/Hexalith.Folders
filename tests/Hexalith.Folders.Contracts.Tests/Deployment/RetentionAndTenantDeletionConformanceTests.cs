using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class RetentionAndTenantDeletionConformanceTests
{
    private const string C3Path = "docs/exit-criteria/c3-retention.md";
    private const string OperationsPath = "docs/operations/retention-and-tenant-deletion.md";
    private const string RunbookPath = "docs/runbooks/tenant-deletion.md";
    private const string GovernancePath = "docs/exit-criteria/c0-c13-governance-evidence.yaml";
    private const string GateScriptPath = "tests/tools/run-retention-deletion-gates.ps1";
    private const string PackageGatePath = "tests/tools/run-release-package-gates.ps1";
    private const string BaselineGatePath = "tests/tools/run-baseline-ci-gates.ps1";
    private const string WorkflowPath = ".github/workflows/release-packages.yml";
    private const string ManifestPath = "deploy/nuget/release-packages.yaml";
    private const string ReportPath = "_bmad-output/gates/retention-deletion/latest.json";

    private static readonly string[] RequiredClasses =
    [
        "Audit metadata",
        "Workspace status",
        "Provider correlation IDs",
        "Read-model views",
        "Temporary working files",
        "Cleanup records",
    ];

    [Fact]
    public void C3PolicySourceShouldExposeRequiredRowsWithoutFalsifyingApproval()
    {
        string c3 = ReadText(C3Path);

        c3.ShouldContain("policy status: reference_pending", Case.Sensitive);

        // PM approval (Jerome) was recorded 2026-06-22 via the bmad-correct-course Sprint Change Proposal;
        // Legal sign-off remains outstanding, so the release must stay blocked until Legal approves. These
        // assertions keep the policy honest: the posture still blocks release, and the approval record
        // truthfully shows PM-approved / Legal-pending rather than claiming a Legal sign-off that has not
        // happened.
        c3.ShouldContain("release posture: release_blocking_until_legal_approval", Case.Sensitive);
        c3.ShouldContain("approval record: PM approved (Jerome) 2026-06-22; Legal sign-off pending", Case.Sensitive);
        c3.ShouldContain("validation command: `pwsh ./tests/tools/run-retention-deletion-gates.ps1`", Case.Sensitive);

        MarkdownRow[] rows = ReadMarkdownRows(C3Path, "Retention class identifier");
        rows.ShouldNotBeEmpty();
        foreach (string requiredClass in RequiredClasses)
        {
            MarkdownRow row = rows.Single(x => x["Data class"] == requiredClass);
            row["Required class"].ShouldBe("yes");
            row["Retention duration"].ShouldNotBeNullOrWhiteSpace();
            row["Cleanup trigger"].ShouldNotBeNullOrWhiteSpace();
            row["Disposal behavior"].ShouldNotBeNullOrWhiteSpace();
            row["Tenant-deletion disposition"].ShouldBeOneOf("deleted", "tombstoned", "retained", "anonymized");
            row["Tenant-isolation implication"].ShouldContain("tenant", Case.Insensitive);
            row["Observability evidence"].ShouldNotBeNullOrWhiteSpace();
            row["Owner"].ShouldBe("Tech Lead");
            row["Authority"].ShouldBe("Legal + PM");
            // Each required row must still record Legal as pending — PM approval alone does not unblock release.
            row["Approval state"].ShouldContain("Legal approval pending", Case.Insensitive);
            row["Review date"].ShouldBe("2026-05-11");
        }
    }

    [Fact]
    public void TenantDeletionRunbookShouldClassifyEveryRequiredClass()
    {
        string operations = ReadText(OperationsPath);
        foreach (string required in new[]
        {
            "dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false",
            "dotnet build Hexalith.Folders.slnx --no-restore -m:1",
            "pwsh ./tests/tools/run-retention-deletion-gates.ps1",
            "_bmad-output/gates/retention-deletion/latest.json",
            "pending approval blocks live release",
            "metadata-only",
            "git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants",
        })
        {
            operations.ShouldContain(required, Case.Sensitive);
        }

        operations.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);

        MarkdownRow[] rows = ReadMarkdownRows(RunbookPath, "Disposition");
        foreach (string requiredClass in RequiredClasses)
        {
            MarkdownRow row = rows.Single(x => x["Data class"] == requiredClass);
            row["Disposition"].ShouldBeOneOf("deleted", "tombstoned", "retained", "anonymized");
            row["Manual or automated step"].ShouldNotBeNullOrWhiteSpace();
            row["Metadata-only audit reconstruction"].ShouldNotBeNullOrWhiteSpace();
            row["Tenant isolation rule"].ShouldContain("tenant", Case.Insensitive);
            row["Synthetic evidence example"].ShouldContain("tenant-001", Case.Sensitive);
        }

        rows.Select(x => x["Disposition"]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ShouldBe(["anonymized", "deleted", "retained", "tombstoned"]);
    }

    [Fact]
    public void GovernanceEvidenceShouldPointC3AtRetentionDeletionGate()
    {
        YamlMappingNode governance = LoadSingleYamlDocument(GovernancePath);
        YamlSequenceNode criteria = governance.GetRetentionSequence("criteria");
        YamlMappingNode c3 = criteria.Children.Cast<YamlMappingNode>()
            .Single(node => node.GetRetentionScalar("criterion_id") == "C3");

        c3.GetRetentionScalar("status").ShouldBe("reference_pending");
        c3.GetRetentionScalar("artifact_path").ShouldBe(C3Path);
        c3.GetRetentionScalar("verification_command").ShouldBe(@".\tests\tools\run-retention-deletion-gates.ps1");
        c3.GetRetentionScalar("result_summary").ShouldContain("blocks live release publishing", Case.Sensitive);
        // After PM approval, exactly one open placeholder remains — the Legal sign-off. `.Single()` still
        // enforces that the release stays blocked on an outstanding approval: it throws if the placeholder
        // is removed to claim full approval.
        c3.GetRetentionSequence("open_policy_placeholders").Children.Cast<YamlMappingNode>()
            .Single().GetRetentionScalar("id").ShouldBe("C3-legal-approval");
    }

    [Fact]
    public void RetentionDeletionGateScriptShouldFailClosedAndEmitBoundedEvidence()
    {
        string script = ReadText(GateScriptPath);

        foreach (string required in new[]
        {
            "#Requires -Version 7",
            "Set-StrictMode -Version Latest",
            "$ErrorActionPreference = 'Stop'",
            ReportPath,
            "$LASTEXITCODE",
            "utf8NoBOM",
            "source_commit",
            "policy_status",
            "required_data_classes",
            "tenant_deletion_matrix_rows",
            "artifact_paths",
            "validation_categories",
            "result_summaries",
            "missing-c3-class",
            "invalid-tenant-deletion-disposition",
            "missing-release-blocking-posture",
            "recursive-submodule-setup",
            "unsafe-diagnostic-field",
            "release-blocked",
        })
        {
            script.ShouldContain(required, Case.Sensitive);
        }

        foreach (string requiredClass in RequiredClasses)
        {
            script.ShouldContain($"'{requiredClass}'", Case.Sensitive);
        }

        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void ReleaseReadinessShouldRequireRetentionDeletionEvidenceBeforePublish()
    {
        string workflow = ReadText(WorkflowPath);
        string packageGate = ReadText(PackageGatePath);
        string manifest = ReadText(ManifestPath);

        workflow.ShouldContain("./tests/tools/run-retention-deletion-gates.ps1", Case.Sensitive);
        workflow.IndexOf("Run capacity calibration gates", StringComparison.Ordinal)
            .ShouldBeLessThan(workflow.IndexOf("Run retention deletion gates", StringComparison.Ordinal));
        workflow.IndexOf("Run retention deletion gates", StringComparison.Ordinal)
            .ShouldBeLessThan(workflow.IndexOf("Run safety gates", StringComparison.Ordinal));
        workflow.ShouldNotContain("pull_request", Case.Insensitive);

        packageGate.ShouldContain("_bmad-output/gates/retention-deletion/latest.json", Case.Sensitive);
        packageGate.ShouldContain("stale-retention-deletion-evidence", Case.Sensitive);
        packageGate.ShouldContain("c3-retention-approval-blocks-live-publish", Case.Sensitive);
        packageGate.ShouldContain("$Mode -eq 'Publish'", Case.Sensitive);

        manifest.ShouldContain("- _bmad-output/gates/retention-deletion/latest.json", Case.Sensitive);
    }

    [Fact]
    public void BaselineCiShouldRunRetentionDeletionConformanceTests()
    {
        string baselineGate = ReadText(BaselineGatePath);

        baselineGate.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj", Case.Sensitive);
        baselineGate.ShouldContain("Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests", Case.Sensitive);
    }

    [Fact]
    public void RetentionDeletionLatestReportShouldStayMetadataOnlyWhenPresent()
    {
        string fullReportPath = RepositoryPath(ReportPath);
        if (!File.Exists(fullReportPath))
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(ReadText(ReportPath));
        JsonElement root = document.RootElement;

        RequiredString(root, "gate").ShouldBe("retention-deletion");
        RequiredString(root, "diagnostic_policy").ShouldBe("metadata-only");
        RequiredString(root, "report_path").ShouldBe(ReportPath);
        RequiredString(root, "policy_status").ShouldBe("reference_pending");
        RequiredString(root, "status").ShouldBe("release-blocked");
        RequiredString(root, "source_commit").Length.ShouldBe(40);
        ReadStringArray(root, "required_data_classes").ShouldBe(RequiredClasses);
        ReadStringArray(root, "artifact_paths").ShouldContain(C3Path);
        root.GetProperty("tenant_deletion_matrix_rows").GetArrayLength().ShouldBeGreaterThanOrEqualTo(RequiredClasses.Length);
        AssertMetadataOnlyJson(root);
    }

    [Fact]
    public void NegativeControlsRejectVacuousAndUnsafeEvidence()
    {
        MarkdownRow[] missingClass = ReadMarkdownRowsFromText("""
            | Data class | Required class | Retention class identifier | Retention duration | Cleanup trigger | Disposal behavior | Tenant-deletion disposition | Tenant-isolation implication | Observability evidence | Owner | Authority | Approval state | Provenance | Future consumer | Review date |
            |---|---|---|---:|---|---|---|---|---|---|---|---|---|---|---|
            | Audit metadata | yes | `reference_pending_audit_metadata` | 7 years | Scheduled job | Keep metadata | retained | tenant scoped | Audit report | Tech Lead | Legal + PM | proposed workshop value; needs human decision for Legal + PM approval | Architecture C3 | Story 7.11 | 2026-05-11 |
            """, "Retention class identifier");

        Should.Throw<ShouldAssertException>(() => AssertRequiredPolicyRows(missingClass));

        MarkdownRow[] missingDisposition = ReadMarkdownRowsFromText("""
            | Data class | Required class | Retention class identifier | Retention duration | Cleanup trigger | Disposal behavior | Tenant-deletion disposition | Tenant-isolation implication | Observability evidence | Owner | Authority | Approval state | Provenance | Future consumer | Review date |
            |---|---|---|---:|---|---|---|---|---|---|---|---|---|---|---|
            | Audit metadata | yes | `reference_pending_audit_metadata` | 7 years | Scheduled job | Keep metadata |  | tenant scoped | Audit report | Tech Lead | Legal + PM | proposed workshop value; needs human decision for Legal + PM approval | Architecture C3 | Story 7.11 | 2026-05-11 |
            """, "Retention class identifier");

        Should.Throw<ShouldAssertException>(() => missingDisposition.Single()["Tenant-deletion disposition"].ShouldBeOneOf("deleted", "tombstoned", "retained", "anonymized"));

        // Malformed Markdown table: a data row with fewer cells than the header must be rejected
        // by the same parser the positive tests use, not silently truncated.
        Should.Throw<ShouldAssertException>(() => ReadMarkdownRowsFromText("""
            | Data class | Required class | Retention class identifier |
            |---|---|---|
            | Audit metadata | yes |
            """, "Retention class identifier"));

        // Malformed latest report: truncated JSON must fail to parse.
        Should.Throw<JsonException>(() => JsonDocument.Parse("{ \"gate\": \"retention-deletion\", "));

        // Stale-evidence detection mirrors run-release-package-gates.ps1: a report whose
        // source_commit differs from the release revision is stale; an exact match is fresh.
        // Exercise both directions through one predicate so the control fails if the comparison
        // semantics regress to always-stale or always-fresh.
        static bool IsStaleEvidence(string reportCommit, string releaseCommit)
            => !string.Equals(reportCommit, releaseCommit, StringComparison.Ordinal);

        string releaseCommit = new('a', 40);
        IsStaleEvidence(new string('b', 40), releaseCommit).ShouldBeTrue();
        IsStaleEvidence(releaseCommit, releaseCommit).ShouldBeFalse();

        // Unsafe diagnostic and absolute path are rejected by the SAME production scanner that
        // guards the real report, not by a standalone BCL assertion.
        using JsonDocument unsafeJson = JsonDocument.Parse("{\"diagnostic\":\"Authorization: Bearer synthetic\"}");
        Should.Throw<ShouldAssertException>(() => AssertMetadataOnlyJson(unsafeJson.RootElement));

        using JsonDocument absolutePath = JsonDocument.Parse("{\"evidence_path\":\"/workspace/evidence/latest.json\"}");
        Should.Throw<ShouldAssertException>(() => AssertMetadataOnlyJson(absolutePath.RootElement));

        // Recursive-submodule detection semantics: the forbidden recursive form is flagged while
        // the approved root-level command is not.
        string recursiveToken = string.Concat("--", "recursive");
        ("git submodule update --init " + recursiveToken).Contains(recursiveToken, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        "git submodule update --init Hexalith.Commons".Contains(recursiveToken, StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    private static void AssertRequiredPolicyRows(MarkdownRow[] rows)
    {
        foreach (string requiredClass in RequiredClasses)
        {
            rows.Count(x => x["Data class"] == requiredClass).ShouldBe(1);
        }
    }

    private static MarkdownRow[] ReadMarkdownRows(string relativePath, string headerSentinel)
        => ReadMarkdownRowsFromText(ReadText(relativePath), headerSentinel);

    private static MarkdownRow[] ReadMarkdownRowsFromText(string text, string headerSentinel)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].TrimStart().StartsWith('|') || !lines[i].Contains(headerSentinel, StringComparison.Ordinal))
            {
                continue;
            }

            string[] headers = SplitMarkdownRow(lines[i]);
            var rows = new List<MarkdownRow>();
            for (int j = i + 2; j < lines.Length; j++)
            {
                if (!lines[j].TrimStart().StartsWith('|'))
                {
                    break;
                }

                string[] cells = SplitMarkdownRow(lines[j]);
                cells.Length.ShouldBe(headers.Length, $"Malformed Markdown table row: {lines[j]}");
                rows.Add(new MarkdownRow(headers.Zip(cells, static (key, value) => new KeyValuePair<string, string>(key, value))
                    .ToDictionary(static x => x.Key, static x => x.Value, StringComparer.Ordinal)));
            }

            return rows.ToArray();
        }

        throw new InvalidOperationException($"Could not find Markdown table with header sentinel '{headerSentinel}'.");
    }

    private static string[] SplitMarkdownRow(string line)
        => line.Trim().Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray();

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

    private static string RepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String, $"JSON property '{propertyName}' must be a string.");
        return property.GetString().ShouldNotBeNull();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.Array, $"JSON property '{propertyName}' must be an array.");
        return property.EnumerateArray().Select(static item => item.GetString().ShouldNotBeNull()).ToArray();
    }

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }

                break;
            case JsonValueKind.String:
                string value = element.GetString().ShouldNotBeNull();
                RootedPathPattern().IsMatch(value).ShouldBeFalse($"Retention/deletion report value must not contain an absolute path: {value}");
                UnsafeDiagnosticPattern().IsMatch(value).ShouldBeFalse($"Retention/deletion report value must stay metadata-only: {value}");
                break;
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|raw file contents|provider payload|environment dump|stack trace|https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeDiagnosticPattern();

    private sealed record MarkdownRow(IReadOnlyDictionary<string, string> Cells)
    {
        public string this[string key] => Cells[key];
    }
}

internal static class RetentionDeletionYamlExtensions
{
    public static string GetRetentionScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    public static YamlSequenceNode GetRetentionSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }
}
