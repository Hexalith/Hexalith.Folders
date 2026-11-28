using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Content;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

public sealed partial class FolderDomainProcessor
{
    private async Task<DomainResult> ProcessDecoratedContentBatchAsync(CommandEnvelope envelope, CancellationToken cancellationToken)
    {
        string taskId = ReadRequiredTaskId(envelope);
        bool translated = GatewayCommandPayloadTranslator.TryTranslateMutateFilesBatch(
                envelope,
                taskId,
                out string? workspaceId,
                out string? acceptedIdempotencyKey,
                out IReadOnlyList<TranslatedFileMutationItem>? items,
                out FolderResultCode translationFailure);
        bool safeCallerKey = FolderCanonicalSegmentIdentifier.IsProcessEvidence(acceptedIdempotencyKey)
            && FolderCommandValidator.IsSafeEvidenceIdentifier(acceptedIdempotencyKey);
        string callerKey = safeCallerKey ? acceptedIdempotencyKey! : string.Empty;
        DomainResult Reject(FolderResultCode code) => Rejection(envelope, code, null, callerKey);

        if (!translated)
        {
            if (GatewayCommandPayloadTranslator.TryReadDecoratedCallerKey(envelope.Payload, out string? rejectedKey))
            {
                callerKey = rejectedKey ?? string.Empty;
            }

            return Reject(translationFailure);
        }

        if (!FolderCommandValidator.IsSafeEvidenceIdentifier(envelope.MessageId) || !safeCallerKey)
        {
            return Reject(FolderResultCode.MalformedEvidence);
        }

        if (_actorFolderRepository is not ActorFoldedFolderRepository
            || contentBatch is null
            || authoritativeContent is null
            || httpContextAccessor?.HttpContext is not { } httpContext)
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }

        FolderState initialState = _actorFolderRepository.Load(
            FolderStreamName.Create(envelope.TenantId, envelope.AggregateId));
        if (!initialState.IsCreated)
        {
            return Reject(FolderResultCode.FolderNotFound);
        }

        if (initialState.LifecycleState != FolderLifecycleState.Active)
        {
            return Reject(FolderResultCode.AlreadyArchived);
        }

        if (initialState.RepositoryBindingState != FolderRepositoryBindingState.Bound
            || initialState.WorkspaceLifecycleState is not (FolderWorkspaceLifecycleState.Locked or FolderWorkspaceLifecycleState.ChangesStaged))
        {
            return Reject(FolderResultCode.StateTransitionInvalid);
        }

        TimeProvider clock = httpContext.RequestServices.GetRequiredService<TimeProvider>();
        if (string.IsNullOrWhiteSpace(initialState.WorkspaceLockId)
            || !string.Equals(initialState.WorkspaceId, workspaceId, StringComparison.Ordinal)
            || !string.Equals(initialState.WorkspaceLockHolderTaskId, taskId, StringComparison.Ordinal)
            || initialState.IsTaskClosed(workspaceId!, taskId))
        {
            return Reject(FolderResultCode.LockNotOwned);
        }

        if (initialState.WorkspaceLockExpiresAt is null
            || initialState.WorkspaceLockExpiresAt <= clock.GetUtcNow())
        {
            return Reject(FolderResultCode.LockExpired);
        }

        IReadOnlyList<TranslatedFileMutationItem> changes = items!;
        if (changes.Any(item => item.ExpectedLockFence != initialState.WorkspaceLockFence))
        {
            return Reject(FolderResultCode.AppendConflict);
        }

        IWorkspaceFilePolicySource policySource = httpContext.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
        WorkspaceFilePolicySnapshot initialPolicy;
        try
        {
            initialPolicy = policySource.Capture();
        }
        catch (InvalidOperationException)
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }

        if (!string.Equals(initialPolicy.ProtectionMode, "non_confidential", StringComparison.Ordinal))
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }

        httpContext.Items[WorkspaceContentAuthority.ActionItem] = "mutate_files";
        ContentScope scope = new(envelope.TenantId, FoldersServerModule.DomainName, envelope.AggregateId, workspaceId!);
        List<byte[]> payloads = new(changes.Count);

        try
        {
            IWorkspacePathPolicyEvidenceProvider pathEvidence = httpContext.RequestServices
                .GetRequiredService<IWorkspacePathPolicyEvidenceProvider>();
            foreach (TranslatedFileMutationItem item in changes)
            {
                PathMetadata path = new(
                    item.PathMetadata.NormalizedPath!,
                    item.PathMetadata.DisplayName!,
                    item.PathMetadata.PathPolicyClass!,
                    item.PathMetadata.UnicodeNormalization!);
                if (initialPolicy.Classify(path) is "excluded" or "restricted")
                {
                    return Reject(FolderResultCode.PathPolicyDenied);
                }

                WorkspaceContentBinding? existing = WorkspaceContentAuthority.ValidateIngress(initialState, scope, taskId, item.OperationId,
                    item.FileOperationKind, path, item.ContentHashReference, item.ByteLength, item.MediaType,
                    item.ExpectedLockFence, item.ExpectedFileVersion, item.OwnerFileId, clock.GetUtcNow());
                if (existing is not null)
                {
                    // Gateway replay returns before dispatch. A new batch cannot reuse a published operation.
                    return Reject(FolderResultCode.AppendConflict);
                }

                WorkspacePathPolicyEvidenceResult evidence = await pathEvidence.GetEvidenceAsync(
                    new WorkspacePathPolicyEvidenceRequest(scope.TenantId, scope.AggregateId, scope.WorkspaceId,
                        taskId, item.OperationId, WorkspacePathPolicyValidator.ComputePathMetadataDigest(path), path.PathPolicyClass)
                    { PathMetadata = path }, cancellationToken).ConfigureAwait(false);
                if (evidence.Decision != WorkspacePathPolicyEvidenceDecision.NoEscape)
                {
                    return Reject(evidence.Decision == WorkspacePathPolicyEvidenceDecision.Unavailable
                        ? FolderResultCode.PolicyEvidenceUnavailable
                        : FolderResultCode.PathPolicyDenied);
                }

                if (evidence.PolicyVersion != initialPolicy.Version || evidence.PolicyDigest != initialPolicy.Digest)
                {
                    return Reject(FolderResultCode.PolicyEvidenceUnavailable);
                }
            }

            ContentReceipt?[] stagedReceipts = new ContentReceipt?[changes.Count];
            for (int index = 0; index < changes.Count; index++)
            {
                TranslatedFileMutationItem item = changes[index];
                if (item.StagingReference is { } stagingReference)
                {
                    ContentReceipt receipt = await authoritativeContent.VerifyUploadAsync(
                        httpContext.User, new ContentUploadVerification(scope, taskId, stagingReference),
                        cancellationToken).ConfigureAwait(false);
                    if (!MatchesContentEvidence(receipt, item, taskId))
                    {
                        return Reject(FolderResultCode.ValidationFailed);
                    }

                    stagedReceipts[index] = receipt;
                }
            }

            if (!policySource.IsCurrent(initialPolicy))
            {
                return Reject(FolderResultCode.PolicyEvidenceUnavailable);
            }

            for (int index = 0; index < changes.Count; index++)
            {
                TranslatedFileMutationItem item = changes[index];
                ContentReceipt? receipt = stagedReceipts[index];
                if (item.DecodedInlineBytes is { } inlineBytes)
                {
                    using MemoryStream contentStream = new(inlineBytes, writable: false);
                    receipt = await authoritativeContent.UploadAsync(
                        httpContext.User,
                        new ContentUploadRequest(scope, taskId, item.ContentHashReference![8..],
                            inlineBytes.LongLength, item.MediaType!, item.OperationId)
                        {
                            LeaseFence = item.ExpectedLockFence,
                        },
                        contentStream, cancellationToken).ConfigureAwait(false);
                    if (!MatchesContentEvidence(receipt, item, taskId))
                    {
                        return Reject(FolderResultCode.ValidationFailed);
                    }
                }

                FileMutationPayload translatedPayload = new(
                    RequestSchemaVersion: "v1",
                    WorkspaceId: workspaceId,
                    OperationId: item.OperationId,
                    FileOperationKind: item.FileOperationKind,
                    TransportOperation: item.TransportOperation,
                    PathMetadata: item.PathMetadata,
                    ContentHashReference: item.ContentHashReference,
                    ByteLength: item.ByteLength,
                    MediaType: item.MediaType,
                    TransportEvidenceKind: item.TransportEvidenceKind,
                    ObservedByteLength: item.ObservedByteLength,
                    TaskId: taskId)
                {
                    AcceptedSdkKey = callerKey,
                    ContentReceiptId = receipt?.Id,
                    ExpectedFileVersion = item.ExpectedFileVersion,
                    ExpectedLockFence = item.ExpectedLockFence,
                    OwnerFileId = item.OwnerFileId,
                };
                payloads.Add(JsonSerializer.SerializeToUtf8Bytes(translatedPayload, PayloadJsonOptions));
            }
        }
        catch (ContentConflictException)
        {
            return Reject(FolderResultCode.AppendConflict);
        }
        catch (UnauthorizedAccessException)
        {
            return Reject(FolderResultCode.FolderAclDenied);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }

        if (!policySource.IsCurrent(initialPolicy))
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }

        contentBatch.Begin();
        try
        {
            List<IEventPayload> events = [];
            for (int index = 0; index < payloads.Count; index++)
            {
                string operationId = changes[index].OperationId;
                CommandEnvelope single = envelope with
                {
                    MessageId = WorkspaceContentBinding.BatchItemKey(envelope.MessageId, operationId),
                    Payload = payloads[index],
                };
                DomainResult result = await ProcessSingleFileMutationAsync(single, cancellationToken, callerKey).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return result;
                }

                events.AddRange(result.Events);
            }

            ContentPublicationRequest publication = contentBatch.Complete();
            if (publication.Changes.Count != payloads.Count)
            {
                return Reject(FolderResultCode.ValidationFailed);
            }

            await authoritativeContent.PrepareAsync(httpContext.User, publication, cancellationToken).ConfigureAwait(false);
            _ = await authoritativeContent.ListAsync(httpContext.User, publication.Scope, cancellationToken).ConfigureAwait(false);
            WorkspaceFilePolicySnapshot finalPolicy = policySource.Capture();
            if (publication.Changes.Any(change => change.ResourceMetadata?.GetValueOrDefault("policyDigest") != finalPolicy.Digest)
                || !policySource.IsCurrent(finalPolicy))
            {
                return Reject(FolderResultCode.PolicyEvidenceUnavailable);
            }

            FolderState finalState = _actorFolderRepository.Load(
                FolderStreamName.Create(publication.Scope.TenantId, publication.Scope.AggregateId));
            if (finalState.WorkspaceLockFence != publication.LockFence
                || finalState.WorkspaceLockHolderTaskId != publication.TaskId
                || finalState.IsTaskClosed(publication.Scope.WorkspaceId, publication.TaskId))
            {
                return Reject(FolderResultCode.LockNotOwned);
            }

            if (finalState.WorkspaceLockExpiresAt is null
                || finalState.WorkspaceLockExpiresAt <= clock.GetUtcNow())
            {
                return Reject(FolderResultCode.LockExpired);
            }

            FolderProcessResultPayload responsePayload = new(
                Status: "accepted",
                IdempotentReplay: false,
                CorrelationId: FolderCanonicalSegmentIdentifier.IsValid(envelope.CorrelationId) ? envelope.CorrelationId : null,
                TaskId: FolderCanonicalSegmentIdentifier.IsValid(taskId) ? taskId : null,
                IdempotencyKey: callerKey);

            // EventStore appends the complete actor decision transactionally. No preparation is visible beforehand.
            return new PayloadDomainResult(JsonSerializer.Serialize(responsePayload, PayloadJsonOptions), events);
        }
        catch (ContentConflictException)
        {
            return Reject(FolderResultCode.AppendConflict);
        }
        catch (UnauthorizedAccessException)
        {
            return Reject(FolderResultCode.FolderAclDenied);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Reject(FolderResultCode.PolicyEvidenceUnavailable);
        }
        finally
        {
            contentBatch.End();
        }
    }

    private static bool MatchesContentEvidence(ContentReceipt receipt, TranslatedFileMutationItem item, string taskId)
        => string.Equals("hashref_" + receipt.Sha256, item.ContentHashReference, StringComparison.Ordinal)
            && receipt.ByteLength == item.ByteLength && string.Equals(receipt.MediaType, item.MediaType, StringComparison.Ordinal)
            && string.Equals(receipt.TaskId, taskId, StringComparison.Ordinal);

    private async Task<DomainResult> ProcessContentBatchAsync(CommandEnvelope envelope, JsonElement root, JsonElement items)
    {
        if (_actorFolderRepository is not ActorFoldedFolderRepository
            || contentBatch is null || authoritativeContent is null || httpContextAccessor?.HttpContext is null
            || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() is < 1 or > 100
            || !root.TryGetProperty("workspaceId", out JsonElement workspace) || workspace.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("requestSchemaVersion", out JsonElement schema) || schema.ValueKind != JsonValueKind.String || schema.GetString() != "v1"
            || !root.TryGetProperty("taskId", out var task) || task.ValueKind != JsonValueKind.String || task.GetString() != ReadRequiredTaskId(envelope)
            || root.EnumerateObject().Any(property => property.Name is not ("items" or "workspaceId" or "taskId" or "requestSchemaVersion")))
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        var paths = new List<string>();
        var operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var payloads = new List<byte[]>();
        long totalBytes = 0;
        try
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
                var node = JsonNode.Parse(item.GetRawText())!.AsObject();
                node["workspaceId"] = workspace.GetString(); node["requestSchemaVersion"] = "v1";
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(node);
                FileMutationPayload mutation = JsonSerializer.Deserialize<FileMutationPayload>(payload, PayloadJsonOptions)!;
                string path = mutation.PathMetadata?.NormalizedPath ?? string.Empty;
                if (mutation.OperationId is null || !operations.Add(mutation.OperationId) || paths.Any(existing =>
                    existing.Equals(path, StringComparison.OrdinalIgnoreCase)
                    || existing.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase)))
                    return Rejection(envelope, FolderResultCode.PathPolicyDenied, null);
                foreach (string existing in paths)
                {
                    string[] left = existing.Split('/'), right = path.Split('/');
                    for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
                    {
                        if (!left[index].Equals(right[index], StringComparison.OrdinalIgnoreCase)) break;
                        if (left[index] != right[index]) return Rejection(envelope, FolderResultCode.PathPolicyDenied, null);
                    }
                }
                paths.Add(path); payloads.Add(payload);
                if (mutation.FileOperationKind != "remove") totalBytes = checked(totalBytes + (mutation.ByteLength ?? -10485761));
                if (totalBytes is < 0 or > 10485760) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or OverflowException)
        { return Rejection(envelope, FolderResultCode.ValidationFailed, null); }
        contentBatch.Begin();
        try
        {
            var events = new List<IEventPayload>();
            for (int index = 0; index < payloads.Count; index++)
            {
                using JsonDocument item = JsonDocument.Parse(payloads[index]);
                string operationId = item.RootElement.GetProperty("operationId").GetString()!;
                var single = envelope with { MessageId = WorkspaceContentBinding.BatchItemKey(envelope.MessageId, operationId), Payload = payloads[index] };
                DomainResult result = await ProcessSingleFileMutationAsync(single, CancellationToken.None).ConfigureAwait(false);
                if (!result.IsSuccess) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
                events.AddRange(result.Events);
            }
            ContentPublicationRequest publication = contentBatch.Complete();
            if (publication.Changes.Count != payloads.Count) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            await authoritativeContent.PrepareAsync(httpContextAccessor.HttpContext.User, publication, CancellationToken.None).ConfigureAwait(false);
            _ = await authoritativeContent.ListAsync(httpContextAccessor.HttpContext.User, publication.Scope, CancellationToken.None).ConfigureAwait(false);
            var policySource = httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
            var finalPolicy = policySource.Capture();
            if (publication.Changes.Any(change => change.ResourceMetadata?.GetValueOrDefault("policyDigest") != finalPolicy.Digest)
                || !policySource.IsCurrent(finalPolicy)) return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
            var finalState = _actorFolderRepository.Load(FolderStreamName.Create(publication.Scope.TenantId, publication.Scope.AggregateId));
            if (finalState.WorkspaceLockFence != publication.LockFence || finalState.WorkspaceLockHolderTaskId != publication.TaskId
                || finalState.IsTaskClosed(publication.Scope.WorkspaceId, publication.TaskId))
                return Rejection(envelope, FolderResultCode.LockNotOwned, null);
            if (finalState.WorkspaceLockExpiresAt is null || finalState.WorkspaceLockExpiresAt <= httpContextAccessor.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow())
                return Rejection(envelope, FolderResultCode.LockExpired, null);
            // EventStore appends the complete actor decision transactionally. No preparation is visible beforehand.
            return new DomainResult(events);
        }
        catch (ContentConflictException) { return Rejection(envelope, FolderResultCode.AppendConflict, null); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null); }
        finally { contentBatch.End(); }
    }
}
