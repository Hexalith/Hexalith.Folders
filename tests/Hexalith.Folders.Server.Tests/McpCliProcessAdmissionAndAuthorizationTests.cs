using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts.Commands;
using Hexalith.Folders.Contracts.Commands.Models;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.ProviderReadiness;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Testing;
using Hexalith.Folders.Testing.Providers;
using Hexalith.Folders.Tests.OperationalStatus;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using Xunit;

using ContractsArchiveFolder = Hexalith.Folders.Contracts.Commands.ArchiveFolder;
using ContractsBindRepository = Hexalith.Folders.Contracts.Commands.BindRepository;
using ContractsCommitWorkspace = Hexalith.Folders.Contracts.Commands.CommitWorkspace;
using ContractsConfigureBranchRefPolicy = Hexalith.Folders.Contracts.Commands.ConfigureBranchRefPolicy;
using ContractsConfigureProviderBinding = Hexalith.Folders.Contracts.Commands.ConfigureProviderBinding;
using ContractsCreateFolder = Hexalith.Folders.Contracts.Commands.CreateFolder;
using ContractsCreateRepositoryBackedFolder = Hexalith.Folders.Contracts.Commands.CreateRepositoryBackedFolder;
using ContractsGrantFolderAccess = Hexalith.Folders.Contracts.Commands.GrantFolderAccess;
using ContractsLockWorkspace = Hexalith.Folders.Contracts.Commands.LockWorkspace;
using ContractsMutateFiles = Hexalith.Folders.Contracts.Commands.MutateFiles;
using ContractsPathMetadata = Hexalith.Folders.Contracts.Commands.Models.PathMetadata;
using ContractsPrepareWorkspace = Hexalith.Folders.Contracts.Commands.PrepareWorkspace;
using ContractsReleaseWorkspaceLock = Hexalith.Folders.Contracts.Commands.ReleaseWorkspaceLock;
using ContractsRevokeFolderAccess = Hexalith.Folders.Contracts.Commands.RevokeFolderAccess;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// End-to-end production-composed <c>POST /process</c> tests proving Story 5.13 admission, uppercase ULID
/// and opaque identifier preservation, all 13 decorated v2 command translations (C01–C13), C10 content-store
/// receipt materialization, and PD10 deny-before-access safe denials with persisted state-store end-state assertions.
/// </summary>
public sealed class McpCliProcessAdmissionAndAuthorizationTests
{
    private const string WorkloadKey = "development-workload-signing-key-32-bytes-minimum";
    private const string TenantId = "tenant-a";
    private const string OpaqueUserId = "User_Opaque_01ARZ3NDEKTSV4";
    private const string OpaqueTargetUserId = "Target_User_01ARZ3NDEKTSV4";
    private const string OpaqueFolderId = "Opaque_Folder_01ARZ3NDEKTS";
    private const string OpaqueRepoFolderId = "Opaque_RepoFl_01ARZ3NDEKTS";
    private const string OpaqueWorkspaceId = "Opaque_WorkSp_01ARZ3NDEKTS";
    private const string OpaqueTaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string OpaqueCorrelationId = "01ARZ3NDEKTSV4RRFFQ69G5FBW";
    private const string OpaqueIdempotencyKey = "01ARZ3NDEKTSV4RRFFQ69G5FCX";
    private const string ProviderBindingRef = "Opaque_ProvBind_01ARZ3NDEK";
    private const string CapabilityProfileRef = "Opaque_CapProf_01ARZ3NDEKT";
    private const string CredentialRef = "Opaque_CredRef_01ARZ3NDEKT";
    private const string RepositoryBindingId = "Opaque_RepoBind_01ARZ3NDEK";
    private const string RepositoryProfileRef = "Opaque_RepoProf_01ARZ3NDEK";
    private const string ExternalRepositoryRef = "Opaque_ExtRepo_01ARZ3NDEKT";
    private const string BranchPolicyRef = "Opaque_BrPolicy_01ARZ3NDEK";
    private const string WorkspacePolicyRef = "Opaque_WsPolicy_01ARZ3NDEK";
    private const string InlineBodyText = "SENSITIVE_FILE_CONTENT_MUST_NOT_APPEAR_IN_EVENTS_7a6b5c";

    private static readonly JsonSerializerOptions DecoratedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandLifecycleExecutesThroughSignedProcessWithUppercaseUlidsAndPreservesLegacyCommit(bool legacyCommit)
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);

        // C05: ConfigureProviderBinding on reserved ProviderBindingActorId
        string providerActorId = ProviderBindingActorId.Create(ProviderBindingRef);
        (DomainServiceWireResult c05, List<EventEnvelope> orgEvents) = await harness.ExecuteAcceptedAsync(
            providerActorId,
            FoldersServerModule.ConfigureProviderBindingCommandType,
            SerializeDecorated(new ContractsConfigureProviderBinding(
                "forgejo",
                CapabilityProfileRef,
                CredentialRef,
                ProviderBindingRef,
                OpaqueIdempotencyKey)),
            [],
            ["configure_provider_binding"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F01").ConfigureAwait(true);
        c05.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(ProviderBindingConfigured).FullName);
        AssertProcessResultEcho(c05.ResultPayload, OpaqueCorrelationId, OpaqueTaskId, OpaqueIdempotencyKey);
        OrganizationState orgState = harness.FoldOrganization(providerActorId, orgEvents);
        orgState.ProviderBindings.ShouldContainKey(ProviderBindingRef);

        // C06: CreateFolder (with mixed-case opaque folder ID and uppercase ULID envelope IDs)
        List<EventEnvelope> folderEvents = [];
        (DomainServiceWireResult c06, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.CreateFolderCommandType,
            SerializeDecorated(new ContractsCreateFolder(
                new FolderMetadata("Engineering Workspace", SensitiveMetadataTier.TenantSensitive),
                OpaqueIdempotencyKey,
                OpaqueFolderId)),
            folderEvents,
            ["create_folder"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F02").ConfigureAwait(true);
        c06.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(FolderCreated).FullName);
        AssertProcessResultEcho(c06.ResultPayload, OpaqueCorrelationId, OpaqueTaskId, OpaqueIdempotencyKey);
        FolderState folderState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        folderState.IsCreated.ShouldBeTrue();
        folderState.FolderId.ShouldBe(OpaqueFolderId);

        // C08: GrantFolderAccess (bare opaque subjectRef -> user per D3)
        string bareAclEntryId = FolderAclContract.DeriveAclEntryId("user", OpaqueTargetUserId, "write");
        (DomainServiceWireResult c08Bare, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.GrantFolderAccessCommandType,
            SerializeDecorated(new ContractsGrantFolderAccess(
                OpaqueTargetUserId,
                FolderPermissionLevel.Write,
                OpaqueFolderId,
                bareAclEntryId,
                OpaqueIdempotencyKey)),
            folderEvents,
            ["manage_folder_access"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F03").ConfigureAwait(true);
        c08Bare.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(FolderAccessGranted).FullName);
        harness.FoldFolder(OpaqueFolderId, folderEvents).AccessOverrides.Values
            .ShouldContain(entry => entry.IsGranted
                && entry.Key.PrincipalKind == FolderAccessPrincipalKind.User
                && entry.Key.PrincipalId == OpaqueTargetUserId
                && entry.Key.Action == FolderAclContract.WriteAction);

        // C08: GrantFolderAccess ({kind}:{principalId} subjectRef per D3)
        string groupAclEntryId = FolderAclContract.DeriveAclEntryId("group", "Group_Opaque_01ARZ3NDEKTSV", "read");
        (_, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.GrantFolderAccessCommandType,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                subjectRef = "group:Group_Opaque_01ARZ3NDEKTSV",
                permissionLevel = "read",
                folderId = OpaqueFolderId,
                aclEntryId = groupAclEntryId,
                idempotencyKey = OpaqueIdempotencyKey,
            })),
            folderEvents,
            ["manage_folder_access"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F04").ConfigureAwait(true);
        harness.FoldFolder(OpaqueFolderId, folderEvents).AccessOverrides.Values
            .ShouldContain(entry => entry.IsGranted
                && entry.Key.PrincipalKind == FolderAccessPrincipalKind.Group
                && entry.Key.PrincipalId == "Group_Opaque_01ARZ3NDEKTSV"
                && entry.Key.Action == FolderAclContract.ReadAction);

        // C13: RevokeFolderAccess
        (DomainServiceWireResult c13, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.RevokeFolderAccessCommandType,
            SerializeDecorated(new ContractsRevokeFolderAccess(
                OpaqueTargetUserId,
                FolderPermissionLevel.Write,
                OpaqueFolderId,
                bareAclEntryId,
                OpaqueIdempotencyKey)),
            folderEvents,
            ["manage_folder_access"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F05").ConfigureAwait(true);
        c13.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(FolderAccessRevoked).FullName);
        harness.FoldFolder(OpaqueFolderId, folderEvents).AccessOverrides.Values
            .ShouldContain(entry => !entry.IsGranted
                && entry.Key.PrincipalId == OpaqueTargetUserId
                && entry.Key.Action == FolderAclContract.WriteAction);

        // C07: CreateRepositoryBackedFolder on a separate active folder (resolves repositoryBindingId & credentialScopeClass)
        List<EventEnvelope> repoBackedEvents = [];
        (_, repoBackedEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueRepoFolderId,
            FoldersServerModule.CreateFolderCommandType,
            SerializeDecorated(new ContractsCreateFolder(
                new FolderMetadata("Seed Folder", SensitiveMetadataTier.TenantSensitive),
                OpaqueIdempotencyKey,
                OpaqueRepoFolderId)),
            repoBackedEvents,
            ["create_folder"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F06").ConfigureAwait(true);
        (DomainServiceWireResult c07, repoBackedEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueRepoFolderId,
            FoldersServerModule.CreateRepositoryBackedFolderCommandType,
            SerializeDecorated(new ContractsCreateRepositoryBackedFolder(
                OpaqueRepoFolderId,
                ProviderBindingRef,
                RepositoryProfileRef,
                new FolderMetadata("Engineering Project", SensitiveMetadataTier.TenantSensitive),
                new BranchRefPolicyRequest(RepositoryBindingId, BranchPolicyRef, "branch_ref_main", ["branch_ref_main"]),
                OpaqueIdempotencyKey)),
            repoBackedEvents,
            ["create_repository_backed_folder", ProviderReadinessValidationService.ReadActionToken],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F07").ConfigureAwait(true);
        c07.Events.ShouldNotBeEmpty();
        FolderState repoBackedState = harness.FoldFolder(OpaqueRepoFolderId, repoBackedEvents);
        repoBackedState.RepositoryBindingId.ShouldBe(RepositoryBindingId);
        repoBackedState.RepositoryProfileRef.ShouldBe(RepositoryProfileRef);
        repoBackedState.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.BindingRequested);
        c07.Events.ShouldContain(evt => evt.EventTypeName == typeof(RepositoryBindingRequested).FullName);

        // C02: BindRepository on main folder
        (DomainServiceWireResult c02, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.BindRepositoryCommandType,
            SerializeDecorated(new ContractsBindRepository(
                ProviderBindingRef,
                ExternalRepositoryRef,
                new BranchRefPolicyRequest(RepositoryBindingId, BranchPolicyRef, "branch_ref_main", ["branch_ref_main"], ["branch_ref_prod"]),
                OpaqueFolderId,
                OpaqueIdempotencyKey)),
            folderEvents,
            ["bind_repository", ProviderReadinessValidationService.ReadActionToken],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F08").ConfigureAwait(true);
        c02.Events.ShouldNotBeEmpty();
        FolderState boundState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        boundState.RepositoryBindingState.ShouldBe(FolderRepositoryBindingState.Bound);
        string boundRepoId = boundState.RepositoryBindingId.ShouldNotBeNull();

        // C04: ConfigureBranchRefPolicy
        (DomainServiceWireResult c04, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.ConfigureBranchRefPolicyCommandType,
            SerializeDecorated(new ContractsConfigureBranchRefPolicy(
                boundRepoId,
                BranchPolicyRef,
                "branch_ref_main",
                ["branch_ref_main", "branch_ref_dev"],
                OpaqueFolderId,
                OpaqueIdempotencyKey,
                ["branch_ref_prod"])),
            folderEvents,
            ["configure_branch_ref_policy", ProviderReadinessValidationService.ReadActionToken],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F09").ConfigureAwait(true);
        c04.Events.ShouldNotBeEmpty();
        harness.FoldFolder(OpaqueFolderId, folderEvents).BranchRefPolicyRef.ShouldBe(BranchPolicyRef);

        // C11: PrepareWorkspace
        (DomainServiceWireResult c11, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.PrepareWorkspaceCommandType,
            SerializeDecorated(new ContractsPrepareWorkspace(
                boundRepoId,
                BranchPolicyRef,
                WorkspacePolicyRef,
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["prepare_workspace", ProviderReadinessValidationService.ReadActionToken],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F10").ConfigureAwait(true);
        c11.Events.ShouldNotBeEmpty();
        harness.FoldFolder(OpaqueFolderId, folderEvents).WorkspaceId.ShouldBe(OpaqueWorkspaceId);

        // C09: LockWorkspace (initial lock with dedicated uppercase ULID taskId so C12 cleanly closes that task)
        const string lockReleaseTaskId = "01ARZ3NDEKTSV4RRFFQ69G5FA0";
        (DomainServiceWireResult c09Initial, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.LockWorkspaceCommandType,
            SerializeDecorated(new ContractsLockWorkspace(
                LockWorkspaceLockIntent.ExclusiveWrite,
                1800,
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                lockReleaseTaskId)),
            folderEvents,
            ["lock_workspace"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F11",
            taskIdOverride: lockReleaseTaskId).ConfigureAwait(true);
        c09Initial.Events.ShouldNotBeEmpty();
        FolderState initialLockState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        initialLockState.WorkspaceLockId.ShouldNotBeNullOrWhiteSpace();
        initialLockState.WorkspaceLockHolderTaskId.ShouldBe(lockReleaseTaskId);
        initialLockState.WorkspaceLockFence.ShouldBe(1);

        // C12: ReleaseWorkspaceLock
        string proof = FolderCommandValidator.DeriveWorkspaceLockOwnershipProof(
            TenantId,
            OpaqueFolderId,
            OpaqueWorkspaceId,
            lockReleaseTaskId,
            initialLockState.WorkspaceLockId!);
        (DomainServiceWireResult c12, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.ReleaseWorkspaceLockCommandType,
            SerializeDecorated(new ContractsReleaseWorkspaceLock(
                initialLockState.WorkspaceLockId!,
                proof,
                ReleaseWorkspaceLockReleaseReasonCode.CallerCompleted,
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                lockReleaseTaskId)),
            folderEvents,
            ["lock_workspace"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F15",
            taskIdOverride: lockReleaseTaskId).ConfigureAwait(true);
        c12.Events.ShouldNotBeEmpty();
        FolderState unlockedState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        unlockedState.WorkspaceLifecycleState.ShouldBe(FolderWorkspaceLifecycleState.Ready);
        unlockedState.WorkspaceLockId.ShouldBeNull();

        // C09: Re-acquire lock for C10 MutateFiles and C03 CommitWorkspace
        (DomainServiceWireResult c09, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.LockWorkspaceCommandType,
            SerializeDecorated(new ContractsLockWorkspace(
                LockWorkspaceLockIntent.ExclusiveWrite,
                1800,
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["lock_workspace"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F11B").ConfigureAwait(true);
        c09.Events.ShouldNotBeEmpty();
        FolderState lockedState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        lockedState.WorkspaceLockId.ShouldNotBeNullOrWhiteSpace();
        lockedState.WorkspaceLockFence.ShouldBe(2);

        // C10: MutateFiles (decorated changes[] batch: inline add -> verifies SHA-256, materializes content receipt, metadata-only events)
        byte[] contentBytes = Encoding.UTF8.GetBytes(InlineBodyText);
        string contentHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(contentBytes));
        (DomainServiceWireResult c10Add, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: lockedState.WorkspaceLockFence,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_FileOp1_01ARZ3NDEKT",
                        PathMetadata: new ContractsPathMetadata("docs/readme.md", "readme.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: contentHash,
                        ByteLength: contentBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(contentBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F12").ConfigureAwait(true);
        c10Add.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(WorkspaceFileMutationAccepted).FullName);
        AssertProcessResultEcho(c10Add.ResultPayload, OpaqueCorrelationId, OpaqueTaskId, OpaqueIdempotencyKey);
        string serializedAddEvent = Encoding.UTF8.GetString(c10Add.Events[0].Payload);
        serializedAddEvent.ShouldNotContain(InlineBodyText);
        serializedAddEvent.ShouldNotContain(Convert.ToBase64String(contentBytes));
        FolderState afterAddState = harness.FoldFolder(OpaqueFolderId, folderEvents);
        WorkspaceContentBinding addBinding = afterAddState.ContentPublications.Values.ShouldHaveSingleItem();
        addBinding.ReceiptId.ShouldNotBeNullOrWhiteSpace();
        addBinding.ContentHashReference.ShouldBe(contentHash);
        addBinding.ByteLength.ShouldBe(contentBytes.Length);

        // C10: MutateFiles remove with explicit byteLength: null (the ONE nullable field in v2 spine!)
        byte[] removeJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            changes = new[]
            {
                new
                {
                    expectedFileVersion = 1,
                    expectedLockFence = lockedState.WorkspaceLockFence,
                    fileOperationKind = "remove",
                    transportOperation = "metadataOnlyRemoval",
                    operationId = "Opaque_FileOp2_01ARZ3NDEKT",
                    pathMetadata = new
                    {
                        normalizedPath = "docs/readme.md",
                        displayName = "readme.md",
                        pathPolicyClass = "content_allowed",
                        unicodeNormalization = "NFC",
                    },
                    byteLength = (int?)null,
                },
            },
            folderId = OpaqueFolderId,
            workspaceId = OpaqueWorkspaceId,
            idempotencyKey = OpaqueIdempotencyKey,
            taskId = OpaqueTaskId,
        }));
        (DomainServiceWireResult c10Remove, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            removeJson,
            folderEvents,
            ["mutate_files"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F13").ConfigureAwait(true);
        c10Remove.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(WorkspaceFileMutationAccepted).FullName);

        // C03 accepts the closed decorated vocabulary and legacy REST classification tokens.
        byte[] commit = legacyCommit
            ? JsonSerializer.SerializeToUtf8Bytes(new
            {
                requestSchemaVersion = "v1",
                workspaceId = OpaqueWorkspaceId,
                operationId = "Opaque_CommitOp_01ARZ3NDEK",
                taskId = OpaqueTaskId,
                branchRefTarget = "branchref_main01",
                changedPathMetadataDigest = "digest_0123456789abcdef",
                authorMetadataReference = "authorref_0123456789",
                commitMessageClassification = "generated_summary",
                auditMetadataKeys = new[] { "generated_summary" },
            })
            : SerializeDecorated(new ContractsCommitWorkspace(
                "Opaque_CommitOp_01ARZ3NDEK",
                OpaqueTaskId,
                "branchref_main01",
                "digest_0123456789abcdef",
                "authorref_0123456789",
                "generated_summary",
                [AuditMetadataKey.OperationId, AuditMetadataKey.TaskId],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey));
        (DomainServiceWireResult c03, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.CommitWorkspaceCommandType,
            commit,
            folderEvents,
            ["commit", ProviderReadinessValidationService.ReadActionToken],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F14").ConfigureAwait(true);
        c03.Events.ShouldNotBeEmpty();
        harness.FoldFolder(OpaqueFolderId, folderEvents).WorkspaceCommitOperationId.ShouldBe("Opaque_CommitOp_01ARZ3NDEK");

        // C01: ArchiveFolder
        (DomainServiceWireResult c01, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.ArchiveFolderCommandType,
            SerializeDecorated(new ContractsArchiveFolder(
                ArchiveFolderArchiveReasonCode.CallerRequested,
                OpaqueFolderId,
                OpaqueIdempotencyKey)),
            folderEvents,
            ["archive_folder"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5F16").ConfigureAwait(true);
        c01.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(FolderArchived).FullName);
        harness.FoldFolder(OpaqueFolderId, folderEvents).LifecycleState.ShouldBe(FolderLifecycleState.Archived);
    }

    [Fact]
    public async Task InvalidDecoratedV2PayloadsFailClosedBeforeStateMutationAndEchoUppercaseUlids()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        List<EventEnvelope> folderEvents = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        FolderState before = harness.FoldFolder(OpaqueFolderId, folderEvents);

        // 1. Missing extensions["taskId"] -> HTTP 400 validation_error
        CommandEnvelope missingTask = new(
            "01ARZ3NDEKTSV4RRFFQ69G5G01",
            TenantId,
            FoldersServerModule.DomainName,
            OpaqueFolderId,
            FoldersServerModule.ArchiveFolderCommandType,
            SerializeDecorated(new ContractsArchiveFolder(ArchiveFolderArchiveReasonCode.CallerRequested, OpaqueFolderId, OpaqueIdempotencyKey)),
            OpaqueCorrelationId,
            null,
            OpaqueUserId,
            Extensions: null);
        using (HttpResponseMessage response = await harness.PostProcessAsync(missingTask, folderEvents, ["archive_folder"]).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. Task reconciliation mismatch on C03, C09, C10, C11, C12 -> ValidationFailed
        const string wrongTask = "01ARZ3NDEKTSV4RRFFQ69G5ZZZ";
        foreach ((string commandType, byte[] payload, string[] permissions) in new (string, byte[], string[])[]
        {
            (FoldersServerModule.CommitWorkspaceCommandType, SerializeDecorated(new ContractsCommitWorkspace("Opaque_CommitOp_01ARZ3NDEK", wrongTask, "branchref_main01", "digest_0123456789abcdef", "authorref_0123456789", "generated_summary", [AuditMetadataKey.OperationId], OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey)), ["commit"]),
            (FoldersServerModule.LockWorkspaceCommandType, SerializeDecorated(new ContractsLockWorkspace(LockWorkspaceLockIntent.ExclusiveWrite, 600, OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, wrongTask)), ["lock_workspace"]),
            (FoldersServerModule.MutateFilesCommandType, SerializeDecorated(new ContractsMutateFiles([new FileMutationRequest(0, 1, FileMutationRequestFileOperationKind.Remove, FileMutationRequestTransportOperation.MetadataOnlyRemoval, "Opaque_FileOp1_01ARZ3NDEKT", new ContractsPathMetadata("docs/a.txt", "a.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC))], OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, wrongTask)), ["mutate_files"]),
            (FoldersServerModule.PrepareWorkspaceCommandType, SerializeDecorated(new ContractsPrepareWorkspace(RepositoryBindingId, BranchPolicyRef, WorkspacePolicyRef, OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, wrongTask)), ["prepare_workspace"]),
            (FoldersServerModule.ReleaseWorkspaceLockCommandType, SerializeDecorated(new ContractsReleaseWorkspaceLock("Opaque_LockId_01ARZ3NDEKTS", "Opaque_LockPrf_01ARZ3NDEKT", ReleaseWorkspaceLockReleaseReasonCode.CallerCompleted, OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, wrongTask)), ["lock_workspace"]),
        })
        {
            RejectedWirePayload rejected = await harness.ExecuteRejectedAsync(OpaqueFolderId, commandType, payload, folderEvents, permissions).ConfigureAwait(true);
            rejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
            rejected.CorrelationId.ShouldBe(OpaqueCorrelationId);
            rejected.TaskId.ShouldBe(OpaqueTaskId);
        }

        // 3. C03 auditMetadataKeys: empty, >24, unknown, duplicate -> ValidationFailed
        foreach (string[] badKeys in new[]
        {
            Array.Empty<string>(),
            Enumerable.Range(0, 25).Select(i => $"key_{i}").ToArray(),
            ["not_a_valid_key"],
            ["operation_id", "operation_id"],
        })
        {
            byte[] badCommit = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                operationId = "Opaque_CommitOp_01ARZ3NDEK",
                taskId = OpaqueTaskId,
                branchRefTarget = "branchref_main01",
                changedPathMetadataDigest = "digest_0123456789abcdef",
                authorMetadataReference = "authorref_0123456789",
                commitMessageClassification = "generated_summary",
                auditMetadataKeys = badKeys,
                folderId = OpaqueFolderId,
                workspaceId = OpaqueWorkspaceId,
                idempotencyKey = OpaqueIdempotencyKey,
            }));
            RejectedWirePayload rejected = await harness.ExecuteRejectedAsync(
                OpaqueFolderId,
                FoldersServerModule.CommitWorkspaceCommandType,
                badCommit,
                folderEvents,
                ["commit"]).ConfigureAwait(true);
            rejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
        }

        // 4. C08 caller-supplied effect -> MalformedJsonPayload
        string aclEntryId = FolderAclContract.DeriveAclEntryId("user", OpaqueTargetUserId, "read");
        byte[] effectAttack = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            subjectRef = OpaqueTargetUserId,
            permissionLevel = "read",
            effect = "revoke",
            folderId = OpaqueFolderId,
            aclEntryId,
            idempotencyKey = OpaqueIdempotencyKey,
        }));
        RejectedWirePayload effectRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.GrantFolderAccessCommandType,
            effectAttack,
            folderEvents,
            ["manage_folder_access"]).ConfigureAwait(true);
        effectRejected.Code.ShouldBe(nameof(FolderResultCode.MalformedJsonPayload));

        // 5. Forbidden explicit null on optional non-nullable property -> ValidationFailed
        byte[] forbiddenNull = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            folderMetadata = new { displayName = "Null Parent", metadataClass = "tenant_sensitive" },
            idempotencyKey = OpaqueIdempotencyKey,
            folderId = OpaqueFolderId,
            parentFolderId = (string?)null,
        }));
        RejectedWirePayload nullRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.CreateFolderCommandType,
            forbiddenNull,
            [],
            ["create_folder"]).ConfigureAwait(true);
        nullRejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        // 6. C10 changes[] edge cases: >262144 inline bytes, SHA-256 mismatch, conflicting paths, remove with inline content
        byte[] oversized = new byte[262_145];
        string oversizedHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(oversized));
        RejectedWirePayload oversizedRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/big.bin", "big.bin", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: oversizedHash,
                        ByteLength: oversized.Length,
                        InlineContent: new PutFileInline("application/octet-stream", Convert.ToBase64String(oversized))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        oversizedRejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        byte[] smallBytes = "hello"u8.ToArray();
        RejectedWirePayload hashMismatchRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/hash.txt", "hash.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: "hashref_" + new string('0', 64),
                        ByteLength: smallBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        hashMismatchRejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        string smallHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(smallBytes));
        RejectedWirePayload conflictingPathsRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/a.txt", "a.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: smallHash,
                        ByteLength: smallBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp2_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/A.txt", "A.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: smallHash,
                        ByteLength: smallBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        conflictingPathsRejected.Code.ShouldBe(nameof(FolderResultCode.PathPolicyDenied));

        RejectedWirePayload removeWithContentRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        1,
                        1,
                        FileMutationRequestFileOperationKind.Remove,
                        FileMutationRequestTransportOperation.MetadataOnlyRemoval,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/a.txt", "a.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        removeWithContentRejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        // 7. C10 non-owned lock and expired lock -> LockNotOwned / LockExpired with zero content uploads
        RejectedWirePayload notOwnedRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/notowned.txt", "notowned.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: smallHash,
                        ByteLength: smallBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                "01ARZ3NDEKTSV4RRFFQ69G5OTHER")),
            folderEvents,
            ["mutate_files"],
            taskIdOverride: "01ARZ3NDEKTSV4RRFFQ69G5OTHER").ConfigureAwait(true);
        notOwnedRejected.Code.ShouldBe(nameof(FolderResultCode.LockNotOwned));

        harness.Clock.Advance(TimeSpan.FromHours(2));
        RejectedWirePayload expiredLockRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        0,
                        1,
                        FileMutationRequestFileOperationKind.Add,
                        FileMutationRequestTransportOperation.PutFileInline,
                        "Opaque_FileOp1_01ARZ3NDEKT",
                        new ContractsPathMetadata("docs/expired.txt", "expired.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: smallHash,
                        ByteLength: smallBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(smallBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        expiredLockRejected.Code.ShouldBe(nameof(FolderResultCode.LockExpired));

        // State store end-state assertion: zero mutations persisted across all rejections.
        harness.FoldFolder(OpaqueFolderId, folderEvents).ShouldBeEquivalentTo(before);
    }

    [Fact]
    public async Task DenyBeforeAccessSafeDenialsPreventProtectedReadsAndStateMutations()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        List<EventEnvelope> folderEvents = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        byte[] archivePayload = SerializeDecorated(new ContractsArchiveFolder(
            ArchiveFolderArchiveReasonCode.CallerRequested,
            OpaqueFolderId,
            OpaqueIdempotencyKey));

        // 1. Missing receipt -> 401
        CommandEnvelope validEnvelope = ProcessHarness.CreateEnvelope(
            OpaqueFolderId,
            FoldersServerModule.ArchiveFolderCommandType,
            archivePayload,
            "01ARZ3NDEKTSV4RRFFQ69G5D01");
        using (HttpResponseMessage missingReceipt = await harness.Client.PostAsJsonAsync(
            FoldersServerModule.ProcessRoute,
            new DomainServiceRequest(validEnvelope, CurrentState: ProcessHarness.StateEnvelope(folderEvents)),
            TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            missingReceipt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            missingReceipt.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
        }

        // 2. Forged envelope TenantId (receipt issued for another tenant, replayed onto TenantId) -> 401 safe denial, zero ACL reads
        harness.PermissionProvider.Calls = 0;
        CommandEnvelope forgedTenant = validEnvelope with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5D02" };
        using (HttpResponseMessage forgedTenantResponse = await harness.PostProcessWithReceiptIssuedForAsync(
            forgedTenant,
            forgedTenant with { TenantId = "tenant-other" },
            folderEvents,
            ["archive_folder"]).ConfigureAwait(true))
        {
            forgedTenantResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            forgedTenantResponse.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
            string forgedTenantBody = await forgedTenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            forgedTenantBody.ShouldNotContain("tenant-other", Case.Insensitive);
            harness.PermissionProvider.Calls.ShouldBe(0);
        }

        // 3. Forged envelope UserId (receipt issued for another user, replayed onto OpaqueUserId) -> 401 safe denial, zero ACL reads
        CommandEnvelope forgedUser = validEnvelope with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5D03" };
        using (HttpResponseMessage forgedUserResponse = await harness.PostProcessWithReceiptIssuedForAsync(
            forgedUser,
            forgedUser with { UserId = "User_Forged_01ARZ3NDEKTSV4" },
            folderEvents,
            ["archive_folder"]).ConfigureAwait(true))
        {
            forgedUserResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            forgedUserResponse.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
            string forgedUserBody = await forgedUserResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            forgedUserBody.ShouldNotContain("User_Forged", Case.Insensitive);
            harness.PermissionProvider.Calls.ShouldBe(0);
        }

        // 4. Revoked / denied folder ACL -> 404 safe denial
        harness.PermissionProvider.Mode = FolderPermissionEvidenceStatus.Denied;
        using (HttpResponseMessage revokedResponse = await harness.PostProcessAsync(
            validEnvelope with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5D04" },
            folderEvents,
            ["archive_folder"]).ConfigureAwait(true))
        {
            revokedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            revokedResponse.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
            string revokedBody = await revokedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using JsonDocument problem = JsonDocument.Parse(revokedBody);
            problem.RootElement.TryGetProperty("events", out _).ShouldBeFalse();
            problem.RootElement.GetProperty("code").GetString().ShouldBe("folder_acl_denied");
            problem.RootElement.GetProperty("correlationId").GetString().ShouldBe(OpaqueCorrelationId);
            problem.RootElement.GetProperty("taskId").GetString().ShouldBe(OpaqueTaskId);
        }

        // 5. Authority unavailable -> 503 safe denial
        harness.PermissionProvider.Mode = FolderPermissionEvidenceStatus.Unavailable;
        using (HttpResponseMessage unavailableResponse = await harness.PostProcessAsync(
            validEnvelope with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5D05" },
            folderEvents,
            ["archive_folder"]).ConfigureAwait(true))
        {
            unavailableResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            unavailableResponse.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
        }

        // 6. C05 ConfigureProviderBinding with non-reserved or tenant AggregateId -> 403 before permission lookup
        harness.PermissionProvider.Mode = FolderPermissionEvidenceStatus.Allowed;
        harness.PermissionProvider.Calls = 0;
        byte[] c05Payload = SerializeDecorated(new ContractsConfigureProviderBinding(
            "forgejo",
            CapabilityProfileRef,
            CredentialRef,
            ProviderBindingRef,
            OpaqueIdempotencyKey));
        foreach (string badAggregateId in new[] { ProviderBindingRef, TenantId })
        {
            CommandEnvelope badC05 = ProcessHarness.CreateEnvelope(
                badAggregateId,
                FoldersServerModule.ConfigureProviderBindingCommandType,
                c05Payload,
                "01ARZ3NDEKTSV4RRFFQ69G5D06");
            using HttpResponseMessage badC05Response = await harness.PostProcessAsync(
                badC05,
                [],
                ["configure_provider_binding"]).ConfigureAwait(true);
            badC05Response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            harness.PermissionProvider.Calls.ShouldBe(0);
        }

        // State-store end-state check: folder remains active (not archived).
        harness.FoldFolder(OpaqueFolderId, folderEvents).LifecycleState.ShouldBe(FolderLifecycleState.Active);

        // 7. Valid HumanAuthorityReceipt for a principal denied by tenant-access projection -> 403 safe denial, 0 ACL reads
        const string deniedTenantUser = "User_Denied_01ARZ3NDEKTSV4";
        harness.PermissionProvider.Mode = FolderPermissionEvidenceStatus.Allowed;
        harness.PermissionProvider.Calls = 0;
        CommandEnvelope deniedTenantUserEnvelope = validEnvelope with
        {
            UserId = deniedTenantUser,
            MessageId = "01ARZ3NDEKTSV4RRFFQ69G5D07",
        };
        using (HttpResponseMessage tenantDeniedResponse = await harness.PostProcessAsync(
            deniedTenantUserEnvelope,
            folderEvents,
            ["archive_folder"],
            receiptTenant: TenantId,
            receiptSubject: deniedTenantUser).ConfigureAwait(true))
        {
            tenantDeniedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            tenantDeniedResponse.Headers.GetValues(AuthorizationDenialTransportResult.HeaderName).ShouldBe(["v1"]);
            string tenantDeniedBody = await tenantDeniedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            tenantDeniedBody.ShouldNotContain(deniedTenantUser, Case.Insensitive);
            harness.PermissionProvider.Calls.ShouldBe(0);
        }

        harness.FoldFolder(OpaqueFolderId, folderEvents).LifecycleState.ShouldBe(FolderLifecycleState.Active);
    }

    [Fact]
    public async Task DecoratedMutateFilesSupportsPutFileStreamMultiItemBatchRollbackStaleFencePrepareAuthAndFolderIdGuard()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var harnessScope = harness.ConfigureAwait(true);
        List<EventEnvelope> folderEvents = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        FolderState initial = harness.FoldFolder(OpaqueFolderId, folderEvents);

        // 1. Stale ExpectedLockFence rejects with AppendConflict BEFORE any upload/verify call
        harness.ContentProbe.Reset();
        byte[] staleBytes = Encoding.UTF8.GetBytes("stale-fence-must-not-upload");
        string staleHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(staleBytes));
        RejectedWirePayload staleFenceRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 99,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_StaleFence_01ARZ3ND",
                        PathMetadata: new ContractsPathMetadata("docs/stale.md", "stale.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: staleHash,
                        ByteLength: staleBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(staleBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        staleFenceRejected.Code.ShouldBe(nameof(FolderResultCode.AppendConflict));
        harness.ContentProbe.UploadCalls.ShouldBe(0);
        harness.ContentProbe.VerifyCalls.ShouldBe(0);
        harness.FoldFolder(OpaqueFolderId, folderEvents).ShouldBeEquivalentTo(initial);

        // 2. Mismatched payload folderId != envelope AggregateId rejects with ValidationFailed (C01 & C10)
        const string otherFolderId = "Opaque_OtherFl_01ARZ3NDEKT";
        RejectedWirePayload archiveMismatch = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.ArchiveFolderCommandType,
            SerializeDecorated(new ContractsArchiveFolder(
                ArchiveFolderArchiveReasonCode.CallerRequested,
                otherFolderId,
                OpaqueIdempotencyKey)),
            folderEvents,
            ["archive_folder"]).ConfigureAwait(true);
        archiveMismatch.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        harness.ContentProbe.Reset();
        RejectedWirePayload mutateMismatch = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_BadFolder_01ARZ3NDE",
                        PathMetadata: new ContractsPathMetadata("docs/badfolder.md", "badfolder.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: staleHash,
                        ByteLength: staleBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(staleBytes))),
                ],
                otherFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        mutateMismatch.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
        harness.ContentProbe.UploadCalls.ShouldBe(0);

        // 3. Invalid ownerFileId containing spaces or control characters rejects with ValidationFailed
        RejectedWirePayload badOwnerFileId = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_BadOwner_01ARZ3NDEK",
                        PathMetadata: new ContractsPathMetadata("docs/badowner.md", "badowner.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        OwnerFileId: "bad owner id\twith spaces",
                        ContentHashReference: staleHash,
                        ByteLength: staleBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(staleBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        badOwnerFileId.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));

        // 4. Mid-flight UnauthorizedAccessException during PrepareAsync rejects with FolderAclDenied
        harness.ContentProbe.Reset();
        harness.ContentProbe.ThrowUnauthorizedOnPrepare = true;
        try
        {
            RejectedWirePayload prepareDenied = await harness.ExecuteRejectedAsync(
                OpaqueFolderId,
                FoldersServerModule.MutateFilesCommandType,
                SerializeDecorated(new ContractsMutateFiles(
                    [
                        new FileMutationRequest(
                            ExpectedFileVersion: 0,
                            ExpectedLockFence: 1,
                            FileOperationKind: FileMutationRequestFileOperationKind.Add,
                            TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                            OperationId: "Opaque_PrepDeny_01ARZ3NDEK",
                            PathMetadata: new ContractsPathMetadata("docs/prepdeny.md", "prepdeny.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                            ContentHashReference: staleHash,
                            ByteLength: staleBytes.Length,
                            InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(staleBytes))),
                    ],
                    OpaqueFolderId,
                    OpaqueWorkspaceId,
                    OpaqueIdempotencyKey,
                    OpaqueTaskId)),
                folderEvents,
                ["mutate_files"]).ConfigureAwait(true);
            prepareDenied.Code.ShouldBe(nameof(FolderResultCode.FolderAclDenied));
        }
        finally
        {
            harness.ContentProbe.ThrowUnauthorizedOnPrepare = false;
        }

        harness.FoldFolder(OpaqueFolderId, folderEvents).ShouldBeEquivalentTo(initial);

        // 5. PutFileStream end-to-end (262_145 bytes staged upload verified via VerifyUploadAsync)
        harness.ContentProbe.Reset();
        byte[] streamBytes = new byte[262_145];
        Array.Fill(streamBytes, (byte)'S');
        string streamHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(streamBytes));
        ContentReceipt stagedReceipt = await harness.StageStreamedUploadAsync(
            OpaqueFolderId,
            OpaqueWorkspaceId,
            OpaqueTaskId,
            streamBytes,
            "application/octet-stream",
            "Opaque_StreamOp_01ARZ3NDEK").ConfigureAwait(true);

        (DomainServiceWireResult streamResult, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileStream,
                        OperationId: "Opaque_StreamOp_01ARZ3NDEK",
                        PathMetadata: new ContractsPathMetadata("docs/streamed.bin", "streamed.bin", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: streamHash,
                        ByteLength: streamBytes.Length,
                        StreamDescriptor: new PutFileStream(
                            "application/octet-stream",
                            streamBytes.Length,
                            streamBytes.Length,
                            stagedReceipt.Id,
                            streamHash,
                            PutFileStreamUploadMode.RequestBodyStream)),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5S01").ConfigureAwait(true);
        streamResult.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(WorkspaceFileMutationAccepted).FullName);
        harness.ContentProbe.UploadCalls.ShouldBe(0);
        harness.ContentProbe.VerifyCalls.ShouldBeGreaterThanOrEqualTo(1);
        FolderState afterStream = harness.FoldFolder(OpaqueFolderId, folderEvents);
        afterStream.ContentPublications.Values.ShouldContain(binding =>
            binding.ReceiptId == stagedReceipt.Id
            && binding.ContentHashReference == streamHash
            && binding.ByteLength == streamBytes.Length);

        // 6. Multi-item changes[] execution (2 items persisted atomically) + second-item failure rollback
        byte[] fileABytes = Encoding.UTF8.GetBytes("multi-item-file-a-content");
        string fileAHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(fileABytes));
        byte[] fileBBytes = Encoding.UTF8.GetBytes("multi-item-file-b-content");
        string fileBHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(fileBBytes));
        (DomainServiceWireResult batchResult, folderEvents) = await harness.ExecuteAcceptedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_BatchOp1_01ARZ3NDEK",
                        PathMetadata: new ContractsPathMetadata("docs/batch-a.md", "batch-a.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: fileAHash,
                        ByteLength: fileABytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(fileABytes))),
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_BatchOp2_01ARZ3NDEK",
                        PathMetadata: new ContractsPathMetadata("docs/batch-b.md", "batch-b.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: fileBHash,
                        ByteLength: fileBBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(fileBBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"],
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5S02").ConfigureAwait(true);
        batchResult.Events.Count.ShouldBe(2);
        FolderState afterBatch = harness.FoldFolder(OpaqueFolderId, folderEvents);
        afterBatch.ContentPublications.Count.ShouldBe(3);

        // Second-item failure rollback: item 1 is valid add, item 2 fails expectedFileVersion mismatch on existing batch-a.md
        harness.ContentProbe.Reset();
        int uploadsBefore = (await harness.ListUploadsAsync().ConfigureAwait(true)).Count;
        byte[] fileCBytes = Encoding.UTF8.GetBytes("must-roll-back-when-second-item-fails");
        string fileCHash = "hashref_" + Convert.ToHexStringLower(SHA256.HashData(fileCBytes));
        RejectedWirePayload rollbackRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [
                    new FileMutationRequest(
                        ExpectedFileVersion: 0,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Add,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_RollOp1_01ARZ3NDEKT",
                        PathMetadata: new ContractsPathMetadata("docs/rollback-c.md", "rollback-c.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: fileCHash,
                        ByteLength: fileCBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(fileCBytes))),
                    new FileMutationRequest(
                        ExpectedFileVersion: 99,
                        ExpectedLockFence: 1,
                        FileOperationKind: FileMutationRequestFileOperationKind.Change,
                        TransportOperation: FileMutationRequestTransportOperation.PutFileInline,
                        OperationId: "Opaque_RollOp2_01ARZ3NDEKT",
                        PathMetadata: new ContractsPathMetadata("docs/batch-a.md", "batch-a.md", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
                        ContentHashReference: fileBHash,
                        ByteLength: fileBBytes.Length,
                        InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(fileBBytes))),
                ],
                OpaqueFolderId,
                OpaqueWorkspaceId,
                OpaqueIdempotencyKey,
                OpaqueTaskId)),
            folderEvents,
            ["mutate_files"]).ConfigureAwait(true);
        rollbackRejected.Code.ShouldBe(nameof(FolderResultCode.AppendConflict));
        rollbackRejected.IdempotencyKey.ShouldBe(OpaqueIdempotencyKey);
        harness.ContentProbe.UploadCalls.ShouldBe(0);
        (await harness.ListUploadsAsync().ConfigureAwait(true)).Count.ShouldBe(uploadsBefore);
        harness.FoldFolder(OpaqueFolderId, folderEvents).ContentPublications.Count.ShouldBe(3);

        RejectedWirePayload pathRejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [InlineAdd("other/allowed.md", "opaque_path_first_operation", fileCBytes),
                    InlineAdd("Docs/case-alias.md", "opaque_path_second_operation", fileCBytes)],
                OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, OpaqueTaskId)),
            folderEvents, ["mutate_files"]).ConfigureAwait(true);

        pathRejected.Code.ShouldBe(nameof(FolderResultCode.PathPolicyDenied));
        harness.ContentProbe.UploadCalls.ShouldBe(0);
        (await harness.ListUploadsAsync().ConfigureAwait(true)).Count.ShouldBe(uploadsBefore);
        harness.FoldFolder(OpaqueFolderId, folderEvents).ContentPublications.Count.ShouldBe(3);
    }

    [Fact]
    public async Task DecoratedBatchExpiryAfterPreparationProducesNoPublication()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var cleanup = harness.ConfigureAwait(true);
        List<EventEnvelope> events = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        byte[] bytes = Encoding.UTF8.GetBytes("lease expiry fixture");
        harness.ContentProbe.AfterPrepare = () => harness.Clock.Advance(TimeSpan.FromHours(1));
        RejectedWirePayload rejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [InlineAdd("docs/expiry.txt", "Opaque_ExpireOp_01ARZ3NDEK", bytes)],
                OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, OpaqueTaskId)),
            events, ["mutate_files"]).ConfigureAwait(true);

        rejected.Code.ShouldBe(nameof(FolderResultCode.LockExpired));
        rejected.IdempotencyKey.ShouldBe(OpaqueIdempotencyKey);
        harness.FoldFolder(OpaqueFolderId, events).ContentPublications.ShouldBeEmpty();
        (await harness.ListVisibleContentAsync().ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestCancellationReachesDecoratedPerItemInspection()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var cleanup = harness.ConfigureAwait(true);
        List<EventEnvelope> events = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        var inspectionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inspectionCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool prepared = false;
        harness.ContentProbe.AfterPrepare = () => prepared = true;
        harness.ContentProbe.BeforeInspect = async token =>
        {
            using var registration = token.Register(() => inspectionCanceled.TrySetResult());
            inspectionEntered.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
        };

        CommandEnvelope command = ProcessHarness.CreateEnvelope(OpaqueFolderId, FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(
                [InlineAdd("docs/cancel.txt", "opaque_canceled_operation", "cancellation fixture"u8.ToArray())],
                OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, OpaqueTaskId)), "01ARZ3NDEKTSV4RRFFQ69G5F02");
        CancellationToken testToken = TestContext.Current.CancellationToken;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        Task<HttpResponseMessage> response = harness.PostProcessAsync(command, events, ["mutate_files"], cancellationToken: request.Token);
        await inspectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), testToken).ConfigureAwait(true);
        await request.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            using HttpResponseMessage unexpected = await response.ConfigureAwait(true);
        }).ConfigureAwait(true);
        await inspectionCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5), testToken).ConfigureAwait(true);

        prepared.ShouldBeFalse();
        harness.FoldFolder(OpaqueFolderId, events).ContentPublications.ShouldBeEmpty();
    }

    private static FileMutationRequest InlineAdd(string path, string operationId, byte[] bytes)
        => new(0, 1, FileMutationRequestFileOperationKind.Add, FileMutationRequestTransportOperation.PutFileInline,
            operationId, new ContractsPathMetadata(path, path[(path.LastIndexOf('/') + 1)..],
                PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
            ContentHashReference: "hashref_" + Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ByteLength: bytes.Length, InlineContent: new PutFileInline("text/plain", Convert.ToBase64String(bytes)));

    [Fact]
    public async Task DecoratedBatchAboveAggregateByteLimitStopsBeforeContentStore()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var cleanup = harness.ConfigureAwait(true);
        List<EventEnvelope> events = await harness.SeedLockedWorkspaceAsync(OpaqueFolderId, OpaqueWorkspaceId, OpaqueTaskId).ConfigureAwait(true);
        const int FileSize = 1_048_576;
        string hash = "hashref_" + new string('a', 64);
        FileMutationRequest[] changes = Enumerable.Range(1, 11).Select(index => new FileMutationRequest(
            0, 1, FileMutationRequestFileOperationKind.Add, FileMutationRequestTransportOperation.PutFileStream,
            $"opaque_batch_operation_{index}",
            new ContractsPathMetadata($"docs/file-{index}.txt", $"file-{index}.txt", PathMetadataPathPolicyClass.ContentAllowed, PathMetadataUnicodeNormalization.NFC),
            ContentHashReference: hash, ByteLength: FileSize,
            StreamDescriptor: new PutFileStream("text/plain", FileSize, FileSize, $"opaque_staged_receipt_{index}", hash, PutFileStreamUploadMode.RequestBodyStream))).ToArray();
        RejectedWirePayload rejected = await harness.ExecuteRejectedAsync(
            OpaqueFolderId,
            FoldersServerModule.MutateFilesCommandType,
            SerializeDecorated(new ContractsMutateFiles(changes, OpaqueFolderId, OpaqueWorkspaceId, OpaqueIdempotencyKey, OpaqueTaskId)),
            events, ["mutate_files"]).ConfigureAwait(true);

        rejected.Code.ShouldBe(nameof(FolderResultCode.ValidationFailed));
        harness.ContentProbe.UploadCalls.ShouldBe(0);
        harness.ContentProbe.VerifyCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CredentialShapedCorrelationIsRejectedBeforeFolderAccess()
    {
        ProcessHarness harness = await ProcessHarness.CreateAsync().ConfigureAwait(true);
        await using var cleanup = harness.ConfigureAwait(true);
        const string SensitiveShape = "ghp_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789";
        CommandEnvelope command = ProcessHarness.CreateEnvelope(OpaqueFolderId, FoldersServerModule.CreateFolderCommandType,
            SerializeDecorated(new ContractsCreateFolder(new FolderMetadata("Folder", SensitiveMetadataTier.TenantSensitive),
                OpaqueIdempotencyKey, OpaqueFolderId)), "01ARZ3NDEKTSV4RRFFQ69G5F02") with { CorrelationId = SensitiveShape };
        using HttpResponseMessage response = await harness.PostProcessAsync(command, [], ["create_folder"]).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotContain(SensitiveShape);
        harness.PermissionProvider.Calls.ShouldBe(0);
    }

    private static byte[] SerializeDecorated<T>(T command)
        => JsonSerializer.SerializeToUtf8Bytes(command, DecoratedJsonOptions);

    private static void AssertProcessResultEcho(string? resultPayload, string expectedCorrelationId, string expectedTaskId, string expectedIdempotencyKey)
    {
        resultPayload.ShouldNotBeNullOrWhiteSpace();
        using JsonDocument doc = JsonDocument.Parse(resultPayload);
        doc.RootElement.GetProperty("status").GetString().ShouldBe("accepted");
        doc.RootElement.GetProperty("correlationId").GetString().ShouldBe(expectedCorrelationId);
        doc.RootElement.GetProperty("taskId").GetString().ShouldBe(expectedTaskId);
        doc.RootElement.GetProperty("idempotencyKey").GetString().ShouldBe(expectedIdempotencyKey);
    }

    private sealed class ProcessHarness : IAsyncDisposable
    {
        private static int _rejectionSequence;
        private readonly string _receiptKey;
        private readonly WebApplication _app;
        private readonly FolderEventReplay _replay;
        private readonly RecordingDaprClient _dapr;

        public HttpClient Client { get; }
        public MutableClock Clock { get; }
        public ControlledPermissionEvidenceProvider PermissionProvider { get; }
        public ContentStoreProbe ContentProbe { get; }

        private ProcessHarness(
            WebApplication app,
            HttpClient client,
            FolderEventReplay replay,
            string receiptKey,
            MutableClock clock,
            ControlledPermissionEvidenceProvider permissionProvider,
            RecordingDaprClient dapr,
            ContentStoreProbe contentProbe)
        {
            _app = app;
            Client = client;
            _replay = replay;
            _receiptKey = receiptKey;
            Clock = clock;
            PermissionProvider = permissionProvider;
            _dapr = dapr;
            ContentProbe = contentProbe;
        }

        public static async Task<ProcessHarness> CreateAsync()
        {
            string receiptKey = Convert.ToBase64String(Enumerable.Repeat((byte)73, 32).ToArray());
            MutableClock clock = new(DateTimeOffset.UtcNow);
            ControlledPermissionEvidenceProvider permissionProvider = new();
            RecordingDaprClient dapr = new() { PersistTypedState = true };
            ContentStoreProbe contentProbe = new();

            InMemoryFolderTenantAccessProjectionStore tenants = new();
            await tenants.SaveAsync(new FolderTenantAccessProjection
            {
                TenantId = TenantId,
                Enabled = true,
                Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(StringComparer.Ordinal)
                {
                    [OpaqueUserId] = new(OpaqueUserId, "TenantOwner"),
                },
                Watermark = 1,
                LastEventTimestamp = clock.GetUtcNow(),
                ProjectionWatermark = $"{TenantId}:1",
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            WebApplicationBuilder builder = FoldersProductionTestHost.CreateBuilder(
                new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Authority"] = null,
                    ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                    ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
                    ["Authentication:JwtBearer:Audience"] = "folders",
                    ["Authentication:JwtBearer:SigningKey"] = WorkloadKey,
                    ["Authentication:HumanAuthorityReceipt:SigningKey"] = receiptKey,
                    ["Folders:WorkspaceFilePolicy:Version"] = "1.1.0",
                    ["Folders:WorkspaceFilePolicy:ProtectionMode"] = "non_confidential",
                    ["Folders:WorkspaceFilePolicy:Includes:0"] = "**",
                    [$"Folders:ProviderBindings:Profiles:{CapabilityProfileRef}:ManagedTenantId"] = TenantId,
                    [$"Folders:ProviderBindings:Profiles:{CapabilityProfileRef}:ProviderKind"] = "forgejo",
                },
                environmentName: Microsoft.Extensions.Hosting.Environments.Development,
                useCallbackCatalog: false);

            builder.Services.Configure<RouteCatalogHostOptions>(options => options.Identity = new(
                "folders",
                "folders",
                Convert.ToHexStringLower(SHA256.HashData("mcpcli-process-source-fixture"u8)),
                Convert.ToHexStringLower(SHA256.HashData("isolated-test-server"u8)),
                Convert.ToHexStringLower(SHA256.HashData("mcpcli-v2-process-fixture"u8))));
            builder.Services.AddSingleton<AllCommandsRouteCatalog>(services => new(
                services,
                () => services.GetRequiredService<EventStoreDomainServiceEndpointSource>().Endpoints!.DataSources
                    .SelectMany(source => source.Endpoints)));
            builder.Services.RemoveAll<IRouteCatalogAuthority>();
            builder.Services.AddSingleton<IRouteCatalogAuthority>(services => services.GetRequiredService<AllCommandsRouteCatalog>());

            builder.Services.RemoveAll<TimeProvider>();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.RemoveAll<DaprClient>();
            builder.Services.AddSingleton<DaprClient>(dapr);
            builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(
                EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
                "Authentication:Workload",
                static (options, _) =>
                {
                    options.Audience = "folders";
                    options.AllowedCallers.Add("eventstore");
                });
            builder.Services.AddEventStoreWorkloadPolicies(
                EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
                [EventStoreWorkloadOperations.DomainServiceProcess, EventStoreWorkloadOperations.DomainServiceProject]);
            builder.Services.AddSingleton<HumanAuthorityReceiptVerifier>();
            builder.Services.RemoveAll<IEventStoreAuthorizationValidator>();
            builder.Services.AddSingleton<IEventStoreAuthorizationValidator, VerifiedReceiptEventStoreAuthorizationValidator>();
            builder.Services.RemoveAll<IFolderTenantAccessProjectionStore>();
            builder.Services.AddSingleton<IFolderTenantAccessProjectionStore>(tenants);
            builder.Services.RemoveAll<IFolderPermissionEvidenceProvider>();
            builder.Services.AddSingleton<IFolderPermissionEvidenceProvider>(permissionProvider);
            builder.Services.RemoveAll<IFolderArchiveAclEvidenceProvider>();
            builder.Services.AddSingleton<IFolderArchiveAclEvidenceProvider>(new AllowingArchiveAclEvidenceProvider());
            builder.Services.RemoveAll<IFolderArchivePolicyEvidenceProvider>();
            builder.Services.AddSingleton<IFolderArchivePolicyEvidenceProvider>(new AllowingArchivePolicyEvidenceProvider());
            builder.Services.RemoveAll<IRepositoryCreationReadinessValidator>();
            builder.Services.AddSingleton<IRepositoryCreationReadinessValidator>(new ReadyRepositoryCreationValidator());
            builder.Services.RemoveAll<IRepositoryBindingReadinessValidator>();
            builder.Services.AddSingleton<IRepositoryBindingReadinessValidator>(new ReadyRepositoryBindingValidator());
            builder.Services.RemoveAll<IBranchRefPolicyReadinessValidator>();
            builder.Services.AddSingleton<IBranchRefPolicyReadinessValidator>(new ReadyBranchRefPolicyValidator());
            builder.Services.RemoveAll<IWorkspacePreparationReadinessValidator>();
            builder.Services.AddSingleton<IWorkspacePreparationReadinessValidator>(new ReadyWorkspacePreparationValidator());
            builder.Services.RemoveAll<IWorkspaceCommitReadinessValidator>();
            builder.Services.AddSingleton<IWorkspaceCommitReadinessValidator>(new ReadyWorkspaceCommitValidator());
            builder.Services.RemoveAll<IWorkspaceCommitExecutor>();
            builder.Services.AddSingleton<IWorkspaceCommitExecutor>(new SucceedingCommitExecutor());
            builder.Services.RemoveAll<IProviderReadinessBindingReader>();
            builder.Services.AddSingleton<IProviderReadinessBindingReader>(new StaticProviderBindingReader());
            builder.Services.RemoveAll<IProviderCapabilityResolver>();
            builder.Services.AddSingleton<IProviderCapabilityResolver>(new RecordingProviderCapabilityResolver(FakeGitProvider.GitHubLike()));
            builder.Services.RemoveAll<IAuthoritativeContentStore>();
            builder.Services.AddScoped<IAuthoritativeContentStore>(services => new ObservingAuthoritativeContentStore(
                new DaprAuthoritativeContentStore(
                    dapr,
                    services.GetRequiredService<IContentAuthority>(),
                    services.GetRequiredService<IOptions<AuthoritativeContentOptions>>(),
                    clock),
                contentProbe));

            WebApplication app = builder.Build();
            app.UseFoldersServerPipeline();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            HttpClient client = app.GetTestClient();
            DateTime issued = DateTime.UtcNow;
            string assertion = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
                new SecurityTokenDescriptor
                {
                    Issuer = "hexalith-dev",
                    Audience = "folders",
                    IssuedAt = issued,
                    NotBefore = issued,
                    Expires = issued.AddMinutes(5),
                    Claims = new Dictionary<string, object>
                    {
                        [RouteCatalogSurfacePolicy.ClaimType] = "service",
                        [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
                        [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProcess,
                    },
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(WorkloadKey)),
                        SecurityAlgorithms.HmacSha256),
                });
            CommittedRouteCatalogSnapshot catalog = app.Services.GetRequiredService<AllCommandsRouteCatalog>().Snapshot;
            client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, catalog.RootDigest);
            client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, catalog.Epoch.ToString(CultureInfo.InvariantCulture));
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "test-sidecar-token");
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "eventstore");

            return new ProcessHarness(
                app,
                client,
                app.Services.GetRequiredService<FolderEventReplay>(),
                receiptKey,
                clock,
                permissionProvider,
                dapr,
                contentProbe);
        }

        public Task<ContentReceipt> StageStreamedUploadAsync(
            string folderId,
            string workspaceId,
            string taskId,
            byte[] bytes,
            string mediaType,
            string operationId)
        {
            DaprAuthoritativeContentStore seedStore = new(
                _dapr,
                new WorkspaceContentProductionSeedAuthority(),
                _app.Services.GetRequiredService<IOptions<AuthoritativeContentOptions>>(),
                Clock);
            ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, OpaqueUserId)], "test"));
            ContentScope scope = new(TenantId, FoldersServerModule.DomainName, folderId, workspaceId);
            ContentUploadRequest request = new(
                scope,
                taskId,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                bytes.LongLength,
                mediaType,
                operationId);
            return seedStore.UploadAsync(principal, request, new MemoryStream(bytes, writable: false), TestContext.Current.CancellationToken);
        }

        public Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync()
            => CreateContentObserver().ListUploadsAsync(ContentObserverPrincipal(), ContentObserverScope(), TestContext.Current.CancellationToken);

        public Task<IReadOnlyList<ContentBinding>> ListVisibleContentAsync()
            => CreateContentObserver().ListAsync(ContentObserverPrincipal(), ContentObserverScope(), TestContext.Current.CancellationToken);

        private DaprAuthoritativeContentStore CreateContentObserver() => new(
            _dapr, new WorkspaceContentProductionSeedAuthority(accepted: false),
            _app.Services.GetRequiredService<IOptions<AuthoritativeContentOptions>>(), Clock);

        private static ClaimsPrincipal ContentObserverPrincipal() => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, OpaqueUserId)], "test"));

        private static ContentScope ContentObserverScope() => new(TenantId, FoldersServerModule.DomainName, OpaqueFolderId, OpaqueWorkspaceId);

        public static CommandEnvelope CreateEnvelope(
            string aggregateId,
            string commandType,
            byte[] payload,
            string messageId,
            string? taskIdOverride = null)
            => new(
                messageId,
                TenantId,
                FoldersServerModule.DomainName,
                aggregateId,
                commandType,
                payload,
                OpaqueCorrelationId,
                null,
                OpaqueUserId,
                new Dictionary<string, string> { ["taskId"] = taskIdOverride ?? OpaqueTaskId });

        public static DomainServiceCurrentState? StateEnvelope(List<EventEnvelope> events)
            => events.Count == 0 ? null : new DomainServiceCurrentState(null, events, 0, events.Count);

        public Task<HttpResponseMessage> PostProcessWithReceiptIssuedForAsync(
            CommandEnvelope sentCommand,
            CommandEnvelope receiptCommand,
            List<EventEnvelope> currentEvents,
            string[] permissions)
        {
            string receipt = HumanAuthorityReceiptCodec.Issue(
                receiptCommand,
                receiptCommand.TenantId,
                receiptCommand.UserId,
                permissions,
                _receiptKey,
                Clock.GetUtcNow());
            return Client.PostAsJsonAsync(
                FoldersServerModule.ProcessRoute,
                new DomainServiceRequest(sentCommand, CurrentState: StateEnvelope(currentEvents))
                {
                    HumanAuthorityReceipt = receipt,
                },
                TestContext.Current.CancellationToken);
        }

        public Task<HttpResponseMessage> PostProcessAsync(
            CommandEnvelope command,
            List<EventEnvelope> currentEvents,
            string[] permissions,
            string? receiptTenant = null,
            string? receiptSubject = null,
            CancellationToken? cancellationToken = null)
        {
            string receipt = HumanAuthorityReceiptCodec.Issue(
                command,
                receiptTenant ?? TenantId,
                receiptSubject ?? OpaqueUserId,
                permissions,
                _receiptKey,
                Clock.GetUtcNow());
            return Client.PostAsJsonAsync(
                FoldersServerModule.ProcessRoute,
                new DomainServiceRequest(command, CurrentState: StateEnvelope(currentEvents))
                {
                    HumanAuthorityReceipt = receipt,
                },
                cancellationToken ?? TestContext.Current.CancellationToken);
        }

        public async Task<(DomainServiceWireResult Result, List<EventEnvelope> UpdatedEvents)> ExecuteAcceptedAsync(
            string aggregateId,
            string commandType,
            byte[] payload,
            List<EventEnvelope> currentEvents,
            string[] permissions,
            string messageId,
            string? taskIdOverride = null)
        {
            CommandEnvelope envelope = CreateEnvelope(aggregateId, commandType, payload, messageId, taskIdOverride);
            using HttpResponseMessage response = await PostProcessAsync(envelope, currentEvents, permissions).ConfigureAwait(true);
            string raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
            DomainServiceWireResult wire = JsonSerializer.Deserialize<DomainServiceWireResult>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                .ShouldNotBeNull();
            wire.IsRejection.ShouldBeFalse(raw);

            List<EventEnvelope> updated = [.. currentEvents];
            foreach (var entry in wire.Events)
            {
                updated.Add(new EventEnvelope(
                    new EventMetadata(
                        messageId,
                        aggregateId,
                        "folder",
                        TenantId,
                        FoldersServerModule.DomainName,
                        updated.Count + 1,
                        updated.Count + 1,
                        Clock.GetUtcNow(),
                        OpaqueCorrelationId,
                        messageId,
                        OpaqueUserId,
                        "folders",
                        entry.EventTypeName,
                        1,
                        "json"),
                    entry.Payload,
                    null));
            }

            return (wire, updated);
        }

        public async Task<RejectedWirePayload> ExecuteRejectedAsync(
            string aggregateId,
            string commandType,
            byte[] payload,
            List<EventEnvelope> currentEvents,
            string[] permissions,
            string? taskIdOverride = null)
        {
            int sequence = Interlocked.Increment(ref _rejectionSequence);
            string messageId = "01ARZ3NDEKTSV4RRFFQ69R" + sequence.ToString("D4", CultureInfo.InvariantCulture);
            CommandEnvelope envelope = CreateEnvelope(aggregateId, commandType, payload, messageId, taskIdOverride);
            using HttpResponseMessage response = await PostProcessAsync(envelope, currentEvents, permissions).ConfigureAwait(true);
            string raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
            DomainServiceWireResult wire = JsonSerializer.Deserialize<DomainServiceWireResult>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                .ShouldNotBeNull();
            wire.IsRejection.ShouldBeTrue(raw);
            var entry = wire.Events.ShouldHaveSingleItem();
            entry.EventTypeName.ShouldBe(typeof(FolderCommandRejected).FullName);
            JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter<FolderResultCode>());
            return JsonSerializer.Deserialize<RejectedWirePayload>(entry.Payload, options)
                .ShouldNotBeNull();
        }

        public Task<List<EventEnvelope>> SeedLockedWorkspaceAsync(string folderId, string workspaceId, string taskId)
        {
            DateTimeOffset now = Clock.GetUtcNow();
            List<EventEnvelope> events =
            [
                BuildStoredEvent(folderId, 1, new FolderCreated(TenantId, TenantId, folderId, "Seeded", null, null, [], FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, OpaqueUserId, OpaqueCorrelationId, taskId, "seed-1", "fp-1", now)),
                BuildStoredEvent(folderId, 2, new RepositoryBindingRequested(TenantId, TenantId, folderId, RepositoryBindingId, ProviderBindingRef, RepositoryProfileRef, BranchPolicyRef, OpaqueUserId, OpaqueCorrelationId, taskId, "seed-2", "fp-2", now)),
                BuildStoredEvent(folderId, 3, new RepositoryBound(TenantId, TenantId, folderId, RepositoryBindingId, ProviderBindingRef, OpaqueCorrelationId, taskId, "seed-3", "fp-3", now)),
                BuildStoredEvent(folderId, 4, new FolderWorkspaceLifecycleEventRecorded(TenantId, TenantId, folderId, workspaceId, FolderWorkspaceLifecycleEvent.WorkspacePrepared, null, "prepare", OpaqueCorrelationId, taskId, "seed-4", "fp-4", now)),
                BuildStoredEvent(folderId, 5, new WorkspaceLockAcquired(TenantId, TenantId, folderId, workspaceId, FolderWorkspaceLifecycleEvent.WorkspaceLocked, "Opaque_LockId_01ARZ3NDEKTS", "exclusive_write", 1800, taskId, now, now, now.AddMinutes(30), "lease_until_expiry", OpaqueUserId, OpaqueCorrelationId, taskId, "seed-5", "fp-5", now) { LockFence = 1 }),
            ];
            return Task.FromResult(events);
        }

        public EventEnvelope BuildStoredEvent<T>(string aggregateId, long sequence, T payload)
            => new(
                new EventMetadata(
                    $"seed-{sequence}",
                    aggregateId,
                    "folder",
                    TenantId,
                    FoldersServerModule.DomainName,
                    sequence,
                    sequence,
                    Clock.GetUtcNow(),
                    OpaqueCorrelationId,
                    $"seed-{sequence}",
                    OpaqueUserId,
                    "folders",
                    typeof(T).FullName!,
                    1,
                    "json"),
                JsonSerializer.SerializeToUtf8Bytes(payload, EventStorePayloadSerialization.Options),
                null);

        public FolderState FoldFolder(string folderId, List<EventEnvelope> events)
            => _replay.FoldFolder(StateEnvelope(events), TenantId, folderId);

        public OrganizationState FoldOrganization(string aggregateId, List<EventEnvelope> events)
            => _replay.FoldOrganization(StateEnvelope(events), TenantId, aggregateId);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync().ConfigureAwait(true);
            _dapr.Dispose();
        }
    }

    private sealed record RejectedWirePayload(
        string Code,
        string CommandType,
        string? ManagedTenantId,
        string? OrganizationId,
        string? FolderId,
        string? ActorPrincipalId,
        string? CorrelationId,
        string? TaskId,
        string? IdempotencyKey);

    private sealed class ContentStoreProbe
    {
        public int UploadCalls { get; private set; }

        public int VerifyCalls { get; private set; }

        public bool ThrowUnauthorizedOnPrepare { get; set; }

        public Action? AfterPrepare { get; set; }

        public Func<CancellationToken, Task>? BeforeInspect { get; set; }

        public void RecordUpload() => UploadCalls++;

        public void RecordVerify() => VerifyCalls++;

        public void Reset()
        {
            UploadCalls = 0;
            VerifyCalls = 0;
            ThrowUnauthorizedOnPrepare = false;
            AfterPrepare = null;
            BeforeInspect = null;
        }
    }

    private sealed class ObservingAuthoritativeContentStore(
        IAuthoritativeContentStore inner,
        ContentStoreProbe probe) : IAuthoritativeContentStore
    {
        public Task<ContentReceipt> UploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, Stream bytes, CancellationToken cancellationToken = default)
        {
            probe.RecordUpload();
            return inner.UploadAsync(principal, request, bytes, cancellationToken);
        }

        public Task<ContentReceipt> VerifyUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default)
        {
            probe.RecordVerify();
            return inner.VerifyUploadAsync(principal, request, cancellationToken);
        }

        public async Task<ContentReadResult> InspectUploadAsync(ClaimsPrincipal principal, ContentUploadVerification request, CancellationToken cancellationToken = default)
        {
            if (probe.BeforeInspect is { } beforeInspect)
            {
                await beforeInspect(cancellationToken).ConfigureAwait(false);
            }

            return await inner.InspectUploadAsync(principal, request, cancellationToken).ConfigureAwait(false);
        }

        public async Task PrepareAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken cancellationToken = default)
        {
            if (probe.ThrowUnauthorizedOnPrepare)
            {
                throw new UnauthorizedAccessException("Simulated mid-flight authorization denial during PrepareAsync.");
            }

            await inner.PrepareAsync(principal, request, cancellationToken).ConfigureAwait(false);
            probe.AfterPrepare?.Invoke();
        }

        public Task<ContentPublicationOutcome> RecoverAsync(ClaimsPrincipal principal, ContentScope scope, string commandId, CancellationToken cancellationToken = default)
            => inner.RecoverAsync(principal, scope, commandId, cancellationToken);

        public Task<ContentReadResult> ReadAsync(ClaimsPrincipal principal, ContentReadRequest request, CancellationToken cancellationToken = default)
            => inner.ReadAsync(principal, request, cancellationToken);

        public Task<IReadOnlyList<ContentBinding>> ListAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default)
            => inner.ListAsync(principal, scope, cancellationToken);

        public Task<IReadOnlyList<ContentUploadStatus>> ListUploadsAsync(ClaimsPrincipal principal, ContentScope scope, CancellationToken cancellationToken = default)
            => inner.ListUploadsAsync(principal, scope, cancellationToken);

        public Task SetHoldAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, bool legalHold, CancellationToken cancellationToken = default)
            => inner.SetHoldAsync(principal, scope, receiptId, legalHold, cancellationToken);

        public Task AbandonUploadAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default)
            => inner.AbandonUploadAsync(principal, scope, receiptId, cancellationToken);

        public Task<bool> ApplyDispositionAsync(ClaimsPrincipal principal, ContentScope scope, string receiptId, CancellationToken cancellationToken = default)
            => inner.ApplyDispositionAsync(principal, scope, receiptId, cancellationToken);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class ControlledPermissionEvidenceProvider : IFolderPermissionEvidenceProvider
    {
        public FolderPermissionEvidenceStatus Mode { get; set; } = FolderPermissionEvidenceStatus.Allowed;

        public int Calls { get; set; }

        public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(
            FolderPermissionEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Mode == FolderPermissionEvidenceStatus.Allowed
                ? FolderPermissionEvidenceResult.Allowed("perm_watermark_v1", organizationId: TenantId)
                : FolderPermissionEvidenceResult.FromStatus(Mode, Mode == FolderPermissionEvidenceStatus.Denied ? "perm_watermark_v1" : null));
        }
    }

    private sealed class AllowingArchiveAclEvidenceProvider : IFolderArchiveAclEvidenceProvider
    {
        public Task<FolderArchiveAclEvidence> GetEvidenceAsync(
            Hexalith.Folders.Aggregates.Folder.ArchiveFolder command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderArchiveAclEvidence.Allowed(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                command.ActorPrincipalId));
    }

    private sealed class AllowingArchivePolicyEvidenceProvider : IFolderArchivePolicyEvidenceProvider
    {
        public Task<FolderArchivePolicyEvidence> GetEvidenceAsync(
            Hexalith.Folders.Aggregates.Folder.ArchiveFolder command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FolderArchivePolicyEvidence.Allowed(
                command.ManagedTenantId,
                command.OrganizationId,
                command.FolderId,
                "policy_v1"));
    }

    private sealed class ReadyRepositoryCreationValidator : IRepositoryCreationReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReadyResult(request));
    }

    private sealed class ReadyRepositoryBindingValidator : IRepositoryBindingReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReadyResult(request));
    }

    private sealed class ReadyBranchRefPolicyValidator : IBranchRefPolicyReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReadyResult(request));
    }

    private sealed class ReadyWorkspacePreparationValidator : IWorkspacePreparationReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReadyResult(request));
    }

    private sealed class ReadyWorkspaceCommitValidator : IWorkspaceCommitReadinessValidator
    {
        public Task<ProviderReadinessValidationResult> ValidateAsync(
            ProviderReadinessValidationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReadyResult(request));
    }

    private sealed class StaticProviderBindingReader : IProviderReadinessBindingReader
    {
        public Task<OrganizationProviderBinding?> GetAsync(
            ProviderReadinessBindingReadRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<OrganizationProviderBinding?>(new(
                TenantId,
                TenantId,
                ProviderBindingRef,
                "github",
                CredentialRef,
                OrganizationProviderBindingPolicy.Empty,
                OrganizationProviderBindingPolicy.Empty,
                OpaqueCorrelationId,
                OpaqueTaskId,
                OpaqueIdempotencyKey,
                "binding-fp",
                "configured",
                DateTimeOffset.UtcNow));
    }

    private sealed class SucceedingCommitExecutor : IWorkspaceCommitExecutor
    {
        public Task<WorkspaceCommitExecutionResult> CommitAsync(
            WorkspaceCommitExecutionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WorkspaceCommitExecutionResult.Succeeded(
                "commitref_0123456789abcdef",
                "provider_corr_0123456789"));
    }

    private sealed class AllCommandsRouteCatalog(IServiceProvider services, Func<IEnumerable<Endpoint>> endpoints) : IRouteCatalogAuthority
    {
        private CommittedRouteCatalogSnapshot? _snapshot;

        public CommittedRouteCatalogSnapshot Snapshot => _snapshot ??= CreateSnapshot();

        public Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new RouteCatalogDispatchLease(Snapshot, static () => Task.CompletedTask));
        }

        public Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RouteCatalogEnvelope envelope = Snapshot.Envelope;
            return Task.FromResult(new RouteCatalogGenerationReadback(
                Snapshot.Epoch,
                envelope.Generation,
                Snapshot.RootDigest,
                envelope.RouteFacetDigest,
                envelope.IdempotencyFacetDigest,
                envelope.Profile,
                [],
                true));
        }

        public Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default)
            => Task.FromException(new RouteCatalogUnavailableException("Immutable fixture catalog."));

        public Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default)
            => Task.FromException<RouteCatalogHostAttestation>(new RouteCatalogUnavailableException("Immutable fixture catalog."));

        public Task CommitAsync(string attemptId, CancellationToken cancellationToken = default)
            => Task.FromException(new RouteCatalogUnavailableException("Immutable fixture catalog."));

        public Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default)
            => Task.FromException(new RouteCatalogUnavailableException("Immutable fixture catalog."));

        public Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default)
            => Task.FromException(new RouteCatalogUnavailableException("Immutable fixture catalog."));

        private CommittedRouteCatalogSnapshot CreateSnapshot()
        {
            using IServiceScope scope = services.CreateScope();
            IServiceProvider local = scope.ServiceProvider;
            DomainProjectionIdentityOptions identity = local.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
            List<RouteContractDeclaration> declarations = [];
            List<RouteCatalogEntry> targets = [];
            List<RouteOperationContract> operations = [];
            List<IdempotencyCatalogEntry> admission = [];

            foreach (CommandIntentDeclaration command in FoldersRuntimeCatalogDeclarations.Commands)
            {
                RouteContractDeclaration declaration = new(
                    command.Route.RouteId,
                    "command",
                    FoldersServerModule.DomainName,
                    command.Route.MessageType,
                    null,
                    "v1",
                    1,
                    "workload",
                    EventStoreOperationIdentifiers.DomainServiceProcess,
                    null,
                    [],
                    new(typeof(FolderDomainProcessor).FullName!, "exact", [command.Route.MessageType!], "domain-service", null, []));
                RouteOperationContract operation = RouteOperationSchema.Create(
                    declaration,
                    typeof(DomainServiceRequest),
                    typeof(DomainServiceWireResult),
                    "internal",
                    "agent-eligible",
                    JsonSerializerOptions.Web);
                declarations.Add(declaration);
                operations.Add(operation);
                targets.Add(new(
                    command.Route.RouteId,
                    1,
                    RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                    identity.AppId,
                    "process",
                    identity.ServiceVersion,
                    declaration.CredentialKind,
                    declaration.Operation,
                    false,
                    TenantId,
                    operation.Digest));
                IdempotencyCatalogEntry entry = new(
                    command.Route.RouteId,
                    1,
                    FoldersServerModule.DomainName,
                    command.Route.MessageType!,
                    "hexalith-folders",
                    command.OperationId,
                    "folders-contract-spine-v2",
                    command.DescriptorSchema,
                    RouteCatalogCodec.ComputeDescriptorDigest(command.DescriptorSchema),
                    command.RetentionTier == IdempotencyReplayRetentionTier.Commit ? "commit" : "mutation",
                    "fixture-v1",
                    ["fixture-v1"],
                    "fixture-keys",
                    "fixture-idempotency-ring",
                    "fixture-v1",
                    [new("fixture-v1", "fixture-v1")],
                    "eventstore",
                    string.Empty);
                admission.Add(entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) });
            }

            RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new(
                3,
                1,
                "Production",
                false,
                declarations,
                targets,
                admission,
                string.Empty,
                string.Empty,
                operations,
                []));
            return new(
                RouteCatalogCodec.Encode(envelope),
                RouteCatalogCodec.ComputeRootDigest(envelope),
                1,
                RouteCatalogDomainObservation.Auxiliary(local, endpoints()),
                RouteCatalogDomainObservation.Capabilities(local),
                identity.AppId);
        }
    }

    private static ProviderReadinessValidationResult ReadyResult(ProviderReadinessValidationRequest request)
        => new(
            ProviderReadinessResultCode.Allowed,
            "ready",
            "success",
            "none",
            false,
            null,
            "none",
            request.CorrelationId ?? OpaqueCorrelationId,
            request.ProviderBindingRef,
            request.ProviderBindingRef,
            RepositoryProfileRef,
            null,
            new("snapshot_per_task", DateTimeOffset.UtcNow, $"{TenantId}:1", false),
            ProviderFailureCategory.None,
            "success");
}
