using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Dapr.Client;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Tenants.Contracts.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Owns a disposable authenticated Folders topology and an isolated retained Redis store. An opted-in
/// infrastructure or authorization failure fails acceptance; it never becomes an unavailable-fixture skip.
/// </summary>
public class FolderDurabilityAcceptanceFixture : IAsyncLifetime
{
    /// <summary>Enables a distinct mutation-serving process only for content qualification.</summary>
    protected virtual bool QualifyContentReplica => false;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal ConcurrentBag<double> CommandAcknowledgementMilliseconds { get; } = [];
    internal ConcurrentBag<double> OperationalDeliveryMilliseconds { get; } = [];
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly string _runId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), "folders-durability-" + Path.GetRandomFileName());
    private IDistributedApplicationTestingBuilder? _builder;
    private DistributedApplication? _app;
    private DevelopmentRouteCatalogComposition? _catalog;
    private (string RunDirectory, string CertificateDirectory)? _localDapr;
    private DaprClient? _dapr;
    private DaprClient? _proofDapr;
    private OperationalProviderFixture? _provider;
    private Hexalith.Platform.Composition.PlatformRunOwnedWorkloadEnrollment? _workloadEnrollment;
    internal Task SetProviderRepositoryAvailableAsync(bool available, CancellationToken cancellationToken)
        => (_provider ?? throw new InvalidOperationException("The disposable provider is unavailable."))
            .SetRepositoryAccessAsync(available, cancellationToken);
    internal Task<(int Status, bool CanRead)> ObserveProviderRepositoryAccessAsync(CancellationToken cancellationToken)
        => (_provider ?? throw new InvalidOperationException("The disposable provider is unavailable."))
            .ObserveRepositoryAccessAsync(cancellationToken);
    private string? _redisName;
    private string _redisAddress = string.Empty;
    private string _humanToken = string.Empty;
    private string _foreignToken = string.Empty;
    private string _adminToken = string.Empty;
    private string _bootstrapWorkloadSecret = string.Empty;
    private string _projectWorkloadToken = string.Empty;
    private string _metadataWorkloadToken = string.Empty;
    private string? _namedCatalogFingerprint;
    private FolderDurabilityTenantMaintenance? _tenantMaintenance;

    /// <summary>Gets whether the explicit integration opt-in is present.</summary>
    public bool Enabled { get; } = Environment.GetEnvironmentVariable(AspireFoldersAppHostFixture.OptInEnvironmentVariable)
        is string value && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    /// <summary>Gets the test-only callback observation sink, retained externally when requested.</summary>
    public string FoldObservationPath => Environment.GetEnvironmentVariable("FOLDERS_TASK36_FOLD_LOG")
        ?? Path.Combine(_temporaryDirectory, "fold-callbacks.jsonl");

    /// <summary>Gets the fresh managed tenant created by this fixture's real Tenants commands.</summary>
    public string TenantId => "durability-" + _runId;

    /// <summary>Gets the subject authenticated by the local identity provider.</summary>
    public string PrincipalId { get; private set; } = string.Empty;

    /// <summary>Gets the isolated standalone Dapr namespace used for service and actor discovery.</summary>
    public string DaprNamespace => "folders-durability-" + _runId;

    private IEnumerable<string> OwningHosts => new[] { "folders", "folders-workers", "folders-workers-replica", "eventstore" }
        .Concat(QualifyContentReplica ? ["folders-replica"] : Array.Empty<string>());

    private string AppId(string name) => (name switch { "folders-workers-replica" => "folders-workers", "folders-replica" => "folders", _ => name }) + "-" + _runId;

    /// <summary>Gets the SDK store used only to inspect persisted derived rows.</summary>
    public IReadModelStore Store { get; private set; } = null!;
    /// <summary>Gets the raw SDK store for controlled generation operations.</summary>
    public IReadModelStore RawStore { get; private set; } = null!;
    /// <summary>Gets a fresh derived namespace shared only by the two authenticated ordinary-write proof processes.</summary>
    public IReadModelStore ProofStore { get; private set; } = null!;

    /// <summary>Gets the running task-owned topology.</summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("Acceptance topology is not running.");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        string tenantsHost = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../references/Hexalith.Tenants/src/Hexalith.Tenants/bin/Debug/net10.0/Hexalith.Tenants.dll"));
        if (!File.Exists(tenantsHost)) throw new InvalidOperationException(
            "Build the independently composed root-declared Tenants host in Debug/source mode before durability acceptance.");

        Directory.CreateDirectory(_temporaryDirectory);
        if (QualifyContentReplica)
            await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "content-clock-days"), "0", TestContext.Current.CancellationToken).ConfigureAwait(false);
        SetEnvironment("EnableKeycloak", "true");
        SetEnvironment("KeycloakPersistent", "false");
        SetEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
        SetEnvironment("DOTNET_ENVIRONMENT", "Development");
        SetEnvironment("NAMESPACE", DaprNamespace);
        try
        {
            _redisName = "folders-durability-" + _runId;
            _ = await FolderDurabilityProcess.RunAsync("docker", "run", "--detach", "--rm", "--name", _redisName,
                "--publish", "127.0.0.1::6379", "redis:7-alpine", "redis-server", "--appendonly", "yes").ConfigureAwait(false);
            _redisAddress = await FolderDurabilityProcess.RunAsync("docker", "port", _redisName, "6379/tcp").ConfigureAwait(false);
            _provider = new OperationalProviderFixture(_temporaryDirectory, _runId);
            await _provider.StartAsync().ConfigureAwait(false);
            await StartAsync().ConfigureAwait(false);
            await BootstrapAsync().ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Creates a discovered public Folders client with the real human bearer.</summary>
    public HttpClient CreateFoldersClient(string? token = null)
    {
        HttpClient client = App.CreateHttpClient("folders", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _humanToken);
        return client;
    }

    /// <summary>Targets a distinct discovered mutation-serving replica.</summary>
    public HttpClient CreateContentReplicaClient()
    {
        if (!QualifyContentReplica) throw new InvalidOperationException("Content replica qualification is disabled.");
        HttpClient client = App.CreateHttpClient("folders-replica", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        return client;
    }

    /// <summary>Returns the actual distinct processes serving public mutations.</summary>
    public (int Primary, int Replica) ContentServingProcessIds() => (ResourceProcessId("folders"), ResourceProcessId("folders-replica"));

    /// <summary>Advances only the SDK disposition clock; source authority, JWTs and lease clocks remain real.</summary>
    public Task AdvanceContentDispositionClockAsync(int days)
        => QualifyContentReplica ? File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "content-clock-days"),
            days.ToString(System.Globalization.CultureInfo.InvariantCulture), TestContext.Current.CancellationToken)
            : throw new InvalidOperationException("Content qualification clock is unavailable.");

    /// <summary>Checks actual body-key absence after governed physical deletion without reading content into diagnostics.</summary>
    public async Task<bool> QualificationPersistedKeyExistsAsync(string key)
        => (await FolderDurabilityProcess.RunAsync("docker", "exec", _redisName!, "redis-cli", "--raw", "EXISTS", key).ConfigureAwait(false)).Trim() == "1";

    /// <summary>Introduces and restores bounded missing/corrupt-body faults inside the task-owned SDK Redis key, without outputting bytes.</summary>
    public async Task SetContentBodyFaultAsync(string key, string fault)
    {
        const string script = "if ARGV[1]=='missing' then redis.call('RENAME',KEYS[1],KEYS[1]..':qualification-missing') "
            + "elseif ARGV[1]=='corrupt' then local d=redis.call('HGET',KEYS[1],'data'); redis.call('HSET',KEYS[1],'qualification-original',d); local v=cjson.decode(d); v.bytes='AA=='; redis.call('HSET',KEYS[1],'data',cjson.encode(v)) "
            + "elseif ARGV[1]=='restore' then if redis.call('EXISTS',KEYS[1]..':qualification-missing')==1 then redis.call('RENAME',KEYS[1]..':qualification-missing',KEYS[1]) else local d=redis.call('HGET',KEYS[1],'qualification-original'); if d then redis.call('HSET',KEYS[1],'data',d); redis.call('HDEL',KEYS[1],'qualification-original') end end else return redis.error_reply('unknown qualification fault') end; return 1";
        _ = await FolderDurabilityProcess.RunAsync("docker", "exec", _redisName!, "redis-cli", "--raw", "EVAL", script, "1", key, fault).ConfigureAwait(false);
    }

    /// <summary>Sends a metadata-only public mutation with fresh request tracing.</summary>
    public async Task<HttpResponseMessage> MutateAsync(string path, object payload, string key, string? token = null,
        string? taskId = null, HttpMethod? method = null, bool retryTransientFailures = true)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken).ConfigureAwait(false);
        using HttpClient client = CreateFoldersClient(token);
        using HttpRequestMessage request = new(method ?? HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", NewId());
        request.Headers.Add("X-Hexalith-Task-Id", taskId ?? NewId());
        long started = Stopwatch.GetTimestamp();
        HttpResponseMessage response = retryTransientFailures
            ? await FolderDurabilityHttp.SendAsync(client, request, false, TestContext.Current.CancellationToken).ConfigureAwait(false)
            : await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Accepted) CommandAcknowledgementMilliseconds.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return response;
    }

    /// <summary>Submits an organization command through the authenticated public EventStore gateway.</summary>
    public async Task<JsonElement> OrganizationCommandAsync(string command, string[] actions)
    {
        JsonElement result = default;
        string[][] batches = command == "InitializeOrganizationAclBaseline" ? [actions]
            : actions.Select(action => new[] { action }).ToArray();
        foreach (string[] batch in batches)
        {
            // The baseline accepts a set; each grant/revocation has one authoritative operation.
            result = await SubmitAsync(TenantId, "folders", TenantId, "Hexalith.Folders.Commands." + command,
                new { requestSchemaVersion = "v1", operations = batch.Select(action => new { principalKind = "User", principalId = PrincipalId, action }).ToArray() }, _humanToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Grants real folder-level inspection actions through the authenticated command gateway.</summary>
    public async Task GrantFolderActionsAsync(string folderId, string[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        foreach (string action in actions)
        {
            _ = await SubmitAsync(TenantId, "folders", folderId, "Hexalith.Folders.Commands.GrantFolderAccess",
                new
                {
                    requestSchemaVersion = "v1",
                    operations = new[] {
                    new { principalKind = "user", principalId = PrincipalId, action } }
                }, _humanToken).ConfigureAwait(false);
            await WaitForCurrentFolderEvidenceAsync(folderId, "folder access grant").ConfigureAwait(false);
        }
    }

    /// <summary>Observes authorized current ACL and operational watermarks before the next positive mutation.</summary>
    public async Task WaitForCurrentFolderEvidenceAsync(string folderId, string phase, bool replica = false)
    {
        await MaintainTenantAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        using HttpClient client = replica ? CreateContentReplicaClient() : CreateFoldersClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/folders/{folderId}/effective-permissions");
        await FolderDurabilityHttp.WaitForCurrentFolderAsync(client, request, folderId, phase, async token =>
        {
            StreamReadPage retained = await ReadStreamWithDeadlineAsync(folderId, token).ConfigureAwait(false);
            var checkpoint = await Store.GetAsync<OperationalStatusCheckpoint>("statestore",
                OperationalStatusKeys.Checkpoint(TenantId, folderId), token).ConfigureAwait(false);
            return (retained.Metadata.LatestSequence, checkpoint.Value?.Sequence ?? 0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads complete retained bytes, bounding retries for authenticated gateway throttling/unavailability.</summary>
    public Task<StreamReadPage> ReadStreamAsync(string aggregateId)
        => ReadStreamWithDeadlineAsync(aggregateId, TestContext.Current.CancellationToken);

    /// <summary>Creates a real rejected command without a body receipt, preserving Locked task state for orphan qualification.</summary>
    public async Task SeedRejectedContentIntentAsync(string folder, string workspace, string task, long fence, string key)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(new SubmitCommandRequest(NewId(), TenantId, "folders", folder,
                "Hexalith.Folders.Commands.MutateFiles", JsonSerializer.SerializeToElement(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = workspace,
                    operationId = NewId(),
                    fileOperationKind = "add",
                    taskId = task,
                    transportOperation = "PutFileInline",
                    pathMetadata = new { normalizedPath = "docs/rejected.txt", displayName = "rejected.txt", pathPolicyClass = "content_allowed", unicodeNormalization = "NFC" },
                    contentHashReference = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1 })),
                    byteLength = 1,
                    mediaType = "application/octet-stream",
                    transportEvidenceKind = "inline_decoded",
                    observedByteLength = 1,
                    expectedFileVersion = 0,
                    expectedLockFence = fence,
                }), Extensions: new Dictionary<string, string> { ["taskId"] = task }, IdempotencyKey: key)),
        };
        request.Headers.Add("X-Correlation-Id", NewId());
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.UnprocessableEntity, "Real receipt-less command rejection").ConfigureAwait(false);
    }

    /// <summary>Submits the private metadata-only batch through the real authenticated EventStore admission/actor path.</summary>
    public async Task<HttpResponseMessage> SubmitContentBatchAsync(string folder, string workspace, string task, object[] items)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        return await client.PostAsJsonAsync("/api/v1/commands", new SubmitCommandRequest(NewId(), TenantId, "folders", folder,
            "Hexalith.Folders.Commands.MutateFiles", JsonSerializer.SerializeToElement(new
            { requestSchemaVersion = "v1", workspaceId = workspace, taskId = task, items }),
            Extensions: new Dictionary<string, string> { ["taskId"] = task }, IdempotencyKey: NewId()), TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    internal async Task<CommandStatusQueryResponse?> ObserveCommandStatusAsync(string correlationId, CancellationToken cancellationToken)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/commands/status/" + correlationId);
        FolderDurabilityGatewayScope.Apply(request, TenantId);
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await RequireStatusAsync(response, HttpStatusCode.OK, "Authenticated diagnostic command status").ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<CommandStatusQueryResponse>(Json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a complete retained prefix within both the caller's deadline and a bounded retry deadline.</summary>
    public async Task<StreamReadPage> ReadStreamWithDeadlineAsync(string aggregateId, CancellationToken cancellationToken)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/streams/read")
        { Content = JsonContent.Create(new StreamReadRequest(TenantId, "folders", aggregateId, PageSize: 1000)) };
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, cancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "Authenticated retained stream read").ConfigureAwait(false);
        StreamReadPage page = (await response.Content.ReadFromJsonAsync<StreamReadPage>(Json, cancellationToken).ConfigureAwait(false))!;
        if (page.Metadata.IsTruncated)
            throw new InvalidOperationException("Acceptance requires a complete retained prefix.");

        return page;
    }

    /// <summary>Proves a failed public create produced neither an accepted event stream nor a derived folder row.</summary>
    public async Task RequireCreateAbsentAsync(string key)
    {
        string folderId = CreatedFolderId(key);
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/streams/read")
        { Content = JsonContent.Create(new StreamReadRequest(TenantId, "folders", folderId)) };
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.NotFound, "Denied create retained stream absence").ConfigureAwait(false);
        if ((await Store.GetAsync<FolderPermissionProjection>("statestore", "projection:folders:folder-permissions:" + TenantId + ":" + folderId,
            TestContext.Current.CancellationToken).ConfigureAwait(false)).Value is not null)
        {
            throw new InvalidOperationException("A failed create unexpectedly persisted a folder projection.");
        }
    }

    /// <summary>Projects a complete zero-checkpoint prefix under a real operation-bound workload token.</summary>
    public async Task<HttpResponseMessage> ProjectAsync(string aggregateId, ProjectionEventDto[] events)
    {
        using HttpClient client = App.CreateHttpClient("folders", "http");
        using HttpRequestMessage request = new(HttpMethod.Post, "/project")
        {
            Content = JsonContent.Create(new ProjectionRequest(TenantId, "folders", aggregateId, events)),
        };
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, _projectWorkloadToken);
        BindCommittedCatalog(request);
        request.Headers.Add("dapr-api-token", await ParameterAsync("folders-app-api-token").ConfigureAwait(false));
        return await FolderDurabilityHttp.SendAsync(client, request, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Discovers and invokes the SDK-owned bounded shared rebuild protocol.</summary>
    public async Task<HttpResponseMessage> NamedProjectAsync(string aggregateId, ProjectionEventDto[] events,
        string dispatchId, bool stopAfterStage = false,
        IReadOnlyList<(string AggregateId, ProjectionEventDto[] Events)>? additionalOwners = null, TimeSpan? retryBudget = null,
        Action<string>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Length == 0)
            throw new ArgumentException("A complete retained history is required.", nameof(events));
        using HttpClient client = App.CreateHttpClient("folders", "http");
        if (retryBudget is not null)
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            await RefreshCapacityWorkloadTokensAsync().ConfigureAwait(false);
        }
        List<(string AggregateId, ProjectionEventDto[] Events)> owners = [(aggregateId, events)];
        if (additionalOwners is not null) owners.AddRange(additionalOwners);
        string catalog = await DiscoverNamedProjectionAsync(client).ConfigureAwait(false);
        DomainSharedProjectionRebuildIdentity identity = new(TenantId, "folders", OperationalStatusKeys.ProjectionType,
            dispatchId, catalog);
        observe?.Invoke("TASK36 session=" + JsonSerializer.Serialize(identity));
        string capture = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < owners.Count; ordinal++)
            capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(capture, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(owners[ordinal].AggregateId, false, owners[ordinal].Events));
        observe?.Invoke($"TASK36 operation={dispatchId} capture={capture} sourceHashes={string.Join(",", owners.Select(owner => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(owner.Events)))))}");
        async Task<DomainSharedProjectionRebuildResponse> StepAsync(DomainSharedProjectionRebuildRequest body)
        {
            if (retryBudget is not null) await RefreshCapacityWorkloadTokensAsync().ConfigureAwait(false);
            using HttpRequestMessage request = new(HttpMethod.Post, "/project/rebuild/shared/v1") { Content = JsonContent.Create(body) };
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, _projectWorkloadToken);
            BindCommittedCatalog(request);
            request.Headers.Add("dapr-api-token", await ParameterAsync("folders-app-api-token").ConfigureAwait(false));
            long started = Stopwatch.GetTimestamp();
            var result = await FolderDurabilitySharedRebuild.SendAsync(client, request, body.Action,
                TestContext.Current.CancellationToken, retryBudget).ConfigureAwait(false);
            if (retryBudget is not null)
                observe?.Invoke($"CAPACITY phase={body.Action} ownerOrdinal={body.AggregateOrdinal} part={body.PartOrdinal} events={body.Events?.Length ?? 0} elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} status={result.Status} sdkPhase={result.Phase} reason={result.ReasonCode}");
            if (result.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted))
                throw new InvalidOperationException("SDK shared " + body.Action + " did not complete: " + result.ReasonCode);
            return result;
        }
        try
        {
            var current = await StepAsync(new(1, DomainSharedProjectionRebuildAction.Begin, identity, CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: owners.ToDictionary(static owner => owner.AggregateId, static owner => owner.Events[^1].GlobalPosition))).ConfigureAwait(false);
            // Begin validates the unchanged capture even when an existing durable session is resumed.
            // Sealed sessions cannot accept transport parts again; resume from the SDK's proven phase.
            if (current.Phase == DomainSharedProjectionRebuildPhase.Accumulating)
            {
                DomainSharedProjectionRebuildResponse? accumulated = null;
                for (int ordinal = 0; ordinal < owners.Count; ordinal++)
                {
                    var owner = owners[ordinal];
                    int part = 0;
                    foreach (ProjectionEventDto[] page in owner.Events.Chunk(256))
                        await StepAsync(new(1, DomainSharedProjectionRebuildAction.AccumulatePart, identity,
                            AggregateOrdinal: ordinal, AggregateId: owner.AggregateId, Events: page, PartOrdinal: part++)).ConfigureAwait(false);
                    accumulated = await StepAsync(new(1, DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                        AggregateOrdinal: ordinal, AggregateId: owner.AggregateId, PartCount: part,
                        ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory(owner.AggregateId, false, owner.Events))).ConfigureAwait(false);
                }
                current = await StepAsync(new(1, DomainSharedProjectionRebuildAction.Finalize, identity,
                    ExpectedAggregateCount: owners.Count, ExpectedInventoryFingerprint: accumulated!.InventoryFingerprint)).ConfigureAwait(false);
            }
            if (current.Phase == DomainSharedProjectionRebuildPhase.Finalized)
                current = await StepAsync(new(1, DomainSharedProjectionRebuildAction.Stage, identity)).ConfigureAwait(false);
            if (current.Phase is not (DomainSharedProjectionRebuildPhase.Prepared or DomainSharedProjectionRebuildPhase.Committed))
                throw new InvalidOperationException("SDK shared rebuild cannot resume from phase " + current.Phase);
            var final = stopAfterStage ? current : await StepAsync(new(1, DomainSharedProjectionRebuildAction.Commit, identity)).ConfigureAwait(false);
            if (!stopAfterStage)
                await StepAsync(new(1, DomainSharedProjectionRebuildAction.Verify, identity)).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(final) };
        }
        catch
        {
            // A failed private generation is aborted through the same authenticated SDK transport.
            await StepAsync(new(1, DomainSharedProjectionRebuildAction.Abort, identity)).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> DiscoverNamedProjectionAsync(HttpClient client)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/admin/operational-index-metadata")
        {
            Content = JsonContent.Create(new AdminOperationalIndexMetadata.Request(["folders"])
            { AppId = AppId("folders"), ServiceVersion = "v1" }),
        };
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, _metadataWorkloadToken);
        BindCommittedCatalog(request);
        request.Headers.Add("dapr-api-token", await ParameterAsync("folders-app-api-token").ConfigureAwait(false));
        using HttpResponseMessage discovery = await FolderDurabilityHttp.SendAsync(client, request, true,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(discovery, HttpStatusCode.OK, "SDK named/shared discovery").ConfigureAwait(false);
        var metadata = (await discovery.Content.ReadFromJsonAsync<AdminOperationalIndexMetadata.Response>(TestContext.Current.CancellationToken).ConfigureAwait(false))!;
        var domain = metadata.Domains.Single(item => item.Domain == "folders");
        if (domain.NamedProjectionTypes?.Contains(OperationalStatusKeys.ProjectionType) != true
            || domain.SharedEpochProjectionTypes?.Contains(OperationalStatusKeys.ProjectionType) != true)
            throw new InvalidOperationException("Production discovery omitted the operational shared route.");
        return _namedCatalogFingerprint = metadata.CatalogFingerprint!;
    }

    /// <summary>Delivers an original capacity-test source through the authenticated SDK named dispatcher.</summary>
    public async Task<ProjectionDispatchOutcome> NamedDeliveryAsync(string aggregateId, ProjectionEventDto source)
    {
        await RefreshCapacityWorkloadTokensAsync().ConfigureAwait(false);
        using HttpClient client = App.CreateHttpClient("folders", "http");
        string catalog = _namedCatalogFingerprint ?? await DiscoverNamedProjectionAsync(client).ConfigureAwait(false);
        ProjectionDispatchRequest body = new(new(TenantId, "folders", aggregateId, [source]),
            [OperationalStatusKeys.ProjectionType], NewId(), catalog);
        using HttpRequestMessage request = new(HttpMethod.Post, "/project/v2") { Content = JsonContent.Create(body) };
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, _projectWorkloadToken);
        BindCommittedCatalog(request);
        request.Headers.Add("dapr-api-token", await ParameterAsync("folders-app-api-token").ConfigureAwait(false));
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "SDK named capacity delivery").ConfigureAwait(false);
        Console.WriteLine("TASK36 named-response " + await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
        return (await response.Content.ReadFromJsonAsync<ProjectionDispatchResponse>(TestContext.Current.CancellationToken)
            .ConfigureAwait(false))!.Outcomes.Single();
    }

    /// <summary>Observes actual mapped host binaries through the caller's retained test output.</summary>
    public void RecordRuntimeBindings(Action<string> observe)
    {
        ArgumentNullException.ThrowIfNull(observe);
        foreach (string resource in OwningHosts)
            FolderDurabilityRuntimeBinding.Record(resource, ResourceProcessId(resource), observe);
    }

    /// <summary>Redelivers an actual retained event to the task-owned authenticated worker callback.</summary>
    public async Task<HttpResponseMessage> DeliverRetainedFolderEventAsync(string aggregateId, StreamReadEvent source,
        string workerResource = "folders-workers")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(source);
        using HttpClient client = App.CreateHttpClient(workerResource, "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Add("dapr-api-token", await ParameterAsync(workerResource + "-app-api-token").ConfigureAwait(false));
        var envelope = new Hexalith.EventStore.Client.Subscriptions.EventStoreDomainEventEnvelope(
            source.MessageId, aggregateId, TenantId, source.EventTypeName, source.SequenceNumber,
            source.Timestamp, source.CorrelationId!, source.SerializationFormat, source.Payload)
        {
            Domain = "folders",
            MetadataVersion = source.MetadataVersion,
            PayloadVersion = source.PayloadVersion,
            GlobalPosition = source.GlobalPosition,
            UserId = source.UserId,
        };
        long started = Stopwatch.GetTimestamp();
        HttpResponseMessage response = await client.PostAsJsonAsync("/folders/events", envelope, TestContext.Current.CancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) OperationalDeliveryMilliseconds.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return response;
    }

    /// <summary>Restarts the domain hosts while retaining the same identity provider, broker and state store.</summary>
    public async Task<IReadOnlyDictionary<string, (int Before, int After)>> RestartHostsAsync()
    {
        await MaintainTenantAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        Dictionary<string, (int Before, int After)> identities = [];
        foreach (string resource in OwningHosts)
        {
            int before = ResourceProcessId(resource);
            FolderDurabilityRuntimeBinding.Record(resource, before);
            await ResourceCommandAsync(resource, KnownResourceCommands.StopCommand).ConfigureAwait(false);
            await ResourceCommandAsync(resource, KnownResourceCommands.StartCommand).ConfigureAwait(false);
            int after = ResourceProcessId(resource);
            if (before == after)
            {
                throw new InvalidOperationException($"Task-owned resource {resource} did not change process identity.");
            }

            identities.Add(resource, (before, after));
            FolderDurabilityRuntimeBinding.Record(resource, after);
        }

        await MaintainTenantAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return identities;
    }

    private int ResourceProcessId(string name)
    {
        if (!App.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state))
        {
            throw new InvalidOperationException($"Task-owned resource {name} has no observed lifecycle state.");
        }

        object? value = state.Snapshot.Properties.Single(property => property.Name == "executable.pid").Value;
        int processId = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        return processId > 0 ? processId : throw new InvalidOperationException($"Task-owned resource {name} has no process identity.");
    }

    /// <summary>Stops or starts a task-owned resource, bounding the command and readiness wait.</summary>
    public async Task ResourceCommandAsync(string name, string command)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
        IResource resource = _builder!.Resources.Single(item => item.Name == name);
        ExecuteCommandResult result = await App.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(resource, command, timeout.Token).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Task-owned resource {name} command {command} failed.");
        }

        if (command == KnownResourceCommands.StartCommand || command == KnownResourceCommands.RestartCommand)
        {
            await App.ResourceNotifications.WaitForResourceHealthyAsync(name, timeout.Token).ConfigureAwait(false);
            await FolderDurabilityRuntimeBinding.RecordWhenReadyAsync(name, ResourceProcessId(name), timeout.Token).ConfigureAwait(false);
        }
        else
        {
            await App.ResourceNotifications.WaitForResourceAsync(name, KnownResourceStates.Finished, timeout.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Pauses or resumes only this fixture's isolated Redis dependency.</summary>
    public async Task SetStoragePausedAsync(bool paused)
        => _ = await FolderDurabilityProcess.RunAsync("docker", paused ? "pause" : "unpause", _redisName!).ConfigureAwait(false);

    /// <summary>Inspects the actual Redis TTL of the SDK-selected task-owned derived row.</summary>
    public async Task<long> OperationalTtlSecondsAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string physical = await ((SelectedOperationalAcceptanceStore)Store).PhysicalKeyAsync(key,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        string value = await FolderDurabilityProcess.RunAsync("docker", "exec", _redisName!, "redis-cli", "--raw", "TTL", physical).ConfigureAwait(false);
        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Observes final retained SDK values in the disposable Redis store without reading payloads into evidence.</summary>
    public async Task<string> RetainedOperationalBoundsAsync()
    {
        const string script = "local c='0'; local n=0; local m=0; local i=0; local p=0; repeat local r=redis.call('SCAN',c,'MATCH','shared*','COUNT',1000); c=r[1]; for _,k in ipairs(r[2]) do if redis.call('TYPE',k).ok=='hash' then local b=redis.call('HSTRLEN',k,'data'); n=n+1; m=math.max(m,b); if string.find(k,'shared-identity-v1:',1,true)==1 then i=math.max(i,b) end; if string.find(k,'shared-checkpoint:',1,true)==1 then p=math.max(p,b) end end end until c=='0'; return {n,m,i,p}";
        return await FolderDurabilityProcess.RunAsync("docker", "exec", _redisName!, "redis-cli", "--raw", "EVAL", script, "0").ConfigureAwait(false);
    }

    /// <summary>Reads exact persisted JSON bytes from the isolated Redis diagnostic sink.</summary>
    public async Task<string> QualificationPersistedValueAsync(string key)
    {
        const string script = "local v=redis.call('HGET',KEYS[1],'data'); if not v then return redis.error_reply('qualification proof value missing') end; return cjson.encode({string.len(v),v})";
        string encoded = await FolderDurabilityProcess.RunAsync("docker", "exec", _redisName!, "redis-cli", "--raw", "EVAL", script, "1", key).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(encoded);
        string raw = document.RootElement[1].GetString()!;
        if (Encoding.UTF8.GetByteCount(raw) != document.RootElement[0].GetInt32() || raw.Length == 0)
            throw new InvalidOperationException("Persisted qualification bytes are missing or changed in transport.");
        return raw;
    }

    /// <summary>Removes one task-owned derived proof value for a fail-closed qualification control.</summary>
    public Task RemoveQualificationProofAsync(string key)
        => _dapr!.DeleteStateAsync("statestore", key, cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>Gets the distinct actual process identities used by the ordinary-write lane.</summary>
    public (int First, int Second) OrdinaryProofProcessIds()
        => (ResourceProcessId("folders-workers-proof-a"), ResourceProcessId("folders-workers-proof-b"));

    /// <summary>Gets a real JWT whose tenant claim is foreign to the fixture's folder.</summary>
    public Task<string> ForeignTokenAsync() => Task.FromResult(_foreignToken);

    /// <summary>Creates a unique request identifier without relying on a caller-selected domain identity.</summary>
    public static string NewId() => Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();

    /// <summary>Returns the existing v1 server's deterministic actor identity for a create request.</summary>
    public string CreatedFolderId(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return "fld-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{TenantId.Length}:{TenantId}|{key.Length}:{key}")))[..40].ToLowerInvariant();
    }

    /// <summary>Compares an HTTP status without including credentials or protected response payloads.</summary>
    public static async Task RequireStatusAsync(HttpResponseMessage response, HttpStatusCode expected, string operation, CancellationToken? cancellationToken = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode != expected)
        {
            string code = "unavailable";
            try
            {
                JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken ?? TestContext.Current.CancellationToken).ConfigureAwait(false);
                if (body.TryGetProperty("code", out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    code = value.GetString()!;
                }
            }
            catch (JsonException) { }

            throw new InvalidOperationException($"{operation}: expected {(int)expected}, observed {(int)response.StatusCode}; code={code}.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        ExceptionDispatchInfo? firstFailure = null;
        void Record(Exception exception) => firstFailure ??= ExceptionDispatchInfo.Capture(exception);

        try
        {
            _dapr?.Dispose();
            _proofDapr?.Dispose();
        }
        catch (Exception exception)
        {
            Record(exception);
        }
        finally
        {
            _dapr = null;
            _proofDapr = null;
        }

        if (_app is not null)
        {
            try
            {
                // Flush terminal resource diagnostics, including failed callbacks after EventsStored.
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
                await _app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _app = null;
            }
        }

        if (_builder is not null)
        {
            try
            {
                await _builder.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _builder = null;
            }
        }

        if (_workloadEnrollment is not null)
        {
            try { await _workloadEnrollment.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { Record(exception); }
            finally { _workloadEnrollment = null; }
        }

        if (_catalog is not null)
        {
            try { await _catalog.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { Record(exception); }
            finally { _catalog = null; }
        }

        if (_localDapr is { } identityInfrastructure)
        {
            foreach (string path in new[] { identityInfrastructure.CertificateDirectory, identityInfrastructure.RunDirectory })
            {
                try
                {
                    if (!await Hexalith.Builds.Tooling.Runtime.CompositionWorkspace.TryDeleteAsync(path, CancellationToken.None).ConfigureAwait(false))
                        throw new IOException("The task-owned Dapr identity directory could not be removed: " + path);
                }
                catch (Exception exception) { Record(exception); }
            }
            _localDapr = null;
        }

        if (_redisName is not null)
        {
            try
            {
                // rm --force also handles a dependency left paused by a failed assertion.
                _ = await FolderDurabilityProcess.RunAsync("docker", "rm", "--force", "--volumes", _redisName).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _redisName = null;
            }
        }

        if (_provider is not null)
        {
            try { await _provider.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { Record(exception); }
            finally { _provider = null; }
        }

        foreach ((string name, string? value) in _environment)
        {
            try
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
        }

        _environment.Clear();
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Record(exception);
        }

        firstFailure?.Throw();
    }

    /// <summary>Preserves mandatory fold evidence while forwarding the optional lifecycle control only to EventStore.</summary>
    internal static void ConfigureObservers(IResourceBuilder<ProjectResource> folders, IResourceBuilder<ProjectResource> eventStore,
        string observer, string foldPath, string shutdownPath, string? lifecycleDiagnosticsDisabled)
    {
        folders.WithEnvironment("DOTNET_STARTUP_HOOKS", observer)
            .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Task36FoldObserver")
            .WithEnvironment("FOLDERS_TASK36_FOLD_LOG", foldPath)
            .WithEnvironment("FOLDERS_TASK36_SHUTDOWN_LOG", "");
        eventStore.WithEnvironment("DOTNET_STARTUP_HOOKS", observer)
            .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Task36FoldObserver")
            .WithEnvironment("FOLDERS_TASK36_FOLD_LOG", "")
            .WithEnvironment("FOLDERS_TASK36_SHUTDOWN_LOG", shutdownPath)
            .WithEnvironment("FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED", lifecycleDiagnosticsDisabled ?? "");
    }

    private async Task StartAsync()
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        // Standalone Dapr loads --config separately from watched resources. Keeping the same
        // Configuration in both paths makes unrelated directory events trigger a runtime reload.
        string componentDirectory = Path.Combine(_temporaryDirectory, "components");
        string configurationDirectory = Path.Combine(_temporaryDirectory, "configuration");
        Directory.CreateDirectory(componentDirectory);
        Directory.CreateDirectory(configurationDirectory);
        _builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false", "--Folders:WorkloadEnrollment:DeferToCohort=true"], timeout.Token).ConfigureAwait(false);
        // The owner's single startup bootstrap sees real signed credentials from its first attempt.
        // Only this disposable realm import is extended; the checked-in issuer and business grants stay intact.
        KeycloakResource security = _builder.Resources.OfType<KeycloakResource>().Single(resource => resource.Name == "security");
        string realmDirectory = Path.Combine(_temporaryDirectory, "realm");
        Directory.CreateDirectory(realmDirectory);
        JsonObject importedRealm = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_builder.AppHostDirectory,
            "KeycloakRealms", "hexalith-realm.json"), timeout.Token).ConfigureAwait(false))!.AsObject();
        string[] sourceHostNames = ["folders", "folders-workers", "folders-workers-proof-a", "folders-workers-proof-b"];
        string[] sourceCallers = [.. sourceHostNames.Select(AppId)];
        await File.WriteAllTextAsync(Path.Combine(realmDirectory, "hexalith-realm.json"),
            FolderDurabilitySourceReadConfiguration.Realm(FolderDurabilityBootstrapConfiguration.Realm(importedRealm,
                AppId("eventstore"), AppId("tenants"), AppId("folders")), sourceCallers).ToJsonString(), timeout.Token).ConfigureAwait(false);
        FolderDurabilityBootstrapConfiguration.RealmImport(_builder.CreateResourceBuilder(security), realmDirectory);
        string tenantsSecret = _bootstrapWorkloadSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tenantsCredential = _builder.AddParameter("durability-tenants-workload-secret", () => tenantsSecret, secret: true);
        _builder.CreateResourceBuilder(security).WithEnvironment("HEXALITH_TENANTS_WORKLOAD_CLIENT_SECRET", tenantsCredential);
        using RSA bootstrapKey = RSA.Create(2048);
        string bootstrapPublicKey = Convert.ToBase64String(bootstrapKey.ExportSubjectPublicKeyInfo());
        string bootstrapPrivateKey = Convert.ToBase64String(bootstrapKey.ExportPkcs8PrivateKey());
        var bootstrapProof = _builder.AddParameter("durability-tenants-bootstrap-proof", () => bootstrapPrivateKey, secret: true);
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "tenants"))
            .WithEnvironment("Tenants__BootstrapGlobalAdminEnabled", "true")
            .WithEnvironment("Tenants__BootstrapGlobalAdminUserId", "admin-user")
            .WithEnvironment("Tenants__BootstrapBoundWorkloadEnabled", "true")
            .WithEnvironment("Tenants__EventStoreAppId", AppId("eventstore"))
            .WithEnvironment("EventStore__Authentication__Authority", ReferenceExpression.Create($"{_builder.CreateResourceBuilder(security).GetEndpoint("http")}/realms/hexalith"))
            .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
            .WithEnvironment("EventStore__Authentication__Username", "admin-user")
            .WithEnvironment("EventStore__Authentication__Password", "admin-pass")
            .WithEnvironment("Authentication__WorkloadIssuer__Workload", AppId("tenants"))
            .WithEnvironment("Authentication__WorkloadIssuer__ClientId", AppId("tenants"))
            .WithEnvironment("Authentication__WorkloadIssuer__ClientSecret", tenantsCredential)
            .WithEnvironment("Authentication__BoundStreamRead__PrivateKey", bootstrapProof);
        FolderDurabilityBootstrapConfiguration.ProjectionPolling(
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")))
            .WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + AppId("tenants"), bootstrapPublicKey)
            .WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + AppId("tenants"), "tenants,global-administrators")
            .WithEnvironment("Authentication__BoundCommand__AllowedDomains__" + AppId("tenants"), "global-administrators")
            .WithEventStoreTrustedEffectSubmitter(AppId("tenants"));
        ProjectResource originalWorker = _builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders-workers");
        DaprSidecarOptions originalOptions = originalWorker.Annotations.OfType<DaprSidecarAnnotation>().Single()
            .Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        IResourceBuilder<ProjectResource> replica = _builder.AddProject<Projects.Hexalith_Folders_Workers>("folders-workers-replica");
        replica.WithReference(_builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")))
            .WithReference(_builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "tenants")))
            .WithDaprSidecar(sidecar =>
            {
                sidecar.WithOptions(originalOptions with { AppId = "folders-workers" });
                foreach (DaprComponentReferenceAnnotation component in originalWorker.Annotations.OfType<DaprSidecarAnnotation>().Single()
                    .Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>())
                    sidecar.WithReference(_builder.CreateResourceBuilder(component.Component));
            });
        replica.WithGeneratedEventStoreAppChannelToken();
        foreach (string name in new[] { "folders-workers-proof-a", "folders-workers-proof-b" })
        {
            _builder.AddProject<Projects.Hexalith_Folders_Workers>(name)
                .WithReference(_builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")))
                .WithDaprSidecar(sidecar => sidecar.WithOptions(originalOptions with { AppId = AppId(name) }))
                .WithGeneratedEventStoreAppChannelToken();
        }
        // Physical replicas retain the primary's real authentication configuration while each
        // sidecar keeps its own generated channel token and its actual catalog host identity.
        foreach (ProjectResource worker in _builder.Resources.OfType<ProjectResource>().Where(project =>
            project.Name.StartsWith("folders-workers-", StringComparison.Ordinal)))
        {
            _builder.CreateResourceBuilder(worker).WithEnvironment(async context =>
            {
                Dictionary<string, object> primaryEnvironment = new(StringComparer.Ordinal);
                foreach (EnvironmentCallbackAnnotation annotation in originalWorker.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await annotation.Callback(new EnvironmentCallbackContext(_builder.ExecutionContext, originalWorker,
                        primaryEnvironment, context.CancellationToken)).ConfigureAwait(false);
                foreach (var setting in primaryEnvironment.Where(setting => setting.Key.StartsWith("Authentication__", StringComparison.OrdinalIgnoreCase)
                    || setting.Key.StartsWith("Authentication:", StringComparison.OrdinalIgnoreCase)))
                    context.EnvironmentVariables[setting.Key] = setting.Value;
            });
        }
        // An unavailable optional telemetry receiver must not keep these disposable hosts alive
        // beyond DCP's process-stop deadline. Production exporter defaults stay unchanged.
        foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
        {
            IResourceBuilder<ProjectResource> host = _builder.CreateResourceBuilder(project);
            foreach (string signal in new[] { "", "TRACES_", "METRICS_", "LOGS_" })
            {
                host.WithEnvironment("OTEL_EXPORTER_OTLP_" + signal + "TIMEOUT", "1000");
            }
        }
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"))
            .WithEnvironment("EventStore__ProjectionDispatch__MaxRebuildEventCount", "40000");
        string observer = Environment.GetEnvironmentVariable("FOLDERS_TASK36_OBSERVER")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/tools/Task36FoldObserver/bin/Debug/net10.0/Task36FoldObserver.dll"));
        if (!File.Exists(observer)) throw new InvalidOperationException("Build the Task36FoldObserver test project before running durability acceptance.");
        ConfigureObservers(
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders")),
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")),
            observer, FoldObservationPath, Environment.GetEnvironmentVariable("FOLDERS_TASK36_SHUTDOWN_LOG")
                ?? Path.Combine(_temporaryDirectory, "eventstore-lifecycle.jsonl"),
            Environment.GetEnvironmentVariable("FOLDERS_TASK36_LIFECYCLE_DIAGNOSTICS_DISABLED"));
        if (QualifyContentReplica)
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"))
                .WithEnvironment("FOLDERS_CONTENT_QUALIFICATION_CLOCK", Path.Combine(_temporaryDirectory, "content-clock-days"));


        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore"))
            .WithEnvironment("Logging__LogLevel__Hexalith.EventStore.Server.Commands.CommandRouter", "Debug")
            .WithEnvironment("Logging__LogLevel__Hexalith.EventStore.Server.Pipeline.SubmitCommandHandler", "Debug")
            .WithEnvironment("Logging__LogLevel__Hexalith.EventStore.Server.Projections.ProjectionUpdateOrchestrator", "Debug")
            .WithEnvironment("Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics", "Information")
            .WithEnvironment("Logging__LogLevel__Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker", "Information")
            .WithEnvironment("Logging__LogLevel__Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor", "Information");
        // Retain standard request/status timing for the independent Folders metadata client.
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"))
            .WithEnvironment("Logging__LogLevel__System.Net.Http.HttpClient.Hexalith.EventStore.NativeSourceMetadata", "Information")
            .WithEnvironment("Logging__LogLevel__Hexalith.Folders.Queries.Folders.TaskStatusQueryHandler", "Debug");
        if (_provider is not null)
        {
            string secretComponent = Path.Combine(componentDirectory, "provider-secretstore.yaml");
            await File.WriteAllTextAsync(secretComponent,
                $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: folders-provider-credentials\nspec:\n  type: secretstores.local.file\n  version: v1\n  metadata:\n    - name: secretsFile\n      value: '{_provider.SecretFile}'\n    - name: multiValued\n      value: 'true'\nscopes:\n  - {AppId("folders")}\n", timeout.Token).ConfigureAwait(false);
            IResourceBuilder<ProjectResource> folders = _builder.CreateResourceBuilder(
                _builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"));
            folders.WithEnvironment("SSL_CERT_FILE", _provider.TrustFile)
                .WithEnvironment("Logging__LogLevel__System.Net.Http.HttpClient.Hexalith.Folders.Forgejo", "Warning");
            const string profile = "Folders__ProviderBindings__Profiles__operational__";
            folders.WithEnvironment(profile + "ManagedTenantId", TenantId)
                .WithEnvironment(profile + "ProviderKind", "forgejo")
                .WithEnvironment(profile + "NamingPolicy__PolicyRef", "operational-target")
                .WithEnvironment(profile + "NamingPolicy__Metadata__external_repository_ref", "operational-remote")
                .WithEnvironment(profile + "NamingPolicy__Metadata__repository_owner", OperationalProviderFixture.Owner)
                .WithEnvironment(profile + "NamingPolicy__Metadata__repository_name", OperationalProviderFixture.Repository)
                .WithEnvironment(profile + "NamingPolicy__Metadata__repository_visibility", "private")
                .WithEnvironment(profile + "NamingPolicy__Metadata__provider_product_version", "16.0.3")
                .WithEnvironment(profile + "NamingPolicy__Metadata__authorized_base_url", _provider.BaseUrl)
                .WithEnvironment(profile + "BranchPolicy__PolicyRef", "operational-main")
                .WithEnvironment(profile + "BranchPolicy__Metadata__default_branch", OperationalProviderFixture.Branch)
                .WithEnvironment(profile + "BranchPolicy__Metadata__selected_ref", OperationalProviderFixture.Branch)
                .WithEnvironment(profile + "BranchPolicy__Metadata__require_protected_ref", "false");
        }
        Dictionary<IDaprComponentResource, DaprComponentResource> replacements = [];
        foreach (DaprComponentResource component in _builder.Resources.OfType<DaprComponentResource>().ToArray())
        {
            if (component.Options?.LocalPath is string source)
            {
                string path = Path.Combine(componentDirectory, Path.GetFileName(source));
                string content = await File.ReadAllTextAsync(source, timeout.Token).ConfigureAwait(false);
                if (component.Name is "statestore" or "pubsub" && !content.Contains("localhost:6379", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Shared component shape changed; acceptance isolation must be revalidated.");
                }

                await File.WriteAllTextAsync(path, IsolateDaprYaml(content), timeout.Token).ConfigureAwait(false);
                _builder.Resources.Remove(component);
                DaprComponentResource isolated = (DaprComponentResource)_builder.AddDaprComponent(component.Name, component.Type,
                    new DaprComponentOptions { LocalPath = path }).Resource;
                foreach (IResourceAnnotation annotation in component.Annotations.Where(annotation => annotation is not
                    ResourceSnapshotAnnotation and not ManifestPublishingCallbackAnnotation))
                {
                    isolated.Annotations.Add(annotation);
                }

                replacements.Add(component, isolated);
            }
        }

        foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
        {
            foreach (DaprSidecarAnnotation annotation in project.Annotations.OfType<DaprSidecarAnnotation>())
            {
                DaprSidecarOptionsAnnotation optionsAnnotation = annotation.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                DaprSidecarOptions options = optionsAnnotation.Options;
                string? isolatedConfig = null;
                if (options.Config is string config)
                {
                    foreach (string source in Directory.EnumerateFiles(Path.GetDirectoryName(config)!))
                    {
                        string target = Path.Combine(source == config ? configurationDirectory : componentDirectory, Path.GetFileName(source));
                        if (!File.Exists(target))
                        {
                            await File.WriteAllTextAsync(target, IsolateDaprYaml(await File.ReadAllTextAsync(source, timeout.Token).ConfigureAwait(false)), timeout.Token).ConfigureAwait(false);
                        }
                    }

                    isolatedConfig = Path.Combine(configurationDirectory, Path.GetFileName(config));
                }

                annotation.Sidecar.Annotations.Remove(optionsAnnotation);
                annotation.Sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(options with
                {
                    AppId = AppId(project.Name),
                    Config = isolatedConfig,
                    ResourcesPaths = [project.Name.StartsWith("folders-workers-proof-", StringComparison.Ordinal)
                        ? Path.Combine(_temporaryDirectory, "ordinary-proof") : componentDirectory],
                }));
                annotation.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["NAMESPACE"] = DaprNamespace));
            }
        }

        string proofDirectory = Path.Combine(_temporaryDirectory, "ordinary-proof");
        Directory.CreateDirectory(proofDirectory);
        foreach (string source in Directory.EnumerateFiles(componentDirectory, "*.yaml"))
        {
            string content = await File.ReadAllTextAsync(source, timeout.Token).ConfigureAwait(false);
            if (Path.GetFileName(source) == "statestore.yaml")
                content = content.Replace("value: \"none\"", "value: \"ordinary-proof-" + _runId + "\"", StringComparison.Ordinal);
            if (Path.GetFileName(source) == "pubsub.yaml")
                content = content.Replace("scopes:", "    - name: subscriptionScopes\n      value: \"" + AppId("folders-workers-proof-a") + "=;" + AppId("folders-workers-proof-b") + "=\"\nscopes:", StringComparison.Ordinal);
            if (Path.GetFileName(source) is "statestore.yaml" or "pubsub.yaml")
                content += "\n  - " + AppId("folders-workers-proof-a") + "\n  - " + AppId("folders-workers-proof-b") + "\n";
            await File.WriteAllTextAsync(Path.Combine(proofDirectory, Path.GetFileName(source)), content, timeout.Token).ConfigureAwait(false);
        }

        IResourceBuilder<ProjectResource> gateway = _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore"));
        // This fixture owns a fresh isolated store and starts the current hosted activation worker.
        // Only Folders commands may acknowledge after durable outbox admission; the unchanged
        // acceptance body waits for each persisted projection before depending on it.
        gateway.WithEnvironment("EventStore__Projections__WorkerBackedResponsesEnabled", "true")
            .WithEnvironment("EventStore__Projections__WorkerBackedResponseDomains__0", "folders");
        foreach ((string registration, string target) in new[]
        {
            ("system|tenants|v1", "tenants"), ("system|global-administrators|v1", "tenants"),
            ("*|counter|v1", "unhosted-sample"), ("*|greeting|v1", "unhosted-sample"),
        })
        {
            gateway.WithEnvironment("EventStore__DomainServices__Registrations__" + registration + "__AppId", AppId(target));
        }

        const string folderRegistration = "EventStore__DomainServices__Registrations__*|folders|v1__";
        gateway.WithEnvironment(folderRegistration + "AppId", AppId("folders"))
            .WithEnvironment(folderRegistration + "MethodName", "process")
            .WithEnvironment(folderRegistration + "TenantId", "*")
            .WithEnvironment(folderRegistration + "Domain", "folders")
            .WithEnvironment(folderRegistration + "Version", "v1");
        gateway.WithEnvironment("Authentication__WorkloadIssuer__ClientId", AppId("eventstore"))
            .WithEnvironment("Authentication__WorkloadIssuer__Workload", AppId("eventstore"));
        foreach (string name in new[] { "folders", "tenants" })
        {
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == name))
                .WithEnvironment("EventStore__DomainService__AppId", AppId(name))
                .WithEnvironment("Authentication__Workload__Audience", AppId(name))
                .WithEnvironment("Authentication__Workload__AllowedCallers__0", AppId("eventstore"));
        }

        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories"))
            .WithEnvironment("EventStoreIntegration__CommandGateway__BaseAddress",
                _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")).GetEndpoint("http"));
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"))
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("Folders__WorkspaceFilePolicy__Version", "1.1.0")
            .WithEnvironment("Folders__WorkspaceFilePolicy__Includes__0", "**")
            .WithEnvironment("Folders__WorkspaceFilePolicy__Restricted__0", "private/**")
            .WithEnvironment("Folders__WorkspaceFilePolicy__MetadataOnly__0", "metadata/**")
            .WithEnvironment("Folders__WorkspaceFilePolicy__ProtectionMode", "non_confidential")
            .WithEnvironment("Authentication__HumanAuthorityReceipt__ExpectedWorkload", AppId("eventstore"))
            .WithEnvironment("Memories__BaseAddress",
                _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories")).GetEndpoint("http"));

        if (QualifyContentReplica)
        {
            ProjectResource primary = _builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders");
            var contentReplica = _builder.AddProject<Projects.Hexalith_Folders_Server>("folders-replica");
            // Environment annotations bind to one resource during composition. Reusing the
            // same annotation instance transfers its resolved environment away from primary.
            foreach (EnvironmentCallbackAnnotation annotation in primary.Annotations.OfType<EnvironmentCallbackAnnotation>())
                contentReplica.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(annotation.Callback));
            foreach (WaitAnnotation annotation in primary.Annotations.OfType<WaitAnnotation>())
                contentReplica.Resource.Annotations.Add(new WaitAnnotation(annotation.Resource, annotation.WaitType, annotation.ExitCode)
                { WaitBehavior = annotation.WaitBehavior });
            var originalSidecar = primary.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            DaprSidecarOptions options = originalSidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            contentReplica.WithDaprSidecar(sidecar =>
            {
                sidecar.WithOptions(options with { AppId = AppId("folders") });
                sidecar.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["NAMESPACE"] = DaprNamespace));
                foreach (var component in originalSidecar.Annotations.OfType<DaprComponentReferenceAnnotation>())
                    sidecar.WithReference(_builder.CreateResourceBuilder(component.Component));
            }).WithGeneratedEventStoreAppChannelToken().WithHttpHealthCheck("/health")
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                .WithEnvironment("DOTNET_ENVIRONMENT", "Development");
        }

        // Inventory: Server authorization/provider/status readers and Workers' semantic-indexing
        // authorization both perform independent source-head reads. Replicas sharing a physical
        // AppId share that workload; proof Workers with different AppIds receive their own identity/key.
        for (int index = 0; index < sourceCallers.Length; index++)
        {
            string caller = sourceCallers[index];
            string sourceSecretValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var sourceSecret = _builder.AddParameter("durability-source-secret-" + index, () => sourceSecretValue, secret: true);
            _builder.CreateResourceBuilder(security).WithEnvironment("HEXALITH_FOLDERS_SOURCE_" + index + "_CLIENT_SECRET", sourceSecret);
            using RSA sourceKey = RSA.Create(2048);
            string sourcePrivateValue = Convert.ToBase64String(sourceKey.ExportPkcs8PrivateKey());
            var sourcePrivateKey = _builder.AddParameter("durability-source-key-" + index, () => sourcePrivateValue, secret: true);
            foreach (ProjectResource sourceHost in _builder.Resources.OfType<ProjectResource>().Where(project => AppId(project.Name) == caller))
            {
                _builder.CreateResourceBuilder(sourceHost).WithEventStoreWorkloadClientCredentials(caller, sourceSecret)
                    .WithEnvironment("Authentication__WorkloadIssuer__Workload", caller)
                    .WithEnvironment("Authentication__BoundStreamRead__PrivateKey", sourcePrivateKey)
                    .WithEnvironment("EventStore__Gateway__AppId", AppId("eventstore"));
            }
            gateway.WithEnvironment("Authentication__BoundStreamRead__PublicKeys__" + caller, Convert.ToBase64String(sourceKey.ExportSubjectPublicKeyInfo()))
                .WithEnvironment("Authentication__BoundStreamRead__AllowedDomains__" + caller, "folders")
                .WithEnvironment("Authentication__BoundStreamRead__MetadataOnlyCallers__" + caller, "true")
                .WithEventStoreTrustedEffectSubmitter(caller);
        }

        IResource[] composedResources = [.. _builder.Resources,
            .. _builder.Resources.OfType<ProjectResource>().SelectMany(project => project.Annotations.OfType<DaprSidecarAnnotation>().Select(annotation => annotation.Sidecar))];
        foreach (IResource resource in composedResources)
        {
            foreach (DaprComponentReferenceAnnotation reference in resource.Annotations.OfType<DaprComponentReferenceAnnotation>().ToArray())
            {
                if (replacements.TryGetValue(reference.Component, out DaprComponentResource? isolated))
                {
                    resource.Annotations.Remove(reference);
                    resource.Annotations.Add(new DaprComponentReferenceAnnotation(isolated));
                }
            }

            foreach (WaitAnnotation wait in resource.Annotations.OfType<WaitAnnotation>().ToArray())
            {
                if (wait.Resource is IDaprComponentResource component && replacements.TryGetValue(component, out DaprComponentResource? isolated))
                {
                    resource.Annotations.Remove(wait);
                    resource.Annotations.Add(new WaitAnnotation(isolated, wait.WaitType, wait.ExitCode) { WaitBehavior = wait.WaitBehavior });
                }
            }
        }

        string sentryConfiguration = Path.Combine(configurationDirectory, "sentry.yaml");
        string ownerSentry = Path.GetFullPath(Path.Combine(_builder.AppHostDirectory,
            "../../references/Hexalith.Tenants/src/Hexalith.Tenants.AppHost/DaprComponents/sentry.yaml"));
        await File.WriteAllTextAsync(sentryConfiguration, await File.ReadAllTextAsync(ownerSentry, timeout.Token).ConfigureAwait(false), timeout.Token).ConfigureAwait(false);
        _localDapr = DaprSelfHostedMtls.AddLocalApplicationInfrastructure(_builder,
            Path.GetFullPath(Path.Combine(_builder.AppHostDirectory, "../..")), sentryConfiguration, DaprNamespace);
        Console.WriteLine("MTLS_RUNTIME_BINDING " + JsonSerializer.Serialize(new
        {
            runDirectory = _localDapr.Value.RunDirectory,
            certificateDirectory = _localDapr.Value.CertificateDirectory,
        }));

        // Configure every actual receiver/caller, including replicas and distinct proof Workers,
        // before the catalog captures their environment. Missing owner references remain disabled.
        _workloadEnrollment = await Hexalith.Platform.Composition.PlatformRunOwnedWorkloadEnrollment.AdoptAsync(
            _builder, TenantId, timeout.Token, _provider?.TrustFile).ConfigureAwait(false);
        _ = await Hexalith.Platform.Composition.PlatformWorkloadEnrollment.ConfigureAsync(_builder, timeout.Token, TenantId).ConfigureAwait(false);

        // This key is provisioned with this fixture's genuinely new, owned Redis backend.
        // It never replaces digest custody for an existing business store.
        string catalogDigest = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string isolatedState = replacements.Values.Single(component => component.Name == "statestore").Options!.LocalPath!;
        _catalog = Hexalith.Folders.AppHost.FoldersDevelopmentCatalog.Compose(_builder,
            new(Path.Combine(_temporaryDirectory, "catalog-custody"), "folders-routing-authority", "folders-routing-authority-v1",
                "local1", new Dictionary<string, string> { ["local1"] = catalogDigest }, "local1"), isolatedState, DaprNamespace);
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "tenants"))
            .WithEnvironment("Tenants__BootstrapBoundWorkloadReceiptPath", Path.Combine(_catalog.Directory, "tenants.bootstrap.json"));

        _app = await _builder.BuildAsync(timeout.Token).ConfigureAwait(false);
        await _app.StartAsync(timeout.Token).ConfigureAwait(false);
        foreach (string name in new[] { "security", "eventstore", "tenants", "folders", "folders-workers", "folders-workers-replica", "folders-workers-proof-a", "folders-workers-proof-b" }.Concat(QualifyContentReplica ? ["folders-replica"] : Array.Empty<string>()))
        {
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(name, timeout.Token).ConfigureAwait(false);
        }

        foreach (string name in new[] { "eventstore", "tenants", "folders", "folders-workers", "folders-workers-replica", "folders-workers-proof-a", "folders-workers-proof-b" }.Concat(QualifyContentReplica ? ["folders-replica"] : Array.Empty<string>()))
        {
            Console.WriteLine("LIFECYCLE_PROBE sidecar=" + name);
            string sidecar = _builder.Resources.OfType<ProjectResource>().Single(project => project.Name == name)
                .Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
            await App.ResourceNotifications.WaitForResourceAsync(sidecar, KnownResourceStates.Running, timeout.Token).ConfigureAwait(false);
            DaprClient client = new DaprClientBuilder()
                .UseHttpEndpoint(App.GetEndpoint(sidecar, "http").ToString())
                .UseGrpcEndpoint(App.GetEndpoint(sidecar, "grpc").ToString())
                .Build();
            try
            {
                await client.WaitForSidecarAsync(timeout.Token).ConfigureAwait(false);
                // Outbound readiness precedes application/actor initialization. Full health is required
                // before the first authenticated command can depend on an actor or callback.
                while (!await client.CheckHealthAsync(timeout.Token).ConfigureAwait(false))
                {
                    await Task.Delay(200, timeout.Token).ConfigureAwait(false);
                }
                Console.WriteLine("LIFECYCLE_READY sidecar=" + name);

                if (name == "folders-workers") { _dapr = client; client = null!; }
                else if (name == "folders-workers-proof-a") { _proofDapr = client; client = null!; }
            }
            finally
            {
                client?.Dispose();
            }
        }

        RawStore = new DaprReadModelStore(_dapr ?? throw new InvalidOperationException("Worker sidecar discovery did not complete."));
        Store = new SelectedOperationalAcceptanceStore(RawStore, TenantId);
        ProofStore = new SelectedOperationalAcceptanceStore(new DaprReadModelStore(_proofDapr
            ?? throw new InvalidOperationException("Ordinary proof sidecar discovery did not complete.")), TenantId);
        foreach (string resource in OwningHosts)
            await FolderDurabilityRuntimeBinding.RecordWhenReadyAsync(resource, ResourceProcessId(resource), timeout.Token).ConfigureAwait(false);
    }

    private async Task BootstrapAsync()
    {
        _adminToken = await TokenAsync("hexalith-eventstore", "admin-user", "admin-pass").ConfigureAwait(false);
        _foreignToken = await TokenAsync("hexalith-eventstore", "tenant-a-user", "tenant-a-pass").ConfigureAwait(false);
        await ConfigureIdentityAsync().ConfigureAwait(false);
        await ObserveOwnerBootstrapAsync().ConfigureAwait(false);
        await SubmitAsync(TenantPlatformScopes.Tenants, "tenants", TenantId, "CreateTenant", new { tenantId = TenantId, name = "Durability acceptance", description = "Disposable local acceptance tenant" }, _adminToken).ConfigureAwait(false);
        _humanToken = await TokenAsync("hexalith-eventstore", "tenant-a-user", "tenant-a-pass").ConfigureAwait(false);
        JsonWebToken human = new(_humanToken);
        if (!human.Claims.Any(claim => claim.Type == "eventstore:tenant" && claim.Value == TenantId)
            || !human.Claims.Any(claim => claim.Type == "eventstore:permission" && claim.Value == "manage_organization_acl"))
        {
            throw new InvalidOperationException("The real identity provider did not issue the required fresh-tenant/action claims.");
        }

        await SubmitAsync(TenantPlatformScopes.Tenants, "tenants", TenantId, "AddUserToTenant", new { tenantId = TenantId, userId = PrincipalId, role = "TenantOwner" }, _adminToken).ConfigureAwait(false);
        _projectWorkloadToken = await IssueWorkloadAsync("project", EventStoreWorkloadOperations.DomainServiceProject).ConfigureAwait(false);
        _metadataWorkloadToken = await IssueWorkloadAsync("metadata", EventStoreWorkloadOperations.DomainServiceMetadata).ConfigureAwait(false);
    }

    private async Task ObserveOwnerBootstrapAsync()
    {
        string directory = _catalog?.Directory ?? throw new InvalidOperationException("The actual bootstrap catalog is unavailable.");
        string principal = new JsonWebToken(_adminToken).Subject;
        if (principal != "admin-user") throw new InvalidOperationException("The real administrator credential has an unexpected subject.");
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        string receiptPath = Path.Combine(directory, "tenants.bootstrap.json");
        JsonElement receipt;
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(receiptPath))
            {
                using JsonDocument candidate = JsonDocument.Parse(await File.ReadAllBytesAsync(receiptPath, deadline.Token).ConfigureAwait(false));
                if (candidate.RootElement.GetProperty("Phase").GetString() != "attempted")
                {
                    FolderDurabilityBootstrapEvidence.AcceptedDelivery(candidate.RootElement);
                    try
                    {
                        var tenants = FolderDurabilityCatalogBinding.Read(await File.ReadAllBytesAsync(Path.Combine(directory, "tenants.committed.json"), deadline.Token).ConfigureAwait(false), _catalog!.RootDigest);
                        var gateway = FolderDurabilityCatalogBinding.Read(await File.ReadAllBytesAsync(Path.Combine(directory, "eventstore.committed.json"), deadline.Token).ConfigureAwait(false), _catalog.RootDigest);
                        if (gateway.AttemptId != tenants.AttemptId || gateway.Generation != tenants.Generation || gateway.Epoch != tenants.Epoch)
                            throw new InvalidOperationException("The bootstrap peers committed different actual catalog generations.");
                        receipt = FolderDurabilityBootstrapEvidence.Receipt(await File.ReadAllBytesAsync(receiptPath, deadline.Token).ConfigureAwait(false), tenants, principal);
                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        // Each peer publishes its actual readback independently; retain the same deadline.
                    }
                }
            }
            await Task.Delay(1000, deadline.Token).ConfigureAwait(false);
        }

        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        string messageId = receipt.GetProperty("MessageId").GetString()!;
        while (true)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/commands/status/" + messageId);
            FolderDurabilityGatewayScope.Apply(request, TenantPlatformScopes.GlobalAdministrators);
            using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, deadline.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                JsonElement status = await response.Content.ReadFromJsonAsync<JsonElement>(deadline.Token).ConfigureAwait(false);
                string? state = status.GetProperty("status").GetString();
                if (status.GetProperty("messageId").GetString() != messageId
                    || status.GetProperty("correlationId").GetString() != receipt.GetProperty("CorrelationId").GetString())
                    throw new InvalidOperationException("The owner bootstrap terminal delivery is not the actual attempt.");
                if (state == "Completed") break;
                if (state is "Rejected" or "Failed" or "PublishFailed" or "TimedOut")
                    throw new InvalidOperationException("The actual owner bootstrap ended in " + state + ".");
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
                await RequireStatusAsync(response, HttpStatusCode.OK, "Actual owner bootstrap command status").ConfigureAwait(false);
            await Task.Delay(1000, deadline.Token).ConfigureAwait(false);
        }
        // Public human bootstrap remains forbidden; its refusal cannot append another administrator event.
        using (HttpRequestMessage direct = new(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(FolderDurabilityGatewayScope.BootstrapCommand(NewId(), principal)),
        })
        {
            FolderDurabilityGatewayScope.Apply(direct, TenantPlatformScopes.GlobalAdministrators);
            using HttpResponseMessage denied = await FolderDurabilityHttp.SendAsync(client, direct, false, deadline.Token).ConfigureAwait(false);
            await RequireStatusAsync(denied, HttpStatusCode.Forbidden, "Original direct human bootstrap refusal").ConfigureAwait(false);
        }
        using (HttpClient issuer = App.CreateHttpClient("security", "http"))
        {
            string assertion = await IssueBootstrapOwnerAssertionAsync(issuer, EventStoreWorkloadOperations.GatewayCommandSubmit, deadline.Token).ConfigureAwait(false);
            string ownerSidecarName = _builder!.Resources.OfType<ProjectResource>().Single(project => project.Name == "tenants")
                .Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
            using DaprClient ownerSidecar = new DaprClientBuilder().UseHttpEndpoint(App.GetEndpoint(ownerSidecarName, "http").ToString()).Build();
            foreach (string credential in new[] { "missing", "human", "missing-proof", "wrong-proof" })
            {
                bool proofControl = credential is "missing-proof" or "wrong-proof";
                using HttpRequestMessage invalid = proofControl
                    ? ownerSidecar.CreateInvokeMethodRequest(HttpMethod.Post, AppId("eventstore"), "api/v1/commands/bound/workload")
                    : new(HttpMethod.Post, "/api/v1/commands/bound/workload");
                var refusalBody = new
                {
                    command = FolderDurabilityGatewayScope.BootstrapCommand(NewId(), principal),
                    principalId = principal,
                };
                invalid.Content = JsonContent.Create(refusalBody);
                FolderDurabilityGatewayScope.Apply(invalid, TenantPlatformScopes.GlobalAdministrators);
                if (credential == "human" || proofControl) invalid.Headers.Authorization = new("Bearer", _adminToken);
                if (proofControl)
                {
                    invalid.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
                    BindCommittedCatalog(invalid, "tenants");
                }
                if (credential == "wrong-proof") invalid.Headers.Add("X-Hexalith-Bound-Workload-Command-Proof", "invalid-proof");
                // The real owning sidecar supplies channel/caller identity on proof controls; direct
                // missing/human controls still exercise the public credential-kind refusal.
                using HttpClient boundary = proofControl ? new() : App.CreateHttpClient("eventstore", "http");
                using HttpResponseMessage denied = await FolderDurabilityHttp.SendAsync(boundary, invalid, false, deadline.Token,
                    contentFactory: proofControl ? () => JsonContent.Create(refusalBody) : null).ConfigureAwait(false);
                await RequireStatusAsync(denied, credential is "missing" or "human" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden,
                    "Actual owner bootstrap " + credential + " refusal").ConfigureAwait(false);
            }
            string sourceAssertion = await IssueBootstrapOwnerAssertionAsync(issuer, EventStoreWorkloadOperations.GatewayStreamRead, deadline.Token).ConfigureAwait(false);
            using HttpRequestMessage source = FolderDurabilityGatewayScope.BootstrapSource(ownerSidecar, AppId("eventstore"), AppId("tenants"),
                sourceAssertion, await ParameterAsync("durability-tenants-bootstrap-proof").ConfigureAwait(false), DateTimeOffset.UtcNow);
            BindCommittedCatalog(source, "tenants");
            using HttpClient sourceBoundary = new();
            using HttpResponseMessage retained = await FolderDurabilityHttp.SendAsync(sourceBoundary, source, true, deadline.Token).ConfigureAwait(false);
            await RequireStatusAsync(retained, HttpStatusCode.OK, "Original persisted administrator source").ConfigureAwait(false);
            FolderDurabilityBootstrapEvidence.Source((await retained.Content.ReadFromJsonAsync<StreamReadPage>(Json, deadline.Token).ConfigureAwait(false))!, messageId, receipt.GetProperty("CorrelationId").GetString()!, principal);
        }
        await FolderDurabilityBootstrapEvidence.WaitForAuthorityAsync(async token =>
            (await RawStore.GetAsync<JsonObject>("statestore", "projection:global-administrators:singleton", token).ConfigureAwait(false)).Value,
            principal, deadline.Token).ConfigureAwait(false);
        Console.WriteLine("BOOTSTRAP_VERIFIED accepted=202 terminal=Completed originalSource=1 humanAttributed=true catalogGenerationBound=true");
    }

    private async Task<string> IssueBootstrapOwnerAssertionAsync(HttpClient issuer, string operation, CancellationToken cancellationToken)
    {
        if (operation is not (EventStoreWorkloadOperations.GatewayCommandSubmit or EventStoreWorkloadOperations.GatewayStreamRead))
            throw new InvalidOperationException("The bootstrap owner assertion names an unsupported operation.");
        string? enrolled = await IssueEnrolledAssertionAsync("tenants", "eventstore", operation, cancellationToken).ConfigureAwait(false);
        if (enrolled is not null) return enrolled;
        using HttpResponseMessage issued = await issuer.PostAsync("/realms/hexalith/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = AppId("tenants"),
                ["client_secret"] = _bootstrapWorkloadSecret,
                ["scope"] = "eventstore-audience.eventstore eventstore-operation." + operation.Replace(':', '.'),
            }), cancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(issued, HttpStatusCode.OK, "Real owner bootstrap operation assertion").ConfigureAwait(false);
        string assertion = (await issued.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString()!;
        if (JwtWorkloadAssertionIssuer.GetTokenScopeFailure(assertion, "eventstore", operation) is not null)
            throw new InvalidOperationException("The real owner issuer did not return the requested exact audience and operation.");
        return assertion;
    }

    private async Task RefreshCapacityWorkloadTokensAsync()
    {
        DateTime refreshBefore = DateTime.UtcNow.AddMinutes(1);
        if (new JsonWebToken(_projectWorkloadToken).ValidTo <= refreshBefore)
            _projectWorkloadToken = await IssueWorkloadAsync("project", EventStoreWorkloadOperations.DomainServiceProject).ConfigureAwait(false);
        if (new JsonWebToken(_metadataWorkloadToken).ValidTo <= refreshBefore)
            _metadataWorkloadToken = await IssueWorkloadAsync("metadata", EventStoreWorkloadOperations.DomainServiceMetadata).ConfigureAwait(false);
    }

    private async Task<string> IssueWorkloadAsync(string scopeOperation, string expectedOperation)
    {
        string? enrolled = await IssueEnrolledAssertionAsync("eventstore", AppId("folders"), expectedOperation,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        if (enrolled is not null) return enrolled;
        using HttpClient client = App.CreateHttpClient("security", "http");
        using HttpResponseMessage workload = await client.PostAsync("/realms/hexalith/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = AppId("eventstore"),
                ["client_secret"] = await ParameterAsync("eventstore-workload-client-secret").ConfigureAwait(false),
                ["scope"] = "eventstore-audience." + AppId("folders") + " eventstore-operation.domain-service." + scopeOperation,
            }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(workload, HttpStatusCode.OK, "Real " + scopeOperation + " workload token").ConfigureAwait(false);
        string token = (await workload.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false))
            .GetProperty("access_token").GetString()!;
        string[] operations = new JsonWebToken(token).Claims.Where(claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.OperationClaimType)
            .Select(static claim => claim.Value).ToArray();
        if (operations.Length != 1 || operations[0] != expectedOperation)
            throw new InvalidOperationException("The real identity provider did not issue the requested single workload operation.");
        return token;
    }

    private async Task<string?> IssueEnrolledAssertionAsync(string resource, string audience, string operation, CancellationToken cancellationToken)
    {
        ProjectResource caller = _builder!.Resources.OfType<ProjectResource>().Single(project => project.Name == resource);
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation annotation in caller.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(new EnvironmentCallbackContext(_builder.ExecutionContext, caller, environment, cancellationToken)).ConfigureAwait(false);
        if (!environment.TryGetValue("Authentication__WorkloadIssuer__EnableResourceBindingRequests", out object? enabled) || enabled is not "true") return null;
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        Dictionary<string, string?> settings = environment.Where(pair => pair.Value is string
            && (pair.Key.StartsWith("Authentication__WorkloadJwtBearer__", StringComparison.Ordinal)
                || pair.Key.StartsWith("Authentication__WorkloadIssuer__", StringComparison.Ordinal)))
            .ToDictionary(pair => pair.Key.Replace("__", ":", StringComparison.Ordinal), pair => (string?)pair.Value, StringComparer.Ordinal);
        settings["Authentication:WorkloadIssuer:ClientSecret"] = await ParameterAsync("enrolled-" + resource + "-workload-secret").ConfigureAwait(false);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddEventStoreWorkloadAssertionIssuer(builder.Configuration);
        using IHost host = builder.Build();
        return await host.Services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(new(audience, operation), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The verified workload enrollment refused a required fixture assertion.");
    }

    private async Task ConfigureIdentityAsync()
    {
        using HttpClient client = App.CreateHttpClient("security", "http");
        KeycloakResource security = _builder!.Resources.OfType<KeycloakResource>().Single(resource => resource.Name == "security");
        using HttpResponseMessage login = await client.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = await security.AdminPasswordParameter.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Local IdP administrative credential is unavailable."),
        }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(login, HttpStatusCode.OK, "Local IdP administration login").ConfigureAwait(false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString());
        // Keycloak's managed user profile otherwise discards unconfigured custom attributes in Admin REST
        // updates. Only administrators of this disposable realm may edit these existing mapper inputs.
        JsonObject profile = JsonNode.Parse(await client.GetStringAsync("/admin/realms/hexalith/users/profile", TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsObject();
        JsonArray attributes = profile["attributes"]!.AsArray();
        foreach (string name in new[] { "tenants", "permissions" })
        {
            if (!attributes.Any(attribute => attribute?["name"]?.GetValue<string>() == name))
            {
                attributes.Add(new JsonObject
                {
                    ["name"] = name,
                    ["multivalued"] = true,
                    ["permissions"] = new JsonObject { ["view"] = new JsonArray("admin"), ["edit"] = new JsonArray("admin") },
                });
            }
        }

        using HttpResponseMessage configureProfile = await client.PutAsJsonAsync("/admin/realms/hexalith/users/profile", profile,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(configureProfile, HttpStatusCode.OK, "Disposable identity claim profile").ConfigureAwait(false);
        JsonArray users = (JsonNode.Parse(await client.GetStringAsync("/admin/realms/hexalith/users?username=tenant-a-user&exact=true", TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsArray());
        JsonObject user = users.Single()!.AsObject();
        PrincipalId = user["id"]!.GetValue<string>();
        user["attributes"] = new JsonObject
        {
            ["tenants"] = new JsonArray(TenantId),
            ["permissions"] = new JsonArray("command:submit", "command:query", "command:replay", "create_folder", "manage_organization_acl", "read_metadata", "manage_folder_access", "mutate_files", "read_file_content",
                "configure_provider_binding", "provider_readiness_read", "tenant-context-and-provider-binding-read", "bind_repository", "configure_branch_ref_policy",
                "prepare_workspace", "lock_workspace", "read_workspace_lock", "read_workspace_status",
                "read_workspace_cleanup_status", "read_task_status"),
        };
        using HttpResponseMessage update = await client.PutAsJsonAsync("/admin/realms/hexalith/users/" + PrincipalId, user, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(update, HttpStatusCode.NoContent, "Fresh-tenant identity claims").ConfigureAwait(false);
    }

    private async Task<string> TokenAsync(string clientId, string username, string password)
    {
        using HttpClient client = App.CreateHttpClient("security", "http");
        using HttpResponseMessage response = await client.PostAsync("/realms/hexalith/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
        }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "Real human JWT acquisition").ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString()!;
    }

    /// <summary>Submits a real scoped command and observes Completed, optionally bounded by a foreground caller and original identities.</summary>
    internal async Task<JsonElement> SubmitAsync(string tenant, string domain, string aggregate, string command, object payload, string token,
        CancellationToken? cancellationToken = null, string? messageId = null, string? correlationId = null)
    {
        CancellationToken caller = cancellationToken ?? TestContext.Current.CancellationToken;
        // Each command fans out into internal actor/projection requests. Keep the acceptance driver
        // below the existing consumer limiter; rate-limit/load qualification is a separate concern.
        await Task.Delay(TimeSpan.FromSeconds(1), caller).ConfigureAwait(false);
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(new SubmitCommandRequest(messageId ?? NewId(), tenant, domain, aggregate, command, JsonSerializer.SerializeToElement(payload),
                CorrelationId: correlationId, Extensions: new Dictionary<string, string> { ["taskId"] = NewId() },
                IdempotencyKey: domain == "folders" ? NewId() : null)),
        };
        request.Headers.Add("X-Correlation-Id", correlationId ?? NewId());
        FolderDurabilityGatewayScope.ApplyHumanCommand(request, tenant);
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, false, caller).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.Accepted, "Authenticated " + command, caller).ConfigureAwait(false);
        JsonElement accepted = await response.Content.ReadFromJsonAsync<JsonElement>(caller).ConfigureAwait(false);
        string statusKey = accepted.GetProperty("messageId").GetString()!;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        while (true)
        {
            using HttpRequestMessage statusRequest = new(HttpMethod.Get, "/api/v1/commands/status/" + statusKey);
            FolderDurabilityGatewayScope.Apply(statusRequest, tenant);
            using HttpResponseMessage status = await FolderDurabilityHttp.SendAsync(client, statusRequest, true, deadline.Token).ConfigureAwait(false);
            if (status.IsSuccessStatusCode)
            {
                JsonElement current = await status.Content.ReadFromJsonAsync<JsonElement>(deadline.Token).ConfigureAwait(false);
                string? state = current.GetProperty("status").GetString();
                if (state == "Completed")
                {
                    return current;
                }

                if (state is "Rejected" or "Failed" or "PublishFailed" or "TimedOut")
                {
                    throw new InvalidOperationException($"Authenticated {command} ended in {state}.");
                }
            }
            else if (status.StatusCode != HttpStatusCode.NotFound)
                await RequireStatusAsync(status, HttpStatusCode.OK, "Authenticated command status", deadline.Token).ConfigureAwait(false);

            await Task.Delay(1000, deadline.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Runs the existing real-source maintenance barrier and reports whether it submitted a genuine update.</summary>
    internal async Task<bool> MaintainTenantAsync(CancellationToken cancellationToken)
    {
        _tenantMaintenance ??= new FolderDurabilityTenantMaintenance(TenantId, PrincipalId, ReadTenantSourceAsync,
            token => new EventStoreFolderTenantAccessProjectionStore(RawStore).GetAsync(TenantId, token),
            (command, correlation, token) => SubmitAsync(command.Tenant, command.Domain, command.AggregateId,
                command.CommandType, command.Payload, _adminToken, token, command.MessageId, correlation));
        bool maintained = await _tenantMaintenance.EnsureFreshAsync(cancellationToken).ConfigureAwait(false);
        if (maintained)
            Console.WriteLine("PASS tenant maintenance: Completed; original TenantUpdated matched; Workers sequence/timestamp matched; authority unchanged.");
        return maintained;
    }

    private async Task<StreamReadPage> ReadTenantSourceAsync(CancellationToken cancellationToken)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/streams/read")
        {
            Content = JsonContent.Create(new StreamReadRequest(TenantPlatformScopes.Tenants, "tenants", TenantId, PageSize: 1000)),
        };
        FolderDurabilityGatewayScope.Apply(request, TenantPlatformScopes.Tenants);
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, cancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "Authenticated tenant maintenance source", cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<StreamReadPage>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Tenant maintenance source is missing.");
    }

    private void BindCommittedCatalog(HttpRequestMessage request, string host = "folders")
    {
        string directory = _catalog?.Directory ?? throw new InvalidOperationException("The actual fixture catalog is unavailable.");
        var readback = FolderDurabilityCatalogBinding.Read(File.ReadAllBytes(Path.Combine(directory, host + ".committed.json")), _catalog!.RootDigest);
        request.Headers.Add(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.RootHeader, readback.RootDigest);
        request.Headers.Add(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.EpochHeader,
            readback.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (readback.ActivationBindingDigest is { } binding)
            request.Headers.Add(Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogInvocationHandler.BindingHeader, binding);
    }

    private async Task<string> ParameterAsync(string name)
        => await _builder!.Resources.OfType<ParameterResource>().Single(item => item.Name == name)
            .GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Required local security parameter is unavailable.");

    private string IsolateDaprYaml(string content)
    {
        string isolated = content.Replace("localhost:6379", _redisAddress, StringComparison.Ordinal);
        isolated = Regex.Replace(isolated,
            @"(?m)(?<prefix>^\s*-\s+(?:appId:\s*)?|^\s*appId:\s*)(?<id>eventstore|tenants|folders-workers|folders-ui|folders|memories)(?<suffix>\s*$)",
            match => match.Groups["prefix"].Value + AppId(match.Groups["id"].Value) + match.Groups["suffix"].Value);
        isolated = Regex.Replace(isolated, @"(?m)^(\s*)eventstore:(\s*)$",
            match => match.Groups[1].Value + AppId("eventstore") + ":" + match.Groups[2].Value);
        return isolated.Replace("namespace: \"default\"", "namespace: \"" + DaprNamespace + "\"", StringComparison.Ordinal);
    }

    private void SetEnvironment(string name, string value)
    {
        _environment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }
}
