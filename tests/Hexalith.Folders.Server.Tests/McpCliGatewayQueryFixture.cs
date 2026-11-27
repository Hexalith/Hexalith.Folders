using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.OperationalStatus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Runs signed callbacks on the production host with the SDK store and actual domain projections.</summary>
internal sealed class McpCliGatewayQueryFixture(WebApplication app, RSA rsa, RsaSecurityKey key,
    McpCliGatewayQuerySequenceReader source, McpCliGatewayQuerySearchSource search,
    RecordingProviderCapabilityResolver provider, DateTimeOffset now) : IAsyncDisposable
{
    internal const string Tenant = "tenant-gateway-proof";
    internal const string User = "user-GatewayPrincipal";
    internal const string RevokedUser = "user-GatewayRevoked";
    internal const string OtherUser = "user-GatewayReader";
    internal const string Folder = "opaque_FolderGateway";
    internal const string Workspace = "opaque_WorkspaceGateway";
    internal const string TaskId = "opaque_TaskGateway";
    internal const string HistoricalTaskId = "opaque_HistoricalTaskGateway";
    internal const string OtherFolder = "opaque_OtherFolderGateway";
    internal const string ForeignTaskId = "opaque_ForeignTask00";
    internal const string Binding = "opaque_RepositoryGateway";
    internal const string ProviderBinding = "opaque_ProviderGateway";
    internal const string CommitOperation = "opaque_CommitGateway";
    internal const string Reconciliation = "opaque_ReconcileGateway";
    internal const string Correlation = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    internal const string Credential = "fixture-CredentialRef";
    private const string Issuer = "https://identity.example.test/realms/folders";
    private static readonly string[] _folderActions =
    [
        "read_metadata", "read_branch_ref_policy", "query_status", "read_context_search", "read_workspace_status",
        "read_workspace_lock", "read_workspace_cleanup_status", "manage_folder_access",
    ];

    private readonly HttpClient _client = app.GetTestClient();

    /// <summary>Gets the production read-store observation point.</summary>
    internal McpCliGatewayQueryReadModelStore Store => (McpCliGatewayQueryReadModelStore)app.Services.GetRequiredService<IReadModelStore>();

    /// <summary>Gets the source sequence for projection catch-up tests.</summary>
    internal McpCliGatewayQuerySequenceReader Source => source;

    /// <summary>Gets recorded calls to the Memories boundary.</summary>
    internal int SearchCalls => search.Calls;

    /// <summary>Gets recorded discovery calls across the provider port.</summary>
    internal int ProviderCalls => provider.ProviderCalls;

    /// <summary>Gets the current number of readiness evidence records.</summary>
    internal int ReadinessEvidenceCount => app.Services.GetRequiredService<InMemoryProviderReadinessEvidenceStore>().Records.Count;

    /// <summary>Gets the verified file version read from durable state.</summary>
    internal string FileVersion => search.FileVersion;

    /// <summary>Gets the other file retained by the actual bridge.</summary>
    internal string NextFileVersion => search.NextFileVersion;

    /// <summary>Controls which visible and foreign tail rows the external search test source serves.</summary>
    internal void SetSearchTail(bool foreign, bool visible)
    {
        search.IncludeForeignTail = foreign;
        search.IncludeVisibleTail = visible;
    }

    /// <summary>Starts a host and folds exact stored events into SDK persistence before testing reads.</summary>
    internal static async Task<McpCliGatewayQueryFixture> CreateAsync(CancellationToken token)
    {
        RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa) { KeyId = "gateway-query" };
        OpenIdConnectConfiguration oidc = new() { Issuer = Issuer };
        oidc.SigningKeys.Add(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = key.KeyId });
        WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(callbackTenant: Tenant);
        builder.Services.PostConfigure<JwtBearerOptions>(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(oidc));
        string? daprGrpc = Environment.GetEnvironmentVariable("FOLDERS_GATEWAY_QUERY_TEST_DAPR_GRPC");
        builder.Services.RemoveAll<DaprClient>();
        builder.Services.AddSingleton<DaprClient>(_ => daprGrpc is null
            ? new RecordingDaprClient { PersistTypedState = true, EnforceTransactionConcurrency = true }
            : new DaprClientBuilder().UseGrpcEndpoint(daprGrpc).Build());
        builder.Services.RemoveAll<IReadModelStore>();
        builder.Services.AddSingleton<IReadModelStore, McpCliGatewayQueryReadModelStore>();
        builder.Services.RemoveAll<IOrganizationStreamSequenceReader>();
        McpCliGatewayQuerySequenceReader source = new();
        builder.Services.AddSingleton<IOrganizationStreamSequenceReader>(source);
        OperationalStatusNativeSource native = OperationalStatusTestData.NativeSource;
        builder.Services.RemoveAll<INativeSourceScanner>();
        builder.Services.AddSingleton(native.Scanner);
        McpCliGatewayQuerySearchSource search = new();
        builder.Services.RemoveAll<IFolderSearchSource>();
        builder.Services.AddSingleton<IFolderSearchSource>(search);
        RecordingProviderCapabilityResolver provider = new(FakeGitProvider.GitHubLike());
        builder.Services.RemoveAll<IProviderCapabilityResolver>();
        builder.Services.AddSingleton<IProviderCapabilityResolver>(provider);
        WebApplication app = builder.Build();
        app.UseFoldersServerPipeline();
        await app.StartAsync(token).ConfigureAwait(false);

        var fixture = new McpCliGatewayQueryFixture(app, rsa, key, source, search, provider, DateTimeOffset.UtcNow);
        try
        {
            await fixture.SeedAsync(token).ConfigureAwait(false);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Constructs a public Gateway query with exact originating authority.</summary>
    internal static QueryEnvelope Query(string operation, string action, object payload, string aggregate = Folder,
        QueryPagingOptions? paging = null, string user = User)
        => new(Tenant, "folders", aggregate, "Hexalith.Folders.Queries." + operation,
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions.Web), Correlation, user)
        {
            OriginalActorId = user,
            CallerAuthority = new("authenticated-caller", [], [action]),
            Paging = paging,
        };

    /// <summary>Sends a callback with independent workload transport credentials and a signed binding.</summary>
    internal async Task<HttpResponseMessage> PostAsync(QueryEnvelope query, CancellationToken token,
        QueryEnvelope? signedEnvelope = null, bool signQuery = true, bool withIdempotency = false)
    {
        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            [RouteCatalogSurfacePolicy.ClaimType] = "service",
            [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
            [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceQuery,
        };
        if (signQuery)
        {
            foreach ((string claim, string value) in QueryCallerAuthorityBinding.Bind(signedEnvelope ?? query))
            {
                claims.Add(claim, value);
            }
        }

        DateTime issued = DateTime.UtcNow;
        string assertion = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = "folders",
            IssuedAt = issued,
            NotBefore = issued,
            Expires = issued.AddMinutes(2),
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
        using HttpRequestMessage request = new(HttpMethod.Post, "/query") { Content = JsonContent.Create(query) };
        CommittedRouteCatalogSnapshot catalog = app.Services.GetRequiredService<FoldersCallbackRouteCatalog>().Snapshot;
        request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, catalog.RootDigest);
        request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, catalog.Epoch.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        if (withIdempotency)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Correlation);
        }

        return await _client.SendAsync(request, token).ConfigureAwait(false);
    }

    /// <summary>Temporarily makes the retained tenant authorization stale.</summary>
    internal async Task SetTenantFreshnessAsync(bool stale, CancellationToken token)
    {
        IFolderTenantAccessProjectionStore tenants = app.Services.GetRequiredService<IFolderTenantAccessProjectionStore>();
        FolderTenantAccessProjection? current = await tenants.GetAsync(Tenant, token).ConfigureAwait(false);
        await tenants.SaveAsync(new FolderTenantAccessProjection
        {
            TenantId = Tenant,
            Enabled = true,
            Version = current?.Version ?? 0,
            Watermark = 1,
            LastEventTimestamp = stale ? now.AddDays(-2) : now,
            ProjectionWatermark = Tenant + ":1",
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
            {
                [User] = new(User, "Member"),
                [RevokedUser] = new(RevokedUser, "Member"),
                [OtherUser] = new(OtherUser, "Member"),
            },
        }, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await app.DisposeAsync().ConfigureAwait(false);
        rsa.Dispose();
    }

    private async Task SeedAsync(CancellationToken token)
    {
        await SetTenantFreshnessAsync(stale: false, token).ConfigureAwait(false);
        WorkspaceFileMutationAccepted file = new(Tenant, Tenant, Folder, Workspace, FolderWorkspaceLifecycleEvent.FileMutated,
            "opaque_FileOperationGateway", "add", "add", "tenant_sensitive_document", "digest_file_metadata",
            "hash_metadata", 15, "text/plain", "inline", 15, User, Correlation, TaskId, "key-file", "fingerprint-file", now);
        List<IFolderEvent> history =
        [
            new FolderCreated(Tenant, Tenant, Folder, "Gateway Folder", null, null, [], FolderLifecycleState.Active,
                FolderRepositoryBindingState.Unbound, User, Correlation, TaskId, "key-create", "fingerprint-create", now),
            new RepositoryBindingRequested(Tenant, Tenant, Folder, Binding, ProviderBinding, "provider-profile", "branch_policy_gateway",
                User, Correlation, HistoricalTaskId, "key-binding", "fingerprint-binding", now),
            new RepositoryBound(Tenant, Tenant, Folder, Binding, ProviderBinding, Correlation, HistoricalTaskId,
                "key-bound", "fingerprint-bound", now),
            new BranchRefPolicyConfigured(Tenant, Tenant, Folder, Binding, "branch_policy_gateway", "refs/heads/main",
                ["refs/heads/*"], ["refs/heads/main"], User, Correlation, TaskId, "key-policy", "fingerprint-policy", now),
            new WorkspacePreparationRequested(Tenant, Tenant, Folder, Workspace, Binding, "branch_policy_gateway", "workspace-policy",
                User, Correlation, TaskId, "prepare-key", "prepare-fingerprint", now),
            new FolderWorkspaceLifecycleEventRecorded(Tenant, Tenant, Folder, Workspace, FolderWorkspaceLifecycleEvent.WorkspacePrepared,
                null, "opaque_PrepareGateway", Correlation, TaskId, "prepared-key", "prepared-fingerprint", now),
            new WorkspaceLockAcquired(Tenant, Tenant, Folder, Workspace, FolderWorkspaceLifecycleEvent.WorkspaceLocked, "opaque_LockGateway",
                "exclusive_write", 3600, TaskId, now, now, now.AddHours(1), "lease_until_expiry",
                User, Correlation, TaskId, "lock-key", "lock-fingerprint", now),
            file,
            new WorkspaceCommitOutcomeUnknown(Tenant, Tenant, Folder, Workspace, FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown,
                CommitOperation, "unknown_provider_outcome", Reconciliation, true, "authorref_gateway", "branchref_gateway",
                "user_supplied", "digest_path_metadata", User, Correlation, TaskId, "commit-key", "commit-fingerprint", now),
        ];
        foreach (string action in _folderActions)
        {
            history.Add(new FolderAccessGranted(Tenant, Tenant, Folder, FolderAccessPrincipalKind.User, User, action, User, Correlation,
                TaskId, "key-" + action, "fingerprint-" + action, history.Count + 1, now));
        }
        history.Add(new FolderAccessGranted(Tenant, Tenant, Folder, FolderAccessPrincipalKind.User, RevokedUser, "read_metadata", User,
            Correlation, TaskId, "revoked-grant", "revoked-grant-fingerprint", history.Count + 1, now));
        history.Add(new FolderAccessRevoked(Tenant, Tenant, Folder, FolderAccessPrincipalKind.User, RevokedUser, "read_metadata", User,
            Correlation, TaskId, "revoked-key", "revoked-fingerprint", history.Count + 1, now));
        history.Add(new FolderAccessGranted(Tenant, Tenant, Folder, FolderAccessPrincipalKind.User, OtherUser, "read_context_search", User,
            Correlation, TaskId, "other-reader-key", "other-reader-fingerprint", history.Count + 1, now));
        source.Heads[(Tenant, Folder)] = history.Count;
        OperationalStatusEvent[] events = OperationalStatusTestData.Sources(history);
        OperationalStatusProjectionWriter writer = app.Services.GetRequiredService<OperationalStatusProjectionWriter>();
        (await writer.ApplyAsync(Tenant, Folder, "seed-gateway-query", events, token).ConfigureAwait(false)).IsSuccess.ShouldBeTrue();
        EventStoreFolderEffectivePermissionsReadModel permissions = app.Services.GetRequiredService<EventStoreFolderEffectivePermissionsReadModel>();
        foreach (OperationalStatusEvent item in events)
        {
            await permissions.ApplyAsync(Tenant, Folder, item.MessageId, item.Sequence, item.Digest, item.Event, token).ConfigureAwait(false);
        }

        OperationalStatusEvent[] foreignEvents = OperationalStatusTestData.Sources(
        [
            ((FolderCreated)history[0]) with { FolderId = OtherFolder, TaskId = ForeignTaskId },
            ((RepositoryBindingRequested)history[1]) with { FolderId = OtherFolder, TaskId = ForeignTaskId },
            new FolderAccessGranted(Tenant, Tenant, OtherFolder, FolderAccessPrincipalKind.User, User, "query_status", User,
                Correlation, ForeignTaskId, "foreign-reader-key", "foreign-reader-fingerprint", 3, now),
        ]);
        source.Heads[(Tenant, OtherFolder)] = foreignEvents.Length;
        (await writer.ApplyAsync(Tenant, OtherFolder, "seed-foreign-task", foreignEvents, token).ConfigureAwait(false)).IsSuccess.ShouldBeTrue();
        foreach (OperationalStatusEvent item in foreignEvents)
        {
            await permissions.ApplyAsync(Tenant, OtherFolder, item.MessageId, item.Sequence, item.Digest, item.Event, token).ConfigureAwait(false);
        }

        EventStoreProviderReadinessBindingReadModel bindingModel = app.Services.GetRequiredService<EventStoreProviderReadinessBindingReadModel>();
        foreach (string reference in new[] { ProviderBinding, Tenant })
        {
            string actor = ProviderBindingActorId.Create(reference);
            ConfigureProviderBinding bindingCommand = new(Tenant, Tenant, OrganizationAclPrincipalKind.User, User, reference, "github",
                Credential, OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
                Correlation, TaskId, "provider-key", now, null);
            var validated = OrganizationProviderBindingCommandValidator.Validate(bindingCommand);
            validated.IsAccepted.ShouldBeTrue();
            ProviderBindingConfigured binding = new(Tenant, Tenant, reference, "github", Credential, OrganizationProviderBindingPolicy.Empty,
                OrganizationProviderBindingPolicy.Empty, Correlation, TaskId, "provider-key", validated.IdempotencyFingerprint, "configured", now);
            source.Heads[(Tenant, actor)] = 1;
            await bindingModel.ApplyAsync(Tenant, actor, "provider-source-1", 1, new string('a', 64), binding, token).ConfigureAwait(false);
        }

        EventStoreSemanticIndexingBridgeStore bridge = app.Services.GetRequiredService<EventStoreSemanticIndexingBridgeStore>();
        var indexed = (await bridge.ApplyFolderEventsAsync([new FolderProjectionEnvelope(Tenant, 8, file)], token)
            .ConfigureAwait(false)).ShouldHaveSingleItem();
        await bridge.RecordIndexingResultAsync(new SemanticIndexingResultUpdate(indexed.Identity, SemanticIndexingBridgeStatus.Indexed,
            "memories_accepted", false, Correlation, TaskId, "event-indexed", "fingerprint-indexed", now), token).ConfigureAwait(false);
        search.FileVersion = (await bridge.ListFolderAsync(Tenant, Folder, token).ConfigureAwait(false)).ShouldHaveSingleItem().Identity.FileVersionId;
        var second = (await bridge.ApplyFolderEventsAsync([new FolderProjectionEnvelope(Tenant, 9, file with
        {
            OperationId = "opaque_AnotherGatewayFile",
            PathMetadataDigest = "digest_another_file",
            ContentHashReference = "hash_another_file",
        })], token).ConfigureAwait(false)).ShouldHaveSingleItem();
        await bridge.RecordIndexingResultAsync(new SemanticIndexingResultUpdate(second.Identity, SemanticIndexingBridgeStatus.Indexed,
            "memories_accepted", false, Correlation, TaskId, "second-indexed", "fingerprint-second", now), token).ConfigureAwait(false);
        search.NextFileVersion = second.Identity.FileVersionId;
        (await bindingModel.GetAsync(new(Tenant, ProviderBinding, Correlation), token).ConfigureAwait(false))
            .ShouldNotBeNull().CredentialReferenceId.ShouldBe(Credential);
        (await permissions.GetCurrentAsync(Tenant, Folder, token).ConfigureAwait(false)).ShouldNotBeNull().LastSequence.ShouldBe(history.Count);
        (await app.Services.GetRequiredService<SharedProjectionEpochCoordinator>().ReadAsync<OperationalStatusCheckpoint>(
            OperationalStatusGeneration.Scope(Tenant), OperationalStatusKeys.Checkpoint(Tenant, Folder), token).ConfigureAwait(false))
            .Value.ShouldNotBeNull().State.BranchRefPolicy.ShouldNotBeNull().PolicyRef.ShouldBe("branch_policy_gateway");
        Store.Reads.Clear();
    }
}
