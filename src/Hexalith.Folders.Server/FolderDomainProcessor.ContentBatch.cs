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
        if (!GatewayCommandPayloadTranslator.TryTranslateMutateFilesBatch(
                envelope,
                taskId,
                out string? workspaceId,
                out string? acceptedIdempotencyKey,
                out IReadOnlyList<TranslatedFileMutationItem>? items,
                out FolderResultCode translationFailure))
        {
            return Rejection(envelope, translationFailure, null);
        }

        if (!FolderCommandValidator.IsSafeEvidenceIdentifier(envelope.MessageId)
            || !FolderCommandValidator.IsSafeEvidenceIdentifier(acceptedIdempotencyKey))
        {
            return Rejection(envelope, FolderResultCode.MalformedEvidence, null);
        }

        if (_actorFolderRepository is not ActorFoldedFolderRepository
            || contentBatch is null
            || authoritativeContent is null
            || httpContextAccessor?.HttpContext is not { } httpContext)
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }

        FolderState initialState = _actorFolderRepository.Load(
            FolderStreamName.Create(envelope.TenantId, envelope.AggregateId));
        if (!initialState.IsCreated)
        {
            return Rejection(envelope, FolderResultCode.FolderNotFound, null);
        }

        if (initialState.LifecycleState != FolderLifecycleState.Active)
        {
            return Rejection(envelope, FolderResultCode.AlreadyArchived, null);
        }

        TimeProvider clock = httpContext.RequestServices.GetRequiredService<TimeProvider>();
        if (!string.Equals(initialState.WorkspaceId, workspaceId, StringComparison.Ordinal)
            || !string.Equals(initialState.WorkspaceLockHolderTaskId, taskId, StringComparison.Ordinal)
            || initialState.IsTaskClosed(workspaceId!, taskId))
        {
            return Rejection(envelope, FolderResultCode.LockNotOwned, null);
        }

        if (initialState.WorkspaceLockExpiresAt is null
            || initialState.WorkspaceLockExpiresAt <= clock.GetUtcNow())
        {
            return Rejection(envelope, FolderResultCode.LockExpired, null);
        }

        foreach (TranslatedFileMutationItem item in items!)
        {
            if (item.ExpectedLockFence != initialState.WorkspaceLockFence)
            {
                return Rejection(envelope, FolderResultCode.AppendConflict, null);
            }
        }

        IWorkspaceFilePolicySource policySource = httpContext.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
        WorkspaceFilePolicySnapshot initialPolicy;
        try
        {
            initialPolicy = policySource.Capture();
        }
        catch (InvalidOperationException)
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }

        if (!string.Equals(initialPolicy.ProtectionMode, "non_confidential", StringComparison.Ordinal))
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }

        foreach (TranslatedFileMutationItem item in items!)
        {
            PathMetadata path = new(
                item.PathMetadata.NormalizedPath ?? string.Empty,
                item.PathMetadata.DisplayName ?? string.Empty,
                item.PathMetadata.PathPolicyClass ?? string.Empty,
                item.PathMetadata.UnicodeNormalization ?? string.Empty);
            if (initialPolicy.Classify(path) is "excluded" or "restricted")
            {
                return Rejection(envelope, FolderResultCode.PathPolicyDenied, null);
            }
        }

        httpContext.Items[WorkspaceContentAuthority.ActionItem] = "mutate_files";
        ContentScope scope = new(envelope.TenantId, FoldersServerModule.DomainName, envelope.AggregateId, workspaceId!);
        List<byte[]> payloads = new(items.Count);

        try
        {
            foreach (TranslatedFileMutationItem item in items)
            {
                string? receiptId = null;
                if (item.FileOperationKind is "add" or "change")
                {
                    ContentReceipt receipt;
                    if (string.Equals(item.TransportOperation, "PutFileInline", StringComparison.Ordinal))
                    {
                        using MemoryStream contentStream = new(item.DecodedInlineBytes!, writable: false);
                        receipt = await authoritativeContent.UploadAsync(
                            httpContext.User,
                            new ContentUploadRequest(
                                scope,
                                taskId,
                                item.ContentHashReference![8..],
                                item.DecodedInlineBytes!.LongLength,
                                item.MediaType!,
                                item.OperationId)
                            {
                                LeaseFence = item.ExpectedLockFence,
                            },
                            contentStream,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        receipt = await authoritativeContent.VerifyUploadAsync(
                            httpContext.User,
                            new ContentUploadVerification(scope, taskId, item.StagingReference!),
                            cancellationToken).ConfigureAwait(false);
                    }

                    if (!string.Equals("hashref_" + receipt.Sha256, item.ContentHashReference, StringComparison.Ordinal)
                        || receipt.ByteLength != item.ByteLength
                        || !string.Equals(receipt.MediaType, item.MediaType, StringComparison.Ordinal))
                    {
                        return Rejection(envelope, FolderResultCode.ValidationFailed, null);
                    }

                    receiptId = receipt.Id;
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
                    AcceptedSdkKey = acceptedIdempotencyKey,
                    ContentReceiptId = receiptId,
                    ExpectedFileVersion = item.ExpectedFileVersion,
                    ExpectedLockFence = item.ExpectedLockFence,
                    OwnerFileId = item.OwnerFileId,
                };
                payloads.Add(JsonSerializer.SerializeToUtf8Bytes(translatedPayload, PayloadJsonOptions));
            }
        }
        catch (ContentConflictException)
        {
            return Rejection(envelope, FolderResultCode.AppendConflict, null);
        }
        catch (UnauthorizedAccessException)
        {
            return Rejection(envelope, FolderResultCode.FolderAclDenied, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }

        if (!policySource.IsCurrent(initialPolicy))
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }

        contentBatch.Begin();
        try
        {
            List<IEventPayload> events = [];
            for (int index = 0; index < payloads.Count; index++)
            {
                string operationId = items[index].OperationId;
                CommandEnvelope single = envelope with
                {
                    MessageId = WorkspaceContentBinding.BatchItemKey(envelope.MessageId, operationId),
                    Payload = payloads[index],
                };
                DomainResult result = await ProcessSingleFileMutationAsync(single).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return result;
                }

                events.AddRange(result.Events);
            }

            ContentPublicationRequest publication = contentBatch.Complete();
            if (publication.Changes.Count != payloads.Count)
            {
                return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            }

            await authoritativeContent.PrepareAsync(httpContext.User, publication, cancellationToken).ConfigureAwait(false);
            _ = await authoritativeContent.ListAsync(httpContext.User, publication.Scope, cancellationToken).ConfigureAwait(false);
            WorkspaceFilePolicySnapshot finalPolicy = policySource.Capture();
            if (publication.Changes.Any(change => change.ResourceMetadata?.GetValueOrDefault("policyDigest") != finalPolicy.Digest)
                || !policySource.IsCurrent(finalPolicy))
            {
                return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
            }

            FolderState finalState = _actorFolderRepository.Load(
                FolderStreamName.Create(publication.Scope.TenantId, publication.Scope.AggregateId));
            if (finalState.WorkspaceLockFence != publication.LockFence
                || finalState.WorkspaceLockHolderTaskId != publication.TaskId
                || finalState.IsTaskClosed(publication.Scope.WorkspaceId, publication.TaskId))
            {
                return Rejection(envelope, FolderResultCode.LockNotOwned, null);
            }

            if (finalState.WorkspaceLockExpiresAt is null
                || finalState.WorkspaceLockExpiresAt <= clock.GetUtcNow())
            {
                return Rejection(envelope, FolderResultCode.LockExpired, null);
            }

            FolderProcessResultPayload responsePayload = new(
                Status: "accepted",
                IdempotentReplay: false,
                CorrelationId: FolderCanonicalSegmentIdentifier.IsValid(envelope.CorrelationId) ? envelope.CorrelationId : null,
                TaskId: FolderCanonicalSegmentIdentifier.IsValid(taskId) ? taskId : null,
                IdempotencyKey: FolderCanonicalSegmentIdentifier.IsValid(envelope.MessageId) ? envelope.MessageId : null);

            // EventStore appends the complete actor decision transactionally. No preparation is visible beforehand.
            return new PayloadDomainResult(JsonSerializer.Serialize(responsePayload, PayloadJsonOptions), events);
        }
        catch (ContentConflictException)
        {
            return Rejection(envelope, FolderResultCode.AppendConflict, null);
        }
        catch (UnauthorizedAccessException)
        {
            return Rejection(envelope, FolderResultCode.FolderAclDenied, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
        }
        finally
        {
            contentBatch.End();
        }
    }

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
                DomainResult result = await ProcessSingleFileMutationAsync(single).ConfigureAwait(false);
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
