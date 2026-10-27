using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Shouldly;
using Xunit;
using PathMetadata = Hexalith.Folders.Aggregates.Folder.PathMetadata;

namespace Hexalith.Folders.Server.Tests;

internal sealed class WorkspaceContentProductionGateway : IEventStoreGatewayClient
{
    internal readonly List<IFolderEvent> Events = []; internal int Submissions;
    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
    {
        Submissions++;
        if (request.CommandType == FoldersServerModule.AuthorizeContentErasureCommandType)
            Events.Add(new WorkspaceContentErasureAuthorized("tenant-a", "tenant-a", "folder-a", "workspace-a",
                request.Payload.GetProperty("receiptId").GetString()!, request.Payload.GetProperty("expectedGeneration").GetInt64(), "user-a",
                request.CorrelationId!, request.Extensions!["taskId"], request.MessageId, "erasure-fp", DateTimeOffset.UtcNow));
        return Task.FromResult(new SubmitCommandResponse(request.CorrelationId!, JsonSerializer.SerializeToElement(new { status = "accepted", idempotentReplay = true })));
    }
    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
    {
        var events = Events.Select((e, i) => new StreamReadEvent(i + 1, e.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(e, e.GetType()), "json", 1,
            e.IdempotencyKey, e.CorrelationId, e.IdempotencyKey, e.OccurredAt, "user-a")).Where(e => e.SequenceNumber > request.FromSequence && e.SequenceNumber <= (request.ToSequence ?? Events.Count)).Take(request.PageSize).ToArray();
        return Task.FromResult(new StreamReadPage(request.Tenant, request.Domain, request.AggregateId, events,
            new(request.FromSequence, request.ToSequence, events.LastOrDefault()?.SequenceNumber, Events.Count, events.Length, false, null)));
    }
}
