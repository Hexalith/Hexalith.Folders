using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.FileContext;

public sealed class WorkspaceFileContextQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IWorkspaceFileContextSource source,
    IWorkspaceFileSensitivityClassifier sensitivityClassifier,
    IUtcClock clock,
    ILogger<WorkspaceFileContextQueryHandler>? logger = null)
{
    public const string MetadataActionToken = "read_metadata";
    public const string ContentActionToken = "read_file_content";

    private const string ActorPresentIdentifier = "actor_present";
    private const string SnapshotPerTask = "snapshot_per_task";
    private const int MaxPathCount = 100;
    private const int MaxTreeResultCount = 2000;
    private const int MaxSearchResultCount = 500;
    private const int MaxQueryTextLength = 256;
    private const int MaxCursorLength = 256;
    private const long MaxRangeBytes = 262144;
    private const long MaxResponseBytes = 1048576;

    private readonly ILogger<WorkspaceFileContextQueryHandler> _logger =
        logger ?? NullLogger<WorkspaceFileContextQueryHandler>.Instance;
    private readonly IUtcClock _clock = clock;

    public async Task<WorkspaceFileContextQueryResult> HandleAsync(
        WorkspaceFileContextQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query.AuthoritativeTenantId)
            || string.IsNullOrWhiteSpace(query.PrincipalId))
        {
            return SafeResult(WorkspaceFileContextResultCode.AuthenticationRequired, query);
        }

        if (string.IsNullOrWhiteSpace(query.FolderId) || string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            return SafeResult(WorkspaceFileContextResultCode.NotFoundSafe, query);
        }

        string actionToken = query.Kind == WorkspaceFileContextQueryKind.Range
            ? ContentActionToken
            : MetadataActionToken;

        LayeredFolderAuthorizationResult authorization = await authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                query.AuthoritativeTenantId,
                query.PrincipalId,
                ActorPresentIdentifier,
                actionToken,
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

        PathPreparationResult pathPreparation = await PreparePathsAsync(query, cancellationToken).ConfigureAwait(false);
        if (pathPreparation.Code is not null)
        {
            return SafeResult(pathPreparation.Code.Value, query);
        }

        WorkspaceFileContextResultCode? boundsFailure = ValidateC4Bounds(query);
        if (boundsFailure is not null)
        {
            return SafeResult(boundsFailure.Value, query);
        }

        int limit = EffectiveLimit(query);
        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        WorkspaceFileContextSourceResult sourceResult;
        try
        {
            sourceResult = await source.QueryAsync(
                new WorkspaceFileContextSourceRequest(
                    query.Kind,
                    allowed.AuthoritativeTenantId,
                    query.FolderId,
                    query.WorkspaceId,
                    query.PrincipalId,
                    actionToken,
                    query.TaskId,
                    query.CorrelationId,
                    allowed.FreshnessWatermark,
                    pathPreparation.Paths,
                    query.QueryText,
                    query.GlobPattern,
                    limit,
                    query.Cursor,
                    query.StartOffset,
                    query.EndOffset),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Workspace file context source failed safely. Exception type: {ExceptionType}",
                ex.GetType().FullName);
            return SafeResult(WorkspaceFileContextResultCode.ReadModelUnavailable, query);
        }

        return MapSourceResult(query, sourceResult);
    }

    private async Task<PathPreparationResult> PreparePathsAsync(
        WorkspaceFileContextQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PathMetadata> paths = query.Kind == WorkspaceFileContextQueryKind.Range
            ? query.Paths ?? []
            : query.Paths ?? [];

        if (query.Kind == WorkspaceFileContextQueryKind.Range && paths.Count != 1)
        {
            return new(WorkspaceFileContextResultCode.ValidationFailed, []);
        }

        if (query.Kind == WorkspaceFileContextQueryKind.Metadata && paths.Count == 0)
        {
            return new(WorkspaceFileContextResultCode.ValidationFailed, []);
        }

        if (query.Kind is WorkspaceFileContextQueryKind.Search or WorkspaceFileContextQueryKind.Glob
            && query.Paths is not null
            && paths.Count == 0)
        {
            return new(WorkspaceFileContextResultCode.ValidationFailed, []);
        }

        if (paths.Count > MaxPathCount)
        {
            return new(WorkspaceFileContextResultCode.InputLimitExceeded, []);
        }

        List<WorkspaceFileContextQueryPath> prepared = new(paths.Count);
        foreach (PathMetadata path in paths)
        {
            WorkspacePathPolicyResult pathPolicy = WorkspacePathPolicyValidator.Validate(path);
            if (!pathPolicy.IsAccepted)
            {
                return new(WorkspaceFileContextResultCode.PathValidationFailed, []);
            }

            WorkspacePathSensitivityResult sensitivity = await sensitivityClassifier
                .ClassifyAsync(path, cancellationToken)
                .ConfigureAwait(false);

            if (sensitivity.Decision == WorkspaceFileSensitivityDecision.Unavailable)
            {
                return new(WorkspaceFileContextResultCode.ReadModelUnavailable, []);
            }

            if (sensitivity.Decision == WorkspaceFileSensitivityDecision.Redacted)
            {
                return new(WorkspaceFileContextResultCode.Redacted, []);
            }

            prepared.Add(new(
                path,
                pathPolicy.PathMetadataDigest!,
                pathPolicy.PathPolicyClass!,
                sensitivity.Sensitivity,
                sensitivity.Redaction));
        }

        return new(null, prepared);
    }

    private static WorkspaceFileContextResultCode? ValidateC4Bounds(WorkspaceFileContextQuery query)
    {
        if (query.Cursor is { Length: > MaxCursorLength })
        {
            return WorkspaceFileContextResultCode.InputLimitExceeded;
        }

        if (query.Limit is <= 0)
        {
            return WorkspaceFileContextResultCode.ValidationFailed;
        }

        if (query.Kind is WorkspaceFileContextQueryKind.Search)
        {
            if (string.IsNullOrWhiteSpace(query.QueryText) || query.Limit is null)
            {
                return WorkspaceFileContextResultCode.ValidationFailed;
            }

            if (query.QueryText.Length > MaxQueryTextLength || query.Limit > MaxSearchResultCount)
            {
                return WorkspaceFileContextResultCode.InputLimitExceeded;
            }
        }

        if (query.Kind is WorkspaceFileContextQueryKind.Glob)
        {
            if (string.IsNullOrWhiteSpace(query.GlobPattern) || query.Limit is null)
            {
                return WorkspaceFileContextResultCode.ValidationFailed;
            }

            if (query.GlobPattern.Length > MaxQueryTextLength || query.Limit > MaxSearchResultCount)
            {
                return WorkspaceFileContextResultCode.InputLimitExceeded;
            }
        }

        if (query.Kind is WorkspaceFileContextQueryKind.Tree && query.Limit > MaxTreeResultCount)
        {
            return WorkspaceFileContextResultCode.InputLimitExceeded;
        }

        if (query.Kind is WorkspaceFileContextQueryKind.Range)
        {
            if (query.StartOffset is null || query.EndOffset is null || query.StartOffset < 0 || query.EndOffset < 0)
            {
                return WorkspaceFileContextResultCode.ValidationFailed;
            }

            if (query.EndOffset < query.StartOffset)
            {
                return WorkspaceFileContextResultCode.ValidationFailed;
            }

            if (query.EndOffset.Value - query.StartOffset.Value > MaxRangeBytes)
            {
                return WorkspaceFileContextResultCode.InputLimitExceeded;
            }
        }

        return null;
    }

    private static int EffectiveLimit(WorkspaceFileContextQuery query)
    {
        int configured = query.Kind switch
        {
            WorkspaceFileContextQueryKind.Search or WorkspaceFileContextQueryKind.Glob => MaxSearchResultCount,
            WorkspaceFileContextQueryKind.Metadata => MaxPathCount,
            WorkspaceFileContextQueryKind.Range => 1,
            _ => MaxTreeResultCount,
        };

        return Math.Min(query.Limit ?? configured, configured);
    }

    private WorkspaceFileContextQueryResult MapSourceResult(
        WorkspaceFileContextQuery query,
        WorkspaceFileContextSourceResult sourceResult)
    {
        WorkspaceFileContextResultCode code = sourceResult.Status switch
        {
            WorkspaceFileContextSourceStatus.Available => WorkspaceFileContextResultCode.Allowed,
            WorkspaceFileContextSourceStatus.Stale => WorkspaceFileContextResultCode.ProjectionStale,
            WorkspaceFileContextSourceStatus.Timeout => WorkspaceFileContextResultCode.QueryTimeout,
            WorkspaceFileContextSourceStatus.InputLimitExceeded => WorkspaceFileContextResultCode.InputLimitExceeded,
            WorkspaceFileContextSourceStatus.ResponseLimitExceeded => WorkspaceFileContextResultCode.ResponseLimitExceeded,
            WorkspaceFileContextSourceStatus.Redacted
                or WorkspaceFileContextSourceStatus.BinaryDisallowed
                or WorkspaceFileContextSourceStatus.LargeFileDisallowed => WorkspaceFileContextResultCode.Redacted,
            WorkspaceFileContextSourceStatus.RangeUnsatisfiable => WorkspaceFileContextResultCode.RangeUnsatisfiable,
            _ => WorkspaceFileContextResultCode.ReadModelUnavailable,
        };

        if (code != WorkspaceFileContextResultCode.Allowed)
        {
            return SafeResult(code, query);
        }

        if (sourceResult.Limits.ActualBytes > MaxResponseBytes
            || sourceResult.Items.Count > EffectiveLimit(query)
            || (query.Kind == WorkspaceFileContextQueryKind.Range && sourceResult.Range?.ActualBytes > MaxRangeBytes))
        {
            return SafeResult(WorkspaceFileContextResultCode.ResponseLimitExceeded, query);
        }

        if (query.Kind == WorkspaceFileContextQueryKind.Range
            && (sourceResult.Range is null
                || sourceResult.RangePath is null
                || sourceResult.ContentBytes is null))
        {
            return SafeResult(WorkspaceFileContextResultCode.ReadModelUnavailable, query);
        }

        if (query.Kind == WorkspaceFileContextQueryKind.Range
            && query.StartOffset is not null
            && query.EndOffset is not null
            && sourceResult.Range is not null)
        {
            long requestedBytes = query.EndOffset.Value - query.StartOffset.Value;
            if (sourceResult.Range.StartOffset != query.StartOffset.Value
                || sourceResult.Range.EndOffset != query.EndOffset.Value
                || sourceResult.Range.ActualBytes < 0
                || sourceResult.Range.ActualBytes > requestedBytes)
            {
                return SafeResult(WorkspaceFileContextResultCode.ResponseLimitExceeded, query);
            }

            if ((sourceResult.Range.Partial && sourceResult.Range.ActualBytes >= requestedBytes)
                || (!sourceResult.Range.Partial && sourceResult.Range.ActualBytes != requestedBytes))
            {
                return SafeResult(WorkspaceFileContextResultCode.ReadModelUnavailable, query);
            }
        }

        return new(
            WorkspaceFileContextResultCode.Allowed,
            query.Kind,
            query.Kind == WorkspaceFileContextQueryKind.Range ? [] : sourceResult.Items,
            sourceResult.RangePath,
            sourceResult.Range,
            sourceResult.ContentBytes,
            sourceResult.Page,
            sourceResult.Limits,
            sourceResult.Freshness,
            query.CorrelationId,
            query.TaskId,
            AuthorizationDenial: null);
    }

    private WorkspaceFileContextQueryResult SafeResult(
        WorkspaceFileContextResultCode code,
        WorkspaceFileContextQuery query,
        LayeredFolderAuthorizationResult? authorizationDenial = null)
        => new(
            code,
            query.Kind,
            [],
            null,
            null,
            null,
            null,
            new WorkspaceFileContextLimits(
                QueryFamily(query.Kind),
                EffectiveLimit(query),
                0,
                0,
                0,
                false,
                "not_truncated"),
            new(SnapshotPerTask, _clock.UtcNow, null, Stale: true, "denied_safe"),
            query.CorrelationId,
            query.TaskId,
            authorizationDenial);

    private static WorkspaceFileContextResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => WorkspaceFileContextResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => WorkspaceFileContextResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => WorkspaceFileContextResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => WorkspaceFileContextResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => WorkspaceFileContextResultCode.AuthorizationDenied,
            _ => WorkspaceFileContextResultCode.ReadModelUnavailable,
        };

    private static string QueryFamily(WorkspaceFileContextQueryKind kind)
        => kind switch
        {
            WorkspaceFileContextQueryKind.Tree => "tree",
            WorkspaceFileContextQueryKind.Metadata => "metadata",
            WorkspaceFileContextQueryKind.Search => "search",
            WorkspaceFileContextQueryKind.Glob => "glob",
            WorkspaceFileContextQueryKind.Range => "range",
            _ => "metadata",
        };

    private sealed record PathPreparationResult(
        WorkspaceFileContextResultCode? Code,
        IReadOnlyList<WorkspaceFileContextQueryPath> Paths);
}
