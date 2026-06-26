using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// Handles the read-only, metadata-only indexing-status projection for a tenant-scoped folder, backing the console
/// page. Runs the full layered authorization (folder ACL) before reading, then maps every authoritative bridge
/// entry to a metadata-only status item — no content, snippet, raw path, or source URI ever crosses the boundary.
/// Denied, cross-tenant, and absent targets collapse to the same safe-denial shape.
/// </summary>
public sealed class FolderIndexingStatusQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    ISemanticIndexingBridgeReadModel bridgeReadModel,
    IUtcClock clock,
    ILogger<FolderIndexingStatusQueryHandler>? logger = null)
{
    private const string EventuallyConsistent = "eventually_consistent";
    private const string ActorPresentIdentifier = "actor_present";
    private const string SensitivityTenant = "tenant_sensitive";
    private const string SensitivityRestricted = "restricted";
    private const string RedactionNone = "not_redacted";
    private const string RedactionRedacted = "redacted";
    private const string UnknownReasonCode = "unknown";
    private const int MaxItems = 500;

    private readonly LayeredFolderAuthorizationService _authorizationService = authorizationService;
    private readonly ISemanticIndexingBridgeReadModel _bridgeReadModel = bridgeReadModel;
    private readonly IUtcClock _clock = clock;
    private readonly ILogger<FolderIndexingStatusQueryHandler> _logger =
        logger ?? NullLogger<FolderIndexingStatusQueryHandler>.Instance;

    /// <summary>Executes the authorized, metadata-only indexing-status projection read.</summary>
    public async Task<FolderIndexingStatusQueryResult> HandleAsync(
        FolderIndexingStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(FolderIndexingStatusResultCode.AuthenticationRequired, query);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId))
        {
            return SafeResult(FolderIndexingStatusResultCode.NotFoundSafe, query);
        }

        LayeredFolderAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                query.AuthoritativeTenantId,
                query.PrincipalId,
                ActorPresentIdentifier,
                ContextSearchQueryHandler.ActionToken,
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
        if (!_bridgeReadModel.IsAvailable)
        {
            return SafeResult(FolderIndexingStatusResultCode.ReadModelUnavailable, query);
        }

        IReadOnlyList<SemanticIndexingBridgeEntry> entries;
        try
        {
            entries = await _bridgeReadModel
                .ListFolderAsync(allowed.AuthoritativeTenantId, query.FolderId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Indexing-status read failed safely. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(FolderIndexingStatusResultCode.ReadModelUnavailable, query);
        }

        bool truncated = entries.Count > MaxItems;
        List<FolderIndexingStatusItem> items = new(Math.Min(entries.Count, MaxItems));
        foreach (SemanticIndexingBridgeEntry entry in entries
            .OrderByDescending(static entry => StatusPriority(entry.Status))
            .ThenBy(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal)
            .Take(MaxItems))
        {
            bool redacted = IsSensitive(entry.Evidence.PathPolicyClass);
            items.Add(new FolderIndexingStatusItem(
                entry.Identity.FileVersionId,
                entry.StatusCode,
                SafeReasonCode(entry.ReasonCode),
                redacted ? SensitivityRestricted : SensitivityTenant,
                redacted ? RedactionRedacted : RedactionNone));
        }

        return new FolderIndexingStatusQueryResult(
            FolderIndexingStatusResultCode.Allowed,
            items,
            truncated,
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

    private static bool IsSensitive(string? pathPolicyClass)
        => pathPolicyClass is not null
            && (pathPolicyClass.Contains("secret", StringComparison.Ordinal)
                || pathPolicyClass.Contains("credential", StringComparison.Ordinal)
                || pathPolicyClass.Contains("redacted", StringComparison.Ordinal));

    private static int StatusPriority(SemanticIndexingBridgeStatus status)
        => status switch
        {
            SemanticIndexingBridgeStatus.Failed => 5,
            SemanticIndexingBridgeStatus.ReconciliationRequired => 4,
            SemanticIndexingBridgeStatus.Tombstoned => 3,
            SemanticIndexingBridgeStatus.Stale => 2,
            SemanticIndexingBridgeStatus.Skipped => 1,
            _ => 0,
        };

    private static string SafeReasonCode(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128)
        {
            return UnknownReasonCode;
        }

        foreach (char ch in reasonCode)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not '_' and not '-' and not '.')
            {
                return UnknownReasonCode;
            }
        }

        return reasonCode;
    }

    private FolderIndexingStatusQueryResult SafeResult(
        FolderIndexingStatusResultCode code,
        FolderIndexingStatusQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial = null)
        => new(
            code,
            [],
            IsTruncated: false,
            new FolderLifecycleFreshness(EventuallyConsistent, _clock.UtcNow, null, Stale: true, "denied_safe"),
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static FolderIndexingStatusResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => FolderIndexingStatusResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound
                or LayeredAuthorizationOutcomeCodes.FolderAclDenied => FolderIndexingStatusResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => FolderIndexingStatusResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => FolderIndexingStatusResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => FolderIndexingStatusResultCode.AuthorizationDenied,
            _ => FolderIndexingStatusResultCode.ReadModelUnavailable,
        };
}
