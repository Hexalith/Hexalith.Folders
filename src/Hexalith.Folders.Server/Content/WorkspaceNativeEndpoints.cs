using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Queries.FileContext;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server.Content;

/// <summary>Maps bounded owner-authorized native evidence and task lifecycle HTTP operations.</summary>
internal static partial class WorkspaceNativeEndpoints
{
    internal const string AuthorizedSubmissionItem = "Folders:NativeAuthorizedSubmission";
    internal const string Route = "/api/v1/folders/{folderId}/workspaces/{workspaceId}/native/";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { AllowDuplicateProperties = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    internal static bool Matches(HttpContext context)
    {
        string[] parts = (context.Request.Path.Value ?? string.Empty).Trim('/').Split('/');
        return HttpMethods.IsPost(context.Request.Method) && parts.Length is 8 or 9
            && parts[0] == "api" && parts[1] == "v1" && parts[2] == "folders" && parts[4] == "workspaces"
            && parts[6] == "native" && (parts[7] is "operation-evidence" or "task-lease" || parts.Length == 9 && parts[7] == "files" && parts[8] == "add");
    }
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route + "operation-evidence", (string folderId, string workspaceId, HttpContext context, CancellationToken token)
            => ExecuteAsync(folderId, workspaceId, context, false, token)).RequireAuthorization();
        endpoints.MapPost(Route + "task-lease", (string folderId, string workspaceId, HttpContext context, CancellationToken token)
            => ExecuteAsync(folderId, workspaceId, context, true, token)).RequireAuthorization();
        endpoints.MapPost(Route + "files/add", FoldersDomainServiceEndpoints.NativeAddFileAsync).RequireAuthorization();
    }
    private static async Task<IResult> ExecuteAsync(string folderId, string workspaceId, HttpContext http, bool lease, CancellationToken token)
    {
        string? task = FolderHttpHeaderReader.ReadHeader(http, "X-Hexalith-Task-Id");
        string? correlation = FolderHttpHeaderReader.ReadHeader(http, "X-Correlation-Id");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            folderId = WorkspaceNativeAddress.Resolve(folderId, "folder"); workspaceId = WorkspaceNativeAddress.Resolve(workspaceId, "workspace");
            var tenant = http.RequestServices.GetRequiredService<ITenantContextAccessor>();
            if (tenant.AuthoritativeTenantId is null || !FolderCanonicalSegmentIdentifier.IsValid(folderId)
                || !FolderCanonicalSegmentIdentifier.IsValid(workspaceId) || !(FolderCanonicalSegmentIdentifier.IsValid(task) || !lease && task is not null && WorkspaceNativeAddress.Resolve(task, "task") != task)
                || !FolderCanonicalSegmentIdentifier.IsValid(correlation)) throw new ArgumentException("Invalid native envelope.");
            var scope = new ContentScope(tenant.AuthoritativeTenantId, "folders", folderId, workspaceId);
            var evidence = http.RequestServices.GetRequiredService<WorkspaceOperationEvidence>();
            await evidence.AuthorizeAsync(scope, task!, deadline.Token).ConfigureAwait(false);
            if (lease) await evidence.AuthorizeAsync(scope, task!, deadline.Token, "lock_workspace").ConfigureAwait(false);
            var policySource = http.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
            var policy = policySource.Capture();
            if (policy.ProtectionMode != "non_confidential") throw new WorkspaceContentPolicyException("confidential_operation_not_durable");
            byte[] bytes = await FoldersDomainServiceEndpoints.ReadBoundedContentAsync(http.Request.Body, 16384, deadline.Token).ConfigureAwait(false);
            if (!lease)
            {
                var request = JsonSerializer.Deserialize<WorkspaceOperationEvidenceRequest>(bytes, Json) ?? throw new JsonException();
                ValidateEvidence(request);
                return Results.Json(await evidence.ReadAsync(scope, task!, request, deadline.Token).ConfigureAwait(false), Json);
            }
            var change = JsonSerializer.Deserialize<WorkspaceNativeLeaseRequest>(bytes, Json) ?? throw new JsonException();
            ValidateLease(change);
            var source = http.RequestServices.GetRequiredService<WorkspaceContentStateReader>();
            var current = await source.ReadAsync(scope, deadline.Token).ConfigureAwait(false);
            WorkspaceOperationEvidence.RequireScope(current.State, scope);
            var clock = http.RequestServices.GetRequiredService<TimeProvider>();
            if (change.Operation == "validate")
            {
                RequireOwnership(current.State, scope, task!, change);
                WorkspaceOperationEvidence.RequireLiveLease(current.State, scope, task!, change.LockFence, clock.GetUtcNow());
                await evidence.AuthorizeAsync(scope, task!, deadline.Token, "lock_workspace").ConfigureAwait(false);
                await evidence.AuthorizeAsync(scope, task!, deadline.Token).ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                current = await RequireUnchangedHeadAsync(source, scope, current, deadline.Token).ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                RequireOwnership(current.State, scope, task!, change);
                WorkspaceOperationEvidence.RequireLiveLease(current.State, scope, task!, change.LockFence, clock.GetUtcNow());
                return LeaseResult(current.State, scope, task!, http.RequestServices.GetRequiredService<IConfiguration>());
            }
            if (change.Operation == "acquire" && current.State.WorkspaceNativeLease
                && current.State.WorkspaceLockHolderTaskId == task && current.State.WorkspaceLockExpiresAt > clock.GetUtcNow())
            {
                await evidence.AuthorizeAsync(scope, task!, deadline.Token, "lock_workspace").ConfigureAwait(false);
                await evidence.AuthorizeAsync(scope, task!, deadline.Token).ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                current = await RequireUnchangedHeadAsync(source, scope, current, deadline.Token).ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                if (!current.State.WorkspaceNativeLease || current.State.WorkspaceLockHolderTaskId != task) throw new ContentConflictException();
                WorkspaceOperationEvidence.RequireLiveLease(current.State, scope, task!, current.State.WorkspaceLockFence, clock.GetUtcNow());
                return LeaseResult(current.State, scope, task!, http.RequestServices.GetRequiredService<IConfiguration>());
            }
            IResult? retained = await RetainedDecisionAsync(current, scope, task!, change, http, source, evidence, deadline.Token).ConfigureAwait(false);
            if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
            if (retained is not null) return retained;
            string id = FoldersDomainServiceEndpoints.GenerateCanonicalMessageId();
            var gateway = http.RequestServices.GetRequiredService<IEventStoreGatewayClient>();
            var submission = new SubmitCommandRequest(id, scope.TenantId, "folders", folderId, FoldersServerModule.NativeTaskCommandType,
                JsonSerializer.SerializeToElement(new WorkspaceNativeTaskCommand(workspaceId, task!, current.Head, change), Json), correlation,
                Extensions: new Dictionary<string, string> { ["taskId"] = task! }, IdempotencyKey: id);
            http.Items[AuthorizedSubmissionItem] = submission;
            await gateway.SubmitCommandAsync(submission, deadline.Token).ConfigureAwait(false);
            // Acceptance is only a queue decision. Read the appended source event/closure before returning evidence.
            while (true)
            {
                current = await source.ReadAsync(scope, deadline.Token).ConfigureAwait(false);
                await evidence.AuthorizeAsync(scope, task!, deadline.Token, "lock_workspace").ConfigureAwait(false);
                await evidence.AuthorizeAsync(scope, task!, deadline.Token).ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                var rejection = current.Events.FirstOrDefault(item => item.CausationId == id && FolderEventReplay.IsKnownRejection(item.EventTypeName));
                if (rejection is not null)
                {
                    using var rejected = JsonDocument.Parse(rejection.Payload);
                    var root = rejected.RootElement;
                    string? code = root.TryGetProperty("Code", out var value) || root.TryGetProperty("code", out value) ? value.GetString() : null;
                    if (code is "TenantMismatch" or "TenantAccessDenied" or "FolderAclDenied" or "FolderNotFound") throw new UnauthorizedAccessException();
                    if (code is "PolicyEvidenceUnavailable" or "PolicyEvidenceStale" or "UnavailableProjection") throw new WorkspaceContentPolicyException("file_policy_unavailable");
                    throw new WorkspaceNativeConflictException(code == "IdempotencyConflict" ? "idempotency_conflict" : "native_contention");
                }
                var decision = current.Events.Where(item => item.CausationId == id && item.EventTypeName == typeof(WorkspaceNativeTaskChanged).FullName)
                    .Select(item => JsonSerializer.Deserialize<WorkspaceNativeTaskChanged>(item.Payload))
                    .SingleOrDefault(item => item?.Operation == change.Operation && item.TaskId == task && item.WorkspaceId == workspaceId);
                if (decision is not null)
                {
                    current = await RequireUnchangedHeadAsync(source, scope, current, deadline.Token).ConfigureAwait(false);
                    if (!policySource.IsCurrent(policy)) throw new WorkspaceContentPolicyException("file_policy_unavailable");
                    if (change.Operation is "complete" or "release")
                    {
                        if (decision.LockId != change.LockId || decision.LockFence != change.LockFence) throw new ContentConflictException();
                        return TerminalResult(change, clock);
                    }
                    if (current.State.WorkspaceLockId != decision.LockId || current.State.WorkspaceLockFence != decision.LockFence)
                        throw new ContentConflictException();
                    WorkspaceOperationEvidence.RequireLiveLease(current.State, scope, task!, decision.LockFence, clock.GetUtcNow());
                    return LeaseResult(current.State, scope, task!, http.RequestServices.GetRequiredService<IConfiguration>());
                }
                await Task.Delay(50, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { return FoldersDomainServiceEndpoints.ContentProblem("resource_unavailable", correlation, task); }
        catch (WorkspaceNativeConflictException conflict) { return FoldersDomainServiceEndpoints.ContentProblem(conflict.Code, correlation, task); }
        catch (ContentConflictException) { return FoldersDomainServiceEndpoints.ContentProblem("native_contention", correlation, task); }
        catch (WorkspaceContentPolicyException error) { return FoldersDomainServiceEndpoints.ContentProblem(error.Code, correlation, task); }
        catch (Exception error) when (error is JsonException or ArgumentException) { return FoldersDomainServiceEndpoints.ContentProblem("content_evidence_invalid", correlation, task); }
        catch (Exception) { return FoldersDomainServiceEndpoints.ContentProblem("file_policy_unavailable", correlation, task); }
    }
    internal static void RequireOwnership(FolderState state, ContentScope scope, string task, WorkspaceNativeLeaseRequest request)
    {
        if (!state.WorkspaceNativeLease || state.WorkspaceLockId != request.LockId || state.WorkspaceLockHolderTaskId != task
            || state.WorkspaceLockFence != request.LockFence || request.OwnershipProof != WorkspaceNativeTaskService.OwnershipProof(
                scope.TenantId, scope.AggregateId, scope.WorkspaceId, task, request.LockId!, request.LockFence)) throw new ContentConflictException();
    }
    internal static (int Renewal, int Authorization) Timing(IConfiguration configuration)
    {
        string[] keys = ["RenewalIntervalSeconds", "AuthorizationIntervalSeconds", "RevocationEffectSeconds", "ExpiredToStaleSeconds"];
        int[] ceilings = [30, 15, 60, 60];
        string?[] values = keys.Select(key => configuration["Folders:NativeLease:" + key]).ToArray();
        if (values.All(value => value is null)) return (30, 15);
        for (int index = 0; index < values.Length; index++)
            if (!int.TryParse(values[index], out int value) || value <= 0 || value > ceilings[index]) throw new ContentUnavailableException();
        int authorization = int.Parse(values[1]!, System.Globalization.CultureInfo.InvariantCulture);
        if (authorization > int.Parse(values[2]!, System.Globalization.CultureInfo.InvariantCulture)) throw new ContentUnavailableException();
        return (int.Parse(values[0]!, System.Globalization.CultureInfo.InvariantCulture), authorization);
    }
    private static IResult LeaseResult(FolderState state, ContentScope scope, string task, IConfiguration configuration)
    {
        var timing = Timing(configuration);
        return Results.Ok(new { lockId = state.WorkspaceLockId,
            ownershipProof = WorkspaceNativeTaskService.OwnershipProof(scope.TenantId, scope.AggregateId, scope.WorkspaceId, task, state.WorkspaceLockId!, state.WorkspaceLockFence),
            lockFence = state.WorkspaceLockFence, effectiveAt = state.WorkspaceLockEffectiveAt, expiresAt = state.WorkspaceLockExpiresAt,
            renewalIntervalSeconds = timing.Renewal, authorizationIntervalSeconds = timing.Authorization });
    }
}
