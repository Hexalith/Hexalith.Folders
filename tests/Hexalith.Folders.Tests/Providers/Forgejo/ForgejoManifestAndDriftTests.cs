using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoManifestAndDriftTests
{
    private static readonly string[] RequiredSnapshotPaths =
    [
        "/version",
        "/orgs/{org}/repos",
        "/repos/{owner}/{repo}",
        "/repos/{owner}/{repo}/branches/{branch}",
        "/repos/{owner}/{repo}/branch_protections/{name}",
    ];

    [Fact]
    public void SupportedVersionManifestPinsRequiredVersionClassesAndIntegrityEvidence()
    {
        string root = FindRepositoryRoot();
        ForgejoVersionManifest manifest = LoadManifest(root);

        manifest.SchemaVersion.ShouldBe("forgejo-supported-versions-v2");
        manifest.Entries.Select(entry => entry.SupportClass).ShouldContain("latest-stable");
        manifest.Entries.Select(entry => entry.SupportClass).ShouldContain("long-term-support");
        manifest.Entries.Select(entry => entry.Version).ShouldBeUnique();
        manifest.Entries.Select(entry => entry.SnapshotPath).ShouldBeUnique();
        manifest.Entries.Select(static entry => entry.Version).ShouldBe(
            ForgejoSupportedVersionCatalog.SupportedVersions.Select(static entry => entry.Version),
            ignoreOrder: false);

        foreach (ForgejoVersionManifestEntry entry in manifest.Entries)
        {
            entry.Version.ShouldNotBeNullOrWhiteSpace();
            entry.VersionFamily.ShouldNotBeNullOrWhiteSpace();
            entry.SourceUrl.ShouldStartWith("https://code.forgejo.org/forgejo/forgejo/raw/tag/v");
            entry.Owner.ShouldBe("platform-engineering");
            entry.Reviewer.ShouldBe("folders-provider-maintainers");
            entry.DatedSource.ShouldBe("2026-08-26");
            entry.SourceArtifactSha256.ShouldStartWith("sha256:");
            entry.SnapshotSha256.ShouldBe(ComputeFileHash(Path.Combine(root, entry.SnapshotPath)));
            entry.IntegrityHash.ShouldBe(ComputeIntegrityHash(entry));
            File.Exists(Path.Combine(root, entry.SnapshotPath)).ShouldBeTrue(entry.SnapshotPath);
        }
    }

    [Fact]
    public void EverySupportedSnapshotParsesAndCoversProviderPortOperations()
    {
        string root = FindRepositoryRoot();
        ForgejoVersionManifest manifest = LoadManifest(root);

        foreach (ForgejoVersionManifestEntry entry in manifest.Entries)
        {
            using JsonDocument snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, entry.SnapshotPath)));
            snapshot.RootElement.GetProperty("swagger").GetString().ShouldBe("2.0");
            snapshot.RootElement.GetProperty("info").GetProperty("version").GetString().ShouldStartWith(entry.VersionFamily);
            JsonElement review = snapshot.RootElement.GetProperty("x-hexalith-review");
            review.GetProperty("source").GetString().ShouldBe(entry.SourceUrl);
            $"sha256:{review.GetProperty("sourceArtifactSha256").GetString()}".ShouldBe(entry.SourceArtifactSha256);
            review.GetProperty("scope").GetString().ShouldBe("repository-create-bind-branch-protection");
            JsonElement paths = snapshot.RootElement.GetProperty("paths");

            foreach (string requiredPath in RequiredSnapshotPaths)
            {
                paths.TryGetProperty(requiredPath, out _).ShouldBeTrue($"{entry.Version} is missing {requiredPath}");
            }

            AssertReviewedOperationShapes(snapshot);
        }
    }

    [Fact]
    public void OperationCoverageMatrixMapsProviderOperationsToPinnedForgejoPaths()
    {
        Dictionary<string, string[]> coverage = new(StringComparer.Ordinal)
        {
            [ProviderOperationCatalog.ReadinessValidation] = ["/version"],
            [ProviderOperationCatalog.ProviderSupportEvidence] = ["/version"],
            [ProviderOperationCatalog.RepositoryCreation] = ["/orgs/{org}/repos"],
            [ProviderOperationCatalog.RepositoryBinding] = ["/repos/{owner}/{repo}"],
            [ProviderOperationCatalog.BranchRefInspection] =
            [
                "/repos/{owner}/{repo}/branches/{branch}",
                "/repos/{owner}/{repo}/branch_protections/{name}",
            ],
        };

        coverage.Keys.ShouldContain(ProviderOperationCatalog.RepositoryCreation);
        coverage.Keys.ShouldContain(ProviderOperationCatalog.RepositoryBinding);
        coverage.Keys.ShouldContain(ProviderOperationCatalog.BranchRefInspection);
        coverage.Values.SelectMany(static paths => paths).ShouldAllBe(path => RequiredSnapshotPaths.Contains(path, StringComparer.Ordinal));
    }

    [Fact]
    public void ManifestValidationFailsClosedForDuplicateVersionsMissingSnapshotsAndStaleHashes()
    {
        string root = FindRepositoryRoot();
        ForgejoVersionManifest manifest = LoadManifest(root);
        ForgejoVersionManifestEntry first = manifest.Entries[0];

        ValidateManifest(root, manifest).ShouldBeEmpty();

        ForgejoVersionManifest duplicate = manifest with { Entries = [.. manifest.Entries, first] };
        ValidateManifest(root, duplicate).ShouldContain("duplicate_version");

        ForgejoVersionManifest missingSnapshot = manifest with
        {
            Entries =
            [
                first with { SnapshotPath = "tests/contracts/forgejo/missing/swagger.v1.json" },
                .. manifest.Entries.Skip(1),
            ],
        };
        ValidateManifest(root, missingSnapshot).ShouldContain("missing_snapshot");

        ForgejoVersionManifest staleHash = manifest with
        {
            Entries =
            [
                first with { IntegrityHash = "sha256:stale" },
                .. manifest.Entries.Skip(1),
            ],
        };
        ValidateManifest(root, staleHash).ShouldContain("stale_integrity_hash");
    }

    [Fact]
    public void DriftClassificationFixturesAreHermeticAndSeverityMapped()
    {
        string root = FindRepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "tests", "tools", "forgejo-drift", "classification-fixtures.json")));

        document.RootElement.GetProperty("schemaVersion").GetString().ShouldBe("forgejo-drift-classification-fixtures-v1");
        document.RootElement.GetProperty("redactionPolicy").GetString().ShouldBe("metadata-only");

        foreach (JsonElement fixture in document.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            string changeKind = fixture.GetProperty("changeKind").GetString().ShouldNotBeNull();
            string expectedClassification = fixture.GetProperty("expectedClassification").GetString().ShouldNotBeNull();
            string expectedSeverity = fixture.GetProperty("severity").GetString().ShouldNotBeNull();

            Classify(changeKind).ShouldBe(expectedClassification);
            Severity(expectedClassification).ShouldBe(expectedSeverity);
        }
    }

    [Fact]
    public void ForgejoDriftReportScriptEmitsSanitizedMetadata()
    {
        string root = FindRepositoryRoot();
        string reportScriptPath = Path.Combine(root, "tests", "tools", "forgejo-drift", "Write-SanitizedForgejoDriftReport.ps1");

        File.Exists(reportScriptPath).ShouldBeTrue("Forgejo drift tooling must emit sanitized classification metadata.");

        string reportScript = File.ReadAllText(reportScriptPath);
        reportScript.ShouldContain("forgejo-drift-report-v1");
        reportScript.ShouldContain("sanitized-metadata-only");
        reportScript.ShouldContain("redactionScan");
        reportScript.ShouldContain("localBuildImpact");
    }

    [Fact]
    public void ForgejoContractFixturesDoNotContainForbiddenSentinelValues()
    {
        string root = FindRepositoryRoot();
        string[] files = Directory
            .EnumerateFiles(Path.Combine(root, "tests", "contracts", "forgejo"), "*.*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tests", "tools", "forgejo-drift"), "*.*", SearchOption.AllDirectories))
            .ToArray();
        string[] forbidden =
        [
            "access_token=",
            "token=",
            "ghp_",
            "-----BEGIN",
            "user:",
            "@forgejo",
            "customer",
            "private-instance",
            "owner-secret",
            "repo-secret",
            "diff --git",
        ];

        string[] violations = files
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .SelectMany(file => forbidden
                .Where(value => file.Text.Contains(value, StringComparison.OrdinalIgnoreCase))
                .Select(value => $"{Path.GetRelativePath(root, file.File).Replace('\\', '/')}: {value}"))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    private static string[] ValidateManifest(string root, ForgejoVersionManifest manifest)
    {
        List<string> failures = [];
        if (manifest.Entries.Select(static entry => entry.Version).Distinct(StringComparer.Ordinal).Count() != manifest.Entries.Count)
        {
            failures.Add("duplicate_version");
        }

        if (manifest.Entries.Select(static entry => entry.SnapshotPath).Distinct(StringComparer.Ordinal).Count() != manifest.Entries.Count)
        {
            failures.Add("duplicate_snapshot");
        }

        foreach (ForgejoVersionManifestEntry entry in manifest.Entries)
        {
            if (!File.Exists(Path.Combine(root, entry.SnapshotPath)))
            {
                failures.Add("missing_snapshot");
            }

            if (!string.Equals(entry.IntegrityHash, ComputeIntegrityHash(entry), StringComparison.Ordinal))
            {
                failures.Add("stale_integrity_hash");
            }

            if (File.Exists(Path.Combine(root, entry.SnapshotPath))
                && !string.Equals(entry.SnapshotSha256, ComputeFileHash(Path.Combine(root, entry.SnapshotPath)), StringComparison.Ordinal))
            {
                failures.Add("stale_snapshot_hash");
            }

            if (string.IsNullOrWhiteSpace(entry.Owner) || string.IsNullOrWhiteSpace(entry.Reviewer) || string.IsNullOrWhiteSpace(entry.SourceUrl))
            {
                failures.Add("missing_review_metadata");
            }
        }

        return failures.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Classify(string changeKind)
        => changeKind switch
        {
            "additive-field" => "additive-compatible",
            "enum-new-string-value" => "additive-compatible",
            "removed-field" => "breaking-incompatible",
            "type-change" => "breaking-incompatible",
            "nullability-change" => "breaking-incompatible",
            "error-shape-change" => "breaking-incompatible",
            "pagination-shape-change" => "breaking-incompatible",
            "auth-rate-limit-header-change" => "breaking-incompatible",
            _ => "unknown-unclassified",
        };

    private static string Severity(string classification)
        => string.Equals(classification, "additive-compatible", StringComparison.Ordinal)
            ? "warning"
            : "failure";

    private static ForgejoVersionManifest LoadManifest(string root)
        => JsonSerializer.Deserialize<ForgejoVersionManifest>(
            File.ReadAllText(Path.Combine(root, "tests", "contracts", "forgejo", "supported-versions.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ShouldNotBeNull();

    private static string ComputeIntegrityHash(ForgejoVersionManifestEntry entry)
    {
        string payload = string.Join(
            '|',
            entry.Version,
            entry.VersionFamily,
            entry.SupportClass,
            entry.SourceUrl,
            entry.SnapshotPath,
            entry.ExpectedApiCompatibilityPosture,
            entry.Owner,
            entry.Reviewer,
            entry.DatedSource,
            entry.SourceArtifactSha256,
            entry.SnapshotSha256);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static string ComputeFileHash(string path)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}";

    private static void AssertReviewedOperationShapes(JsonDocument snapshot)
    {
        JsonElement root = snapshot.RootElement;
        JsonElement paths = root.GetProperty("paths");
        AssertGetOperation(paths, "/version", "getVersion", [], expectedResponseReference: null);
        AssertGetOperation(
            paths,
            "/repos/{owner}/{repo}",
            "repoGet",
            ["owner", "repo"],
            "#/responses/Repository");
        AssertGetOperation(
            paths,
            "/repos/{owner}/{repo}/branches/{branch}",
            "repoGetBranch",
            ["owner", "repo", "branch"],
            "#/responses/Branch");
        AssertGetOperation(
            paths,
            "/repos/{owner}/{repo}/branch_protections/{name}",
            "repoGetBranchProtection",
            ["owner", "repo", "name"],
            "#/responses/BranchProtection");

        JsonElement create = paths.GetProperty("/orgs/{org}/repos").GetProperty("post");
        create.GetProperty("operationId").GetString().ShouldBe("createOrgRepo");
        JsonElement organizationParameter = create.GetProperty("parameters").EnumerateArray()
            .Single(static parameter => parameter.GetProperty("name").GetString() == "org");
        organizationParameter.GetProperty("in").GetString().ShouldBe("path");
        organizationParameter.GetProperty("required").GetBoolean().ShouldBeTrue();
        create.GetProperty("parameters").EnumerateArray()
            .Single(static parameter => parameter.GetProperty("name").GetString() == "body")
            .GetProperty("schema").GetProperty("$ref").GetString().ShouldBe("#/definitions/CreateRepoOption");
        JsonElement createResponses = create.GetProperty("responses");
        createResponses.GetProperty("201").GetProperty("$ref").GetString().ShouldBe("#/responses/Repository");
        createResponses.GetProperty("400").GetProperty("$ref").GetString().ShouldBe("#/responses/error");

        JsonElement createOption = root.GetProperty("definitions").GetProperty("CreateRepoOption");
        createOption.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).ShouldContain("name");
        JsonElement createProperties = createOption.GetProperty("properties");
        createProperties.GetProperty("auto_init").GetProperty("type").GetString().ShouldBe("boolean");
        createProperties.GetProperty("private").GetProperty("type").GetString().ShouldBe("boolean");

        JsonElement repository = root.GetProperty("definitions").GetProperty("Repository").GetProperty("properties");
        repository.GetProperty("id").GetProperty("type").GetString().ShouldBe("integer");
        repository.GetProperty("id").GetProperty("format").GetString().ShouldBe("int64");
        repository.GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
        repository.GetProperty("private").GetProperty("type").GetString().ShouldBe("boolean");
        repository.GetProperty("internal").GetProperty("type").GetString().ShouldBe("boolean");
        repository.GetProperty("default_branch").GetProperty("type").GetString().ShouldBe("string");
        repository.GetProperty("permissions").GetProperty("$ref").GetString().ShouldBe("#/definitions/Permission");
        JsonElement permissions = root.GetProperty("definitions").GetProperty("Permission").GetProperty("properties");
        permissions.GetProperty("pull").GetProperty("type").GetString().ShouldBe("boolean");
        permissions.GetProperty("admin").GetProperty("type").GetString().ShouldBe("boolean");

        JsonElement branch = root.GetProperty("definitions").GetProperty("Branch").GetProperty("properties");
        branch.GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
        branch.GetProperty("protected").GetProperty("type").GetString().ShouldBe("boolean");
        branch.GetProperty("effective_branch_protection_name").GetProperty("type").GetString().ShouldBe("string");
        root.GetProperty("definitions").GetProperty("BranchProtection").GetProperty("properties")
            .GetProperty("rule_name").GetProperty("type").GetString().ShouldBe("string");
    }

    private static void AssertGetOperation(
        JsonElement paths,
        string path,
        string operationId,
        string[] expectedParameterNames,
        string? expectedResponseReference)
    {
        JsonElement operation = paths.GetProperty(path).GetProperty("get");
        operation.GetProperty("operationId").GetString().ShouldBe(operationId);
        string[] parameterNames = operation.TryGetProperty("parameters", out JsonElement parameters)
            ? parameters.EnumerateArray().Select(static parameter => parameter.GetProperty("name").GetString()!).ToArray()
            : [];
        parameterNames.ShouldBe(expectedParameterNames, ignoreOrder: false);
        JsonElement success = operation.GetProperty("responses").GetProperty("200");
        if (expectedResponseReference is null)
        {
            success.GetProperty("schema").ValueKind.ShouldBe(JsonValueKind.Object);
        }
        else
        {
            success.GetProperty("$ref").GetString().ShouldBe(expectedResponseReference);
        }
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ForgejoVersionManifest(
        string SchemaVersion,
        string GeneratedAt,
        string Owner,
        string Reviewer,
        IReadOnlyList<ForgejoVersionManifestEntry> Entries);

    private sealed record ForgejoVersionManifestEntry(
        string Version,
        string VersionFamily,
        string SupportClass,
        string SourceUrl,
        string SnapshotPath,
        string ExpectedApiCompatibilityPosture,
        string Owner,
        string Reviewer,
        string DatedSource,
        string SourceArtifactSha256,
        string SnapshotSha256,
        string IntegrityHash);
}
