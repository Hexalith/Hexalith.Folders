using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Content;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

public sealed partial class FolderDomainProcessor
{
    private async Task<DomainResult> ProcessNativeTaskAsync(CommandEnvelope envelope)
    {
        var http = httpContextAccessor?.HttpContext;
        if (http is null || _actorFolderRepository is not ActorFoldedFolderRepository || authoritativeContent is null)
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        try
        {
            var command = JsonSerializer.Deserialize<WorkspaceNativeTaskCommand>(envelope.Payload, PayloadJsonOptions);
            if (command?.Request is null || command.TaskId != ReadRequiredTaskId(envelope)
                || !FolderCanonicalSegmentIdentifier.IsValid(command.WorkspaceId)
                || command.Request.RequestedLeaseSeconds is < 1 or > 3600)
                return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            var request = command.Request;
            WorkspaceNativeEndpoints.ValidateLease(request);
            var scope = new ContentScope(envelope.TenantId, "folders", envelope.AggregateId, command.WorkspaceId);
            var evidence = http.RequestServices.GetRequiredService<WorkspaceOperationEvidence>();
            await evidence.AuthorizeAsync(scope, command.TaskId, CancellationToken.None, "lock_workspace").ConfigureAwait(false);
            await evidence.AuthorizeAsync(scope, command.TaskId, CancellationToken.None).ConfigureAwait(false);
            var source = http.RequestServices.GetRequiredService<WorkspaceContentStateReader>();
            var current = await source.ReadAsync(scope, CancellationToken.None).ConfigureAwait(false);
            var state = current.State;
            WorkspaceOperationEvidence.RequireScope(state, scope);
            var policySource = http.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
            var policy = policySource.Capture();
            if (policy.ProtectionMode != "non_confidential") throw new ContentConflictException();
            _ = WorkspaceNativeEndpoints.Timing(http.RequestServices.GetRequiredService<IConfiguration>());
            WorkspaceContentBinding? publication = null;
            if (request.Operation == "complete")
            {
                var matches = state.ContentPublications.Values.Where(value => value.TaskId == command.TaskId
                    && value.WorkspaceId == scope.WorkspaceId && value.OperationId == request.OperationId).ToArray();
                if (matches.Length != 1) throw new ContentConflictException();
                publication = matches[0];
                var visible = await authoritativeContent.ListAsync(http.User, scope, CancellationToken.None).ConfigureAwait(false);
                var confirmed = visible.SingleOrDefault(item => item.Change.FileId == publication.FileId
                    && item.Change.Version == publication.Version && item.Receipt.Id == publication.ReceiptId) ?? throw new ContentConflictException();
                var path = WorkspaceContentAuthority.MetadataPath(confirmed.Change.ResourceMetadata ?? throw new ContentConflictException());
                _ = await evidence.ReadAsync(scope, command.TaskId, new("accepted", publication.OperationId, publication.AcceptedSdkKey ?? string.Empty,
                    path, publication.ContentHashReference!, publication.ByteLength!.Value, publication.MediaType!), CancellationToken.None).ConfigureAwait(false);
                await evidence.AuthorizeAsync(scope, command.TaskId, CancellationToken.None, "lock_workspace").ConfigureAwait(false);
                if (!policySource.IsCurrent(policy)) throw new ContentConflictException();
                if (publication.AcceptedSdkKey is null || publication.AcceptedSdkKey != request.AcceptedKey
                    || publication.LockFence != request.LockFence
                    || state.IdempotencyFingerprints.GetValueOrDefault(publication.CommandId) != request.Fingerprint)
                    return Rejection(envelope, FolderResultCode.IdempotencyConflict, null);
                if (state.TaskClosures.TryGetValue(FolderState.TaskBindingKey(scope.WorkspaceId, command.TaskId), out var closed))
                    return closed.TerminalState == "native_completed" && closed.OperationId == publication.CommandId
                        && HasRetainedNativeDecision(current, scope, command.TaskId, request)
                        ? new DomainResult([]) : Rejection(envelope, FolderResultCode.AppendConflict, null);
            }
            else if (request.Operation == "release" && request.LockFence > 0 && request.LockId is { } releasedLock
                && state.NativeReleasedLeaseFences.GetValueOrDefault(FolderState.TaskBindingKey(command.TaskId, releasedLock), 0) == request.LockFence)
            {
                if (!HasRetainedNativeDecision(current, scope, command.TaskId, request)) throw new ContentConflictException();
                return new DomainResult([]);
            }
            if (!current.ActorOwned || current.Head != command.ExpectedHead || state.IsTaskClosed(scope.WorkspaceId, command.TaskId))
                throw new ContentConflictException();
            var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            string lockId;
            long fence;
            var decisions = new List<WorkspaceNativeTaskChanged>();
            if (request.Operation == "acquire")
            {
                if (state.WorkspaceNativeLease && state.WorkspaceLockHolderTaskId == command.TaskId
                    && state.WorkspaceLockExpiresAt <= now)
                {
                    // A lost acquire response does not transfer ownership. Recover only this task's
                    // exact clean expired native lease; the SDK fence seal rejects staged/racing work.
                    if (state.WorkspaceNativeProviderWork || state.ContentPublications.Values.Any(item => item.TaskId == command.TaskId)
                        || state.WorkspaceLifecycleState != FolderWorkspaceLifecycleState.Locked && !IsRecordedNativeExpiry(state, now)
                        || !await authoritativeContent.TryQuiesceTaskLeaseAsync(http.User, scope, command.TaskId, state.WorkspaceLockFence, CancellationToken.None).ConfigureAwait(false))
                        throw new ContentConflictException();
                    var released = new WorkspaceNativeTaskChanged(scope.TenantId, state.OrganizationId!, scope.AggregateId, scope.WorkspaceId,
                        "release", state.WorkspaceLockId!, state.WorkspaceLockFence, request.RequestedLeaseSeconds, now, now,
                        null, envelope.UserId, envelope.CorrelationId, command.TaskId, envelope.MessageId,
                        Convert.ToHexStringLower(SHA256.HashData(envelope.Payload)), now);
                    decisions.Add(released);
                    state = state.Apply([released], FolderStreamName.Create(scope.TenantId, scope.AggregateId));
                }
                if (state.WorkspaceLifecycleState != FolderWorkspaceLifecycleState.Ready || state.WorkspaceLockHolderTaskId is not null)
                    throw new ContentConflictException();
                lockId = "native_lock_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
                fence = checked(state.WorkspaceLockFence + 1);
            }
            else
            {
                WorkspaceNativeEndpoints.RequireOwnership(state, scope, command.TaskId, request);
                lockId = request.LockId!; fence = request.LockFence;
                if (request.Operation == "renew")
                    WorkspaceOperationEvidence.RequireLiveLease(state, scope, command.TaskId, fence, now);
                else if (request.Operation is "complete" or "release")
                {
                    if (state.WorkspaceNativeProviderWork || state.WorkspaceLifecycleState is not
                        (FolderWorkspaceLifecycleState.Locked or FolderWorkspaceLifecycleState.ChangesStaged) && !IsRecordedNativeExpiry(state, now)) throw new ContentConflictException();
                    var owned = state.ContentPublications.Values.Where(value => value.TaskId == command.TaskId && value.LockFence == fence).ToArray();
                    if (request.Operation == "release" && owned.Length != 0 || request.Operation == "complete" && (owned.Length != 1
                        || owned.Any(value => value.AcceptedSdkKey is null || value.ReceiptId is null))) throw new ContentConflictException();
                    foreach (var binding in owned)
                    {
                        if (await authoritativeContent.RecoverAsync(http.User, scope, binding.CommandId, CancellationToken.None).ConfigureAwait(false) != ContentPublicationOutcome.Accepted)
                            throw new ContentConflictException();
                    }
                    // This SDK CAS seal shares admission with Upload/Prepare and prevents racing staged work.
                    bool sealedTask = request.Operation == "complete"
                        ? await authoritativeContent.TrySealTaskForCompletionAsync(http.User, scope, command.TaskId, fence, CancellationToken.None).ConfigureAwait(false)
                        : await authoritativeContent.TryQuiesceTaskLeaseAsync(http.User, scope, command.TaskId, fence, CancellationToken.None).ConfigureAwait(false);
                    if (!sealedTask) throw new ContentConflictException();
                }
                else throw new ContentConflictException();
            }
            await evidence.AuthorizeAsync(scope, command.TaskId, CancellationToken.None, "lock_workspace").ConfigureAwait(false);
            await evidence.AuthorizeAsync(scope, command.TaskId, CancellationToken.None).ConfigureAwait(false);
            var final = await source.ReadAsync(scope, CancellationToken.None).ConfigureAwait(false);
            if (final.Head != current.Head || !policySource.IsCurrent(policy)) throw new ContentConflictException();
            if (request.Operation == "renew") WorkspaceOperationEvidence.RequireLiveLease(final.State, scope, command.TaskId, fence,
                http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
            now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var changed = new WorkspaceNativeTaskChanged(scope.TenantId, state.OrganizationId!, scope.AggregateId, scope.WorkspaceId,
                request.Operation, lockId, fence, request.RequestedLeaseSeconds, now, now.AddSeconds(request.RequestedLeaseSeconds),
                publication?.CommandId, envelope.UserId, envelope.CorrelationId, command.TaskId, envelope.MessageId,
                Convert.ToHexStringLower(SHA256.HashData(envelope.Payload)), now);
            _ = state.Apply([changed], FolderStreamName.Create(scope.TenantId, scope.AggregateId));
            decisions.Add(changed);
            return new DomainResult([.. decisions]);
        }
        catch (UnauthorizedAccessException) { return Rejection(envelope, FolderResultCode.TenantMismatch, null); }
        catch (WorkspaceNativeConflictException conflict) { return Rejection(envelope, conflict.Code == "idempotency_conflict" ? FolderResultCode.IdempotencyConflict : FolderResultCode.AppendConflict, null); }
        catch (ContentConflictException) { return Rejection(envelope, FolderResultCode.AppendConflict, null); }
        catch (Exception) { return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null); }
    }
    private static bool HasRetainedNativeDecision(WorkspaceContentState current, ContentScope scope, string task, WorkspaceNativeLeaseRequest request)
        => request.OwnershipProof == WorkspaceNativeTaskService.OwnershipProof(scope.TenantId, scope.AggregateId,
                scope.WorkspaceId, task, request.LockId!, request.LockFence)
            && current.Events.Where(item => item.EventTypeName == typeof(WorkspaceNativeTaskChanged).FullName)
                .Select(item => JsonSerializer.Deserialize<WorkspaceNativeTaskChanged>(item.Payload))
                .Any(item => item?.Operation == request.Operation && item.WorkspaceId == scope.WorkspaceId && item.TaskId == task
                    && item.LockId == request.LockId && item.LockFence == request.LockFence);
    private static bool IsRecordedNativeExpiry(FolderState state, DateTimeOffset now)
        => state.WorkspaceNativeLease && state.WorkspaceLifecycleState == FolderWorkspaceLifecycleState.Dirty
            && state.WorkspaceLifecycleEvent == FolderWorkspaceLifecycleEvent.LockLeaseExpired
            && state.WorkspaceLockExpiresAt is { } expiry && expiry <= now;
}
