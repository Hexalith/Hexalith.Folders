using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed partial class InMemoryProviderReadinessEvidenceStore : IProviderReadinessEvidenceStore, IProviderSupportEvidenceReadModel
{
    private const string RepositoryCreation = "repository_creation";
    private const string ExistingRepositoryBinding = "existing_repository_binding";
    private const string BranchRefPolicy = "branch_ref_policy";
    private const string FileOperations = "file_operations";
    private const string CommitStatus = "commit_status";
    private const string ProviderErrors = "provider_errors";
    private const string FailureBehavior = "failure_behavior";
    private const string Supported = "supported";
    private const string Unsupported = "unsupported";
    private const string TemporarilyUnavailable = "temporarily_unavailable";
    private const string DocumentedFailureBehavior = "documented";
    private const string RetryAfterBackoffFailureBehavior = "retry_after_backoff";
    private const string CursorPrefix = "cursor_";
    private static readonly TimeSpan EvidenceFreshnessBudget = TimeSpan.FromMinutes(30);

    private readonly ConcurrentQueue<ProviderReadinessEvidenceRecord> _records = new();
    private readonly IUtcClock _clock;

    public InMemoryProviderReadinessEvidenceStore()
        : this(new SystemUtcClock())
    {
    }

    public InMemoryProviderReadinessEvidenceStore(IUtcClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IReadOnlyList<ProviderReadinessEvidenceRecord> Records => _records.ToArray();

    public Task StoreAsync(ProviderReadinessEvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();

        _records.Enqueue(evidence);
        return Task.CompletedTask;
    }

    public Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
        ProviderSupportEvidenceReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        int offset = CursorOffset(request.Cursor);
        ProviderReadinessEvidenceRecord[] scopedRecords = _records
            .Where(record => string.Equals(record.ManagedTenantId, request.ManagedTenantId, StringComparison.Ordinal))
            .OrderBy(static record => record.CapabilityProfileRef, StringComparer.Ordinal)
            .ThenBy(static record => record.ObservedAt)
            .ThenBy(static record => record.ProviderBindingRef, StringComparer.Ordinal)
            .ToArray();

        if (scopedRecords.Length == 0)
        {
            return Task.FromResult(ProviderSupportEvidenceReadModelResult.Available([], request.EmptyFreshness(), nextCursor: null));
        }

        ProviderReadinessFreshness freshness = Freshness(request, scopedRecords);
        if (scopedRecords.Any(record => record.ObservedAt > _clock.UtcNow))
        {
            return Task.FromResult(ProviderSupportEvidenceReadModelResult.Malformed(freshness));
        }

        if (scopedRecords.Any(record => record.ObservedAt < request.RequestedAt.Subtract(EvidenceFreshnessBudget)))
        {
            return Task.FromResult(ProviderSupportEvidenceReadModelResult.Stale(freshness));
        }

        List<ProviderSupportEvidenceItem> items = [];
        foreach (ProviderReadinessEvidenceRecord record in LatestRecordsByCapabilityProfile(scopedRecords))
        {
            if (!IsSafeOpaqueIdentifier(record.CapabilityProfileRef)
                || !TryProject(record, out IReadOnlyList<ProviderSupportEvidenceItem>? recordItems)
                || recordItems is null)
            {
                return Task.FromResult(ProviderSupportEvidenceReadModelResult.Malformed(freshness));
            }

            items.AddRange(recordItems);
        }

        ProviderSupportEvidenceItem[] ordered = items
            .OrderBy(static item => item.CapabilityProfileRef, StringComparer.Ordinal)
            .ThenBy(static item => CapabilityOrder(item.Capability))
            .ToArray();

        ProviderSupportEvidenceItem[] page = ordered.Skip(offset).Take(request.Limit).ToArray();
        string? nextCursor = offset + page.Length < ordered.Length ? $"{CursorPrefix}{offset + page.Length}" : null;
        return Task.FromResult(ProviderSupportEvidenceReadModelResult.Available(page, freshness, nextCursor));
    }

    private static IReadOnlyList<ProviderReadinessEvidenceRecord> LatestRecordsByCapabilityProfile(
        IReadOnlyCollection<ProviderReadinessEvidenceRecord> scopedRecords)
        => scopedRecords
            .GroupBy(static record => record.CapabilityProfileRef, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static record => record.ObservedAt)
                .ThenByDescending(static record => record.FreshnessWatermark, StringComparer.Ordinal)
                .First())
            .OrderBy(static record => record.CapabilityProfileRef, StringComparer.Ordinal)
            .ToArray();

    private static ProviderReadinessFreshness Freshness(
        ProviderSupportEvidenceReadModelRequest request,
        IReadOnlyCollection<ProviderReadinessEvidenceRecord> scopedRecords)
    {
        DateTimeOffset observedAt = scopedRecords.Count == 0
            ? request.RequestedAt
            : scopedRecords.Max(static record => record.ObservedAt);

        string? projectionWatermark = scopedRecords
            .Select(static record => record.FreshnessWatermark)
            .Where(static watermark => !string.IsNullOrWhiteSpace(watermark))
            .OrderBy(static watermark => watermark, StringComparer.Ordinal)
            .LastOrDefault()
            ?? request.AuthorizationWatermark;

        return new(request.ReadConsistency, observedAt, projectionWatermark, Stale: false);
    }

    private static bool TryProject(
        ProviderReadinessEvidenceRecord record,
        out IReadOnlyList<ProviderSupportEvidenceItem>? items)
    {
        items = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(record.DiagnosticJson);
            if (ContainsUnsafeDiagnosticValue(document.RootElement))
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("evidence", out JsonElement evidence)
                || evidence.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                items = [];
                return true;
            }

            if (evidence.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryCapabilityState(evidence, "repositoryCreation", out string? repositoryCreation)
                || !TryCapabilityState(evidence, "existingRepositoryBinding", out string? existingRepositoryBinding)
                || !TryCapabilityState(evidence, "branchRefPolicy", out string? branchRefPolicy)
                || !TryCapabilityState(evidence, "fileOperations", out string? fileOperations)
                || !TryCapabilityState(evidence, "commitStatus", out string? commitStatus)
                || !TryCapabilityState(evidence, "providerErrors", out string? providerErrors)
                || !TryFailureBehaviorState(evidence, out string? failureBehavior))
            {
                return false;
            }

            items =
            [
                Item(record, RepositoryCreation, repositoryCreation),
                Item(record, ExistingRepositoryBinding, existingRepositoryBinding),
                Item(record, BranchRefPolicy, branchRefPolicy),
                Item(record, FileOperations, fileOperations),
                Item(record, CommitStatus, commitStatus),
                Item(record, ProviderErrors, providerErrors),
                Item(record, FailureBehavior, failureBehavior),
            ];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProviderSupportEvidenceItem Item(
        ProviderReadinessEvidenceRecord record,
        string capability,
        string supportState)
        => new(record.CapabilityProfileRef!, capability, supportState);

    private static bool TryCapabilityState(JsonElement evidence, string propertyName, out string state)
    {
        state = Unsupported;
        if (!evidence.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? raw = value.GetString();
        if (raw is Supported or Unsupported or TemporarilyUnavailable)
        {
            state = raw;
            return true;
        }

        return false;
    }

    private static bool TryFailureBehaviorState(JsonElement evidence, out string state)
    {
        state = Unsupported;
        if (!evidence.TryGetProperty("failureBehavior", out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? raw = value.GetString();
        if (raw is DocumentedFailureBehavior or RetryAfterBackoffFailureBehavior)
        {
            state = Supported;
            return true;
        }

        return raw is Supported or Unsupported or TemporarilyUnavailable && TryCapabilityState(evidence, "failureBehavior", out state);
    }

    private static bool ContainsUnsafeDiagnosticValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (ContainsUnsafeDiagnosticValue(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (ContainsUnsafeDiagnosticValue(item))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.String:
                string? value = element.GetString();
                return IsUnsafeDiagnosticString(value);
            default:
                return false;
        }
    }

    private static bool IsUnsafeDiagnosticString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string canonical = value.Trim();
        return canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains("@", StringComparison.Ordinal)
            || canonical.Contains("diff --git", StringComparison.OrdinalIgnoreCase)
            || canonical.Contains("providerpayload", StringComparison.OrdinalIgnoreCase)
            || canonical.Contains("privatekey", StringComparison.OrdinalIgnoreCase)
            || canonical.Contains("private key", StringComparison.OrdinalIgnoreCase)
            || canonical.Contains("repository-secret", StringComparison.OrdinalIgnoreCase)
            || ProviderTokenPattern().IsMatch(canonical)
            || JwtPattern().IsMatch(canonical)
            || PemPattern().IsMatch(canonical);
    }

    private static int CursorOffset(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || !cursor.StartsWith(CursorPrefix, StringComparison.Ordinal))
        {
            return 0;
        }

        return int.TryParse(cursor[CursorPrefix.Length..], out int offset) && offset > 0 ? offset : 0;
    }

    private static bool IsSafeOpaqueIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 16 and <= 128
        && OpaqueIdentifierPattern().IsMatch(value);

    private static int CapabilityOrder(string capability)
        => capability switch
        {
            RepositoryCreation => 0,
            ExistingRepositoryBinding => 1,
            BranchRefPolicy => 2,
            FileOperations => 3,
            CommitStatus => 4,
            ProviderErrors => 5,
            FailureBehavior => 6,
            _ => 99,
        };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex OpaqueIdentifierPattern();

    [GeneratedRegex("gh[pousr]_[a-zA-Z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderTokenPattern();

    [GeneratedRegex("eyJ[a-zA-Z0-9_-]{10,}\\.[a-zA-Z0-9_-]{5,}\\.[a-zA-Z0-9_-]{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();
}
