using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Observability;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;

namespace Hexalith.Folders.Queries.Gateway;

/// <summary>Enforces caller authority, request shape and metadata-only results for public Folders queries.</summary>
internal sealed partial class GatewayQueryExecutionSupport(
    IHttpContextAccessor httpContextAccessor,
    LayeredFolderAuthorizationService authorization,
    EventStoreOperationalStatusReadModel operations,
    ILogger<GatewayQueryExecutionSupport> logger)
{
    /// <summary>The fixed browsing consistency class.</summary>
    internal const string EventuallyConsistent = "eventually_consistent";

    /// <summary>The fixed post-mutation consistency class.</summary>
    internal const string ReadYourWrites = "read_your_writes";

    /// <summary>The fixed task snapshot consistency class.</summary>
    internal const string SnapshotPerTask = "snapshot_per_task";

    private static readonly JsonSerializerOptions _requestJson = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions _responseJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The shared non-disclosing denial.</summary>
    internal static QueryResult Denied => QueryResult.Failure("safe-denial");

    /// <summary>The retryable infrastructure failure understood by EventStore.</summary>
    internal static QueryResult Unavailable => QueryResult.Failure(QueryAdapterFailureReason.ReadModelUnavailable);

    /// <summary>The shared validation failure for incompatible query inputs.</summary>
    internal static QueryResult InvalidEnvelope => QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);

    /// <summary>The shared validation failure for incompatible cursors.</summary>
    internal static QueryResult InvalidCursor => QueryResult.Failure(QueryAdapterFailureReason.InvalidCursor);

    /// <summary>Returns verified claim evidence for the authorized in-process handler.</summary>
    internal static EventStoreClaimTransformEvidence Evidence(QueryEnvelope query, string? authorizedHistoricalAction = null)
        => EventStoreClaimTransformEvidence.Allowed(query.TenantId, query.UserId,
            authorizedHistoricalAction is null ? query.CallerAuthority!.Permissions : [authorizedHistoricalAction]);

    /// <summary>Deserializes a v2 query, enforcing required fields, bounds and exact enum tokens.</summary>
    internal static T? Deserialize<T>(byte[] bytes) where T : class
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || ContainsNull(root))
        {
            return null;
        }

        T? payload = root.Deserialize<T>(_requestJson);
        if (payload is null || !Validator.TryValidateObject(payload, new ValidationContext(payload), null, validateAllProperties: true))
        {
            return null;
        }

        // Decorated enums use a permissive converter. Compare their wire tokens with canonical serialization.
        JsonElement canonical = JsonSerializer.SerializeToElement(payload, _requestJson);
        return typeof(T).GetProperties().Where(property =>
                (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType).IsEnum)
            .All(property =>
            {
                string name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                return !root.TryGetProperty(name, out JsonElement supplied)
                    || supplied.ValueKind == JsonValueKind.String && supplied.GetString() == canonical.GetProperty(name).GetString();
            })
            ? payload : null;
    }

    /// <summary>Checks for the SDK's verified authority for this exact callback and action.</summary>
    internal bool HasVerifiedCaller(QueryEnvelope query, string action)
    {
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(httpContextAccessor.HttpContext);
        return caller?.Query is { Domain: "folders", CallerAuthority: { Origin: "authenticated-caller" } authority } verified
            && ReferenceEquals(verified, query)
            // The public SDK binding includes the originator's permissions only. It cannot prove both grants.
            && !query.IsDelegated && query.DelegationId is null
            && authority.Permissions.Contains(action, StringComparer.Ordinal)
            && query.TenantId.Length == query.TenantId.AsSpan().Trim().Length
            && query.UserId.Length == query.UserId.AsSpan().Trim().Length
            && FolderStreamName.TryValidateTenant(query.TenantId, out _);
    }

    /// <summary>Checks for any idempotency header on a verified public read.</summary>
    internal bool HasIdempotencyKey()
        => httpContextAccessor.HttpContext is { } context && FolderHttpHeaderReader.HasReadIdempotencyKey(context, includeEmpty: true);

    /// <summary>Resolves the two paging inputs and rejects incompatible values before observation.</summary>
    internal static QueryResult? ResolvePage(QueryPagingOptions? envelope, string? cursor, int? limit, int max,
        out GatewayQueryPage page)
    {
        page = new(envelope?.Cursor ?? cursor, envelope?.PageSize ?? limit ?? max);
        if (envelope?.Offset is not null || page.Limit < 1 || page.Limit > max
            || limit is not null && envelope?.PageSize is not null && limit != envelope.PageSize)
        {
            return InvalidEnvelope;
        }

        return page.Cursor is not null && (string.IsNullOrWhiteSpace(page.Cursor) || page.Cursor.Length > 256)
            || cursor is not null && envelope?.Cursor is not null && cursor != envelope.Cursor
            ? InvalidCursor : null;
    }

    /// <summary>Enforces the declared resource scope and verifies fresh ACL authority before the protected read.</summary>
    internal async Task<QueryResult> ReadAsync(QueryEnvelope query, string target, string action, string historicalAction,
        bool providerScope, string? taskId, string? workspaceId, bool requiresCurrentTask, bool requiresCurrentWorkspace,
        Func<Task<QueryResult>> read, CancellationToken token)
    {
        if (providerScope)
        {
            if (query.AggregateId == query.TenantId
                || ProviderBindingActorId.IsReserved(target)
                || query.AggregateId != ProviderBindingActorId.Create(target))
            {
                return InvalidEnvelope;
            }
        }
        else if (query.AggregateId != target || !FolderStreamName.TryCreate(query.TenantId, target, out _, out _))
        {
            return Denied;
        }

        if (query.EntityId is { } entity && entity != query.AggregateId && entity != workspaceId && entity != taskId)
        {
            return InvalidEnvelope;
        }

        try
        {
            LayeredFolderAuthorizationResult result = await authorization.AuthorizeGatewayReadAsync(new(
                query.TenantId, query.UserId, "actor_present", action, LayeredFolderOperationPolicy.StrictRead(),
                Evidence(query), query.AggregateId, query.CorrelationId, taskId, null, null),
                includeFolderAcl: !providerScope, token).ConfigureAwait(false);
            if (!result.IsAllowed || result.AllowedContext is not { } allowed)
            {
                return result.Decision.Retryable ? Unavailable : Denied;
            }

            string? currentTask = requiresCurrentTask ? taskId : null;
            if ((currentTask is not null || requiresCurrentWorkspace) && !await operations.MatchesCurrentScopeAsync(
                query.TenantId, target, query.UserId, currentTask, workspaceId, token).ConfigureAwait(false))
            {
                return Denied;
            }

            PreauthorizedRequestContext.Begin(new(query.TenantId, query.UserId, query.AggregateId,
                allowed.FreshnessWatermark, allowed.OrganizationId, action, historicalAction, DelegatorPrincipalId: null)
            {
                TaskId = taskId,
                UsesGatewayQueryContract = true,
            });
            try
            {
                return await read().ConfigureAwait(false);
            }
            finally
            {
                PreauthorizedRequestContext.End();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            LogReadFailure(logger, exception.GetType().Name);
            return Unavailable;
        }
    }

    /// <summary>Builds safe successful metadata using the operation's fixed freshness class.</summary>
    internal static QueryResult Success<T>(T value, string projectionType, string consistency,
        FolderLifecycleFreshness freshness, QueryPagingMetadata? paging = null)
    {
        if (freshness.Stale)
        {
            return Unavailable;
        }

        var payload = (JsonObject)JsonSerializer.SerializeToNode(value, _responseJson)!;
        payload["freshness"] = JsonSerializer.SerializeToNode(freshness with { ReadConsistency = consistency }, _responseJson);
        Redact(payload, consistency);
        string? projectionVersion = payload["freshness"]?["projectionWatermark"]?.GetValue<string>();

        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(payload), projectionType,
            new QueryResponseMetadata(IsStale: false, ProjectionVersion: projectionVersion, Paging: paging)
            {
                Provenance = QueryResponseProvenance.ProjectionBacked,
            });
    }

    /// <summary>Normalizes the effective-permissions freshness evidence.</summary>
    internal static FolderLifecycleFreshness Freshness(EffectivePermissionsFreshness value)
        => new(value.ReadConsistency, value.ObservedAt, value.ProjectionWatermark, value.Stale, value.ReasonCode);

    /// <summary>Normalizes the provider freshness evidence.</summary>
    internal static FolderLifecycleFreshness Freshness(ProviderReadinessFreshness value)
        => new(value.ReadConsistency, value.ObservedAt, value.ProjectionWatermark, value.Stale, null);

    private static bool ContainsNull(JsonElement value)
        => value.ValueKind == JsonValueKind.Null
            || value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any(property => ContainsNull(property.Value))
            || value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(ContainsNull);

    private static void Redact(JsonNode? value, string consistency)
    {
        if (value is JsonObject obj)
        {
            foreach ((string key, JsonNode? member) in obj.ToArray())
            {
                if (IsProtectedMember(key) || member is JsonValue scalar && scalar.TryGetValue(out string? text)
                    && text is not null && FolderAuditSanitizer.ContainsProviderCredential(text))
                {
                    _ = obj.Remove(key);
                }
                else if (string.Equals(key, "readConsistency", StringComparison.OrdinalIgnoreCase))
                {
                    obj[key] = consistency;
                }
                else
                {
                    Redact(member, consistency);
                }
            }
        }
        else if (value is JsonArray array)
        {
            for (int index = array.Count - 1; index >= 0; index--)
            {
                JsonNode? member = array[index];
                if (member is JsonValue scalar && scalar.TryGetValue(out string? text)
                    && text is not null && FolderAuditSanitizer.ContainsProviderCredential(text))
                {
                    array.RemoveAt(index);
                }
                else
                {
                    Redact(member, consistency);
                }
            }
        }
    }

    private static bool IsProtectedMember(string key)
    {
        string name = key.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return name.Contains("CREDENTIAL", StringComparison.Ordinal) || name.Contains("SECRET", StringComparison.Ordinal)
            || name.Contains("PASSWORD", StringComparison.Ordinal) || name.Contains("TOKEN", StringComparison.Ordinal)
            || name.Contains("PRIVATEKEY", StringComparison.Ordinal)
            || name is "CONTENTBYTES" or "INLINECONTENT" or "STREAMDESCRIPTOR" or "CONTENT" or "BODY" or "DIFF"
                or "SNIPPET" or "SOURCEURI" or "PATH" or "PATHS" or "RAWPATH" or "RAWPATHS" or "FILEPATH"
                or "FILEPATHS" or "RELATIVEPATH" or "ABSOLUTEPATH" or "REPOSITORYPATH" or "UNAUTHORIZEDPATH"
                or "UNAUTHORIZEDPATHS" or "PATHLABEL" or "PROVIDERPAYLOAD";
    }

    [LoggerMessage(EventId = 5114, Level = LogLevel.Warning, Message = "Folders Gateway query evidence unavailable ({ExceptionType}).")]
    private static partial void LogReadFailure(ILogger logger, string exceptionType);
}
