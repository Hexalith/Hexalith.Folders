using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Testing.Providers;

public sealed class FakeGitProvider : IGitProvider
{
    private readonly IReadOnlyList<ProviderCapabilityOperationRow> _operationRows;
    private readonly ProviderFailureCategory? _failureCategory;
    private readonly string _schemaVersion;
    private readonly IReadOnlyDictionary<string, string> _evidence;

    private FakeGitProvider(
        string providerFamily,
        string providerKey,
        IReadOnlyList<ProviderCapabilityOperationRow> operationRows,
        ProviderFailureCategory? failureCategory = null,
        string schemaVersion = "v1",
        IReadOnlyDictionary<string, string>? evidence = null)
    {
        ProviderFamily = ProviderIdentityIdentifier.Normalize(providerFamily);
        ProviderKey = ProviderIdentityIdentifier.Normalize(providerKey);
        _operationRows = operationRows;
        _failureCategory = failureCategory;
        _schemaVersion = schemaVersion;
        _evidence = evidence ?? new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile_source"] = "static_fake",
        };
    }

    public string ProviderFamily { get; }

    public string ProviderKey { get; }

    public static FakeGitProvider GitHubLike(bool reversedOperations = false, string schemaVersion = "v1")
    {
        ProviderCapabilityOperationRow[] rows =
        [
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryCreation),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.BranchRefInspection,
                ProviderOperationSupport.Supported,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["max_refs"] = "1000",
                }),
            ProviderCapabilityOperationRow.WithDetails(
                ProviderOperationCatalog.FileMutationSupport,
                ProviderOperationSupport.Partial,
                limits: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["max_file_bytes"] = "1048576",
                }),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
            ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
            ProviderCapabilityOperationRow.Emulated(ProviderOperationCatalog.CleanupExpiration),
        ];

        return new(
            "github",
            "github",
            reversedOperations ? rows.Reverse().ToArray() : rows,
            schemaVersion: schemaVersion);
    }

    public static FakeGitProvider ForgejoLike()
        => new(
            "forgejo",
            "forgejo",
            [
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence),
                ProviderCapabilityOperationRow.Partial(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Partial(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Partial(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Unsupported(ProviderOperationCatalog.CleanupExpiration),
            ]);

    public static FakeGitProvider CustomFamily()
        => new(
            "acme custom",
            "acme_custom",
            [
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ProviderSupportEvidence),
                ProviderCapabilityOperationRow.Emulated(ProviderOperationCatalog.RepositoryCreation),
                ProviderCapabilityOperationRow.Emulated(ProviderOperationCatalog.RepositoryBinding),
                ProviderCapabilityOperationRow.Partial(ProviderOperationCatalog.BranchRefInspection),
                ProviderCapabilityOperationRow.Emulated(ProviderOperationCatalog.FileMutationSupport),
                ProviderCapabilityOperationRow.Emulated(ProviderOperationCatalog.CommitSupport),
                ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.StatusQuery),
                ProviderCapabilityOperationRow.Unsupported(ProviderOperationCatalog.CleanupExpiration),
            ]);

    public static FakeGitProvider Failing(ProviderFailureCategory category)
        => new(
            "github",
            "github",
            [],
            category);

    public static FakeGitProvider WithOperationRows(params ProviderCapabilityOperationRow[] rows)
        => new("github", "github", rows);

    public static FakeGitProvider WithEvidenceMetadata(params (string Key, string Value)[] entries)
        => new(
            "github",
            "github",
            [ProviderCapabilityOperationRow.Supported(ProviderOperationCatalog.ReadinessValidation)],
            evidence: entries.ToDictionary(static x => x.Key, static x => x.Value, StringComparer.Ordinal));

    public Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        if (_failureCategory is { } category)
        {
            return Task.FromResult(ProviderCapabilityDiscoveryResult.Failure(
                category,
                category.ToCategoryCode(),
                request.CorrelationId,
                retryAfter: category == ProviderFailureCategory.ProviderRateLimited ? TimeSpan.FromSeconds(60) : null));
        }

        if (!string.Equals(ProviderIdentityIdentifier.Normalize(request.ProviderFamily), ProviderFamily, StringComparison.Ordinal))
        {
            return Task.FromResult(ProviderCapabilityDiscoveryResult.Failure(
                ProviderFailureCategory.UnsupportedProviderCapability,
                "unsupported_provider_family",
                request.CorrelationId));
        }

        ProviderCapabilityDiscoveryRequest effectiveRequest = request with
        {
            ProviderFamily = ProviderFamily,
            ProviderKey = ProviderKey,
            ProfileSchemaVersion = _schemaVersion,
        };

        return Task.FromResult(ProviderCapabilityProfileFactory.Create(
            effectiveRequest,
            ProviderFamily,
            ProviderKey,
            _operationRows,
            RateLimit(),
            FailureMappings(),
            _evidence));
    }

    public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
        ProviderCapabilityProfile current,
        ProviderCapabilityProfile candidate)
        => ProviderCapabilityProfileFactory.Compare(current, candidate);

    private static ProviderRateLimitPosture RateLimit()
        => new(
            "bounded",
            true,
            TimeSpan.FromSeconds(60),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["posture"] = "bounded_retry",
            });

    private static IReadOnlyDictionary<string, string> FailureMappings()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["not_supported"] = ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode(),
            ["unavailable"] = ProviderFailureCategory.ProviderUnavailable.ToCategoryCode(),
            ["rate_limited"] = ProviderFailureCategory.ProviderRateLimited.ToCategoryCode(),
            ["unknown"] = ProviderFailureCategory.UnknownProviderOutcome.ToCategoryCode(),
        };
}
