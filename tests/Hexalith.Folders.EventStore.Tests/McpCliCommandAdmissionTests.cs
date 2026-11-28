using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Contracts.Commands;
using Hexalith.Folders.Contracts.Commands.Models;
using Hexalith.Folders.Server;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>
/// Proves Gateway admission, canonical v1/v2 intent parity, caller idempotency-key independence from
/// generated uppercase ULID message IDs, and real replay/conflict/expiry behavior for all 13 decorated
/// McpCli Folders commands (C01–C13).
/// </summary>
public sealed class McpCliCommandAdmissionTests
{
    private const string TenantId = "tenant-a";
    private const string OpaqueFolderId = "Opaque_Folder_01ARZ3NDEKTS";
    private const string OpaqueParentFolderId = "Opaque_Parent_01ARZ3NDEKTS";
    private const string OpaqueWorkspaceId = "Opaque_WorkSp_01ARZ3NDEKTS";
    private const string OpaqueTaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string OpaqueCorrelationId = "01ARZ3NDEKTSV4RRFFQ69G5FBW";
    private const string OpaqueIdempotencyKey = "01ARZ3NDEKTSV4RRFFQ69G5FCX";
    private const string MessageIdFirst = "01ARZ3NDEKTSV4RRFFQ69G5FDY";
    private const string MessageIdSecond = "01ARZ3NDEKTSV4RRFFQ69G5FEZ";
    private const string MessageIdThird = "01ARZ3NDEKTSV4RRFFQ69G5FF0";
    private const string OpaqueUserId = "User_Opaque_01ARZ3NDEKTSV4";
    private const string ProviderBindingRef = "Opaque_ProvBind_01ARZ3NDEK";
    private const string ProviderFamilyRef = "Opaque_ProvFam_01ARZ3NDEKT";
    private const string CapabilityProfileRef = "Opaque_CapProf_01ARZ3NDEKT";
    private const string CredentialRef = "Opaque_CredRef_01ARZ3NDEKT";
    private const string RepositoryBindingId = "Opaque_RepoBind_01ARZ3NDEK";
    private const string RepositoryProfileRef = "Opaque_RepoProf_01ARZ3NDEK";
    private const string ExternalRepoRef = "Opaque_ExtRepo_01ARZ3NDEKT";
    private const string BranchPolicyRef = "Opaque_BrPolicy_01ARZ3NDEK";
    private const string WorkspacePolicyRef = "Opaque_WsPolicy_01ARZ3NDEK";
    private const string LockId = "Opaque_LockId_01ARZ3NDEKTS";
    private const string LockProof = "Opaque_LockPrf_01ARZ3NDEKT";
    private const string OperationId = "Opaque_OperId_01ARZ3NDEKTS";
    private const string OperationIdSecond = "Opaque_Oper2d_01ARZ3NDEKTS";
    private const string InlineContentSecret = "RAW_FILE_BODY_NEVER_IN_INTENT_DESCRIPTOR_9f8e7d6c";

    private static readonly JsonSerializerOptions DecoratedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly PropertyInfo ActorStateManagerProperty =
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Dapr Actor.StateManager property was not found.");

    public static TheoryData<string> AllThirteenCommandIds() =>
    [
        "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10", "C11", "C12", "C13",
    ];

    [Theory]
    [MemberData(nameof(AllThirteenCommandIds))]
    public async Task EveryDecoratedV2CommandProducesIdenticalCanonicalIntentToEquivalentV1AndPreservesOpaqueIds(string commandId)
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);

        (string commandType, string aggregateId, byte[] v1Payload, byte[] v2Payload) = BuildV1AndV2Pair(commandId);
        CancellationToken token = TestContext.Current.CancellationToken;

        SubmitCommand v1Command = new(
            MessageIdFirst,
            TenantId,
            FoldersServerModule.DomainName,
            aggregateId,
            commandType,
            v1Payload,
            OpaqueCorrelationId,
            OpaqueUserId,
            new Dictionary<string, string> { ["taskId"] = OpaqueTaskId },
            IdempotencyKey: OpaqueIdempotencyKey);

        SubmitCommand v2Command = v1Command with
        {
            MessageId = MessageIdSecond,
            Payload = v2Payload,
        };

        TrustedIdempotencyDescriptor v1Descriptor = await fixture.Registry.ResolveAsync(v1Command, token).ConfigureAwait(true);
        TrustedIdempotencyDescriptor v2Descriptor = await fixture.Registry.ResolveAsync(v2Command, token).ConfigureAwait(true);

        v2Descriptor.CanonicalIntent.ShouldBe(v1Descriptor.CanonicalIntent);
        v2Descriptor.OperationId.ShouldBe(v1Descriptor.OperationId);
        v2Descriptor.RetentionTier.ShouldBe(v1Descriptor.RetentionTier);
        Encoding.UTF8.GetString(v2Descriptor.CanonicalIntent).ShouldContain(aggregateId);
        Encoding.UTF8.GetString(v2Descriptor.CanonicalIntent).ShouldNotContain(OpaqueIdempotencyKey);
        Encoding.UTF8.GetString(v2Descriptor.CanonicalIntent).ShouldNotContain(MessageIdSecond);
        Encoding.UTF8.GetString(v2Descriptor.CanonicalIntent).ShouldNotContain(InlineContentSecret);
        Encoding.UTF8.GetString(v2Descriptor.CanonicalIntent).ShouldNotContain(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(InlineContentSecret)));
    }

    [Fact]
    public async Task RegisteredAdaptersRejectMalformedDecoratedRetriesBeforeResolvingIntent()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);
        CancellationToken token = TestContext.Current.CancellationToken;

        foreach (string id in new[] { "C01", "C03", "C10" })
        {
            (string commandType, string target, _, byte[] payload) = BuildV1AndV2Pair(id);
            var command = new SubmitCommand(MessageIdFirst, TenantId, FoldersServerModule.DomainName, target,
                commandType, payload, OpaqueCorrelationId, OpaqueUserId,
                new Dictionary<string, string> { ["taskId"] = OpaqueTaskId }, IdempotencyKey: OpaqueIdempotencyKey);
            _ = await fixture.Registry.ResolveAsync(command, token).ConfigureAwait(true);
            await Should.ThrowAsync<ArgumentException>(() => fixture.Registry.ResolveAsync(
                command with { MessageId = MessageIdSecond, Extensions = null }, token)).ConfigureAwait(true);
            await Should.ThrowAsync<ArgumentException>(() => fixture.Registry.ResolveAsync(command with
            {
                MessageId = MessageIdSecond,
                Extensions = new Dictionary<string, string> { ["taskId"] = "ghp_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789" },
            }, token)).ConfigureAwait(true);

            JsonObject malformed = JsonNode.Parse(payload)!.AsObject();
            malformed["unknownProperty"] = true;
            await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);
            malformed.Remove("unknownProperty");
            if (id == "C01")
            {
                malformed["archiveReasonCode"] = 0;
                await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);
            }

            if (id == "C03")
            {
                JsonArray keys = malformed["auditMetadataKeys"]!.AsArray();
                keys.Add(keys[0]!.DeepClone());
                await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);
            }

            if (id == "C10")
            {
                JsonObject item = malformed["changes"]![0]!.AsObject();
                JsonNode inline = item["inlineContent"]!;
                inline["contentMediaType"] = null;
                await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);
                inline.AsObject().Remove("contentMediaType");
                byte[] wrongBytes = new byte[item["byteLength"]!.GetValue<int>()];
                inline["contentBytes"] = Convert.ToBase64String(wrongBytes);
                await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);

                item.Remove("inlineContent");
                item["transportOperation"] = "PutFileStream";
                item["byteLength"] = 262_145;
                JsonObject stream = new()
                {
                    ["mediaType"] = "text/plain",
                    ["declaredLength"] = 262_145,
                    ["observedLength"] = 262_145,
                    ["stagingReference"] = "content_" + new string('a', 64),
                    ["observedContentHashReference"] = item["contentHashReference"]!.DeepClone(),
                    ["uploadMode"] = "request_body_stream",
                };
                item["streamDescriptor"] = stream;
                _ = await ResolveChangedAsync(malformed).ConfigureAwait(true);
                stream["observedLength"] = 262_146;
                await Should.ThrowAsync<ArgumentException>(() => ResolveChangedAsync(malformed)).ConfigureAwait(true);
            }

            Task<TrustedIdempotencyDescriptor> ResolveChangedAsync(JsonObject changed)
                => fixture.Registry.ResolveAsync(command with
                {
                    MessageId = MessageIdSecond,
                    Payload = Encoding.UTF8.GetBytes(changed.ToJsonString()),
                }, token);
        }
    }

    [Fact]
    public async Task ReplayConflictAndExpiryUseCallerIdempotencyKeyIndependentlyOfGeneratedMessageId()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);

        CancellationToken token = TestContext.Current.CancellationToken;
        (_, string aggregateId, _, byte[] archivePayload) = BuildV1AndV2Pair("C01");
        byte[] mutatedArchivePayload = SerializeDecorated(new ArchiveFolder(
            ArchiveFolderArchiveReasonCode.PolicyRetention,
            OpaqueFolderId,
            OpaqueIdempotencyKey));

        SubmitCommand firstCommand = new(
            MessageIdFirst,
            TenantId,
            FoldersServerModule.DomainName,
            aggregateId,
            FoldersServerModule.ArchiveFolderCommandType,
            archivePayload,
            OpaqueCorrelationId,
            OpaqueUserId,
            new Dictionary<string, string> { ["taskId"] = OpaqueTaskId },
            IdempotencyKey: OpaqueIdempotencyKey);

        TrustedIdempotencyDescriptor firstDescriptor = await fixture.Registry.ResolveAsync(firstCommand, token).ConfigureAwait(true);
        TrustedIdempotencyDescriptor replayDescriptor = await fixture.Registry.ResolveAsync(
            firstCommand with { MessageId = MessageIdSecond }, token).ConfigureAwait(true);
        TrustedIdempotencyDescriptor conflictDescriptor = await fixture.Registry.ResolveAsync(
            firstCommand with { MessageId = MessageIdThird, Payload = mutatedArchivePayload }, token).ConfigureAwait(true);

        IdempotencyKeyProtector protector = CreateProtector();
        IdempotencyProtectedIdentity firstIdentity = (await protector.ProtectAsync(TenantId, OpaqueIdempotencyKey, firstDescriptor, token).ConfigureAwait(true)).Active;
        IdempotencyProtectedIdentity replayIdentity = (await protector.ProtectAsync(TenantId, OpaqueIdempotencyKey, replayDescriptor, token).ConfigureAwait(true)).Active;
        IdempotencyProtectedIdentity conflictIdentity = (await protector.ProtectAsync(TenantId, OpaqueIdempotencyKey, conflictDescriptor, token).ConfigureAwait(true)).Active;

        replayIdentity.ActorId.ShouldBe(firstIdentity.ActorId);
        replayIdentity.IntentDigest.ShouldBe(firstIdentity.IntentDigest);
        conflictIdentity.ActorId.ShouldBe(firstIdentity.ActorId);
        conflictIdentity.IntentDigest.ShouldNotBe(firstIdentity.IntentDigest);

        MutableClock clock = new(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        IdempotencyAdmissionActor actor = CreateAdmissionActor(firstIdentity.ActorId, clock);

        IdempotencyAdmissionResult initial = await actor.AdmitAsync(CreateAdmissionRequest(firstIdentity, MessageIdFirst, OpaqueCorrelationId)).ConfigureAwait(true);
        initial.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        initial.ExecutionMessageId.ShouldBe(MessageIdFirst);

        await actor.BeginAsync(new IdempotencyAdmissionTransitionRequest(initial.FencingToken)).ConfigureAwait(true);
        CommandProcessingResult completedResult = new(
            Accepted: true,
            ErrorMessage: null,
            CorrelationId: OpaqueCorrelationId,
            EventCount: 1,
            ResultPayload: "{\"status\":\"accepted\",\"idempotentReplay\":false}");
        await actor.CompleteAsync(new IdempotencyAdmissionCompletionRequest(initial.FencingToken, completedResult)).ConfigureAwait(true);

        // Equivalent replay with a brand-new uppercase ULID MessageId returns original result without re-executing.
        IdempotencyAdmissionResult replay = await actor.AdmitAsync(CreateAdmissionRequest(replayIdentity, MessageIdSecond, "01ARZ3NDEKTSV4RRFFQ69G5FG1")).ConfigureAwait(true);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ExecutionMessageId.ShouldBe(MessageIdFirst);
        replay.ReplayResult.ShouldNotBeNull();
        replay.ReplayResult.ResultPayload.ShouldBe(completedResult.ResultPayload);

        // Mutated intent with the same caller IdempotencyKey conflicts.
        IdempotencyAdmissionResult conflict = await actor.AdmitAsync(CreateAdmissionRequest(conflictIdentity, MessageIdThird, OpaqueCorrelationId)).ConfigureAwait(true);
        conflict.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);

        // After 24h TTL for Mutation tier, both equivalent and mutated reuse expire and never re-execute.
        clock.Advance(TimeSpan.FromHours(25));
        IdempotencyAdmissionResult expiredEquivalent = await actor.AdmitAsync(CreateAdmissionRequest(replayIdentity, "01ARZ3NDEKTSV4RRFFQ69G5FH2", OpaqueCorrelationId)).ConfigureAwait(true);
        IdempotencyAdmissionResult expiredMutated = await actor.AdmitAsync(CreateAdmissionRequest(conflictIdentity, "01ARZ3NDEKTSV4RRFFQ69G5FJ3", OpaqueCorrelationId)).ConfigureAwait(true);
        expiredEquivalent.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
        expiredMutated.Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
    }

    [Fact]
    public async Task CommitWorkspaceUsesSevenYearRetentionAndExpiresAfterTtl()
    {
        using FoldersDeclaredAdmissionFixture fixture = await FoldersDeclaredAdmissionFixture.CreateAsync().ConfigureAwait(true);
        await fixture.ActivateAsync().ConfigureAwait(true);

        CancellationToken token = TestContext.Current.CancellationToken;
        (_, string aggregateId, _, byte[] commitPayload) = BuildV1AndV2Pair("C03");
        SubmitCommand command = new(
            MessageIdFirst,
            TenantId,
            FoldersServerModule.DomainName,
            aggregateId,
            FoldersServerModule.CommitWorkspaceCommandType,
            commitPayload,
            OpaqueCorrelationId,
            OpaqueUserId,
            new Dictionary<string, string> { ["taskId"] = OpaqueTaskId },
            IdempotencyKey: OpaqueIdempotencyKey);

        TrustedIdempotencyDescriptor descriptor = await fixture.Registry.ResolveAsync(command, token).ConfigureAwait(true);
        descriptor.RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Commit);

        IdempotencyKeyProtector protector = CreateProtector();
        IdempotencyProtectedIdentity identity = (await protector.ProtectAsync(TenantId, OpaqueIdempotencyKey, descriptor, token).ConfigureAwait(true)).Active;

        MutableClock clock = new(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        IdempotencyAdmissionActor actor = CreateAdmissionActor(identity.ActorId, clock);
        IdempotencyAdmissionResult initial = await actor.AdmitAsync(CreateAdmissionRequest(identity, MessageIdFirst, OpaqueCorrelationId)).ConfigureAwait(true);
        await actor.BeginAsync(new IdempotencyAdmissionTransitionRequest(initial.FencingToken)).ConfigureAwait(true);
        await actor.CompleteAsync(new IdempotencyAdmissionCompletionRequest(
            initial.FencingToken,
            new CommandProcessingResult(true, null, OpaqueCorrelationId, 1, "{\"status\":\"accepted\"}"))).ConfigureAwait(true);

        clock.Advance(TimeSpan.FromDays(30));
        (await actor.AdmitAsync(CreateAdmissionRequest(identity, MessageIdSecond, OpaqueCorrelationId)).ConfigureAwait(true))
            .Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);

        clock.Advance(TimeSpan.FromDays(365 * 7 + 10));
        (await actor.AdmitAsync(CreateAdmissionRequest(identity, MessageIdThird, OpaqueCorrelationId)).ConfigureAwait(true))
            .Decision.ShouldBe(IdempotencyAdmissionDecision.Expired);
    }

    [Fact]
    public void ConfigureBranchRefPolicyFlatDecoratedV2DistinguishesFieldsAndSortsPatternSets()
    {
        ConfigureBranchRefPolicyIdempotencyIntentAdapter adapter = new();
        byte[] first = SerializeDecorated(new ConfigureBranchRefPolicy(
            RepositoryBindingId,
            BranchPolicyRef,
            "branch_ref_main",
            ["branch_ref_main", "branch_ref_release"],
            OpaqueFolderId,
            OpaqueIdempotencyKey,
            ["branch_ref_locked_a", "branch_ref_locked_b"]));
        byte[] reordered = SerializeDecorated(new ConfigureBranchRefPolicy(
            RepositoryBindingId,
            BranchPolicyRef,
            "branch_ref_main",
            ["branch_ref_release", "branch_ref_main"],
            OpaqueFolderId,
            "01ARZ3NDEKTSV4RRFFQ69G5FZZ",
            ["branch_ref_locked_b", "branch_ref_locked_a"]));
        byte[] changedPolicy = SerializeDecorated(new ConfigureBranchRefPolicy(
            RepositoryBindingId,
            "Opaque_BrPolicy_Changed001",
            "branch_ref_main",
            ["branch_ref_main", "branch_ref_release"],
            OpaqueFolderId,
            OpaqueIdempotencyKey,
            ["branch_ref_locked_a", "branch_ref_locked_b"]));

        IdempotencyCanonicalIntent firstIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.ConfigureBranchRefPolicyCommandType, OpaqueFolderId, first));
        IdempotencyCanonicalIntent reorderedIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.ConfigureBranchRefPolicyCommandType, OpaqueFolderId, reordered));
        IdempotencyCanonicalIntent changedIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.ConfigureBranchRefPolicyCommandType, OpaqueFolderId, changedPolicy));

        firstIntent.SemanticPayload.ShouldBe(reorderedIntent.SemanticPayload);
        firstIntent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(reorderedIntent.SemanticOptions!["semantic_payload_sha256"]);
        changedIntent.SemanticPayload.ShouldNotBe(firstIntent.SemanticPayload);
        changedIntent.SemanticOptions!["semantic_payload_sha256"].ShouldNotBe(firstIntent.SemanticOptions["semantic_payload_sha256"]);
    }

    [Fact]
    public void PutFileStreamProducesV1V2CanonicalParityAndMatchesInlineTransportRetry()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        const int streamSize = 262_145;
        string hashRef = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("streamed-body-sample")));

        byte[] v1Stream = SerializeV1(new
        {
            requestSchemaVersion = "v1",
            workspaceId = OpaqueWorkspaceId,
            operationId = "Opaque_FileOpStream_01ARZ3",
            fileOperationKind = "add",
            transportOperation = "PutFileStream",
            pathMetadata = new
            {
                normalizedPath = "docs/large.bin",
                displayName = "large.bin",
                pathPolicyClass = "content_allowed",
                unicodeNormalization = "NFC",
            },
            contentHashReference = hashRef,
            byteLength = streamSize,
            mediaType = "application/octet-stream",
            taskId = OpaqueTaskId,
            expectedFileVersion = 0,
            expectedLockFence = 1,
        });

        byte[] v2Stream = SerializeDecorated(new MutateFiles(
            [
                new FileMutationRequest(
                    ExpectedFileVersion: 0,
                    ExpectedLockFence: 1,
                    FileOperationKind: FileMutationRequestFileOperationKind.Add,
                    TransportOperation: FileMutationRequestTransportOperation.PutFileStream,
                    OperationId: "Opaque_FileOpStream_01ARZ3",
                    PathMetadata: new PathMetadata("docs/large.bin", "large.bin", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                    ContentHashReference: hashRef,
                    ByteLength: streamSize,
                    StreamDescriptor: new PutFileStream(
                        "application/octet-stream",
                        streamSize,
                        streamSize,
                        "Opaque_StagingRef_01ARZ3ND",
                        hashRef,
                        PutFileStreamUploadMode.RequestBodyStream)),
            ],
            OpaqueFolderId,
            OpaqueWorkspaceId,
            OpaqueIdempotencyKey,
            OpaqueTaskId));

        byte[] v2StreamRetryDifferentStaging = SerializeDecorated(new MutateFiles(
            [
                new FileMutationRequest(
                    ExpectedFileVersion: 0,
                    ExpectedLockFence: 1,
                    FileOperationKind: FileMutationRequestFileOperationKind.Add,
                    TransportOperation: FileMutationRequestTransportOperation.PutFileStream,
                    OperationId: "Opaque_FileOpStream_01ARZ3",
                    PathMetadata: new PathMetadata("docs/large.bin", "large.bin", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                    ContentHashReference: hashRef,
                    ByteLength: streamSize,
                    StreamDescriptor: new PutFileStream(
                        "application/octet-stream",
                        streamSize,
                        streamSize,
                        "Opaque_StagingRef_DIFFERENT",
                        hashRef,
                        PutFileStreamUploadMode.RequestBodyStream)),
            ],
            OpaqueFolderId,
            OpaqueWorkspaceId,
            "01ARZ3NDEKTSV4RRFFQ69G5ALT",
            OpaqueTaskId));

        IdempotencyCanonicalIntent v1Intent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, v1Stream));
        IdempotencyCanonicalIntent v2Intent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, v2Stream));
        IdempotencyCanonicalIntent retryIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, v2StreamRetryDifferentStaging));

        v2Intent.SemanticPayload.ShouldBe(v1Intent.SemanticPayload);
        v2Intent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(v1Intent.SemanticOptions!["semantic_payload_sha256"]);
        retryIntent.SemanticPayload.ShouldBe(v2Intent.SemanticPayload);
        retryIntent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(v2Intent.SemanticOptions["semantic_payload_sha256"]);
    }

    [Fact]
    public void DecoratedV2AdaptersRejectMissingOrMismatchedFolderIdAndHybridOrSmuggledPayloads()
    {
        const string wrongFolderId = "Opaque_WrongFl_01ARZ3NDEKT";

        // 1. BuildNormalizedV2Guard (stripFolderId: true) rejects omitted or mismatched folderId
        ArchiveFolderIdempotencyIntentAdapter archiveAdapter = new();
        byte[] archiveMissingFolderId = SerializeV1(new { archiveReasonCode = "caller_requested", idempotencyKey = OpaqueIdempotencyKey });
        byte[] archiveWrongFolderId = SerializeV1(new { archiveReasonCode = "caller_requested", folderId = wrongFolderId, idempotencyKey = OpaqueIdempotencyKey });
        _ = Should.Throw<JsonException>(() => archiveAdapter.CreateIntent(IntentCommand(FoldersServerModule.ArchiveFolderCommandType, OpaqueFolderId, archiveMissingFolderId)));
        _ = Should.Throw<JsonException>(() => archiveAdapter.CreateIntent(IntentCommand(FoldersServerModule.ArchiveFolderCommandType, OpaqueFolderId, archiveWrongFolderId)));

        BindRepositoryIdempotencyIntentAdapter bindAdapter = new();
        byte[] bindMissingFolderId = SerializeV1(new
        {
            providerBindingRef = ProviderBindingRef,
            externalRepositoryRef = "Opaque_ExtRepo_01ARZ3NDEKT",
            branchRefPolicy = new { repositoryBindingId = RepositoryBindingId, policyRef = BranchPolicyRef, defaultRef = "branch_ref_main", allowedRefPatterns = new[] { "branch_ref_main" } },
            idempotencyKey = OpaqueIdempotencyKey,
        });
        _ = Should.Throw<JsonException>(() => bindAdapter.CreateIntent(IntentCommand(FoldersServerModule.BindRepositoryCommandType, OpaqueFolderId, bindMissingFolderId)));

        // 2. FolderAccessIdempotencyIntent rejects omitted/mismatched folderId and hybrid requestSchemaVersion
        GrantFolderAccessIdempotencyIntentAdapter grantAdapter = new();
        RevokeFolderAccessIdempotencyIntentAdapter revokeAdapter = new();
        string aclEntryId = DeriveAclEntryId("user", OpaqueUserId, "write");
        byte[] accessMissingFolderId = SerializeV1(new { subjectRef = OpaqueUserId, permissionLevel = "write", aclEntryId, idempotencyKey = OpaqueIdempotencyKey });
        byte[] accessWrongFolderId = SerializeV1(new { subjectRef = OpaqueUserId, permissionLevel = "write", folderId = wrongFolderId, aclEntryId, idempotencyKey = OpaqueIdempotencyKey });
        byte[] accessHybridSchemaVersion = SerializeV1(new { requestSchemaVersion = "v1", subjectRef = OpaqueUserId, permissionLevel = "write", folderId = OpaqueFolderId, aclEntryId, idempotencyKey = OpaqueIdempotencyKey });
        _ = Should.Throw<JsonException>(() => grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, accessMissingFolderId)));
        _ = Should.Throw<JsonException>(() => grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, accessWrongFolderId)));
        _ = Should.Throw<JsonException>(() => grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, accessHybridSchemaVersion)));
        _ = Should.Throw<JsonException>(() => revokeAdapter.CreateIntent(IntentCommand(FoldersServerModule.RevokeFolderAccessCommandType, OpaqueFolderId, accessHybridSchemaVersion)));

        // 3. MutateFilesIdempotencyIntentAdapter rejects missing/mismatched folderId and hybrid changes+requestSchemaVersion/items
        MutateFilesIdempotencyIntentAdapter mutateAdapter = new();
        object validChange = new
        {
            expectedFileVersion = 0,
            expectedLockFence = 1,
            fileOperationKind = "remove",
            transportOperation = "metadataOnlyRemoval",
            operationId = "Opaque_FileOp1_01ARZ3NDEKT",
            pathMetadata = new { normalizedPath = "docs/a.md", displayName = "a.md", pathPolicyClass = "content_allowed", unicodeNormalization = "NFC" },
        };
        byte[] mutateMissingFolderId = SerializeV1(new { changes = new[] { validChange }, workspaceId = OpaqueWorkspaceId, idempotencyKey = OpaqueIdempotencyKey, taskId = OpaqueTaskId });
        byte[] mutateWrongFolderId = SerializeV1(new { changes = new[] { validChange }, folderId = wrongFolderId, workspaceId = OpaqueWorkspaceId, idempotencyKey = OpaqueIdempotencyKey, taskId = OpaqueTaskId });
        byte[] mutateHybridSchema = SerializeV1(new { requestSchemaVersion = "v1", changes = new[] { validChange }, folderId = OpaqueFolderId, workspaceId = OpaqueWorkspaceId, idempotencyKey = OpaqueIdempotencyKey, taskId = OpaqueTaskId });
        byte[] mutateHybridItems = SerializeV1(new { changes = new[] { validChange }, items = new[] { validChange }, folderId = OpaqueFolderId, workspaceId = OpaqueWorkspaceId, idempotencyKey = OpaqueIdempotencyKey, taskId = OpaqueTaskId });
        _ = Should.Throw<JsonException>(() => mutateAdapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, mutateMissingFolderId)));
        _ = Should.Throw<JsonException>(() => mutateAdapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, mutateWrongFolderId)));
        _ = Should.Throw<JsonException>(() => mutateAdapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, mutateHybridSchema)));
        _ = Should.Throw<JsonException>(() => mutateAdapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, mutateHybridItems)));

        // 4. ConfigureBranchRefPolicy rejects nested branchRefPolicy without requestSchemaVersion
        ConfigureBranchRefPolicyIdempotencyIntentAdapter branchAdapter = new();
        byte[] hybridBranchPolicy = SerializeV1(new
        {
            repositoryBindingId = RepositoryBindingId,
            folderId = OpaqueFolderId,
            branchRefPolicy = new { policyRef = BranchPolicyRef, defaultRef = "branch_ref_main", allowedRefPatterns = new[] { "branch_ref_main" } },
            idempotencyKey = OpaqueIdempotencyKey,
        });
        _ = Should.Throw<JsonException>(() => branchAdapter.CreateIntent(IntentCommand(FoldersServerModule.ConfigureBranchRefPolicyCommandType, OpaqueFolderId, hybridBranchPolicy)));

        // 5. CreateRepositoryBackedFolder (C07) rejects smuggled root repositoryBindingId
        CreateRepositoryBackedFolderIdempotencyIntentAdapter repoFolderAdapter = new();
        byte[] smuggledRootBinding = SerializeV1(new
        {
            folderId = OpaqueFolderId,
            repositoryBindingId = "Opaque_Smuggled_01ARZ3NDEK",
            providerBindingRef = ProviderBindingRef,
            repositoryProfileRef = "Opaque_RepoProf_01ARZ3NDEK",
            folderMetadata = new { displayName = "Engineering", metadataClass = "tenant_sensitive" },
            branchRefPolicy = new { repositoryBindingId = RepositoryBindingId, policyRef = BranchPolicyRef, defaultRef = "branch_ref_main", allowedRefPatterns = new[] { "branch_ref_main" } },
            idempotencyKey = OpaqueIdempotencyKey,
        });
        _ = Should.Throw<JsonException>(() => repoFolderAdapter.CreateIntent(IntentCommand(FoldersServerModule.CreateRepositoryBackedFolderCommandType, OpaqueFolderId, smuggledRootBinding)));
    }

    [Fact]
    public void ConfigureProviderBindingRejectsUnreservedOrTenantAggregateId()
    {
        ConfigureProviderBindingIdempotencyIntentAdapter adapter = new();
        byte[] payload = SerializeDecorated(new Hexalith.Folders.Contracts.Commands.ConfigureProviderBinding(
            ProviderFamilyRef,
            CapabilityProfileRef,
            CredentialRef,
            ProviderBindingRef,
            OpaqueIdempotencyKey));

        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(IntentCommand(
            FoldersServerModule.ConfigureProviderBindingCommandType,
            ProviderBindingRef,
            payload)));
        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(IntentCommand(
            FoldersServerModule.ConfigureProviderBindingCommandType,
            TenantId,
            payload)));
        _ = Should.Throw<JsonException>(() => adapter.CreateIntent(IntentCommand(
            FoldersServerModule.ConfigureProviderBindingCommandType,
            ProviderBindingActorId.Create("Opaque_OtherBind_01ARZ3NDE"),
            payload)));
    }

    [Fact]
    public void FolderAccessDecoratedV2SupportsPrefixedAndBareOpaqueSubjectsAndRejectsEffectOrMismatchedAclEntryId()
    {
        GrantFolderAccessIdempotencyIntentAdapter grantAdapter = new();
        RevokeFolderAccessIdempotencyIntentAdapter revokeAdapter = new();

        const string bareSubject = "User_Opaque_01ARZ3NDEKTSV4";
        string aclEntryId = DeriveAclEntryId("user", bareSubject, "write");

        byte[] bareGrant = SerializeDecorated(new GrantFolderAccess(
            bareSubject,
            FolderPermissionLevel.Write,
            OpaqueFolderId,
            aclEntryId,
            OpaqueIdempotencyKey));
        byte[] prefixedGrant = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            subjectRef = $"user:{bareSubject}",
            permissionLevel = "write",
            folderId = OpaqueFolderId,
            aclEntryId,
            idempotencyKey = OpaqueIdempotencyKey,
        }));
        byte[] v1Grant = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            requestSchemaVersion = "v1",
            operations = new[]
            {
                new { principalKind = "user", principalId = bareSubject, action = "mutate_files" },
            },
        }));

        IdempotencyCanonicalIntent bareIntent = grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, bareGrant));
        IdempotencyCanonicalIntent prefixedIntent = grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, prefixedGrant));
        IdempotencyCanonicalIntent v1Intent = grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, v1Grant));

        bareIntent.SemanticPayload.ShouldBe(v1Intent.SemanticPayload);
        prefixedIntent.SemanticPayload.ShouldBe(v1Intent.SemanticPayload);
        bareIntent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(v1Intent.SemanticOptions!["semantic_payload_sha256"]);
        prefixedIntent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(v1Intent.SemanticOptions["semantic_payload_sha256"]);

        // Revoke intent differs from grant intent.
        IdempotencyCanonicalIntent revokeIntent = revokeAdapter.CreateIntent(IntentCommand(
            FoldersServerModule.RevokeFolderAccessCommandType,
            OpaqueFolderId,
            SerializeDecorated(new RevokeFolderAccess(bareSubject, FolderPermissionLevel.Write, OpaqueFolderId, aclEntryId, OpaqueIdempotencyKey))));
        revokeIntent.SemanticPayload.ShouldNotBe(bareIntent.SemanticPayload);

        // Caller-supplied effect is rejected.
        byte[] withEffect = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            subjectRef = bareSubject,
            permissionLevel = "write",
            effect = "revoke",
            folderId = OpaqueFolderId,
            aclEntryId,
            idempotencyKey = OpaqueIdempotencyKey,
        }));
        _ = Should.Throw<JsonException>(() => grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, withEffect)));

        // Mismatched aclEntryId is rejected.
        byte[] wrongAcl = SerializeDecorated(new GrantFolderAccess(
            bareSubject,
            FolderPermissionLevel.Write,
            OpaqueFolderId,
            "Wrong_AclEntry_01ARZ3NDEKT",
            OpaqueIdempotencyKey));
        _ = Should.Throw<JsonException>(() => grantAdapter.CreateIntent(IntentCommand(FoldersServerModule.GrantFolderAccessCommandType, OpaqueFolderId, wrongAcl)));
    }

    [Fact]
    public void MutateFilesDecoratedChangesBatchSeparatesDistinctBatchesMatchesV1ItemsAndExcludesRawBytes()
    {
        MutateFilesIdempotencyIntentAdapter adapter = new();
        byte[] bodyA = Encoding.UTF8.GetBytes(InlineContentSecret);
        byte[] bodyB = Encoding.UTF8.GetBytes("SECOND_FILE_BODY_NEVER_IN_INTENT_1234");
        string hashA = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(bodyA));
        string hashB = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(bodyB));

        MutateFiles decoratedBatch = new(
            [
                new FileMutationRequest(
                    ExpectedFileVersion: 0,
                    ExpectedLockFence: 1,
                    FileOperationKind: FileMutationRequestFileOperationKind.Add,
                    TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                    OperationId: OperationId,
                    PathMetadata: new PathMetadata("docs/a.txt", "a.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                    ContentHashReference: hashA,
                    ByteLength: bodyA.Length,
                    InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(bodyA))),
                new FileMutationRequest(
                    ExpectedFileVersion: 0,
                    ExpectedLockFence: 1,
                    FileOperationKind: FileMutationRequestFileOperationKind.Add,
                    TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                    OperationId: OperationIdSecond,
                    PathMetadata: new PathMetadata("docs/b.txt", "b.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                    ContentHashReference: hashB,
                    ByteLength: bodyB.Length,
                    InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(bodyB))),
            ],
            OpaqueFolderId,
            OpaqueWorkspaceId,
            OpaqueIdempotencyKey,
            OpaqueTaskId);

        byte[] v1ItemsBatch = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            requestSchemaVersion = "v1",
            workspaceId = OpaqueWorkspaceId,
            taskId = OpaqueTaskId,
            items = new object[]
            {
                new
                {
                    operationId = OperationId,
                    fileOperationKind = "add",
                    pathMetadata = new { normalizedPath = "docs/a.txt", displayName = "a.txt", pathPolicyClass = "content_allowed", unicodeNormalization = "NFC" },
                    contentHashReference = hashA,
                    byteLength = bodyA.Length,
                    mediaType = "text/plain",
                    expectedFileVersion = 0,
                    expectedLockFence = 1,
                },
                new
                {
                    operationId = OperationIdSecond,
                    fileOperationKind = "add",
                    pathMetadata = new { normalizedPath = "docs/b.txt", displayName = "b.txt", pathPolicyClass = "content_allowed", unicodeNormalization = "NFC" },
                    contentHashReference = hashB,
                    byteLength = bodyB.Length,
                    mediaType = "text/plain",
                    expectedFileVersion = 0,
                    expectedLockFence = 1,
                },
            },
        }));

        MutateFiles distinctBatch = decoratedBatch with
        {
            Changes = [decoratedBatch.Changes[0], decoratedBatch.Changes[1] with { ExpectedFileVersion = 2 }],
        };

        IdempotencyCanonicalIntent decoratedIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, SerializeDecorated(decoratedBatch)));
        IdempotencyCanonicalIntent v1BatchIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, v1ItemsBatch));
        IdempotencyCanonicalIntent distinctIntent = adapter.CreateIntent(IntentCommand(FoldersServerModule.MutateFilesCommandType, OpaqueFolderId, SerializeDecorated(distinctBatch)));

        decoratedIntent.SemanticPayload.ShouldBe(v1BatchIntent.SemanticPayload);
        decoratedIntent.SemanticOptions!["semantic_payload_sha256"].ShouldBe(v1BatchIntent.SemanticOptions!["semantic_payload_sha256"]);
        distinctIntent.SemanticOptions!["semantic_payload_sha256"].ShouldNotBe(decoratedIntent.SemanticOptions["semantic_payload_sha256"]);

        string serializedSemantic = Encoding.UTF8.GetString(decoratedIntent.SemanticPayload);
        serializedSemantic.ShouldNotContain(InlineContentSecret);
        serializedSemantic.ShouldNotContain(Convert.ToBase64String(bodyA));
        serializedSemantic.ShouldNotContain(OpaqueIdempotencyKey);
    }

    private static (string CommandType, string AggregateId, byte[] V1Payload, byte[] V2Payload) BuildV1AndV2Pair(string id)
    {
        byte[] inlineBytes = Encoding.UTF8.GetBytes(InlineContentSecret);
        string inlineHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(inlineBytes));
        string aclEntryId = DeriveAclEntryId("user", OpaqueUserId, "read");

        return id switch
        {
            "C01" => (
                FoldersServerModule.ArchiveFolderCommandType,
                OpaqueFolderId,
                SerializeV1(new { requestSchemaVersion = "v1", archiveReasonCode = "caller_requested" }),
                SerializeDecorated(new ArchiveFolder(ArchiveFolderArchiveReasonCode.CallerRequested, OpaqueFolderId, OpaqueIdempotencyKey))),
            "C02" => (
                FoldersServerModule.BindRepositoryCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    providerBindingRef = ProviderBindingRef,
                    externalRepositoryRef = ExternalRepoRef,
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v1",
                        repositoryBindingId = RepositoryBindingId,
                        policyRef = BranchPolicyRef,
                        defaultRef = "branch_ref_main",
                        allowedRefPatterns = new[] { "branch_ref_main" },
                        protectedRefPatterns = new[] { "branch_ref_prod" },
                    },
                }),
                SerializeDecorated(new BindRepository(
                    ProviderBindingRef,
                    ExternalRepoRef,
                    new BranchRefPolicyRequest(RepositoryBindingId, BranchPolicyRef, "branch_ref_main", ["branch_ref_main"], ["branch_ref_prod"]),
                    OpaqueFolderId,
                    OpaqueIdempotencyKey))),
            "C03" => (
                FoldersServerModule.CommitWorkspaceCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = OpaqueWorkspaceId,
                    operationId = OperationId,
                    taskId = OpaqueTaskId,
                    branchRefTarget = "branchref_main01",
                    changedPathMetadataDigest = "digest_0123456789ab",
                    authorMetadataReference = "authorref_01234567",
                    commitMessageClassification = "generated_summary",
                    auditMetadataKeys = new[] { "operation_id", "task_id" },
                }),
                SerializeDecorated(new CommitWorkspace(
                    OperationId,
                    OpaqueTaskId,
                    "branchref_main01",
                    "digest_0123456789ab",
                    "authorref_01234567",
                    "generated_summary",
                    [AuditMetadataKey.TaskId, AuditMetadataKey.OperationId],
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey))),
            "C04" => (
                FoldersServerModule.ConfigureBranchRefPolicyCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    repositoryBindingId = RepositoryBindingId,
                    policyRef = BranchPolicyRef,
                    defaultRef = "branch_ref_main",
                    allowedRefPatterns = new[] { "branch_ref_main", "branch_ref_dev" },
                    protectedRefPatterns = new[] { "branch_ref_prod" },
                }),
                SerializeDecorated(new ConfigureBranchRefPolicy(
                    RepositoryBindingId,
                    BranchPolicyRef,
                    "branch_ref_main",
                    ["branch_ref_dev", "branch_ref_main"],
                    OpaqueFolderId,
                    OpaqueIdempotencyKey,
                    ["branch_ref_prod"]))),
            "C05" => (
                FoldersServerModule.ConfigureProviderBindingCommandType,
                ProviderBindingActorId.Create(ProviderBindingRef),
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    providerBindingRef = ProviderBindingRef,
                    providerFamilyRef = ProviderFamilyRef,
                    capabilityProfileRef = CapabilityProfileRef,
                    nonSecretCredentialReference = CredentialRef,
                }),
                SerializeDecorated(new Hexalith.Folders.Contracts.Commands.ConfigureProviderBinding(
                    ProviderFamilyRef,
                    CapabilityProfileRef,
                    CredentialRef,
                    ProviderBindingRef,
                    OpaqueIdempotencyKey))),
            "C06" => (
                FoldersServerModule.CreateFolderCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    folderId = OpaqueFolderId,
                    parentFolderId = OpaqueParentFolderId,
                    folderMetadata = new { displayName = "Engineering", metadataClass = "tenant_sensitive" },
                }),
                SerializeDecorated(new CreateFolder(
                    new FolderMetadata("Engineering", SensitiveMetadataTier.TenantSensitive),
                    OpaqueIdempotencyKey,
                    OpaqueFolderId,
                    OpaqueParentFolderId))),
            "C07" => (
                FoldersServerModule.CreateRepositoryBackedFolderCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    folderId = OpaqueFolderId,
                    repositoryBindingId = RepositoryBindingId,
                    providerBindingRef = ProviderBindingRef,
                    repositoryProfileRef = RepositoryProfileRef,
                    folderMetadata = new { displayName = "Repo Folder", metadataClass = "tenant_sensitive" },
                    branchRefPolicy = new
                    {
                        requestSchemaVersion = "v1",
                        repositoryBindingId = RepositoryBindingId,
                        policyRef = BranchPolicyRef,
                        defaultRef = "branch_ref_main",
                        allowedRefPatterns = new[] { "branch_ref_main" },
                    },
                    credentialScopeClass = "provider_binding",
                }),
                SerializeDecorated(new CreateRepositoryBackedFolder(
                    OpaqueFolderId,
                    ProviderBindingRef,
                    RepositoryProfileRef,
                    new FolderMetadata("Repo Folder", SensitiveMetadataTier.TenantSensitive),
                    new BranchRefPolicyRequest(RepositoryBindingId, BranchPolicyRef, "branch_ref_main", ["branch_ref_main"]),
                    OpaqueIdempotencyKey))),
            "C08" => (
                FoldersServerModule.GrantFolderAccessCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    operations = new[] { new { principalKind = "user", principalId = OpaqueUserId, action = "read_metadata" } },
                }),
                SerializeDecorated(new GrantFolderAccess(
                    OpaqueUserId,
                    FolderPermissionLevel.Read,
                    OpaqueFolderId,
                    aclEntryId,
                    OpaqueIdempotencyKey))),
            "C09" => (
                FoldersServerModule.LockWorkspaceCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = OpaqueWorkspaceId,
                    lockIntent = "exclusive_write",
                    requestedLeaseSeconds = 600,
                    taskId = OpaqueTaskId,
                }),
                SerializeDecorated(new LockWorkspace(
                    LockWorkspaceLockIntent.ExclusiveWrite,
                    600,
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey,
                    OpaqueTaskId))),
            "C10" => (
                FoldersServerModule.MutateFilesCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = OpaqueWorkspaceId,
                    operationId = OperationId,
                    fileOperationKind = "add",
                    transportOperation = "PutFileInline",
                    pathMetadata = new
                    {
                        normalizedPath = "docs/readme.md",
                        displayName = "readme.md",
                        pathPolicyClass = "content_allowed",
                        unicodeNormalization = "NFC",
                    },
                    contentHashReference = inlineHash,
                    byteLength = inlineBytes.Length,
                    mediaType = "text/plain",
                    transportEvidenceKind = "inline_decoded",
                    observedByteLength = inlineBytes.Length,
                    taskId = OpaqueTaskId,
                    expectedFileVersion = 0,
                    expectedLockFence = 1,
                }),
                SerializeDecorated(new MutateFiles(
                    [
                        new FileMutationRequest(
                            ExpectedFileVersion: 0,
                            ExpectedLockFence: 1,
                            FileOperationKind: FileMutationRequestFileOperationKind.Add,
                            TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                            OperationId: OperationId,
                            PathMetadata: new PathMetadata("docs/readme.md", "readme.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                            ContentHashReference: inlineHash,
                            ByteLength: inlineBytes.Length,
                            InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(inlineBytes))),
                    ],
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey,
                    OpaqueTaskId))),
            "C11" => (
                FoldersServerModule.PrepareWorkspaceCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = OpaqueWorkspaceId,
                    repositoryBindingId = RepositoryBindingId,
                    branchRefPolicyRef = BranchPolicyRef,
                    workspacePolicyRef = WorkspacePolicyRef,
                    taskId = OpaqueTaskId,
                }),
                SerializeDecorated(new PrepareWorkspace(
                    RepositoryBindingId,
                    BranchPolicyRef,
                    WorkspacePolicyRef,
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey,
                    OpaqueTaskId))),
            "C12" => (
                FoldersServerModule.ReleaseWorkspaceLockCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    workspaceId = OpaqueWorkspaceId,
                    lockId = LockId,
                    lockOwnershipProof = LockProof,
                    releaseReasonCode = "caller_completed",
                    taskId = OpaqueTaskId,
                }),
                SerializeDecorated(new ReleaseWorkspaceLock(
                    LockId,
                    LockProof,
                    ReleaseWorkspaceLockReleaseReasonCode.CallerCompleted,
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey,
                    OpaqueTaskId))),
            "C13" => (
                FoldersServerModule.RevokeFolderAccessCommandType,
                OpaqueFolderId,
                SerializeV1(new
                {
                    requestSchemaVersion = "v1",
                    operations = new[] { new { principalKind = "user", principalId = OpaqueUserId, action = "read_metadata" } },
                }),
                SerializeDecorated(new RevokeFolderAccess(
                    OpaqueUserId,
                    FolderPermissionLevel.Read,
                    OpaqueFolderId,
                    aclEntryId,
                    OpaqueIdempotencyKey))),
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
    }

    private static byte[] SerializeDecorated<T>(T command)
        => JsonSerializer.SerializeToUtf8Bytes(command, DecoratedJsonOptions);

    private static byte[] SerializeV1(object payload)
        => JsonSerializer.SerializeToUtf8Bytes(payload, DecoratedJsonOptions);

    private static IdempotencyIntentCommand IntentCommand(string commandType, string aggregateId, byte[] payload)
        => new(
            commandType,
            TenantId,
            FoldersServerModule.DomainName,
            aggregateId,
            payload,
            new Dictionary<string, string> { ["taskId"] = OpaqueTaskId });

    private static string DeriveAclEntryId(string kindToken, string principalId, string permissionLevel)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{kindToken}|{principalId}|{permissionLevel}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static IdempotencyKeyProtector CreateProtector()
        => new(new StaticIdempotencyDigestKeyProvider(
            "folders-test-v1",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["folders-test-v1"] = new byte[32],
            },
            []));

    private static IdempotencyAdmissionRequest CreateAdmissionRequest(
        IdempotencyProtectedIdentity identity,
        string executionMessageId,
        string executionCorrelationId)
        => new(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            identity.TenantPartition,
            identity.DigestKeyVersion,
            identity.KeyDigest,
            identity.VerificationTag,
            identity.IntentDigest,
            identity.RetentionTier,
            executionMessageId,
            executionCorrelationId);

    private static IdempotencyAdmissionActor CreateAdmissionActor(string actorId, TimeProvider clock)
    {
        ActorHost host = ActorHost.CreateForTest<IdempotencyAdmissionActor>(
            new ActorTestOptions
            {
                ActorId = new ActorId(actorId),
                TimerManager = new NoOpActorTimerManager(),
            });
        IdempotencyAdmissionActor actor = new(host, NullLogger<IdempotencyAdmissionActor>.Instance, clock);
        ActorStateManagerProperty.SetValue(actor, new InMemoryActorStateManager());
        return actor;
    }

    private sealed class NoOpActorTimerManager : ActorTimerManager
    {
        public override Task<IActorReminder> GetReminderAsync(ActorReminderToken reminder) => Task.FromResult<IActorReminder>(null!);

        public override Task RegisterReminderAsync(ActorReminder reminder) => Task.CompletedTask;

        public override Task RegisterTimerAsync(ActorTimer timer) => Task.CompletedTask;

        public override Task UnregisterReminderAsync(ActorReminderToken reminder) => Task.CompletedTask;

        public override Task UnregisterTimerAsync(ActorTimerToken timer) => Task.CompletedTask;
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    // CACHE-NON-TENANT: this in-memory actor state manager is a test double, not a tenant data cache.
    private sealed class InMemoryActorStateManager : IActorStateManager
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
            => _store.TryAdd(stateName, value)
                ? Task.CompletedTask
                : throw new InvalidOperationException($"State '{stateName}' already exists.");

        public Task AddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => AddStateAsync(stateName, value, cancellationToken);

        public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, CancellationToken cancellationToken = default)
        {
            T next = _store.TryGetValue(stateName, out object? existing) && existing is T typed
                ? updateValueFactory(stateName, typed)
                : addValue;
            _store[stateName] = next;
            return Task.FromResult(next);
        }

        public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, TimeSpan ttl, CancellationToken cancellationToken = default)
            => AddOrUpdateStateAsync(stateName, addValue, updateValueFactory, cancellationToken);

        public Task ClearCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(stateName));

        public Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(stateName, out object? existing) && existing is T typed)
            {
                return Task.FromResult(typed);
            }

            _store[stateName] = value;
            return Task.FromResult(value);
        }

        public Task<T> GetOrAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => GetOrAddStateAsync(stateName, value, cancellationToken);

        public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
            => _store.TryGetValue(stateName, out object? existing) && existing is T typed
                ? Task.FromResult(typed)
                : throw new KeyNotFoundException(stateName);

        public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
        {
            _ = _store.Remove(stateName);
            return Task.CompletedTask;
        }

        public Task SaveStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            _store[stateName] = value;
            return Task.CompletedTask;
        }

        public Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => SetStateAsync(stateName, value, cancellationToken);

        public Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryAdd(stateName, value));

        public Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => TryAddStateAsync(stateName, value, cancellationToken);

        public Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(stateName, out object? existing) && existing is T typed
                ? new ConditionalValue<T>(true, typed)
                : new ConditionalValue<T>(false, default!));

        public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Remove(stateName));

        public Task UnloadStateAsync(string stateName, UnloadStateOptions? options = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
