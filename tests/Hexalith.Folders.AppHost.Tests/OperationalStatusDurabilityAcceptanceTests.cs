using System.Text.Json.Nodes;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.ProviderReadiness;
using System.Buffers.Binary;
using System.Text;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Folders.Aggregates.Folder;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Queries.Folders;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Real-source operational projection evidence; workspace/task closure requires provider prerequisites.</summary>
[Collection("Folder durability acceptance")]
public sealed class OperationalStatusDurabilityAcceptanceTests(FolderDurabilityAcceptanceFixture fixture, ITestOutputHelper output)
    : IClassFixture<FolderDurabilityAcceptanceFixture>
{
    /// <summary>Checks real persistence, process replacement, two consumers and repeated empty-checkpoint rebuild.</summary>
    [Fact]
    public async Task RetainedFolderProjectionShouldSurviveRestartRebuildAndSafeFailures()
    {
        if (!fixture.Enabled)
        {
            Assert.Skip($"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true to execute operational durability acceptance.");
            return;
        }

        await fixture.OrganizationCommandAsync("InitializeOrganizationAclBaseline", ["manage_organization_acl"]);
        await fixture.OrganizationCommandAsync("GrantOrganizationAclPrincipal", ["create_folder", "read_metadata", "manage_folder_access"]);
        await RequireRealTaskClosureAsync();
        string key = FolderDurabilityAcceptanceFixture.NewId();
        string folder = fixture.CreatedFolderId(key);
        using (HttpResponseMessage created = await fixture.MutateAsync("/api/v1/folders",
            new { requestSchemaVersion = "v1", folderMetadata = new { displayName = "Operational durability" } }, key))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(created, HttpStatusCode.Accepted, "Operational source creation");
        }

        string checkpointKey = OperationalStatusKeys.Checkpoint(fixture.TenantId, folder);
        string lifecycleKey = OperationalStatusKeys.Lifecycle(fixture.TenantId, folder);
        OperationalStatusCheckpoint checkpoint = await WaitForCheckpointAsync(checkpointKey);
        checkpoint.Sequence.ShouldBe(1);
        checkpoint.EventDigests.ShouldHaveSingleItem();
        FolderLifecycleStatusReadModelSnapshot original = (await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>(
            "statestore", lifecycleKey, TestContext.Current.CancellationToken)).Value!;
        original.LifecycleState.ShouldBe(FolderLifecycleProjectionState.Active);
        await RequireLifecycleAsync(folder, HttpStatusCode.OK);
        StreamReadPage stream = await fixture.ReadStreamAsync(folder);
        string retainedHash = Hash(stream);
        ProjectionEventDto[] replay = stream.Events.Select(source => new ProjectionEventDto(source.EventTypeName,
            source.Payload, source.SerializationFormat, source.SequenceNumber, source.Timestamp,
            source.CorrelationId!, source.MessageId, source.UserId)
        { MetadataVersion = source.MetadataVersion, StoredPayloadVersion = source.PayloadVersion, GlobalPosition = source.GlobalPosition }).ToArray();
        output.WriteLine($"PASS real-source: sequence=1 retainedSha256={retainedHash}");

        var processIds = await fixture.RestartHostsAsync();
        processIds["folders"].After.ShouldNotBe(processIds["folders-workers"].After);
        foreach (var process in processIds)
        {
            process.Value.Before.ShouldNotBe(process.Value.After);
            output.WriteLine($"PASS process-replacement: {process.Key} {process.Value.Before}->{process.Value.After}");
        }
        await WaitForLifecycleRecoveryAsync(folder);
        JsonSerializer.Serialize((await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>(
            "statestore", lifecycleKey, TestContext.Current.CancellationToken)).Value).ShouldBe(JsonSerializer.Serialize(original));
        Hash(await fixture.ReadStreamAsync(folder)).ShouldBe(retainedHash);

        IReadModelConditionalEraser eraser = fixture.Store.ShouldBeAssignableTo<IReadModelConditionalEraser>();
        for (int rebuild = 0; rebuild < 2; rebuild++)
        {
            foreach (string target in new[] { lifecycleKey, checkpointKey })
            {
                var entry = await eraser.TryReadEtagAsync("statestore", target, TestContext.Current.CancellationToken);
                entry.Present.ShouldBeTrue();
                (await eraser.TryEraseAsync("statestore", target, entry.Etag, TestContext.Current.CancellationToken)).ShouldBeTrue();
            }
            using HttpResponseMessage rebuilt = await fixture.ProjectAsync(folder, replay);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rebuilt, HttpStatusCode.OK, "Repeated zero-checkpoint rebuild");
            (await WaitForCheckpointAsync(checkpointKey)).SourceDigest.ShouldBe(checkpoint.SourceDigest);
            JsonSerializer.Serialize((await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>(
                "statestore", lifecycleKey, TestContext.Current.CancellationToken)).Value).ShouldBe(JsonSerializer.Serialize(original));
        }
        output.WriteLine("PASS rebuild: two complete empty-checkpoint rebuilds preserve views and retained bytes");

        // These invoke the same writer in the distinct Folders and Workers processes.
        HttpResponseMessage[] concurrent = await Task.WhenAll(fixture.ProjectAsync(folder, replay),
            fixture.DeliverRetainedFolderEventAsync(folder, stream.Events.Single()));
        try
        {
            foreach (HttpResponseMessage response in concurrent)
            {
                response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError);
            }
        }
        finally
        {
            foreach (HttpResponseMessage response in concurrent) response.Dispose();
        }
        using (HttpResponseMessage retry = await fixture.DeliverRetainedFolderEventAsync(folder, stream.Events.Single()))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(retry, HttpStatusCode.OK, "Duplicate consumer recovery");
        }
        (await WaitForCheckpointAsync(checkpointKey)).Sequence.ShouldBe(1);
        Hash(await fixture.ReadStreamAsync(folder)).ShouldBe(retainedHash);
        output.WriteLine("PASS two-process-delivery: existing Server and Workers converge without duplicate source advancement");

        using (HttpResponseMessage unsupported = await fixture.ProjectAsync(folder, [replay[0] with { StoredPayloadVersion = 2 }]))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(unsupported, HttpStatusCode.UnprocessableEntity, "Unsupported operational replay");
        }
        (await WaitForCheckpointAsync(checkpointKey)).SourceDigest.ShouldBe(checkpoint.SourceDigest);

        var before = await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken);
        (await fixture.Store.TrySaveAsync("statestore", lifecycleKey, original with { ManagedTenantId = "foreign-evidence" },
            before.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        try
        {
            await RequireLifecycleAsync(folder, HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            var corrupt = await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken);
            (await fixture.Store.TrySaveAsync("statestore", lifecycleKey, original, corrupt.ETag!, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        using (HttpClient foreign = fixture.CreateFoldersClient(await fixture.ForeignTokenAsync()))
        using (HttpResponseMessage denied = await foreign.GetAsync($"/api/v1/folders/{folder}/lifecycle-status", TestContext.Current.CancellationToken))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.NotFound, "Foreign tenant operational read");
            (await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain(folder);
        }
        await fixture.SetStoragePausedAsync(true);
        try
        {
            await RequireLifecycleAsync(folder, HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            await fixture.SetStoragePausedAsync(false);
        }
        await RequireLifecycleAsync(folder, HttpStatusCode.OK);
        (await WaitForCheckpointAsync(checkpointKey)).SourceDigest.ShouldBe(checkpoint.SourceDigest);
        Hash(await fixture.ReadStreamAsync(folder)).ShouldBe(retainedHash);
        output.WriteLine("PASS safe-failure: unsupported version, corrupt view, foreign tenant and unavailable store preserve source/checkpoint");

    }

    private async Task RequireRealTaskClosureAsync()
    {
        await fixture.OrganizationCommandAsync("GrantOrganizationAclPrincipal",
            ["configure_provider_binding", "provider_readiness_read", "bind_repository", "configure_branch_ref_policy",
                "prepare_workspace", "lock_workspace"]).ConfigureAwait(true);
        const string binding = "operational-provider";
        using (HttpResponseMessage deniedConfiguration = await fixture.MutateAsync("/api/v1/provider-bindings/" + binding,
            new
            {
                requestSchemaVersion = "v1",
                providerFamilyRef = "forgejo",
                capabilityProfileRef = "absent-profile",
                nonSecretCredentialReference = OperationalProviderFixture.CredentialReference
            },
            FolderDurabilityAcceptanceFixture.NewId(), method: HttpMethod.Put).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(deniedConfiguration, HttpStatusCode.Forbidden, "Unknown provider profile safe denial").ConfigureAwait(true);
        using (HttpResponseMessage configured = await fixture.MutateAsync("/api/v1/provider-bindings/" + binding,
            new
            {
                requestSchemaVersion = "v1",
                providerFamilyRef = "forgejo",
                capabilityProfileRef = "operational",
                nonSecretCredentialReference = OperationalProviderFixture.CredentialReference
            },
            FolderDurabilityAcceptanceFixture.NewId(), method: HttpMethod.Put).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(configured, HttpStatusCode.Accepted, "Real provider configuration").ConfigureAwait(true);
        await WaitForPublicStatusAsync("/api/v1/provider-bindings/" + binding).ConfigureAwait(true);
        await RequireProviderBindingRebuildAsync(binding).ConfigureAwait(true);
        using (HttpClient client = fixture.CreateFoldersClient())
        using (HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/provider-readiness/validations")
        { Content = JsonContent.Create(new { providerBindingRef = binding, requestedCapability = "existing_repository_binding" }) })
        using (HttpResponseMessage readiness = await FolderDurabilityHttp.SendAsync(client, request, true, TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(readiness, HttpStatusCode.OK, "Real provider readiness").ConfigureAwait(true);
            JsonElement diagnostic = await readiness.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
            string category = diagnostic.TryGetProperty("sanitizedErrorCategory", out JsonElement error) ? error.ToString() : "none";
            output.WriteLine($"Provider readiness: status={diagnostic.GetProperty("status")}; category={category}; reason={diagnostic.GetProperty("safeReasonCode")}");
            diagnostic.GetProperty("status").GetString().ShouldBe("ready");
        }
        string createKey = FolderDurabilityAcceptanceFixture.NewId();
        string folder = fixture.CreatedFolderId(createKey);
        using (HttpResponseMessage created = await fixture.MutateAsync("/api/v1/folders",
            new { requestSchemaVersion = "v1", folderMetadata = new { displayName = "Provider pipeline" } }, createKey).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(created, HttpStatusCode.Accepted, "Pipeline folder creation").ConfigureAwait(true);
        await WaitForPublicStatusAsync($"/api/v1/folders/{folder}/lifecycle-status").ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "repository binding").ConfigureAwait(true);
        using (HttpResponseMessage bound = await fixture.MutateAsync($"/api/v1/folders/{folder}/repository-bindings",
            new
            {
                requestSchemaVersion = "v1",
                providerBindingRef = binding,
                externalRepositoryRef = "operational-remote",
                branchRefPolicy = new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = "target",
                    policyRef = "operational-main",
                    defaultRef = "branch_ref_main",
                    allowedRefPatterns = new[] { "branch_ref_main" },
                    protectedRefPatterns = Array.Empty<string>()
                }
            },
            FolderDurabilityAcceptanceFixture.NewId()).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(bound, HttpStatusCode.Accepted, "Real supported repository binding").ConfigureAwait(true);
        RepositoryBound repository = await WaitForEventAsync<RepositoryBound>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "folder access grants").ConfigureAwait(true);
        await fixture.GrantFolderActionsAsync(folder, ["read_workspace_lock", "read_workspace_status", "read_workspace_cleanup_status"]).ConfigureAwait(true);
        using (HttpResponseMessage policy = await fixture.MutateAsync($"/api/v1/folders/{folder}/branch-ref-policy",
            new
            {
                requestSchemaVersion = "v1",
                repositoryBindingId = repository.RepositoryBindingId,
                policyRef = "operational-main",
                defaultRef = "branch_ref_main",
                allowedRefPatterns = new[] { "branch_ref_main" },
                protectedRefPatterns = Array.Empty<string>()
            }, FolderDurabilityAcceptanceFixture.NewId(), method: HttpMethod.Put).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(policy, HttpStatusCode.Accepted, "Real provider branch policy").ConfigureAwait(true);
        _ = await WaitForEventAsync<BranchRefPolicyConfigured>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "workspace preparation").ConfigureAwait(true);
        const string workspace = "operational-workspace";
        string task = FolderDurabilityAcceptanceFixture.NewId();
        object preparation = new
        {
            requestSchemaVersion = "v1",
            repositoryBindingId = repository.RepositoryBindingId,
            branchRefPolicyRef = "operational-main",
            workspacePolicyRef = "metadata-workspace"
        };
        string preparationKey = FolderDurabilityAcceptanceFixture.NewId();
        await fixture.SetProviderRepositoryAvailableAsync(false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            using HttpResponseMessage denied = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/preparation",
                preparation, FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.Forbidden,
                "Missing bound repository preparation denial").ConfigureAwait(true);
            JsonElement denial = await denied.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
            denial.GetProperty("category").GetString().ShouldBe("tenant_access_denied");
            var deniedSource = await fixture.ReadStreamAsync(folder).ConfigureAwait(true);
            var rejection = deniedSource.Events.Last();
            rejection.EventTypeName.ShouldBe("Hexalith.Folders.Server.FolderCommandRejected");
            using JsonDocument rejectionPayload = JsonDocument.Parse(rejection.Payload);
            bool hasCode = rejectionPayload.RootElement.TryGetProperty("Code", out JsonElement rejectionCode)
                || rejectionPayload.RootElement.TryGetProperty("code", out rejectionCode);
            hasCode.ShouldBeTrue();
            rejectionCode.GetString().ShouldBe(nameof(FolderResultCode.ProviderPermissionInsufficient));
            deniedSource.Events.ShouldNotContain(item => item.EventTypeName == typeof(WorkspacePreparationRequested).FullName);
            deniedSource.Events.ShouldNotContain(item => item.EventTypeName == typeof(FolderWorkspaceLifecycleEventRecorded).FullName);
        }
        finally
        {
            await fixture.SetProviderRepositoryAvailableAsync(true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "provider-target denial recovery").ConfigureAwait(true);
        output.WriteLine("PASS bound-target denial: absent real repository cannot append preparation success; exact target restored privately");
        using (HttpResponseMessage prepared = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/preparation",
            preparation, preparationKey, taskId: task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(prepared, HttpStatusCode.Accepted, "Authenticated metadata preparation").ConfigureAwait(true);
        _ = await WaitForEventAsync<WorkspacePreparationRequested>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "workspace lock").ConfigureAwait(true);
        (await WaitForPublicStatusAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/status").ConfigureAwait(true))
            .GetProperty("currentState").GetString().ShouldBe("ready");
        using (HttpResponseMessage locked = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock",
            new
            {
                requestSchemaVersion = "v1",
                lockIntent = "exclusive_write",
                requestedLeaseSeconds = 900
            }, FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(locked, HttpStatusCode.Accepted, "Authenticated workspace lock").ConfigureAwait(true);
        WorkspaceLockAcquired lease = await WaitForEventAsync<WorkspaceLockAcquired>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "clean release").ConfigureAwait(true);
        string releaseKey = FolderDurabilityAcceptanceFixture.NewId();
        object release = new
        {
            requestSchemaVersion = "v1",
            lockId = lease.LockId,
            lockOwnershipProof = OwnershipProof(fixture.TenantId, folder, workspace, task, lease.LockId),
            releaseReasonCode = "caller_completed"
        };
        using (HttpResponseMessage released = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock/release",
            release, releaseKey, taskId: task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(released, HttpStatusCode.Accepted, "Authenticated clean release").ConfigureAwait(true);
        _ = await WaitForEventAsync<WorkspaceLockReleased>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "terminal task read").ConfigureAwait(true);
        string taskPath = $"/api/v1/tasks/{task}/status?folderId={folder}";
        JsonElement terminal = await WaitForPublicStatusAsync(taskPath).ConfigureAwait(true);
        terminal.GetProperty("terminalState").GetString().ShouldBe("ready");
        foreach (string concern in new[] { "status", "lock", "cleanup/status" })
            _ = await WaitForPublicStatusAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/{concern}").ConfigureAwait(true);
        StreamReadPage retained = await fixture.ReadStreamAsync(folder).ConfigureAwait(true);
        string sourceHash = Hash(retained);
        await RequireAllConcernRebuildAndRecoveryAsync(folder, workspace, task, retained).ConfigureAwait(true);
        await RequireConcurrentWorkersWritesAsync(folder, workspace, task, retained).ConfigureAwait(true);
        foreach (var process in await fixture.RestartHostsAsync().ConfigureAwait(true))
        {
            process.Value.Before.ShouldNotBe(process.Value.After);
            output.WriteLine($"PASS task-pipeline restart: {process.Key} {process.Value.Before}->{process.Value.After}");
        }
        (await WaitForPublicStatusAsync(taskPath).ConfigureAwait(true)).GetProperty("terminalState").GetString().ShouldBe("ready");
        await WaitForPublicStatusAsync("/api/v1/provider-bindings/" + binding).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "equivalent terminal replay").ConfigureAwait(true);
        using (HttpResponseMessage replay = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock/release",
            release, releaseKey, taskId: task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(replay, HttpStatusCode.Accepted, "Equivalent terminal release replay").ConfigureAwait(true);
        using (HttpResponseMessage denied = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/preparation",
            preparation, FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(true))
            denied.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        // A rejected source position may be appended; the retained accepted prefix stays byte-identical.
        StreamReadPage after = await fixture.ReadStreamAsync(folder).ConfigureAwait(true);
        Hash(after with { Events = after.Events.Take(retained.Events.Count).ToArray() }).ShouldBe(sourceHash);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "post-rejection terminal task read").ConfigureAwait(true);
        (await WaitForPublicStatusAsync(taskPath).ConfigureAwait(true)).GetProperty("terminalState").GetString().ShouldBe("ready");
        using (HttpClient client = fixture.CreateFoldersClient())
        {
            using HttpResponseMessage missing = await client.GetAsync($"/api/v1/tasks/{task}/status", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(missing, HttpStatusCode.ServiceUnavailable, "Missing task folder scope").ConfigureAwait(true);
            using HttpResponseMessage foreign = await client.GetAsync($"/api/v1/tasks/{task}/status?folderId=absent-folder", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(foreign, HttpStatusCode.NotFound, "Foreign task folder scope").ConfigureAwait(true);
        }
        output.WriteLine("PASS real-provider task pipeline: supported Forgejo TLS, private credential resolution, bound/prepared/locked/clean release, six metadata concerns, restart, equivalent replay, closed-binding denial and scoped task reads");
    }

    private async Task RequireProviderBindingRebuildAsync(string binding)
    {
        string actor = ProviderBindingActorId.Create(binding);
        string key = EventStoreProviderReadinessBindingReadModel.Key(fixture.TenantId, actor);
        var original = await fixture.Store.GetAsync<ProviderBindingProjection>("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true);
        original.Value!.LastSequence.ShouldBe(2);
        original.Value.Messages.Count.ShouldBe(2);
        original.Value.Binding.ShouldNotBeNull();
        StreamReadPage retained = await fixture.ReadStreamAsync(actor).ConfigureAwait(true);
        retained.Events.Count.ShouldBe(2);
        string sourceBytes = Encoding.UTF8.GetString(retained.Events.SelectMany(source => source.Payload).ToArray());
        string storedProjection = JsonSerializer.Serialize(original.Value);
        foreach (string sentinel in new[] { OperationalProviderFixture.Owner, OperationalProviderFixture.Repository,
            OperationalProviderFixture.Branch, "authorized_base_url", "repository_owner", "repository_name", "default_branch", "selected_ref", "https://" })
        {
            sourceBytes.ShouldNotContain(sentinel, Case.Sensitive);
            storedProjection.ShouldNotContain(sentinel, Case.Sensitive);
        }
        retained.Events[0].EventTypeName.ShouldBe("Hexalith.Folders.Server.FolderCommandRejected");
        using (JsonDocument rejected = JsonDocument.Parse(retained.Events[0].Payload))
        {
            JsonElement code = rejected.RootElement.TryGetProperty("code", out JsonElement camelCode)
                ? camelCode : rejected.RootElement.GetProperty("Code");
            code.GetString().ShouldBe("ValidationFailed");
        }
        IReadModelConditionalEraser eraser = fixture.Store.ShouldBeAssignableTo<IReadModelConditionalEraser>();
        var row = await eraser.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await eraser.TryEraseAsync("statestore", key, row.Etag, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        using (HttpClient client = fixture.CreateFoldersClient())
        using (HttpResponseMessage missing = await client.GetAsync("/api/v1/provider-bindings/" + binding, TestContext.Current.CancellationToken).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(missing, HttpStatusCode.ServiceUnavailable, "Missing retained provider configuration projection").ConfigureAwait(true);
        ProjectionEventDto[] events = retained.Events.Select(source => new ProjectionEventDto(source.EventTypeName,
            source.Payload, source.SerializationFormat, source.SequenceNumber, source.Timestamp, source.CorrelationId!, source.MessageId, source.UserId)
        { MetadataVersion = source.MetadataVersion, StoredPayloadVersion = source.PayloadVersion, GlobalPosition = source.GlobalPosition }).ToArray();
        using (HttpResponseMessage rebuilt = await fixture.ProjectAsync(actor, events).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rebuilt, HttpStatusCode.OK, "Provider binding complete-prefix rebuild").ConfigureAwait(true);
        JsonSerializer.Serialize((await fixture.Store.GetAsync<ProviderBindingProjection>("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(original.Value));
        Hash(await fixture.ReadStreamAsync(actor).ConfigureAwait(true)).ShouldBe(Hash(retained));
        output.WriteLine("PASS provider binding: retained rejection does not strand later configuration; missing retained view fails closed; complete source rebuild preserves exact binding and retained source");
    }

    private async Task RequireConcurrentWorkersWritesAsync(string folder, string workspace, string task, StreamReadPage stream)
    {
        string lifecycleKey = OperationalStatusKeys.Lifecycle(fixture.TenantId, folder);
        string checkpointKey = OperationalStatusKeys.Checkpoint(fixture.TenantId, folder);
        string workspaceKey = OperationalStatusKeys.Workspace(fixture.TenantId, folder, workspace);
        string taskKey = OperationalStatusKeys.Task(fixture.TenantId, folder, task);
        var expectedCheckpoint = await fixture.Store.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var expectedLifecycle = await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var expectedWorkspace = await fixture.Store.GetAsync<OperationalWorkspaceStatus>("statestore", workspaceKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var expectedTask = await fixture.Store.GetAsync<TaskStatusReadModelSnapshot>("statestore", taskKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string permissionKey = "projection:folders:folder-permissions:" + fixture.TenantId + ":" + folder;
        var expectedPermissions = await fixture.Store.GetAsync<FolderPermissionProjection>("statestore", permissionKey,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        expectedPermissions.Value.ShouldNotBeNull();
        expectedPermissions.Value.LastSequence.ShouldBe(stream.Events.Last().SequenceNumber);
        foreach (StreamReadEvent retained in stream.Events)
        {
            FolderProjectionMessage receipt = expectedPermissions.Value.RecentMessages.Single(item => item.Sequence == retained.SequenceNumber);
            receipt.MessageId.ShouldBe(retained.MessageId);
            receipt.PayloadDigest.ShouldBe(Convert.ToHexString(SHA256.HashData(
                [.. Encoding.UTF8.GetBytes(retained.EventTypeName + ":" + (retained.PayloadVersion ?? 1) + ":"), .. retained.Payload])));
        }
        (await fixture.ProofStore.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).Value.ShouldBeNull();
        var processes = fixture.OrdinaryProofProcessIds();
        processes.First.ShouldNotBe(processes.Second);
        output.WriteLine($"PASS ordinary-write processes: proof-a={processes.First}; proof-b={processes.Second}");
        DateTimeOffset proofStartedAt = DateTimeOffset.UtcNow;
        foreach (StreamReadEvent source in stream.Events)
        {
            HttpResponseMessage[] responses = await Task.WhenAll(
                fixture.DeliverRetainedFolderEventAsync(folder, source, "folders-workers-proof-a"),
                fixture.DeliverRetainedFolderEventAsync(folder, source, "folders-workers-proof-b")).ConfigureAwait(true);
            for (int replica = 0; replica < responses.Length; replica++)
            {
                using HttpResponseMessage first = responses[replica];
                if (first.StatusCode != HttpStatusCode.OK)
                {
                    first.StatusCode.ShouldBeOneOf(HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError);
                    string resource = replica == 0 ? "folders-workers-proof-a" : "folders-workers-proof-b";
                    bool acknowledged = false;
                    for (int retry = 0; retry < 5 && !acknowledged; retry++)
                    {
                        await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(true);
                        using HttpResponseMessage response = await fixture.DeliverRetainedFolderEventAsync(folder, source, resource).ConfigureAwait(true);
                        acknowledged = response.StatusCode == HttpStatusCode.OK;
                        if (!acknowledged) response.StatusCode.ShouldBeOneOf(HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError);
                    }
                    acknowledged.ShouldBeTrue($"Both Workers must acknowledge retained position {source.SequenceNumber}; replica={resource}");
                }
            }
            (await fixture.ProofStore.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey, TestContext.Current.CancellationToken).ConfigureAwait(true))
                .Value!.Sequence.ShouldBe(source.SequenceNumber);
        }
        JsonSerializer.Serialize((await fixture.ProofStore.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value).ShouldBe(JsonSerializer.Serialize(expectedCheckpoint.Value));
        JsonSerializer.Serialize((await fixture.ProofStore.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value).ShouldBe(JsonSerializer.Serialize(expectedLifecycle.Value));
        JsonSerializer.Serialize((await fixture.ProofStore.GetAsync<OperationalWorkspaceStatus>("statestore", workspaceKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value).ShouldBe(JsonSerializer.Serialize(expectedWorkspace.Value));
        JsonSerializer.Serialize((await fixture.ProofStore.GetAsync<TaskStatusReadModelSnapshot>("statestore", taskKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value).ShouldBe(JsonSerializer.Serialize(expectedTask.Value));
        Hash(await fixture.ReadStreamAsync(folder).ConfigureAwait(true)).ShouldBe(Hash(stream));
        var actualPermissions = await fixture.ProofStore.GetAsync<FolderPermissionProjection>("statestore", permissionKey,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        actualPermissions.Value.ShouldNotBeNull();
        // Permission projection time records processing; event identities and permission state must still match exactly.
        TimeSpan projectedClockSkew = TimeSpan.FromMinutes(2);
        actualPermissions.Value.ProjectedAt.ShouldBeInRange(proofStartedAt - projectedClockSkew,
            DateTimeOffset.UtcNow + projectedClockSkew);
        JsonSerializer.Serialize(actualPermissions.Value with { ProjectedAt = expectedPermissions.Value.ProjectedAt })
            .ShouldBe(JsonSerializer.Serialize(expectedPermissions.Value));
        output.WriteLine("PASS two concurrent Workers replica processes: overlapping ordered writes from a fresh shared derived-state namespace ACK in both processes and converge exactly without changing retained source");
    }

    private async Task RequireAllConcernRebuildAndRecoveryAsync(string folder, string workspace, string task, StreamReadPage stream)
    {
        string lifecycleKey = OperationalStatusKeys.Lifecycle(fixture.TenantId, folder);
        string workspaceKey = OperationalStatusKeys.Workspace(fixture.TenantId, folder, workspace);
        string taskKey = OperationalStatusKeys.Task(fixture.TenantId, folder, task);
        string checkpointKey = OperationalStatusKeys.Checkpoint(fixture.TenantId, folder);
        var lifecycle = await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var workspaceRow = await fixture.Store.GetAsync<OperationalWorkspaceStatus>("statestore", workspaceKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var taskRow = await fixture.Store.GetAsync<TaskStatusReadModelSnapshot>("statestore", taskKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var checkpoint = await fixture.Store.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey, TestContext.Current.CancellationToken).ConfigureAwait(true);
        lifecycle.Value.ShouldNotBeNull(); workspaceRow.Value.ShouldNotBeNull(); taskRow.Value.ShouldNotBeNull(); checkpoint.Value.ShouldNotBeNull();
        taskRow.Value.TerminalState.ShouldBe("ready");
        workspaceRow.Value.Cleanup.Status.ShouldBe("status_only");
        workspaceRow.Value.Cleanup.LastAttemptedAt.ShouldBeNull();
        await RequirePersistedRetentionAsync([lifecycleKey, workspaceKey, taskKey], "ordinary writes").ConfigureAwait(true);
        DateTimeOffset boundary = DateTimeOffset.UtcNow - OperationalStatusKeys.Retention;
        // Corrupt only rebuildable view metadata; this checks integrity, independently of retention.
        (await fixture.Store.TrySaveAsync("statestore", lifecycleKey, lifecycle.Value with
        { Freshness = lifecycle.Value.Freshness with { ObservedAt = boundary } }, lifecycle.ETag!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        OperationalWorkspaceStatus expiredWorkspace = workspaceRow.Value with
        {
            Lock = workspaceRow.Value.Lock with { Freshness = workspaceRow.Value.Lock.Freshness with { ObservedAt = boundary } },
            Workspace = workspaceRow.Value.Workspace with { Freshness = workspaceRow.Value.Workspace.Freshness with { ObservedAt = boundary } },
            Cleanup = workspaceRow.Value.Cleanup with { Freshness = workspaceRow.Value.Cleanup.Freshness with { ObservedAt = boundary } },
        };
        (await fixture.Store.TrySaveAsync("statestore", workspaceKey, expiredWorkspace, workspaceRow.ETag!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        (await fixture.Store.TrySaveAsync("statestore", taskKey, taskRow.Value with
        { Freshness = taskRow.Value.Freshness with { ObservedAt = boundary } }, taskRow.ETag!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        using (HttpClient client = fixture.CreateFoldersClient())
        {
            foreach (string path in new[] { $"/api/v1/folders/{folder}/lifecycle-status",
                $"/api/v1/folders/{folder}/workspaces/{workspace}/lock", $"/api/v1/folders/{folder}/workspaces/{workspace}/status",
                $"/api/v1/folders/{folder}/workspaces/{workspace}/cleanup/status", $"/api/v1/tasks/{task}/status?folderId={folder}" })
            {
                using HttpResponseMessage expired = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
                await FolderDurabilityAcceptanceFixture.RequireStatusAsync(expired, HttpStatusCode.ServiceUnavailable, "Corrupt timestamp metadata concern").ConfigureAwait(true);
            }
        }
        IReadModelConditionalEraser eraser = fixture.Store.ShouldBeAssignableTo<IReadModelConditionalEraser>();
        foreach (string key in new[] { lifecycleKey, workspaceKey, taskKey, checkpointKey })
        {
            var current = await eraser.TryReadEtagAsync("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true);
            (await eraser.TryEraseAsync("statestore", key, current.Etag, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        }
        ProjectionEventDto[] events = stream.Events.Select(source => new ProjectionEventDto(source.EventTypeName,
            source.Payload, source.SerializationFormat, source.SequenceNumber, source.Timestamp, source.CorrelationId!, source.MessageId, source.UserId)
        { MetadataVersion = source.MetadataVersion, StoredPayloadVersion = source.PayloadVersion, GlobalPosition = source.GlobalPosition }).ToArray();
        using (HttpResponseMessage rebuilt = await fixture.NamedProjectAsync(folder, events, FolderDurabilityAcceptanceFixture.NewId()).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rebuilt, HttpStatusCode.OK, "SDK named empty-checkpoint rebuild").ConfigureAwait(true);
            DomainSharedProjectionRebuildResponse result = (await rebuilt.Content.ReadFromJsonAsync<DomainSharedProjectionRebuildResponse>(TestContext.Current.CancellationToken).ConfigureAwait(true))!;
            result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        JsonSerializer.Serialize((await fixture.Store.GetAsync<FolderLifecycleStatusReadModelSnapshot>("statestore", lifecycleKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(lifecycle.Value));
        JsonSerializer.Serialize((await fixture.Store.GetAsync<OperationalWorkspaceStatus>("statestore", workspaceKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(workspaceRow.Value));
        JsonSerializer.Serialize((await fixture.Store.GetAsync<TaskStatusReadModelSnapshot>("statestore", taskKey, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(taskRow.Value));
        await RequirePersistedRetentionAsync([lifecycleKey, workspaceKey, taskKey], "complete-source rebuild").ConfigureAwait(true);
        output.WriteLine("PASS all six concerns: altered observation timestamps fail full-row integrity; SDK shared empty-checkpoint rebuild restores exact source-folded lifecycle/lock/workspace/cleanup/task/commit metadata");

        string recoveryId = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage staged = await fixture.NamedProjectAsync(folder, events, recoveryId, stopAfterStage: true).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(staged, HttpStatusCode.OK, "SDK streamed generation staged before promotion").ConfigureAwait(true);
        SharedProjectionEpochCoordinator coordinator = new(fixture.RawStore, fixture.RawStore.ShouldBeAssignableTo<IReadModelBatchStore>());
        var scope = OperationalStatusGeneration.Scope(fixture.TenantId);
        (await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken).ConfigureAwait(true)).IsBuilding.ShouldBeTrue();
        (await coordinator.GetStreamingStageStatusAsync(scope, recoveryId, TestContext.Current.CancellationToken).ConfigureAwait(true)).IsSealed.ShouldBeTrue();
        foreach (var process in await fixture.RestartHostsAsync().ConfigureAwait(true))
            process.Value.Before.ShouldNotBe(process.Value.After);
        using (HttpResponseMessage resumed = await fixture.NamedProjectAsync(folder, events, recoveryId).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(resumed, HttpStatusCode.OK, "SDK staged generation resumed after changed process identities").ConfigureAwait(true);
        using (HttpResponseMessage recovered = await fixture.DeliverRetainedFolderEventAsync(folder, stream.Events.Last()).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(recovered, HttpStatusCode.OK, "Authenticated ordinary replay after SDK generation recovery").ConfigureAwait(true);
        var completed = await coordinator.GetStatusAsync(scope, TestContext.Current.CancellationToken).ConfigureAwait(true);
        completed.IsBuilding.ShouldBeFalse();
        completed.PendingDeliveryCount.ShouldBe(0);
        (await fixture.Store.GetAsync<OperationalStatusCheckpoint>("statestore", checkpointKey, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .Value!.SourceDigest.ShouldBe(checkpoint.Value.SourceDigest);
        Hash(await fixture.ReadStreamAsync(folder).ConfigureAwait(true)).ShouldBe(Hash(stream));
        output.WriteLine("PASS SDK interrupted generation: sealed private streamed staging survives changed writer PIDs; the same SDK session resumes promotion and authenticated ordinary replay before completion; source hashes unchanged");

    }

    private async Task RequirePersistedRetentionAsync(string[] keys, string phase)
    {
        long configured = (long)OperationalStatusKeys.Retention.TotalSeconds;
        foreach (string key in keys)
        {
            long remaining = await fixture.OperationalTtlSecondsAsync(key).ConfigureAwait(true);
            remaining.ShouldBeGreaterThan(configured - 3600);
            remaining.ShouldBeLessThanOrEqualTo(configured);
            output.WriteLine($"PASS persisted retention: {phase}; selected view TTL={remaining}s; configured={configured}s");
        }
    }

    private async Task<T> WaitForEventAsync<T>(string folder)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        while (true)
        {
            StreamReadPage stream = await fixture.ReadStreamWithDeadlineAsync(folder, deadline.Token).ConfigureAwait(true);
            StreamReadEvent? source = stream.Events.LastOrDefault(item => item.EventTypeName == typeof(T).FullName);
            if (source is not null) return JsonSerializer.Deserialize<T>(source.Payload, EventStorePayloadSerialization.Options)!;
            await Task.Delay(1000, deadline.Token).ConfigureAwait(true);
        }
    }

    private async Task<JsonElement> WaitForPublicStatusAsync(string path)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        using HttpClient client = fixture.CreateFoldersClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, deadline.Token).ConfigureAwait(true);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK, "Current authorized pipeline status").ConfigureAwait(true);
        return await response.Content.ReadFromJsonAsync<JsonElement>(deadline.Token).ConfigureAwait(true);
    }

    private static string OwnershipProof(params string[] fields)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string field in new[] { "workspace-lock-ownership-proof-v1" }.Concat(fields))
        {
            byte[] value = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
            hash.AppendData(length);
            hash.AppendData(value);
        }
        return "lock_proof_" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32];
    }

    private async Task<OperationalStatusCheckpoint> WaitForCheckpointAsync(string key)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            var entry = await fixture.Store.GetAsync<OperationalStatusCheckpoint>("statestore", key, deadline.Token).ConfigureAwait(true);
            if (entry.Value is not null && !string.IsNullOrEmpty(entry.ETag)) return entry.Value;
            await Task.Delay(TimeSpan.FromMilliseconds(500), deadline.Token).ConfigureAwait(true);
        }
    }

    private async Task RequireLifecycleAsync(string folder, HttpStatusCode expected)
    {
        using HttpClient client = fixture.CreateFoldersClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/folders/{folder}/lifecycle-status");
        using HttpResponseMessage response = expected == HttpStatusCode.OK
            ? await FolderDurabilityHttp.SendAsync(client, request, true, TestContext.Current.CancellationToken).ConfigureAwait(true)
            : await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, expected, "Public operational lifecycle read").ConfigureAwait(true);
    }

    private async Task WaitForLifecycleRecoveryAsync(string folder)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        using HttpClient client = fixture.CreateFoldersClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/folders/{folder}/lifecycle-status");
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, deadline.Token).ConfigureAwait(true);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK, "Recovered public operational lifecycle read").ConfigureAwait(true);
    }

    private static string Hash(StreamReadPage stream)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(stream.Events)));
}
