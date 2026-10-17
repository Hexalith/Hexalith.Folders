using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.TenantAccess;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Real-authority durable repository acceptance against an isolated local AppHost.</summary>
[Collection("Folder durability acceptance")]
public sealed class FolderDurabilityAcceptanceTests(FolderDurabilityAcceptanceFixture fixture, ITestOutputHelper output)
    : IClassFixture<FolderDurabilityAcceptanceFixture>
{
    private readonly FolderDurabilityAcceptanceFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Exercises the complete ordered durability matrix without granting authority through test state writes.
    /// Every dependency failure on the opted-in lane is a failed test, including fixture initialization.
    /// </summary>
    [Fact]
    public async Task AuthenticatedRepositoryShouldPersistRestartReplayAndFailSafely()
    {
        if (!_fixture.Enabled)
        {
            Assert.Skip($"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true to execute durability acceptance.");
            return;
        }

        await WaitUntilAsync(async () => (await _fixture.Store.GetAsync<FolderTenantAccessProjection>("statestore",
            "projection:folders:tenant-access:" + _fixture.TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value is not null, "managed tenant event delivery").ConfigureAwait(true);
        _output.WriteLine("PASS tenant-authority: fresh managed tenant, real JWT claims and persisted tenant-access row");

        await _fixture.OrganizationCommandAsync("InitializeOrganizationAclBaseline", ["manage_organization_acl"]).ConfigureAwait(true);
        _output.WriteLine("PASS organization-command: authenticated baseline completed through EventStore");
        await WaitForOrganizationAsync(2).ConfigureAwait(true);

        string deniedKey = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage denied = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("authority-required"), deniedKey).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.NotFound, "Missing explicit create grant").ConfigureAwait(true);
            await AssertCanonicalDenialAsync(denied).ConfigureAwait(true);
        }

        await _fixture.RequireCreateAbsentAsync(deniedKey).ConfigureAwait(true);
        _output.WriteLine("PASS missing-grant-denial: canonical safe failure, no retained folder stream or permission row");
        await _fixture.OrganizationCommandAsync("GrantOrganizationAclPrincipal", ["create_folder", "read_metadata", "manage_folder_access"]).ConfigureAwait(true);
        OrganizationPermissionProjection organization = await WaitForOrganizationAsync(5).ConfigureAwait(true);
        _output.WriteLine("PASS organization-grants: three single-operation grants completed and persisted through sequence 5");
        organization.Grants.ShouldContain(item => item.PrincipalId == _fixture.PrincipalId && item.Action == "create_folder");
        StreamReadPage organizationStream = await _fixture.ReadStreamAsync(_fixture.TenantId).ConfigureAwait(true);
        string organizationHash = RetainedHash(organizationStream);
        _output.WriteLine("PASS organization-authority: managed tenant, explicit baseline/grants, persisted permission row");

        string key = FolderDurabilityAcceptanceFixture.NewId();
        string folderId = _fixture.CreatedFolderId(key);
        using (HttpResponseMessage created = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("durability-acceptance"), key).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(created, HttpStatusCode.Accepted, "Public folder creation").ConfigureAwait(true);
        }

        FolderPermissionProjection folder = await WaitForFolderAsync(folderId).ConfigureAwait(true);
        folder.IsCreated.ShouldBeTrue();
        folder.IsArchived.ShouldBeFalse();
        folder.TenantId.ShouldBe(_fixture.TenantId);
        folder.OrganizationId.ShouldBe(_fixture.TenantId);
        await AssertPositiveAclAsync(folderId).ConfigureAwait(true);
        StreamReadPage stream = await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true);
        stream.Events.Count.ShouldBe(1);
        stream.Events[0].EventTypeName.ShouldBe("Hexalith.Folders.Aggregates.Folder.FolderCreated");
        stream.Events[0].PayloadVersion.ShouldBeNull();
        string retainedHash = RetainedHash(stream);
        AssertMetadataOnly(stream);
        AssertMetadataOnly(organizationStream);
        _output.WriteLine($"PASS accepted-mutation: persisted folder/event/ACL; events={stream.Events.Count}; retainedSha256={retainedHash}");

        using (HttpResponseMessage equivalent = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("durability-acceptance"), key).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(equivalent, HttpStatusCode.Accepted, "Equivalent intent replay").ConfigureAwait(true);
        }

        using (HttpResponseMessage conflicting = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("different-intent"), key).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(conflicting, HttpStatusCode.Conflict, "Different intent conflict").ConfigureAwait(true);
        }

        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        _output.WriteLine("PASS idempotency: equivalent replay and conflicting intent preserve retained bytes");

        IReadOnlyDictionary<string, (int Before, int After)> processIds = await _fixture.RestartHostsAsync().ConfigureAwait(true);
        foreach ((string resource, (int before, int after)) in processIds)
        {
            _output.WriteLine($"PASS process-replacement: resource={resource}; before={before}; after={after}");
        }
        await AssertPositiveAclAsync(folderId).ConfigureAwait(true);
        FolderPermissionProjection restarted = await WaitForFolderAsync(folderId).ConfigureAwait(true);
        JsonSerializer.Serialize(restarted).ShouldBe(JsonSerializer.Serialize(folder));
        JsonSerializer.Serialize((await _fixture.Store.GetAsync<OrganizationPermissionProjection>("statestore",
            "projection:folders:organization-permissions:" + _fixture.TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(organization));
        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        RetainedHash(await _fixture.ReadStreamAsync(_fixture.TenantId).ConfigureAwait(true)).ShouldBe(organizationHash);
        // A fresh accepted request forces actor reconstruction after the EventStore host restart.
        using (HttpResponseMessage replayAfterRestart = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("durability-acceptance"), key).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(replayAfterRestart, HttpStatusCode.Accepted, "Durable idempotency after host restart").ConfigureAwait(true);
        }

        _output.WriteLine("PASS host-restart: persisted organization/folder authority, actor reconstruction and original retained hashes");

        ProjectionEventDto[] events = ToProjectionEvents(stream);
        using (HttpResponseMessage projected = await _fixture.ProjectAsync(folderId, events).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(projected, HttpStatusCode.OK, "Empty-checkpoint folder replay").ConfigureAwait(true);
            ProjectionResponse replay = (await projected.Content.ReadFromJsonAsync<ProjectionResponse>(TestContext.Current.CancellationToken).ConfigureAwait(true))!;
            replay.ProjectionType.ShouldBe("folder-state");
            replay.State.GetProperty("folderId").GetString().ShouldBe(folderId);
            replay.State.GetProperty("managedTenantId").GetString().ShouldBe(restarted.TenantId);
            replay.State.GetProperty("organizationId").GetString().ShouldBe(restarted.OrganizationId);
            replay.State.GetProperty("isCreated").GetBoolean().ShouldBe(restarted.IsCreated);
            replay.State.GetProperty("lifecycleState").GetInt32().ShouldBe(0);
        }

        using (HttpResponseMessage projected = await _fixture.ProjectAsync(_fixture.TenantId, ToProjectionEvents(organizationStream)).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(projected, HttpStatusCode.OK, "Empty-checkpoint organization replay").ConfigureAwait(true);
            ProjectionResponse replay = (await projected.Content.ReadFromJsonAsync<ProjectionResponse>(TestContext.Current.CancellationToken).ConfigureAwait(true))!;
            replay.ProjectionType.ShouldBe("organization-provider-binding");
            replay.State.GetProperty("IsInitialized").GetBoolean().ShouldBe(organization.IsInitialized);
            replay.State.GetProperty("Grants").GetArrayLength().ShouldBe(organization.Grants.Count);
            foreach (JsonElement grant in replay.State.GetProperty("Grants").EnumerateArray())
            {
                grant.GetProperty("ManagedTenantId").GetString().ShouldBe(organization.TenantId);
                grant.GetProperty("OrganizationId").GetString().ShouldBe(organization.OrganizationId);
                string? principal = grant.GetProperty("PrincipalId").GetString();
                string? action = grant.GetProperty("Action").GetString();
                organization.Grants.ShouldContain(item => item.PrincipalId == principal && item.Action == action);
            }
        }

        foreach (ProjectionEventDto invalid in new[]
        {
            events[0] with { EventTypeName = "Unknown.Folders.Event" },
            events[0] with { StoredPayloadVersion = 2 },
        })
        {
            using HttpResponseMessage unreadable = await _fixture.ProjectAsync(folderId, [invalid]).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(unreadable, HttpStatusCode.UnprocessableEntity, "Unsupported replay event").ConfigureAwait(true);
            JsonElement problem = await unreadable.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
            problem.GetProperty("code").GetString().ShouldBe("projection_unreadable");
        }

        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        JsonSerializer.Serialize((await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", FolderKey(folderId), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(folder));
        _output.WriteLine("PASS zero-checkpoint-replay: both folds agree; unknown/version-invalid events fail before persisted advancement");

        string foreign = await _fixture.ForeignTokenAsync().ConfigureAwait(true);
        using (HttpClient client = _fixture.CreateFoldersClient(foreign))
        using (HttpResponseMessage denied = await client.GetAsync($"/api/v1/folders/{folderId}/acl", TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.NotFound, "Foreign tenant protected read").ConfigureAwait(true);
            await AssertCanonicalDenialAsync(denied, "safe_not_found").ConfigureAwait(true);
        }

        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        JsonSerializer.Serialize((await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", FolderKey(folderId), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(folder));
        _output.WriteLine("PASS foreign-tenant-denial: canonical safe failure and unchanged persisted folder/events");

        await AssertMalformedEvidenceAsync(folderId, folder).ConfigureAwait(true);
        await AssertUnavailableStorageAsync(folderId).ConfigureAwait(true);
        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        await AssertPositiveAclAsync(folderId).ConfigureAwait(true);
        _output.WriteLine("PASS dependency-failures: malformed evidence and bounded unavailable-store failure preserve persisted folder/events");

        await _fixture.OrganizationCommandAsync("RevokeOrganizationAclPrincipal", ["create_folder", "read_metadata", "manage_folder_access"]).ConfigureAwait(true);
        OrganizationPermissionProjection revoked = await WaitForOrganizationAsync(8).ConfigureAwait(true);
        revoked.Grants.ShouldNotContain(item => item.PrincipalId == _fixture.PrincipalId
            && (item.Action == "create_folder" || item.Action == "read_metadata" || item.Action == "manage_folder_access"));
        string revokedKey = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage denied = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("revoked-grant"), revokedKey).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.NotFound, "Revoked create grant").ConfigureAwait(true);
            await AssertCanonicalDenialAsync(denied).ConfigureAwait(true);
        }

        await _fixture.RequireCreateAbsentAsync(revokedKey).ConfigureAwait(true);
        using (HttpClient client = _fixture.CreateFoldersClient())
        using (HttpResponseMessage denied = await client.GetAsync($"/api/v1/folders/{folderId}/acl", TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(denied, HttpStatusCode.NotFound, "Revoked folder access").ConfigureAwait(true);
            await AssertCanonicalDenialAsync(denied).ConfigureAwait(true);
        }

        RetainedHash(await _fixture.ReadStreamAsync(folderId).ConfigureAwait(true)).ShouldBe(retainedHash);
        JsonSerializer.Serialize((await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", FolderKey(folderId), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value)
            .ShouldBe(JsonSerializer.Serialize(folder));
        _output.WriteLine("PASS authority-denial: missing/revoked/foreign tenant denied without disclosure or folder append");
    }

    private async Task AssertMalformedEvidenceAsync(string folderId, FolderPermissionProjection original)
    {
        string key = FolderKey(folderId);
        var before = await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await _fixture.Store.TrySaveAsync("statestore", key, original with { TenantId = "foreign-evidence" }, before.ETag!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        try
        {
            using HttpClient client = _fixture.CreateFoldersClient();
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/folders/{folderId}/acl", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Forbidden, "Malformed derived authorization evidence").ConfigureAwait(true);
            JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
            problem.GetProperty("code").GetString().ShouldBe("authorization_evidence_malformed");
        }
        finally
        {
            var corrupt = await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", key, TestContext.Current.CancellationToken).ConfigureAwait(true);
            (await _fixture.Store.TrySaveAsync("statestore", key, original, corrupt.ETag!, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        }
    }

    private async Task AssertUnavailableStorageAsync(string folderId)
    {
        string failedKey = FolderDurabilityAcceptanceFixture.NewId();
        await _fixture.SetStoragePausedAsync(true).ConfigureAwait(true);
        try
        {
            using HttpClient client = _fixture.CreateFoldersClient();
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/folders/{folderId}/acl", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.ServiceUnavailable, "Unavailable dependency protected read").ConfigureAwait(true);
            await AssertCanonicalUnavailableAsync(response).ConfigureAwait(true);
            using HttpResponseMessage mutation = await _fixture.MutateAsync("/api/v1/folders", CreatePayload("unavailable-dependency"), failedKey).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(mutation, HttpStatusCode.ServiceUnavailable, "Unavailable dependency mutation").ConfigureAwait(true);
            await AssertCanonicalUnavailableAsync(mutation).ConfigureAwait(true);
        }
        finally
        {
            await _fixture.SetStoragePausedAsync(false).ConfigureAwait(true);
        }

        await _fixture.RequireCreateAbsentAsync(failedKey).ConfigureAwait(true);
    }

    private async Task AssertPositiveAclAsync(string folderId)
    {
        await WaitUntilAsync(async () =>
        {
            using HttpClient client = _fixture.CreateFoldersClient();
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/folders/{folderId}/acl", TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
            body.GetProperty("items").ValueKind.ShouldBe(JsonValueKind.Array);
            return true;
        }, "positive public ACL read").ConfigureAwait(true);
    }

    private async Task<OrganizationPermissionProjection> WaitForOrganizationAsync(long sequence)
    {
        OrganizationPermissionProjection? projection = null;
        await WaitUntilAsync(async () =>
        {
            projection = (await _fixture.Store.GetAsync<OrganizationPermissionProjection>("statestore", "projection:folders:organization-permissions:" + _fixture.TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value;
            return projection?.LastSequence == sequence;
        }, "persisted organization permission contents").ConfigureAwait(true);
        return projection!;
    }

    private async Task<FolderPermissionProjection> WaitForFolderAsync(string folderId)
    {
        FolderPermissionProjection? projection = null;
        await WaitUntilAsync(async () =>
        {
            projection = (await _fixture.Store.GetAsync<FolderPermissionProjection>("statestore", FolderKey(folderId), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value;
            return projection?.IsCreated == true;
        }, "persisted folder permission contents").ConfigureAwait(true);
        return projection!;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string operation)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            while (!await condition().WaitAsync(timeout.Token).ConfigureAwait(true))
            {
                await Task.Delay(500, timeout.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Acceptance timed out waiting for " + operation + ".");
        }
    }

    private string FolderKey(string folderId) => "projection:folders:folder-permissions:" + _fixture.TenantId + ":" + folderId;

    private static object CreatePayload(string name) => new { requestSchemaVersion = "v1", folderMetadata = new { displayName = name } };

    private static async Task AssertCanonicalDenialAsync(HttpResponseMessage response, string? exactCode = null)
    {
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
        string? code = problem.GetProperty("code").GetString();
        if (exactCode is not null)
        {
            code.ShouldBe(exactCode);
        }
        else
        {
            new[] { "safe_not_found", "folder_acl_denied", "not_found" }.ShouldContain(code);
        }

        // The public gateway mapper normalizes an aggregate denial to not_found;
        // direct layered authorization uses not_found_to_caller before submission.
        problem.GetProperty("category").GetString().ShouldBe(code == "not_found" ? "not_found" : "not_found_to_caller");
        string text = problem.GetRawText();
        text.ShouldNotContain("FolderCreated");
        text.ShouldNotContain("accessOverrides");
        text.ShouldNotContain("durability-acceptance");
    }

    private static async Task AssertCanonicalUnavailableAsync(HttpResponseMessage response)
    {
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(true);
        problem.GetProperty("category").GetString().ShouldBe("read_model_unavailable");
        new[] { "tenant_projection_unavailable", "tenant_projection_stale", "folder_acl_unavailable", "folder_acl_stale", "evidence_unavailable", "read_model_unavailable" }
            .ShouldContain(problem.GetProperty("code").GetString());
        problem.GetProperty("retryable").GetBoolean().ShouldBeTrue();
        problem.GetRawText().ShouldNotContain("durability-acceptance");
        problem.GetRawText().ShouldNotContain("accessOverrides");
        problem.GetRawText().ShouldNotContain("Exception");
    }

    private static ProjectionEventDto[] ToProjectionEvents(StreamReadPage page)
        => page.Events.Select(item => new ProjectionEventDto(item.EventTypeName, item.Payload, item.SerializationFormat,
            item.SequenceNumber, item.Timestamp, item.CorrelationId!, item.MessageId, item.UserId)
        {
            MetadataVersion = item.MetadataVersion,
            StoredPayloadVersion = item.PayloadVersion,
        }).ToArray();

    private static string RetainedHash(StreamReadPage page)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page.Events))).ToLowerInvariant();

    private static void AssertMetadataOnly(StreamReadPage page)
    {
        foreach (StreamReadEvent item in page.Events)
        {
            using JsonDocument document = JsonDocument.Parse(item.Payload);
            foreach (string forbidden in new[] { "contentBytes", "inlineContent", "access_token", "client_secret", "password", "humanAuthorityReceipt" })
            {
                document.RootElement.GetRawText().ShouldNotContain(forbidden, Case.Insensitive);
            }
        }
    }
}
