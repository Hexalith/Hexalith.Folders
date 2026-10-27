#if CHATBOT_NATIVE_FOLDERS_OWNER_ALIAS
extern alias FoldersOwner;
using FoldersOwner::Hexalith.Folders.Server;
using FoldersOwner::Hexalith.Folders.Server.Authorization;
using FoldersOwner::Hexalith.Folders.Server.Authentication;
#else
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Server.Authentication;
#endif

using System.Text.Json;
using System.Security.Claims;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Dapr.Client; // CACHE-NON-TENANT: this fixture reads the test event stream, not a tenant data cache.
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.NativeContent.Tests;

internal sealed class NativeContentGateway(NativeContentFixture fixture) : IEventStoreGatewayClient
{
    private readonly SemaphoreSlim _actor = new(1);
    private const string StreamKey = "native-test:retained-events";
    internal int Mutations;
    internal int StreamReads;
    internal Exception? Failure { get; private set; }
    internal string? LastDecision { get; private set; }
    internal Func<SubmitCommandRequest, Task>? AfterAppend = null;
    internal Func<SubmitCommandRequest, Task<bool>>? QueueOnly = null;
    internal readonly List<SubmitCommandRequest> Submissions = [];
    internal Task<List<StreamReadEvent>> EventsAsync() => ReadEventsAsync();
    private async Task<List<StreamReadEvent>> ReadEventsAsync()
        => await ((DaprClient)fixture.Dapr).GetStateAsync<List<StreamReadEvent>>("statestore", StreamKey).ConfigureAwait(true) ?? [];
    private async Task SaveAsync(List<StreamReadEvent> events)
    {
        var (_, etag) = await ((DaprClient)fixture.Dapr).GetStateAndETagAsync<List<StreamReadEvent>>("statestore", StreamKey).ConfigureAwait(true);
        if (!await ((DaprClient)fixture.Dapr).TrySaveStateAsync("statestore", StreamKey, events, etag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }).ConfigureAwait(true))
            throw new InvalidOperationException("Test stream append conflict.");
    }
    internal async Task SeedAsync(IEnumerable<IFolderEvent> seed)
    {
        var events = new List<StreamReadEvent>();
        foreach (var item in seed) events.Add(new(events.Count + 1, item.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(item, item.GetType()), "json", 1,
            item.IdempotencyKey, item.CorrelationId, item.IdempotencyKey, item.OccurredAt, "user-a"));
        await SaveAsync(events).ConfigureAwait(true);
    }
    internal async Task AppendSeedAsync(IFolderEvent item)
    {
        var events = await ReadEventsAsync().ConfigureAwait(true);
        events.Add(new(events.Count + 1, item.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(item, item.GetType()), "json", 1,
            item.IdempotencyKey, item.CorrelationId, item.IdempotencyKey, item.OccurredAt, "user-a"));
        await SaveAsync(events).ConfigureAwait(true);
    }
    public async Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
    {
        lock (Submissions) Submissions.Add(request);
        if (QueueOnly is { } queued && await queued(request).ConfigureAwait(true)) return new(request.CorrelationId!);
        await _actor.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            var events = await ReadEventsAsync().ConfigureAwait(true);
            using var scope = fixture.App.Services.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            var http = accessor.HttpContext ?? throw new InvalidOperationException("Native processing requires its HTTP principal.");
            var beforeServices = http.RequestServices;
            var beforePrincipal = http.User;
            var beforePath = http.Request.Path;
            try
            {
                http.RequestServices = scope.ServiceProvider;
                http.Request.Headers["X-Hexalith-Task-Id"] = request.Extensions!["taskId"]; http.Request.Headers["X-Correlation-Id"] = request.CorrelationId;
                var envelopes = events.Select(item => new EventEnvelope(new EventMetadata(item.MessageId, fixture.Folder, "folder", "tenant-a", "folders", item.SequenceNumber,
                    item.GlobalPosition, item.Timestamp, item.CorrelationId ?? "seed", item.CausationId ?? "seed", "user-a", "native-test", item.EventTypeName, 1, "json")
                { PayloadVersion = item.PayloadVersion }, item.Payload, null)).ToArray();
                // Model the gateway's persisted SDK-key admission: replay uses the original
                // execution identity, and the real owner processor still validates its fingerprint.
                string executionId = request.CommandType == FoldersServerModule.MutateFilesCommandType
                    ? events.Where(item => item.EventTypeName == typeof(WorkspaceFileMutationAccepted).FullName)
                        .Select(item => JsonSerializer.Deserialize<WorkspaceFileMutationAccepted>(item.Payload))
                        .FirstOrDefault(item => item?.ContentBinding?.AcceptedSdkKey == request.IdempotencyKey)?.ContentBinding?.CommandId ?? request.MessageId
                    : request.MessageId;
                var command = new CommandEnvelope(executionId, request.Tenant, request.Domain, request.AggregateId, request.CommandType,
                    JsonSerializer.SerializeToUtf8Bytes(request.Payload), request.CorrelationId!, null, "user-a", new Dictionary<string, string>(request.Extensions));
                // Exercise the real /process handler and signed human receipt. The process
                // principal is reconstructed from workload authority, never inherited from HTTP.
                var signed = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "user-a",
                    ["mutate_files", "lock_workspace", "read_metadata"], NativeContentFixture.ReceiptKey, fixture.Clock.Now);
                http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore")], "Workload"));
                http.Request.Path = "/process";
                http.Items.Remove("Folders:WorkspaceContentTask"); http.Items.Remove("Folders:WorkspaceContentAction");
                var processed = await scope.ServiceProvider.GetRequiredService<FoldersDomainServiceRequestHandler>()
                    .ProcessAsync(new DomainServiceRequest(command, new DomainServiceCurrentState(null, envelopes, 0, events.Count))
                    { HumanAuthorityReceipt = signed }, cancellationToken).ConfigureAwait(true);
                if (processed is not IValueHttpResult { Value: DomainServiceWireResult result })
                    throw new InvalidOperationException("Real process authorization rejected native command: " + JsonSerializer.Serialize((processed as IValueHttpResult)?.Value));
                LastDecision = LastDecision + "\n" + (result.ResultPayload ?? string.Join("; ", result.Events.Select(item => JsonSerializer.Serialize(item))));
                foreach (var item in result.Events) events.Add(new(events.Count + 1, item.EventTypeName, item.Payload, "json", 1,
                    executionId, request.CorrelationId, executionId, fixture.Clock.Now, "user-a"));
                await SaveAsync(events).ConfigureAwait(true);
                Mutations += result.Events.Count(item => item.EventTypeName == typeof(WorkspaceFileMutationAccepted).FullName);
                if (AfterAppend is { } after) await after(request).ConfigureAwait(true);
                JsonElement payload = result.ResultPayload is { } actual ? JsonSerializer.Deserialize<JsonElement>(actual)
                    : JsonSerializer.SerializeToElement(new { status = !result.IsRejection ? "accepted" : "rejected", failureCode = !result.IsRejection ? null : "AppendConflict", idempotentReplay = false });
                return new(request.CorrelationId!, payload);
            }
            finally { http.User = beforePrincipal; http.Request.Path = beforePath; http.RequestServices = beforeServices; }
        }
        catch (Exception error) { Failure = error; throw; }
        finally { _actor.Release(); }
    }
    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public async Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref StreamReads);
        var events = await ReadEventsAsync().ConfigureAwait(true);
        var page = events.Where(item => item.SequenceNumber > request.FromSequence && item.SequenceNumber <= (request.ToSequence ?? events.Count)).Take(request.PageSize).ToArray();
        return new(request.Tenant, request.Domain, request.AggregateId, page, new(request.FromSequence, request.ToSequence, page.LastOrDefault()?.SequenceNumber, events.Count, page.Length, false, null));
    }
}
