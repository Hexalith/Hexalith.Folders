using System.Diagnostics;
using System.Globalization;

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

    private readonly LayeredFolderAuthorizationService _authorizationService = authorizationService;
    private readonly IFolderSearchSource _source = source;
    private readonly ISemanticIndexingBridgeReadModel _bridgeReadModel = bridgeReadModel;
    private readonly IUtcClock _clock = clock;
    private readonly ILogger<ContextSearchQueryHandler> _logger =
        logger ?? NullLogger<ContextSearchQueryHandler>.Instance;

    /// <summary>Executes the authorized, security-trimmed, metadata-only context search.</summary>
    public async Task<ContextSearchQueryResult> HandleAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken = default)
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

        ContextSearchResultCode? boundsFailure = ValidateC4Bounds(query);
        if (boundsFailure is not null)
        {
            return SafeResult(boundsFailure.Value, query);
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

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        int limit = EffectiveLimit(query);
        int offset = ParseOffset(query.Cursor);
        string organizationId = allowed.OrganizationId ?? string.Empty;

        Stopwatch stopwatch = Stopwatch.StartNew();

        FolderSearchSourceResult sourceResult;
        try
        {
            sourceResult = await _source.SearchAsync(
                new FolderSearchSourceRequest(
                    allowed.AuthoritativeTenantId,
                    organizationId,
                    query.FolderId,
                    query.WorkspaceId,
                    query.PrincipalId,
                    ActionToken,
                    query.TaskId,
                    query.CorrelationId,
                    allowed.FreshnessWatermark,
                    query.QueryText!,
                    limit,
                    offset),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
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

        // Hydrate from the authoritative Folders read: the index is non-authoritative and may be stale, so a hit only
        // survives if a current bridge entry exists for the same file version AND the entry is in a live indexed state.
        IReadOnlyList<SemanticIndexingBridgeEntry> folderEntries;
        try
        {
            folderEntries = await _bridgeReadModel
                .ListFolderAsync(allowed.AuthoritativeTenantId, query.FolderId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Context search hydration failed safely. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(ContextSearchResultCode.ReadModelUnavailable, query);
        }

        Dictionary<string, SemanticIndexingBridgeEntry> entriesByFileVersion = new(StringComparer.Ordinal);
        foreach (SemanticIndexingBridgeEntry entry in folderEntries)
        {
            entriesByFileVersion[entry.Identity.FileVersionId] = entry;
        }

        List<ContextSearchItem> items = [];
        long actualBytes = 0;
        foreach (FolderSearchSourceHit hit in sourceResult.Hits)
        {
            // Security-trim (defense in depth): a poisoned/stale index could echo a foreign hit despite the
            // server-side attribute filter; drop anything whose recovered identity is not the authorized scope.
            if (!string.Equals(hit.ManagedTenantId, allowed.AuthoritativeTenantId, StringComparison.Ordinal)
                || !string.Equals(hit.FolderId, query.FolderId, StringComparison.Ordinal)
                || !string.Equals(hit.WorkspaceId, query.WorkspaceId, StringComparison.Ordinal)
                || (organizationId.Length > 0 && !string.Equals(hit.OrganizationId, organizationId, StringComparison.Ordinal)))
            {
                continue;
            }

            // Authoritative hydration: drop hits without a current bridge entry, in a non-live state, or whose
            // hydrated identity disagrees with the recovered hit (the bridge read, not the index, is the truth).
            if (!entriesByFileVersion.TryGetValue(hit.FileVersionId, out SemanticIndexingBridgeEntry? entry)
                || !IsVisible(entry.Status)
                || !string.Equals(entry.Identity.WorkspaceId, query.WorkspaceId, StringComparison.Ordinal))
            {
                continue;
            }

            ContextSearchItem item = MapItem(hit, entry);
            actualBytes += EstimateBytes(item);
            if (actualBytes > MaxResponseBytes)
            {
                return SafeResult(ContextSearchResultCode.ResponseLimitExceeded, query);
            }

            items.Add(item);
        }

        stopwatch.Stop();

        int nextOffset = offset + sourceResult.Hits.Count;
        bool hasMore = sourceResult.TotalCount > nextOffset;

        return new ContextSearchQueryResult(
            ContextSearchResultCode.Allowed,
            items,
            hasMore ? BuildCursor(nextOffset) : null,
            new ContextSearchLimits(
                QueryFamily,
                limit,
                items.Count,
                actualBytes,
                stopwatch.ElapsedMilliseconds,
                hasMore,
                hasMore ? TruncatedResultCount : NotTruncated),
            new FolderLifecycleFreshness(
                EventuallyConsistent,
                _clock.UtcNow,
                allowed.FreshnessWatermark,
                Stale: false,
                "search_index"),
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
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

        return null;
    }

    private static int EffectiveLimit(ContextSearchQuery query)
        => Math.Min(query.Limit ?? MaxResultCount, MaxResultCount);

    private static bool IsVisible(SemanticIndexingBridgeStatus status)
        => status is SemanticIndexingBridgeStatus.Indexed or SemanticIndexingBridgeStatus.Stale;

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

    private static long EstimateBytes(ContextSearchItem item)
        => item.FileVersionReference.Length
            + item.IndexingStatus.Length
            + item.Sensitivity.Length
            + item.Redaction.Length
            + 24;

    private static int ParseOffset(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)
            || !cursor.StartsWith(CursorPrefix, StringComparison.Ordinal)
            || !int.TryParse(cursor[CursorPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int offset)
            || offset < 0)
        {
            return 0;
        }

        return offset;
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
