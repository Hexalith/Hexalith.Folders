using System.Text.Json;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Content;

/// <summary>Resolves a generic gateway rejection only from the exact retained source decision.</summary>
internal sealed class WorkspaceContentRejectionReader(IContentAuthority authority, WorkspaceContentStateReader source, IHttpContextAccessor http)
{
    internal async Task<FolderResultCode?> ReadAsync(ContentScope scope, string correlationId, string taskId, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        await authority.AuthorizeAsync(http.HttpContext!.User, scope, ContentOperation.Recover, scope, deadline.Token).ConfigureAwait(false);
        var current = await source.ReadAsync(scope, deadline.Token).ConfigureAwait(false);
        return ReadCode(current.Events, correlationId, taskId);
    }

    internal static FolderResultCode? ReadCode(IReadOnlyList<StreamReadEvent> events, string correlationId, string taskId)
    {
        // Admission replaces transport message IDs with protected execution IDs. Require
        // one retained decision for this correlation/task and prove its execution binding;
        // reused/ambiguous correlations never select an arbitrary earlier failure.
        var matches = events.Where(item => item.CorrelationId == correlationId
            && item.EventTypeName == typeof(FolderCommandRejected).FullName).ToArray();
        if (matches.Length != 1) return null;
        var rejection = matches[0];
        using JsonDocument document = JsonDocument.Parse(rejection.Payload);
        var root = document.RootElement;
        string? Field(string name, string camel) => (root.TryGetProperty(name, out var value) || root.TryGetProperty(camel, out value))
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (Field("TaskId", "taskId") != taskId || Field("CorrelationId", "correlationId") != correlationId
            || Field("CommandType", "commandType") != FoldersServerModule.MutateFilesCommandType
            || string.IsNullOrWhiteSpace(rejection.CausationId) || Field("IdempotencyKey", "idempotencyKey") != rejection.CausationId
            || !Enum.TryParse<FolderResultCode>(Field("Code", "code"), out var result) || !Enum.IsDefined(result)) return null;
        return result;
    }
}
