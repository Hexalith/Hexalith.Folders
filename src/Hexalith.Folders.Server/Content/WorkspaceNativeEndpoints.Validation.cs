using System.Text.Json;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server.Content;

internal static partial class WorkspaceNativeEndpoints
{
    private static bool Bounded(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
    private static void ValidateEvidence(WorkspaceOperationEvidenceRequest request)
    {
        if (request.LookupKind is not ("accepted" or "absence") || !Bounded(request.OperationId, 128)
            || !WorkspaceNativeAddress.IsSdkKey(request.AcceptedKey) || request.ContentHashReference is not { Length: 72 }
            || !request.ContentHashReference.StartsWith("hashref_", StringComparison.Ordinal)
            || request.ContentHashReference.AsSpan(8).IndexOfAnyExcept("0123456789abcdef") >= 0
            || request.ByteLength is < 0 or > 262144 || !Bounded(request.MediaType, 128) || request.LockFence < 0
            || request.PathMetadata is not { } path || !Bounded(path.NormalizedPath, 1024) || !Bounded(path.DisplayName, 256)
            || path.PathPolicyClass != "metadata_only" || path.UnicodeNormalization != "NFC")
            throw new ArgumentException("Invalid native evidence.");
    }
    internal static void ValidateLease(WorkspaceNativeLeaseRequest request)
    {
        if (request.Operation is not ("acquire" or "renew" or "validate" or "release" or "complete")
            || request.RequestedLeaseSeconds is < 1 or > 3600) throw new ArgumentException("Invalid native lease operation.");
        if (request.Operation != "acquire" && (!FolderCanonicalSegmentIdentifier.IsValid(request.LockId)
            || !Bounded(request.OwnershipProof, 128) || request.LockFence <= 0)) throw new ArgumentException("Invalid native ownership proof.");
        if (request.Operation == "complete" && (!FolderCanonicalSegmentIdentifier.IsValid(request.OperationId)
            || !WorkspaceNativeAddress.IsSdkKey(request.AcceptedKey) || !Bounded(request.Fingerprint, 128)))
            throw new ArgumentException("Invalid native completion evidence.");
    }
    private static async Task<WorkspaceContentState> RequireUnchangedHeadAsync(WorkspaceContentStateReader source, ContentScope scope,
        WorkspaceContentState observed, CancellationToken token)
    {
        var final = await source.ReadAsync(scope, token).ConfigureAwait(false);
        if (final.Head != observed.Head) throw new ContentConflictException();
        return final;
    }
    private static IResult TerminalResult(WorkspaceNativeLeaseRequest request, TimeProvider clock)
        => Results.Ok(new
        {
            lockId = request.LockId,
            ownershipProof = request.OwnershipProof,
            lockFence = request.LockFence,
            effectiveAt = clock.GetUtcNow(),
            expiresAt = clock.GetUtcNow(),
            renewalIntervalSeconds = 30,
            authorizationIntervalSeconds = 15
        });

    private static async Task<IResult?> RetainedDecisionAsync(WorkspaceContentState current, ContentScope scope, string task,
        WorkspaceNativeLeaseRequest request, HttpContext http, WorkspaceContentStateReader source, WorkspaceOperationEvidence evidence, CancellationToken token)
    {
        bool completed = request.Operation == "complete" && current.State.IsTaskClosed(scope.WorkspaceId, task);
        bool released = request.Operation == "release" && current.State.NativeReleasedLeaseFences
            .GetValueOrDefault(FolderState.TaskBindingKey(task, request.LockId!), 0) == request.LockFence;
        if (!completed && !released) return null;
        var retained = current.Events.Where(item => item.EventTypeName == typeof(WorkspaceNativeTaskChanged).FullName)
            .Select(item => JsonSerializer.Deserialize<WorkspaceNativeTaskChanged>(item.Payload))
            .SingleOrDefault(item => item?.Operation == request.Operation && item.TaskId == task && item.WorkspaceId == scope.WorkspaceId
                && item.LockId == request.LockId && item.LockFence == request.LockFence);
        if (retained is null || request.OwnershipProof != WorkspaceNativeTaskService.OwnershipProof(scope.TenantId,
            scope.AggregateId, scope.WorkspaceId, task, retained.LockId, retained.LockFence)) throw new ContentConflictException();
        if (completed)
        {
            var closure = current.State.TaskClosures[FolderState.TaskBindingKey(scope.WorkspaceId, task)];
            var publication = current.State.ContentPublications.Values.SingleOrDefault(item => item.TaskId == task
                && item.WorkspaceId == scope.WorkspaceId && item.OperationId == request.OperationId);
            if (closure.TerminalState != "native_completed" || publication is null || closure.OperationId != publication.CommandId
                || retained.PublicationCommandId != publication.CommandId || publication.AcceptedSdkKey != request.AcceptedKey
                || publication.LockFence != request.LockFence || current.State.IdempotencyFingerprints.GetValueOrDefault(publication.CommandId) != request.Fingerprint)
                throw new WorkspaceNativeConflictException("idempotency_conflict");
            var visible = await http.RequestServices.GetRequiredService<IAuthoritativeContentStore>().ListAsync(http.User, scope, token).ConfigureAwait(false);
            var binding = visible.SingleOrDefault(item => item.Change.FileId == publication.FileId && item.Change.Version == publication.Version
                && item.Receipt.Id == publication.ReceiptId) ?? throw new ContentConflictException();
            _ = await evidence.ReadAsync(scope, task, new("accepted", publication.OperationId, request.AcceptedKey!,
                WorkspaceContentAuthority.MetadataPath(binding.Change.ResourceMetadata ?? throw new ContentConflictException()),
                publication.ContentHashReference!, publication.ByteLength!.Value, publication.MediaType!), token).ConfigureAwait(false);
        }
        await evidence.AuthorizeAsync(scope, task, token, "lock_workspace").ConfigureAwait(false);
        await evidence.AuthorizeAsync(scope, task, token).ConfigureAwait(false);
        _ = await RequireUnchangedHeadAsync(source, scope, current, token).ConfigureAwait(false);
        return TerminalResult(request, http.RequestServices.GetRequiredService<TimeProvider>());
    }
}
