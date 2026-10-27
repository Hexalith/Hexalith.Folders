using System.Net;
using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Exercises actual content transport, gateway decisions and durable bodies in replaced hosts.</summary>
[Collection("Folder durability acceptance")]
public sealed class WorkspaceContentDurabilityAcceptanceTests(WorkspaceContentDurabilityAcceptanceFixture fixture, ITestOutputHelper output)
    : IClassFixture<WorkspaceContentDurabilityAcceptanceFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Qualifies actual bytes, replay, fences, suppression, hold and unavailable persistence.</summary>
    [Fact]
    public async Task ContentShouldSurviveProcessReplacementAndRemainSuppressedAfterRemoval()
    {
        if (!fixture.Enabled)
        {
            Assert.Skip($"Set {AspireFoldersAppHostFixture.OptInEnvironmentVariable}=true to execute content durability acceptance.");
            return;
        }
        (string folder, string workspace, string task, long fence) = await PrepareAsync();
        byte[] bytes = Encoding.UTF8.GetBytes("Exact authoritative workspace bytes\r\nWith preserved line endings.\n");
        FileUploadDescriptor descriptor = Descriptor(folder, workspace, "docs/content.txt", fence);
        string key = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage accepted = await UploadAsync(descriptor, bytes, key, task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(accepted, HttpStatusCode.Accepted, "Actual inline content admission");
        WorkspaceFileMutationAccepted added = await WaitForMutationAsync(folder, descriptor.OperationId);
        WorkspaceContentBinding binding = added.ContentBinding.ShouldNotBeNull();
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "authoritative content read");
        await RequireRangeAsync(folder, workspace, task, "docs/content.txt", bytes);
        await RequirePersistedBodyAsync(folder, workspace, binding.ReceiptId!, bytes);
        using (HttpResponseMessage replay = await UploadAsync(descriptor, bytes, key, task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(replay, HttpStatusCode.Accepted, "Equivalent content replay");
        (await MutationsAsync(folder)).Count(item => item.OperationId == descriptor.OperationId).ShouldBe(1);
        output.WriteLine($"PASS stage/replay: bytes={bytes.Length}; sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}; version={binding.Version}");

        using (HttpResponseMessage stale = await UploadAsync(descriptor with
        {
            OperationId = FolderDurabilityAcceptanceFixture.NewId(),
            FileOperationKind = FileMutationRequestFileOperationKind.Change,
            OwnerFileId = binding.FileId,
            ExpectedFileVersion = 0,
        }, bytes, FolderDurabilityAcceptanceFixture.NewId(), task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(stale, HttpStatusCode.Conflict, "Stale file version");

        var processes = await fixture.RestartHostsAsync();
        foreach (var process in processes)
        {
            process.Value.Before.ShouldNotBe(process.Value.After);
            output.WriteLine($"PASS process-replacement: {process.Key} {process.Value.Before}->{process.Value.After}");
        }
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content recovery after process replacement");
        await RequireRangeAsync(folder, workspace, task, "docs/content.txt", bytes);
        await RequirePersistedBodyAsync(folder, workspace, binding.ReceiptId!, bytes);

        // The multipart body crosses the inline threshold and is inspected in the real Redis/Dapr store.
        byte[] streamed = Enumerable.Repeat((byte)'s', 262145).ToArray();
        FileUploadDescriptor streamedDescriptor = Descriptor(folder, workspace, "docs/stream.txt", fence);
        using (HttpResponseMessage streamedResponse = await UploadAsync(streamedDescriptor, streamed, FolderDurabilityAcceptanceFixture.NewId(), task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(streamedResponse, HttpStatusCode.Accepted, "Actual multipart content admission");
        WorkspaceFileMutationAccepted streamAdded = await WaitForMutationAsync(folder, streamedDescriptor.OperationId);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "multipart content publication");
        await RequireRangeAsync(folder, workspace, task, "docs/stream.txt", streamed[..262144]);
        await RequirePersistedBodyAsync(folder, workspace, streamAdded.ContentBinding!.ReceiptId!, streamed);
        output.WriteLine($"PASS multipart: bytes={streamed.Length}; sha256={Convert.ToHexStringLower(SHA256.HashData(streamed))}");
        var bodyScope = new ContentScope(fixture.TenantId, "folders", folder, workspace);
        string bodyScopeHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(bodyScope)));
        string streamedBodyKey = $"authoritative-content:body:{bodyScopeHash}:{streamAdded.ContentBinding.ReceiptId}";
        foreach (string fault in new[] { "missing", "corrupt" })
        {
            await fixture.SetContentBodyFaultAsync(streamedBodyKey, fault);
            try
            {
                using HttpClient faultClient = fixture.CreateFoldersClient();
                using HttpRequestMessage faultRequest = RangeRequest(folder, workspace, task, "docs/stream.txt", 32);
                using HttpResponseMessage faultResponse = await faultClient.SendAsync(faultRequest, TestContext.Current.CancellationToken);
                await FolderDurabilityAcceptanceFixture.RequireStatusAsync(faultResponse, HttpStatusCode.ServiceUnavailable, "Authoritative " + fault + " body");
            }
            finally { await fixture.SetContentBodyFaultAsync(streamedBodyKey, "restore"); }
            await RequireRangeAsync(folder, workspace, task, "docs/stream.txt", streamed[..32]);
        }
        output.WriteLine("PASS integrity-faults: missing/corrupt physical bodies return unavailable; exact bytes recover after restoration");

        // Independent public clients target distinct mutation-serving Folders processes.
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "serving replica current authority", replica: true);
        var serving = fixture.ContentServingProcessIds();
        serving.Primary.ShouldNotBe(serving.Replica);
        using HttpClient primaryDestination = fixture.CreateFoldersClient();
        using HttpClient replicaDestination = fixture.CreateContentReplicaClient();
        primaryDestination.BaseAddress.ShouldNotBe(replicaDestination.BaseAddress);
        output.WriteLine($"SERVING-REPLICAS primary={serving.Primary} endpoint={primaryDestination.BaseAddress}; replica={serving.Replica} endpoint={replicaDestination.BaseAddress}");
        FileUploadDescriptor firstRace = Descriptor(folder, workspace, "docs/race.txt", fence);
        FileUploadDescriptor secondRace = firstRace with { OperationId = FolderDurabilityAcceptanceFixture.NewId() };
        byte[] firstBytes = Encoding.UTF8.GetBytes("Concurrent contender one\n"), secondBytes = Encoding.UTF8.GetBytes("Concurrent contender two\n");
        string firstCorrelation = FolderDurabilityAcceptanceFixture.NewId(), secondCorrelation = FolderDurabilityAcceptanceFixture.NewId();
        HttpResponseMessage[] raceResponses = await Task.WhenAll(
            UploadAsync(firstRace, firstBytes, FolderDurabilityAcceptanceFixture.NewId(), task, firstCorrelation),
            UploadAsync(secondRace, secondBytes, FolderDurabilityAcceptanceFixture.NewId(), task, secondCorrelation, replica: true)).ConfigureAwait(true);
        for (int index = 0; index < raceResponses.Length; index++)
        {
            using HttpResponseMessage response = raceResponses[index];
            output.WriteLine($"RACE-RESPONSE contender={index}; http={(int)response.StatusCode}");
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.Conflict, HttpStatusCode.ServiceUnavailable);
            if (response.StatusCode == HttpStatusCode.Accepted)
                await WaitForTerminalCommandAsync(index == 0 ? firstCorrelation : secondCorrelation).ConfigureAwait(true);
        }
        foreach (StreamReadEvent retained in (await fixture.ReadStreamAsync(folder)).Events.Where(item =>
            item.EventTypeName == "Hexalith.Folders.Server.FolderCommandRejected"
                && (item.CorrelationId == firstCorrelation || item.CorrelationId == secondCorrelation)))
        {
            using JsonDocument rejected = JsonDocument.Parse(retained.Payload);
            string code = (rejected.RootElement.TryGetProperty("Code", out var value) ? value : rejected.RootElement.GetProperty("code")).GetString()!;
            Enum.TryParse<FolderResultCode>(code, out var knownCode).ShouldBeTrue();
            output.WriteLine($"RETAINED-RACE-REJECTION code={knownCode}");
        }
        WorkspaceFileMutationAccepted raceWinner = (await MutationsAsync(folder)).Where(item =>
            item.OperationId == firstRace.OperationId || item.OperationId == secondRace.OperationId).ShouldHaveSingleItem();
        raceWinner.ContentBinding!.Version.ShouldBe(1);
        byte[] winnerBytes = raceWinner.OperationId == firstRace.OperationId ? firstBytes : secondBytes;
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "concurrent content winner");
        await RequireRangeAsync(folder, workspace, task, "docs/race.txt", winnerBytes);
        await RequirePersistedBodyAsync(folder, workspace, raceWinner.ContentBinding!.ReceiptId!, winnerBytes);
        output.WriteLine($"PERSISTED-RACE-WINNER version={raceWinner.ContentBinding.Version}; receipt={raceWinner.ContentBinding.ReceiptId}; bytes={winnerBytes.Length}; sha256={Convert.ToHexStringLower(SHA256.HashData(winnerBytes))}");
        FileUploadDescriptor loser = raceWinner.OperationId == firstRace.OperationId ? secondRace : firstRace;
        byte[] loserBytes = raceWinner.OperationId == firstRace.OperationId ? secondBytes : firstBytes;
        using (HttpResponseMessage conflicted = await UploadAsync(loser, loserBytes, FolderDurabilityAcceptanceFixture.NewId(), task, replica: true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(conflicted, HttpStatusCode.Conflict, "Retried concurrent loser remains fenced");
        output.WriteLine("PASS serving-replica-race: independent Folders processes; exactly one immutable version; persisted winner bytes; loser conflicts");

        byte[] binary = [0, 255, 1, 128];
        FileUploadDescriptor binaryDescriptor = Descriptor(folder, workspace, "docs/binary.dat", fence);
        using (HttpResponseMessage binaryResponse = await UploadAsync(binaryDescriptor, binary, FolderDurabilityAcceptanceFixture.NewId(), task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(binaryResponse, HttpStatusCode.Accepted, "Binary content preservation");
        WorkspaceFileMutationAccepted binaryAdded = await WaitForMutationAsync(folder, binaryDescriptor.OperationId);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "binary metadata classification");
        await RequirePersistedBodyAsync(folder, workspace, binaryAdded.ContentBinding!.ReceiptId!, binary);
        await RequireDeniedRangeAsync(folder, workspace, task, "docs/binary.dat");
        await RequireDeniedRangeAsync(folder, workspace, task, "private/hidden.txt");
        await RequireDeniedRangeAsync(folder, workspace, task, "docs/missing.txt");
        using (HttpClient foreign = fixture.CreateFoldersClient(await fixture.ForeignTokenAsync()))
        using (HttpRequestMessage request = RangeRequest(folder, workspace, task, "docs/content.txt", bytes.Length))
        using (HttpResponseMessage response = await foreign.SendAsync(request, TestContext.Current.CancellationToken))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.NotFound, "Foreign tenant content read");

        await DispositionAsync(folder, workspace, binding.ReceiptId!, "hold");
        string removeOperation = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage removed = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/files/remove",
            new
            {
                requestSchemaVersion = "v1",
                operationId = removeOperation,
                fileOperationKind = "remove",
                transportOperation = "metadataOnlyRemoval",
                pathMetadata = Metadata("docs/content.txt"),
                ownerFileId = binding.FileId,
                expectedFileVersion = 1,
                expectedLockFence = fence
            },
            FolderDurabilityAcceptanceFixture.NewId(), taskId: task))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(removed, HttpStatusCode.Accepted, "Authoritative removal admission");
        _ = await WaitForMutationAsync(folder, removeOperation);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "immediate deletion suppression");
        await RequireDeniedRangeAsync(folder, workspace, task, "docs/content.txt");
        await DispositionAsync(folder, workspace, binding.ReceiptId!, "apply");
        await RequirePersistedBodyAsync(folder, workspace, binding.ReceiptId!, bytes);
        await DispositionAsync(folder, workspace, binding.ReceiptId!, "release");
        await DispositionAsync(folder, workspace, binding.ReceiptId!, "apply");
        await RequirePersistedBodyAsync(folder, workspace, binding.ReceiptId!, bytes);
        output.WriteLine("PASS suppression/hold: tombstone denies reads immediately; hold and fresh epoch preserve physical bytes");

        await fixture.SetStoragePausedAsync(true);
        try
        {
            using HttpClient client = fixture.CreateFoldersClient();
            using HttpRequestMessage request = RangeRequest(folder, workspace, task, "docs/stream.txt", 32);
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.ServiceUnavailable, "Unavailable authoritative store");
        }
        finally { await fixture.SetStoragePausedAsync(false); }
        await RequireRangeAsync(folder, workspace, task, "docs/stream.txt", streamed[..32]);
        output.WriteLine("PASS safe-failure: stale version, binary, hidden/missing paths, foreign tenant and unavailable persistence");
        await RequireOrphanErasureAsync();
        await RequireAtomicBatchAsync();
    }

    private async Task<(string Folder, string Workspace, string Task, long Fence)> PrepareAsync(bool initialize = true)
    {
        const string provider = "content-provider";
        if (initialize)
        {
            await fixture.OrganizationCommandAsync("InitializeOrganizationAclBaseline", ["manage_organization_acl"]).ConfigureAwait(false);
            await fixture.OrganizationCommandAsync("GrantOrganizationAclPrincipal", ["create_folder", "read_metadata", "manage_folder_access",
            "configure_provider_binding", "provider_readiness_read", "bind_repository", "configure_branch_ref_policy",
            "prepare_workspace", "lock_workspace", "mutate_files", "read_file_content"]).ConfigureAwait(false);
            using (HttpResponseMessage response = await fixture.MutateAsync("/api/v1/provider-bindings/" + provider,
                new
                {
                    requestSchemaVersion = "v1",
                    providerFamilyRef = "forgejo",
                    capabilityProfileRef = "operational",
                    nonSecretCredentialReference = OperationalProviderFixture.CredentialReference
                }, FolderDurabilityAcceptanceFixture.NewId(), method: HttpMethod.Put).ConfigureAwait(false))
                await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content provider configuration").ConfigureAwait(false);
        }
        string key = FolderDurabilityAcceptanceFixture.NewId(), folder = fixture.CreatedFolderId(key);
        using (HttpResponseMessage response = await fixture.MutateAsync("/api/v1/folders",
            new { requestSchemaVersion = "v1", folderMetadata = new { displayName = "Content durability" } }, key).ConfigureAwait(false))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content folder creation").ConfigureAwait(false);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content repository binding").ConfigureAwait(false);
        using (HttpResponseMessage response = await fixture.MutateAsync($"/api/v1/folders/{folder}/repository-bindings",
            new
            {
                requestSchemaVersion = "v1",
                providerBindingRef = provider,
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
            FolderDurabilityAcceptanceFixture.NewId()).ConfigureAwait(false))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content repository binding").ConfigureAwait(false);
        RepositoryBound repository = await WaitForEventAsync<RepositoryBound>(folder).ConfigureAwait(false);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content folder grants").ConfigureAwait(false);
        await fixture.GrantFolderActionsAsync(folder, ["read_file_content", "mutate_files", "read_workspace_lock", "read_workspace_status"]).ConfigureAwait(false);
        using (HttpResponseMessage response = await fixture.MutateAsync($"/api/v1/folders/{folder}/branch-ref-policy",
            new
            {
                requestSchemaVersion = "v1",
                repositoryBindingId = repository.RepositoryBindingId,
                policyRef = "operational-main",
                defaultRef = "branch_ref_main",
                allowedRefPatterns = new[] { "branch_ref_main" },
                protectedRefPatterns = Array.Empty<string>()
            },
            FolderDurabilityAcceptanceFixture.NewId(), method: HttpMethod.Put).ConfigureAwait(false))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content branch policy").ConfigureAwait(false);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content workspace preparation").ConfigureAwait(false);
        const string workspace = "content-workspace";
        string task = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage response = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/preparation",
            new
            {
                requestSchemaVersion = "v1",
                repositoryBindingId = repository.RepositoryBindingId,
                branchRefPolicyRef = "operational-main",
                workspacePolicyRef = "metadata-workspace"
            }, FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(false))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content workspace preparation").ConfigureAwait(false);
        _ = await WaitForEventAsync<WorkspacePreparationRequested>(folder).ConfigureAwait(false);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content workspace lock").ConfigureAwait(false);
        using (HttpResponseMessage response = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock",
            new { requestSchemaVersion = "v1", lockIntent = "exclusive_write", requestedLeaseSeconds = 900 },
            FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(false))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.Accepted, "Content workspace lock").ConfigureAwait(false);
        WorkspaceLockAcquired lease = await WaitForEventAsync<WorkspaceLockAcquired>(folder).ConfigureAwait(false);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "content staging").ConfigureAwait(false);
        return (folder, workspace, task, lease.LockFence!.Value);
    }

    private static FileUploadDescriptor Descriptor(string folder, string workspace, string path, long fence) => new()
    {
        FolderId = folder,
        WorkspaceId = workspace,
        OperationId = FolderDurabilityAcceptanceFixture.NewId(),
        MediaType = "application/octet-stream",
        PathMetadata = new() { NormalizedPath = path, DisplayName = path.Split('/').Last(), PathPolicyClass = PathMetadataPathPolicyClass.Content_allowed },
        ExpectedLockFence = fence,
        ExpectedFileVersion = 0,
    };
    private async Task<HttpResponseMessage> UploadAsync(FileUploadDescriptor descriptor, byte[] bytes, string key, string task, string? correlation = null, bool replica = false)
    {
        using HttpClient client = replica ? fixture.CreateContentReplicaClient() : fixture.CreateFoldersClient();
        using var stream = new MemoryStream(bytes, writable: false);
        return await client.UploadWorkspaceFileAsync(descriptor, stream, key, correlation ?? FolderDurabilityAcceptanceFixture.NewId(), task, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
    private async Task WaitForTerminalCommandAsync(string correlation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            var status = await fixture.ObserveCommandStatusAsync(correlation, deadline.Token).ConfigureAwait(false);
            if (status?.Status == "Completed" || status?.IsRejected == true) return;
            if (status?.Status is "Failed" or "PublishFailed" or "TimedOut")
                throw new InvalidOperationException("Concurrent command did not reach a verifiable domain result.");
            await Task.Delay(300, deadline.Token).ConfigureAwait(false);
        }
    }
    private static object Metadata(string path) => new { normalizedPath = path, displayName = path.Split('/').Last(), pathPolicyClass = "content_allowed", unicodeNormalization = "NFC" };
    private static HttpRequestMessage RangeRequest(string folder, string workspace, string task, string path, long length)
    {
        var request = new HttpMethod("POST");
        var message = new HttpRequestMessage(request, $"/api/v1/folders/{folder}/workspaces/{workspace}/context/range-read")
        { Content = JsonContent.Create(new { requestSchemaVersion = "v1", path = Metadata(path), startOffset = 0, endOffset = length }) };
        message.Headers.Add("X-Correlation-Id", FolderDurabilityAcceptanceFixture.NewId()); message.Headers.Add("X-Hexalith-Task-Id", task);
        return message;
    }
    private async Task RequireRangeAsync(string folder, string workspace, string task, string path, byte[] expected)
    {
        using HttpClient client = fixture.CreateFoldersClient();
        using HttpRequestMessage request = RangeRequest(folder, workspace, task, path, expected.Length);
        using HttpResponseMessage response = await FolderDurabilityHttp.SendAsync(client, request, true, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK, "Exact authoritative range").ConfigureAwait(false);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        Convert.FromBase64String(body.GetProperty("contentBytes").GetString()!).AsSpan().SequenceEqual(expected).ShouldBeTrue("Actual range bytes must be preserved.");
    }
    private async Task RequireDeniedRangeAsync(string folder, string workspace, string task, string path)
    {
        using HttpClient client = fixture.CreateFoldersClient();
        using HttpRequestMessage request = RangeRequest(folder, workspace, task, path, 1);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.NotFound, "Safe content denial").ConfigureAwait(false);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldNotContain(path);
    }
    private async Task RequirePersistedBodyAsync(string folder, string workspace, string receipt, byte[] expected)
    {
        var scope = new ContentScope(fixture.TenantId, "folders", folder, workspace);
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
        string raw = await fixture.QualificationPersistedValueAsync($"authoritative-content:body:{hash}:{receipt}").ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement value = document.RootElement;
        if (!value.TryGetProperty("bytes", out JsonElement content)) content = value.GetProperty("Bytes");
        Convert.FromBase64String(content.GetString()!).AsSpan().SequenceEqual(expected).ShouldBeTrue("Persisted body must match actual uploaded bytes.");
    }
    private async Task<bool> DispositionAsync(string folder, string workspace, string receipt, string operation)
    {
        using HttpResponseMessage response = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/content/disposition",
            new { receiptId = receipt, operation }, FolderDurabilityAcceptanceFixture.NewId()).ConfigureAwait(false);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK, "Content disposition " + operation).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("erased").GetBoolean();
    }
    private async Task RequireOrphanErasureAsync()
    {
        var (folder, workspace, task, fence) = await PrepareAsync(initialize: false).ConfigureAwait(true);
        string conflictKey = FolderDurabilityAcceptanceFixture.NewId();
        await fixture.SeedRejectedContentIntentAsync(folder, workspace, task, fence, conflictKey).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "retained rejected intent").ConfigureAwait(true);
        byte[] orphanBytes = Encoding.UTF8.GetBytes("Verified orphan awaiting authoritative disposition.\n");
        FileUploadDescriptor descriptor = Descriptor(folder, workspace, "docs/orphan.txt", fence);
        using (HttpResponseMessage conflict = await UploadAsync(descriptor, orphanBytes, conflictKey, task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(conflict, HttpStatusCode.Conflict, "Post-upload idempotency conflict").ConfigureAwait(true);
        (await MutationsAsync(folder).ConfigureAwait(true)).ShouldBeEmpty();
        var scope = new ContentScope(fixture.TenantId, "folders", folder, workspace);
        string scopeHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
        using JsonDocument metadata = JsonDocument.Parse(await fixture.QualificationPersistedValueAsync("authoritative-content:metadata:" + scopeHash).ConfigureAwait(true));
        JsonElement ledger = metadata.RootElement;
        JsonElement uploads = ledger.TryGetProperty("uploads", out var value) ? value : ledger.GetProperty("Uploads");
        JsonElement upload = uploads.EnumerateObject().Single(item =>
            (item.Value.TryGetProperty("uploadId", out var id) ? id : item.Value.GetProperty("UploadId")).GetString() == descriptor.OperationId).Value;
        JsonElement receiptValue = upload.TryGetProperty("receipt", out value) ? value : upload.GetProperty("Receipt");
        string receipt = (receiptValue.TryGetProperty("id", out value) ? value : receiptValue.GetProperty("Id")).GetString()!;
        await RequirePersistedBodyAsync(folder, workspace, receipt, orphanBytes).ConfigureAwait(true);
        await RequireDeniedRangeAsync(folder, workspace, task, "docs/orphan.txt").ConfigureAwait(true);
        WorkspaceLockAcquired lease = await WaitForEventAsync<WorkspaceLockAcquired>(folder).ConfigureAwait(true);
        string proof = OwnershipProof(fixture.TenantId, folder, workspace, task, lease.LockId);
        using (HttpResponseMessage released = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock/release",
            new { requestSchemaVersion = "v1", lockId = lease.LockId, lockOwnershipProof = proof, releaseReasonCode = "caller_completed" },
            FolderDurabilityAcceptanceFixture.NewId(), taskId: task).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(released, HttpStatusCode.Accepted, "Terminal orphan task release").ConfigureAwait(true);
        _ = await WaitForEventAsync<WorkspaceLockReleased>(folder).ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "terminal orphan disposition").ConfigureAwait(true);
        await DispositionAsync(folder, workspace, receipt, "hold").ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await fixture.AdvanceContentDispositionClockAsync(8).ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await RequirePersistedBodyAsync(folder, workspace, receipt, orphanBytes).ConfigureAwait(true);
        await DispositionAsync(folder, workspace, receipt, "release").ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await fixture.AdvanceContentDispositionClockAsync(14).ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await RequirePersistedBodyAsync(folder, workspace, receipt, orphanBytes).ConfigureAwait(true);
        string resumedTask = FolderDurabilityAcceptanceFixture.NewId();
        using (HttpResponseMessage resumed = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock",
            new { requestSchemaVersion = "v1", lockIntent = "exclusive_write", requestedLeaseSeconds = 900 },
            FolderDurabilityAcceptanceFixture.NewId(), taskId: resumedTask).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(resumed, HttpStatusCode.Accepted, "Resumed source task").ConfigureAwait(true);
        WorkspaceLockAcquired resumedLease = await WaitForEventAsync<WorkspaceLockAcquired>(folder,
            value => value.HolderTaskId == resumedTask).ConfigureAwait(true);
        resumedLease.LockFence!.Value.ShouldBeGreaterThan(fence);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "resumed task cancels erasure epoch").ConfigureAwait(true);
        using (HttpResponseMessage stale = await UploadAsync(Descriptor(folder, workspace, "docs/stale-lock.txt", fence), orphanBytes,
            FolderDurabilityAcceptanceFixture.NewId(), resumedTask).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(stale, HttpStatusCode.Conflict, "Old lock fence after reacquisition").ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        using (HttpResponseMessage completed = await fixture.MutateAsync($"/api/v1/folders/{folder}/workspaces/{workspace}/lock/release",
            new
            {
                requestSchemaVersion = "v1",
                lockId = resumedLease.LockId,
                lockOwnershipProof = OwnershipProof(fixture.TenantId, folder, workspace, resumedTask, resumedLease.LockId),
                releaseReasonCode = "caller_completed"
            },
            FolderDurabilityAcceptanceFixture.NewId(), taskId: resumedTask).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(completed, HttpStatusCode.Accepted, "Resumed task terminal release").ConfigureAwait(true);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "fresh epoch after resumed task").ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await fixture.AdvanceContentDispositionClockAsync(20).ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeFalse();
        await fixture.AdvanceContentDispositionClockAsync(22).ConfigureAwait(true);
        (await DispositionAsync(folder, workspace, receipt, "apply").ConfigureAwait(true)).ShouldBeTrue();
        using JsonDocument erasedBody = JsonDocument.Parse(await fixture.QualificationPersistedValueAsync($"authoritative-content:body:{scopeHash}:{receipt}").ConfigureAwait(true));
        erasedBody.RootElement.GetProperty("erased").GetBoolean().ShouldBeTrue();
        Convert.FromBase64String(erasedBody.RootElement.GetProperty("bytes").GetString()!).ShouldBeEmpty();
        _ = await WaitForEventAsync<WorkspaceContentErasureAuthorized>(folder).ConfigureAwait(true);
        output.WriteLine("PASS physical-erasure: verified orphan; terminal source tasks; hold/release and resumed work reset P7D; stale reacquired lock denied; actor grant; real Redis bytes erased and create fence retained");
    }
    private async Task RequireAtomicBatchAsync()
    {
        var (folder, workspace, task, fence) = await PrepareAsync(initialize: false).ConfigureAwait(true);
        var descriptors = new[] { Descriptor(folder, workspace, "docs/batch-a.txt", fence), Descriptor(folder, workspace, "docs/batch-b.txt", fence) };
        byte[][] bodies = [Encoding.UTF8.GetBytes("Atomic first body\n"), Encoding.UTF8.GetBytes("Atomic second body\n")];
        var receipts = new string[2];
        for (int index = 0; index < 2; index++)
        {
            string key = FolderDurabilityAcceptanceFixture.NewId();
            await fixture.SeedRejectedContentIntentAsync(folder, workspace, task, fence, key).ConfigureAwait(true);
            await fixture.WaitForCurrentFolderEvidenceAsync(folder, "batch upload retained intent").ConfigureAwait(true);
            using HttpResponseMessage conflict = await UploadAsync(descriptors[index], bodies[index], key, task).ConfigureAwait(true);
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(conflict, HttpStatusCode.Conflict, "Verified batch upload remains unbound").ConfigureAwait(true);
            receipts[index] = await ReceiptForUploadAsync(folder, workspace, descriptors[index].OperationId).ConfigureAwait(true);
            await RequirePersistedBodyAsync(folder, workspace, receipts[index], bodies[index]).ConfigureAwait(true);
            await RequireDeniedRangeAsync(folder, workspace, task, descriptors[index].PathMetadata.NormalizedPath).ConfigureAwait(true);
        }
        object Item(int index, bool change = false, long expectedVersion = 0, string? owner = null) => new
        {
            operationId = change ? FolderDurabilityAcceptanceFixture.NewId() : descriptors[index].OperationId,
            fileOperationKind = change ? "change" : "add",
            transportOperation = "PutFileInline",
            pathMetadata = Metadata(descriptors[index].PathMetadata.NormalizedPath),
            contentHashReference = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(bodies[index])),
            byteLength = bodies[index].Length,
            mediaType = "application/octet-stream",
            transportEvidenceKind = "inline_decoded",
            observedByteLength = bodies[index].Length,
            contentReceiptId = receipts[index],
            expectedFileVersion = expectedVersion,
            expectedLockFence = fence,
            ownerFileId = owner,
        };
        using (HttpResponseMessage accepted = await fixture.SubmitContentBatchAsync(folder, workspace, task, [Item(0), Item(1)]).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(accepted, HttpStatusCode.Accepted, "Atomic multi-item metadata admission").ConfigureAwait(true);
        var first = await WaitForMutationAsync(folder, descriptors[0].OperationId).ConfigureAwait(true);
        var second = await WaitForMutationAsync(folder, descriptors[1].OperationId).ConfigureAwait(true);
        first.ContentBinding!.CommandId.ShouldBe(second.ContentBinding!.CommandId);
        (await MutationsAsync(folder).ConfigureAwait(true)).Length.ShouldBe(2);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "atomic batch publication").ConfigureAwait(true);
        for (int index = 0; index < 2; index++)
            await RequireRangeAsync(folder, workspace, task, descriptors[index].PathMetadata.NormalizedPath, bodies[index]).ConfigureAwait(true);
        using (HttpResponseMessage rejected = await fixture.SubmitContentBatchAsync(folder, workspace, task,
            [Item(0, true, 1, first.ContentBinding.FileId), Item(1, true, 99, second.ContentBinding.FileId)]).ConfigureAwait(true))
            await FolderDurabilityAcceptanceFixture.RequireStatusAsync(rejected, HttpStatusCode.UnprocessableEntity, "Invalid second item rejects the complete batch").ConfigureAwait(true);
        (await MutationsAsync(folder).ConfigureAwait(true)).Length.ShouldBe(2);
        await fixture.WaitForCurrentFolderEvidenceAsync(folder, "rejected batch remains unpublished").ConfigureAwait(true);
        for (int index = 0; index < 2; index++)
            await RequireRangeAsync(folder, workspace, task, descriptors[index].PathMetadata.NormalizedPath, bodies[index]).ConfigureAwait(true);
        output.WriteLine("PASS atomic-batch: two pending receipts invisible; one real actor decision publishes both; invalid later item publishes neither change");
    }
    private async Task<string> ReceiptForUploadAsync(string folder, string workspace, string operation)
    {
        var scope = new ContentScope(fixture.TenantId, "folders", folder, workspace);
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
        using JsonDocument metadata = JsonDocument.Parse(await fixture.QualificationPersistedValueAsync("authoritative-content:metadata:" + hash).ConfigureAwait(false));
        var ledger = metadata.RootElement;
        var uploads = ledger.TryGetProperty("uploads", out var value) ? value : ledger.GetProperty("Uploads");
        var upload = uploads.EnumerateObject().Single(item =>
            (item.Value.TryGetProperty("uploadId", out var id) ? id : item.Value.GetProperty("UploadId")).GetString() == operation).Value;
        var receipt = upload.TryGetProperty("receipt", out value) ? value : upload.GetProperty("Receipt");
        return (receipt.TryGetProperty("id", out value) ? value : receipt.GetProperty("Id")).GetString()!;
    }
    private async Task<WorkspaceFileMutationAccepted[]> MutationsAsync(string folder)
        => (await fixture.ReadStreamAsync(folder).ConfigureAwait(false)).Events.Where(item => item.EventTypeName == typeof(WorkspaceFileMutationAccepted).FullName)
            .Select(item => JsonSerializer.Deserialize<WorkspaceFileMutationAccepted>(item.Payload, Json)!).ToArray();
    private static string OwnershipProof(params string[] fields)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string field in new[] { "workspace-lock-ownership-proof-v1" }.Concat(fields))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        return "lock_proof_" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32];
    }
    private async Task<WorkspaceFileMutationAccepted> WaitForMutationAsync(string folder, string operation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            WorkspaceFileMutationAccepted? found = (await MutationsAsync(folder).ConfigureAwait(false)).SingleOrDefault(item => item.OperationId == operation);
            if (found is not null) return found;
            await Task.Delay(500, deadline.Token).ConfigureAwait(false);
        }
    }
    private async Task<T> WaitForEventAsync<T>(string folder, Func<T, bool>? predicate = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            foreach (StreamReadEvent item in (await fixture.ReadStreamAsync(folder).ConfigureAwait(false)).Events
                .Where(value => value.EventTypeName == typeof(T).FullName).Reverse())
            {
                T value = JsonSerializer.Deserialize<T>(item.Payload, Json)!;
                if (predicate is null || predicate(value)) return value;
            }
            await Task.Delay(500, deadline.Token).ConfigureAwait(false);
        }
    }
}
