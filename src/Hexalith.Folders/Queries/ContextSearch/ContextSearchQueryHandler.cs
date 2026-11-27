using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// Handles an authorized search over the Memories search index (<c>folders-index</c>). The handler is the
/// authoritative Folders-side control: it runs the full layered authorization BEFORE any egress, then — because
/// the shared index is security-untrusted and non-authoritative — re-checks every returned hit against the
/// authorized scope, hydrates each survivor from the authoritative Folders bridge read model, and emits only
/// metadata-only items (no content snippet, no raw path, no Memories source URI). Denied, cross-tenant, and absent
/// targets all collapse to the same safe-denial shape.
/// </summary>
public sealed class ContextSearchQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IFolderSearchSource source,
    ISemanticIndexingBridgeReadModel bridgeReadModel,
    IUtcClock clock,
    ILogger<ContextSearchQueryHandler>? logger = null)
{
    /// <summary>The folder-ACL action token gating this read; must be present in <see cref="EffectivePermissionsActionCatalog"/>.</summary>
    public const string ActionToken = "read_context_search";

    /// <summary>The C4 query family recorded in audit/limits metadata for this facade.</summary>
    public const string QueryFamily = "semantic_reference_pending";

    private const string EventuallyConsistent = "eventually_consistent";
    private const string ActorPresentIdentifier = "actor_present";
    private const string CursorPrefix = "memories-search:";
    private const string SensitivityTenant = "tenant_sensitive";
    private const string SensitivityRestricted = "restricted";
    private const string RedactionNone = "not_redacted";
    private const string RedactionRedacted = "redacted";
    private const string NotTruncated = "not_truncated";
    private const string TruncatedResultCount = "result_count_limit";

    private const int MaxQueryTextLength = 256;
    private const int MaxResultCount = 500;
    private const int MaxCursorLength = 256;
    private const long MaxResponseBytes = 1048576;
    private static readonly TimeSpan _gatewaySearchBudget = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions ResponseBudgetJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LayeredFolderAuthorizationService _authorizationService = authorizationService;
    private readonly IFolderSearchSource _source = source;
    private readonly ISemanticIndexingBridgeReadModel _bridgeReadModel = bridgeReadModel;
    private readonly IUtcClock _clock = clock;
    private readonly ILogger<ContextSearchQueryHandler> _logger =
        logger ?? NullLogger<ContextSearchQueryHandler>.Instance;

    /// <summary>Executes the authorized, security-trimmed, metadata-only context search.</summary>
    public Task<ContextSearchQueryResult> HandleAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken = default)
        => HandleCoreAsync(query, verifyContinuation: false, cancellationToken);

    /// <summary>Searches with a public continuation based on a subsequent visible hydrated hit.</summary>
    internal Task<ContextSearchQueryResult> HandleGatewayAsync(ContextSearchQuery query, CancellationToken cancellationToken)
        => HandleCoreAsync(query, verifyContinuation: true, cancellationToken);

    private async Task<ContextSearchQueryResult> HandleCoreAsync(ContextSearchQuery query, bool verifyContinuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(ContextSearchResultCode.AuthenticationRequired, query);
        }

        // A missing folder/workspace is an absence, surfaced as the safe-denial shape (never "this folder exists").
        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            return SafeResult(ContextSearchResultCode.NotFoundSafe, query);
        }

        // Layered authorization BEFORE any egress: JWT -> claim transform -> tenant-access freshness -> folder ACL ->
        // EventStore validator -> Dapr deny-by-default. The caller targets the `folders` domain service; the
        // folders -> memories egress is governed separately by the production Dapr access-control allow-rule.
        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                query.AuthoritativeTenantId,
                query.PrincipalId,
                ActorPresentIdentifier,
                ActionToken,
                LayeredFolderOperationPolicy.StrictRead(),
                query.ClaimTransformEvidence,
                OperationScope: query.FolderId,
                query.CorrelationId,
                query.TaskId,
                query.ClientControlledTenantValues,
                query.ClientControlledPrincipalValues),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return SafeResult(MapAuthorizationDenial(authorization), query, authorization);
        }

        ContextSearchResultCode? boundsFailure = ValidateC4Bounds(query);
        if (boundsFailure is not null)
        {
            return SafeResult(boundsFailure.Value, query);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        int limit = EffectiveLimit(query);
        int offset = ParseOffset(query.Cursor);
        string organizationId = allowed.OrganizationId ?? string.Empty;
        if (organizationId.Length == 0)
        {
            return SafeResult(ContextSearchResultCode.ReadModelUnavailable, query);
        }

        // Authorization first: never probe bridge health or Memories before the layered allow. After allow,
        // an unavailable authoritative bridge must fail closed before any index egress (HTTP 503).
        if (!_bridgeReadModel.IsAvailable)
        {
            return SafeResult(ContextSearchResultCode.ReadModelUnavailable, query);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        FolderSearchSourceRequest sourceRequest = new(allowed.AuthoritativeTenantId, organizationId, query.FolderId,
            query.WorkspaceId, query.PrincipalId, ActionToken, query.TaskId, query.CorrelationId,
            allowed.FreshnessWatermark, query.QueryText!, limit, offset);
        FolderSearchSourceResult sourceResult;
        try
        {
            sourceResult = await _source.SearchAsync(sourceRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Context search source failed safely. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(ContextSearchResultCode.ReadModelUnavailable, query);
        }

        ContextSearchResultCode? sourceFailure = sourceResult.Status switch
        {
            FolderSearchSourceStatus.Available => null,
            FolderSearchSourceStatus.Timeout => ContextSearchResultCode.QueryTimeout,
            _ => ContextSearchResultCode.ReadModelUnavailable,
        };
        if (sourceFailure is not null)
        {
            return SafeResult(sourceFailure.Value, query);
        }

        List<ContextSearchItem> items = [];
        HashSet<string> emittedFileVersions = new(StringComparer.Ordinal);
        HashSet<string> rejectedFileVersions = new(StringComparer.Ordinal);
        int duplicateRows = 0;
        int observedSourceRows = 0;
        foreach (FolderSearchSourceHit hit in sourceResult.Hits)
        {
            if (observedSourceRows >= limit || items.Count >= limit)
            {
                break;
            }

            observedSourceRows++;

            // Security-trim (defense in depth): a poisoned/stale index could echo a foreign hit despite the
            // server-side attribute filter; drop anything whose recovered identity is not the authorized scope.
            if (!MatchesScope(hit, sourceRequest))
            {
                continue;
            }

            // The index can return several rows for one file version. Within a page, the first row that passes every
            // check represents it, and later rows advance the source offset without adding an item. The cursor is a
            // raw source offset, so a later page can return the same file version again.
            if (emittedFileVersions.Contains(hit.FileVersionId))
            {
                duplicateRows++;
                continue;
            }

            // Hydration depends only on the file version within this query, so a rejected version is not read again.
            if (rejectedFileVersions.Contains(hit.FileVersionId))
            {
                continue;
            }

            // Authoritative hydration: drop hits without a current bridge entry, in a non-live state, or whose
            // hydrated identity disagrees with the recovered hit (the bridge read, not the index, is the truth).
            ContextSearchItem? item;
            try
            {
                item = await HydrateAsync(hit, sourceRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Context search hydration failed safely. Exception type: {ExceptionType}",
                    ex.GetType().FullName);
                return SafeResult(ContextSearchResultCode.ReadModelUnavailable, query);
            }

            if (item is null)
            {
                rejectedFileVersions.Add(hit.FileVersionId);
                continue;
            }

            items.Add(item);
            emittedFileVersions.Add(hit.FileVersionId);
        }

        int rawRowsAdvanced = Math.Max(
            observedSourceRows,
            Math.Clamp(sourceResult.RawCount, 0, limit));
        int nextOffset = offset > int.MaxValue - rawRowsAdvanced
            ? int.MaxValue
            : offset + rawRowsAdvanced;
        // A duplicate of an emitted item hides nothing from the caller, so it counts toward a full page. Rows dropped
        // by trimming or hydration do not, which keeps their existence out of the pagination metadata.
        bool hasMore = nextOffset > offset && sourceResult.TotalCount > nextOffset
            && items.Count + duplicateRows >= limit;
        if (verifyContinuation && hasMore)
        {
            (ContextSearchResultCode code, hasMore) = await ProbeContinuationAsync(
                sourceRequest with { Offset = nextOffset }, emittedFileVersions,
                _gatewaySearchBudget - stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
            if (code != ContextSearchResultCode.Allowed)
            {
                return SafeResult(code, query);
            }
        }
        stopwatch.Stop();
        string? nextCursor = hasMore ? BuildCursor(nextOffset) : null;
        DateTimeOffset observedAt = _clock.UtcNow;
        if (verifyContinuation)
        {
            // Keep the public byte estimate stable even when the bounded probe delays the response.
            observedAt = DateTimeOffset.FromUnixTimeSeconds(observedAt.ToUnixTimeSeconds());
        }
        FolderLifecycleFreshness freshness = new(
            EventuallyConsistent,
            observedAt,
            allowed.FreshnessWatermark,
            Stale: false,
            "search_index");
        string truncatedReason = hasMore ? TruncatedResultCount : NotTruncated;
        long actualBytes = EstimateResponseBytes(items, nextCursor, freshness, limit, hasMore, truncatedReason, actualBytes: 0);
        actualBytes = EstimateResponseBytes(items, nextCursor, freshness, limit, hasMore, truncatedReason, actualBytes);
        if (actualBytes > MaxResponseBytes)
        {
            return SafeResult(ContextSearchResultCode.ResponseLimitExceeded, query);
        }

        return new ContextSearchQueryResult(
            ContextSearchResultCode.Allowed,
            items,
            nextCursor,
            new ContextSearchLimits(
                QueryFamily,
                limit,
                items.Count,
                actualBytes,
                verifyContinuation ? 0 : stopwatch.ElapsedMilliseconds,
                hasMore,
                truncatedReason),
            freshness,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private async Task<(ContextSearchResultCode Code, bool HasMore)> ProbeContinuationAsync(
        FolderSearchSourceRequest request, HashSet<string> emitted, TimeSpan remaining, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (remaining <= TimeSpan.Zero)
        {
            return (ContextSearchResultCode.QueryTimeout, false);
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining);
        try
        {
            // Verify the same bounded raw window a following request will read. Raw totals never authorize a cursor.
            FolderSearchSourceResult page = await _source.SearchAsync(request, deadline.Token)
                .WaitAsync(deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (page.Status != FolderSearchSourceStatus.Available)
            {
                return (page.Status == FolderSearchSourceStatus.Timeout
                    ? ContextSearchResultCode.QueryTimeout : ContextSearchResultCode.ReadModelUnavailable, false);
            }

            HashSet<string> seen = new(emitted, StringComparer.Ordinal);
            foreach (FolderSearchSourceHit hit in page.Hits.Take(request.Limit))
            {
                if (MatchesScope(hit, request) && seen.Add(hit.FileVersionId)
                    && await HydrateAsync(hit, request, deadline.Token)
                        .WaitAsync(deadline.Token).ConfigureAwait(false) is not null)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    return (ContextSearchResultCode.Allowed, true);
                }
            }

            deadline.Token.ThrowIfCancellationRequested();
            return (ContextSearchResultCode.Allowed, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogWarning("Context search continuation could not be verified. Exception type: {ExceptionType}", ex.GetType().FullName);
            return (deadline.IsCancellationRequested || ex is TimeoutException
                ? ContextSearchResultCode.QueryTimeout : ContextSearchResultCode.ReadModelUnavailable, false);
        }
    }

    private static bool MatchesScope(FolderSearchSourceHit hit, FolderSearchSourceRequest request)
        => hit.ManagedTenantId == request.ManagedTenantId && hit.FolderId == request.FolderId
            && hit.WorkspaceId == request.WorkspaceId && hit.OrganizationId == request.OrganizationId;

    private async Task<ContextSearchItem?> HydrateAsync(FolderSearchSourceHit hit, FolderSearchSourceRequest request,
        CancellationToken cancellationToken)
    {
        SemanticIndexingBridgeEntry? entry = await _bridgeReadModel.GetFileVersionByIdAsync(
            request.ManagedTenantId, request.FolderId, hit.FileVersionId, cancellationToken).ConfigureAwait(false);
        return entry is not null && IsVisible(entry.Status) && entry.Identity.ManagedTenantId == request.ManagedTenantId
            && entry.Identity.OrganizationId == request.OrganizationId && entry.Identity.FolderId == request.FolderId
            && entry.Identity.WorkspaceId == request.WorkspaceId && entry.Identity.FileVersionId == hit.FileVersionId
            ? MapItem(hit, entry) : null;
    }

    private static ContextSearchResultCode? ValidateC4Bounds(ContextSearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            return ContextSearchResultCode.ValidationFailed;
        }

        if (query.Limit is <= 0)
        {
            return ContextSearchResultCode.ValidationFailed;
        }

        if (query.QueryText.Length > MaxQueryTextLength
            || query.Limit > MaxResultCount
            || query.Cursor is { Length: > MaxCursorLength })
        {
            return ContextSearchResultCode.InputLimitExceeded;
        }

        if (query.Cursor is not null && !TryParseCursor(query.Cursor, out _))
        {
            return ContextSearchResultCode.ValidationFailed;
        }

        return null;
    }

    private static int EffectiveLimit(ContextSearchQuery query)
        => query.Limit is > 0
            ? Math.Min(query.Limit.Value, MaxResultCount)
            : MaxResultCount;

    private static bool IsVisible(SemanticIndexingBridgeStatus status)
        => status is SemanticIndexingBridgeStatus.Indexed;

    private static ContextSearchItem MapItem(FolderSearchSourceHit hit, SemanticIndexingBridgeEntry entry)
    {
        bool redacted = IsSensitive(entry.Evidence.PathPolicyClass);
        return new ContextSearchItem(
            hit.FileVersionId,
            entry.StatusCode,
            redacted ? SensitivityRestricted : SensitivityTenant,
            redacted ? RedactionRedacted : RedactionNone,
            hit.Score);
    }

    // Mirrors WorkspaceFileSensitivityClassifier: a path-policy class flagged secret/credential/redacted yields a
    // visibly-distinct redacted marker (redaction is never silently hidden), without exposing the path itself.
    private static bool IsSensitive(string? pathPolicyClass)
        => pathPolicyClass is not null
            && (pathPolicyClass.Contains("secret", StringComparison.Ordinal)
                || pathPolicyClass.Contains("credential", StringComparison.Ordinal)
                || pathPolicyClass.Contains("redacted", StringComparison.Ordinal));

    private static long EstimateResponseBytes(
        IReadOnlyList<ContextSearchItem> items,
        string? nextCursor,
        FolderLifecycleFreshness freshness,
        int limit,
        bool hasMore,
        string truncatedReason,
        long actualBytes)
        => JsonSerializer.SerializeToUtf8Bytes(
            new ContextSearchResponseBudgetProbe(
                items,
                nextCursor,
                new ContextSearchLimits(
                    QueryFamily,
                    limit,
                    items.Count,
                    actualBytes,
                    0,
                    hasMore,
                    truncatedReason),
                freshness),
            ResponseBudgetJsonOptions).Length;

    private sealed record ContextSearchResponseBudgetProbe(
        IReadOnlyList<ContextSearchItem> Items,
        string? NextCursor,
        ContextSearchLimits Limits,
        FolderLifecycleFreshness Freshness);

    private static int ParseOffset(string? cursor)
    {
        _ = TryParseCursor(cursor, out int offset);
        return offset;
    }

    /// <summary>Validates the existing index search cursor for both query transports.</summary>
    internal static bool TryParseCursor(string? cursor, out int offset)
    {
        offset = 0;
        return cursor is null
            || (!string.IsNullOrWhiteSpace(cursor)
                && cursor.StartsWith(CursorPrefix, StringComparison.Ordinal)
                && int.TryParse(cursor[CursorPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out offset)
                && offset >= 0);
    }

    private static string BuildCursor(int offset)
        => CursorPrefix + offset.ToString(CultureInfo.InvariantCulture);

    private ContextSearchQueryResult SafeResult(
        ContextSearchResultCode code,
        ContextSearchQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial = null)
        => new(
            code,
            [],
            NextCursor: null,
            new ContextSearchLimits(QueryFamily, EffectiveLimit(query), 0, 0, 0, false, NotTruncated),
            new FolderLifecycleFreshness(EventuallyConsistent, _clock.UtcNow, null, Stale: true, "denied_safe"),
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static ContextSearchResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => ContextSearchResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound
                or LayeredAuthorizationOutcomeCodes.FolderAclDenied => ContextSearchResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => ContextSearchResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => ContextSearchResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => ContextSearchResultCode.AuthorizationDenied,
            _ => ContextSearchResultCode.ReadModelUnavailable,
        };
}
