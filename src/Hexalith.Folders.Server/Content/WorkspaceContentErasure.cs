using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Serializes irreversible receipt disposal after the SDK reserves a revocable claim against references and store-owned holds.</summary>
internal static class WorkspaceContentErasure
{
    internal static bool IsCanonicalRequest(WorkspaceContentErasureRequest request)
        => FolderCanonicalSegmentIdentifier.IsValid(request.WorkspaceId)
            && request.ReceiptId is { Length: 72 } receipt && receipt.StartsWith("content_", StringComparison.Ordinal)
            && receipt.AsSpan(8).IndexOfAnyExcept("0123456789abcdef") < 0
            && (request.AssignedTaskId is null || FolderCanonicalSegmentIdentifier.IsValid(request.AssignedTaskId))
            && request.ExpectedGeneration is >= 0 and < long.MaxValue;

    internal static bool HasActiveTask(FolderState state) => state.WorkspaceTaskId is { } task
        && (state.WorkspaceId is null || !state.IsTaskClosed(state.WorkspaceId, task));
    internal static bool HasLiveReference(FolderState state, string receiptId)
        => state.CommittedContentReferences.Values.Contains(receiptId, StringComparer.Ordinal)
            || state.ContentPublications.Values.Any(binding => binding.ReceiptId == receiptId
                && state.FileIdentities.TryGetValue(binding.FileId, out var file) && file.Active
                && state.ContentPublications.Values.Where(value => value.FileId == binding.FileId).Max(value => value.Version) == binding.Version);


    internal static bool HasCurrentGrant(WorkspaceContentState current, ContentScope scope, ContentReceipt receipt, long generation)
        => generation is >= 0 and < long.MaxValue && current.Head == generation + 1
            && current.State.ContentErasureGrants.GetValueOrDefault(receipt.Id, -1) == generation
            && !HasActiveTask(current.State) && !HasLiveReference(current.State, receipt.Id)
            && (receipt.TaskId is null || current.State.IsTaskClosed(scope.WorkspaceId, receipt.TaskId));

    internal static async Task<bool> AuthorizeAsync(IEventStoreGatewayClient gateway, WorkspaceContentStateReader source,
        ContentScope scope, ContentReceipt receipt, long generation, HttpContext? http, CancellationToken token)
    {
        var current = await source.ReadAsync(scope, token).ConfigureAwait(false);
        if (current.State.ContentErasureGrants.TryGetValue(receipt.Id, out long existing) && existing == generation)
            return HasCurrentGrant(current, scope, receipt, generation);
        if (current.ActorOwned || http is null || current.Head != generation) return false;
        http.Items["Folders:ContentRetentionAuthorized"] = true;
        string id = "erase-" + receipt.Id.Replace('_', '-') + "-" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await gateway.SubmitCommandAsync(new(id, scope.TenantId, "folders", scope.AggregateId,
            FoldersServerModule.AuthorizeContentErasureCommandType,
            JsonSerializer.SerializeToElement(new WorkspaceContentErasureRequest(scope.WorkspaceId, receipt.Id, receipt.TaskId, generation), JsonSerializerOptions.Web),
            CorrelationId: id, Extensions: new Dictionary<string, string> { ["taskId"] = id }, IdempotencyKey: id), token).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            current = await source.ReadAsync(scope, timeout.Token).ConfigureAwait(false);
            if (current.State.ContentErasureGrants.TryGetValue(receipt.Id, out existing) && existing == generation)
                return HasCurrentGrant(current, scope, receipt, generation);
            var status = await gateway.GetCommandStatusAsync(id, timeout.Token).ConfigureAwait(false);
            if (status?.IsRejected == true) return false;
            await Task.Delay(100, timeout.Token).ConfigureAwait(false);
        }
    }
}
