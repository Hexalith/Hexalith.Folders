using System.Reflection;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Helpers;

public sealed class LegacyFingerprintCompatibilityTests
{
    // Golden values were computed with the original helper sources at
    // 591194c7766c72d8bd51398d66f814a6a1fca922, before the 11.5 extraction.
    [Fact]
    public void CreateFolderFingerprintKeepsNormalizedMetadataAndTagCount()
    {
        CreateFolder command = new(
            "tenant-a", "organization-b", "folder-c", " Cafe\u0301 ", " Description ", " Label ",
            ["alpha", "zeta"], "actor-d", "correlation-e", "task-f", "idempotency-g", "payload-h");

        Fingerprint(command, typeof(CreateFolder), [typeof(IReadOnlyList<string>)], [command.Tags])
            .ShouldBe("6af7a00eb661a5b6e24c3a6afdaddbe95d867694a3d2cc6bc9eec7e2139da666");
    }

    [Fact]
    public void CreateFolderValidationFingerprintsDeduplicatedOrdinalTags()
    {
        CreateFolder canonical = new(
            "tenant-a", "tenant-a", "folder-c", "Folder C", "Description", "folder-c",
            ["alpha", "zeta"], "actor-d", "correlation-e", "task-f", "idempotency-g", "payload-h");

        FolderCommandValidationResult result = FolderCommandValidator.Validate(canonical with { Tags = ["zeta", "alpha", "zeta"] });

        result.IsAccepted.ShouldBeTrue(result.Code.ToString());
        result.CanonicalTags.ShouldBe(["alpha", "zeta"], ignoreOrder: false);
        result.IdempotencyFingerprint.ShouldBe("3027ec500b36e39624272f522951041a108dd3af54c42bb19146295fc3dc99de");
        FolderCommandValidator.Validate(canonical).IdempotencyFingerprint.ShouldBe(result.IdempotencyFingerprint);
    }

    [Fact]
    public void ArchiveFolderFingerprintKeepsOrderedClientTenantEntries()
    {
        ArchiveFolder command = ArchiveCommand();

        Fingerprint(command, typeof(ArchiveFolder), [typeof(FolderArchiveReasonCode)], [FolderArchiveReasonCode.PolicyRetention])
            .ShouldBe("de5f070e1c413a2ed24eaa0d63dfc0bc1921d607c88e87f1fd9eb9e36cdcb9c0");
    }

    [Fact]
    public void RepositoryBackedFolderFingerprintKeepsItsFieldOrder()
    {
        CreateRepositoryBackedFolder command = new(
            "tenant-a", "organization-b", "folder-c", "v1", "binding-d", "provider-e", "profile-f", "policy-g",
            " Cafe\u0301 ", "scope-h", "actor-i", "correlation-j", "task-k", "idempotency-l", "payload-m");

        Fingerprint(command, typeof(CreateRepositoryBackedFolder)).ShouldBe("a96a298e2ae5a0b7775f97d5c1bb4a7ccef20640cb58793c8b8e2dbc491537d5");
    }

    [Fact]
    public void RepositoryBindingFingerprintKeepsItsFieldOrder()
    {
        Fingerprint(BindCommand(), typeof(BindRepository)).ShouldBe("4961bd2ba48b7873a5e33f661df1dd010874b3392c82cca791f122967a8085d2");
    }

    [Fact]
    public void ExternalRepositoryReferenceFingerprintKeepsItsCommandFieldMapping()
    {
        FolderCommandValidator.ExternalRepositoryRefFingerprint(BindCommand())
            .ShouldBe("63d0dc2178d2f589794e28f518234d6c7b4753553eccc91457351fe4b5c59f7e");
    }

    [Fact]
    public void BranchRefPolicyFingerprintKeepsCollectionCountsAndOrdering()
    {
        ConfigureBranchRefPolicy command = new(
            "tenant-a", "organization-b", "folder-c", "v1", "binding-d", "policy-e", "refs/heads/main",
            ["refs/heads/main", "refs/heads/feature/*"], ["refs/heads/release/*"],
            "actor-f", "correlation-g", "task-h", "idempotency-i", "payload-j");

        Fingerprint(command, typeof(ConfigureBranchRefPolicy)).ShouldBe("250c79b18d568802f49adc6651a53657d2689bb959631b7f86b95a43e6ac2dc9");
    }

    [Fact]
    public void WorkspacePreparationFingerprintKeepsItsFieldOrder()
    {
        PrepareWorkspace command = new(
            "tenant-a", "organization-b", "folder-c", "v1", "workspace-d", "binding-e", "branch-policy-f",
            "workspace-policy-g", "actor-h", "correlation-i", "task-j", "idempotency-k", "payload-l");

        Fingerprint(command, typeof(PrepareWorkspace)).ShouldBe("7c836d7edb6da23d0c7acd8dc12c63387f2e2b75c2cda27c079b6e0667694f01");
    }

    [Fact]
    public void WorkspaceLockFingerprintKeepsItsIntegerLeaseEncoding()
    {
        Fingerprint(LockCommand(), typeof(LockWorkspace)).ShouldBe("bfefe93e45692caaa12addc5319e62ea2e3586c2f0b7fabc02ff5df020a10ba2");
    }

    [Fact]
    public void WorkspaceLockReleaseFingerprintKeepsItsFieldOrder()
    {
        ReleaseWorkspaceLock command = new(
            "tenant-a", "organization-b", "folder-c", "v1", "workspace-d", "lock-e", "proof-f", "completed",
            "actor-g", "correlation-h", "task-i", "idempotency-j", "payload-k");

        Fingerprint(command, typeof(ReleaseWorkspaceLock)).ShouldBe("77c21a34a2c5f0e55d77da361caa2d7126361d3d093a61f3caed022e9d98b21d");
    }

    [Fact]
    public void WorkspaceFileMutationFingerprintKeepsContentPathAndOptionalOwnerFields()
    {
        MutateWorkspaceFile command = new(
            "tenant-a", "organization-b", "folder-c", "v1", "workspace-d", "operation-e", "change", "inline",
            new PathMetadata("src/example.cs", "example.cs", "source_files", "NFC"), "content-hash-f", 42,
            "text/plain", "inline", 42, "actor-g", "correlation-h", "task-i", "idempotency-j", "payload-k")
        {
            ExpectedLockFence = 1234567890123,
            ExpectedFileVersion = 9876543210,
            OwnerFileId = "file-owner-l",
            ProjectFileReferenceId = "project-reference-m",
        };

        Fingerprint(command, typeof(MutateWorkspaceFile)).ShouldBe("7c1116d31e50205e50ee62060dcfdbc61df6c0336b11f6eeffc9c7f869a31805");
    }

    [Fact]
    public void WorkspaceCommitFingerprintKeepsItsFieldOrder()
    {
        Fingerprint(CommitCommand(), typeof(CommitWorkspace)).ShouldBe("a5d5728586986f15c1e1419cfb5c3da6da243dcfd723f5b58270aec8e301f38b");
    }

    [Fact]
    public void FolderAccessFingerprintKeepsOperationCountIntentAndCanonicalEntryKeys()
    {
        FolderAccessOperation[] operations =
        [
            new(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "principal-d", "read"),
            new(FolderAccessOperationIntent.Revoke, FolderAccessPrincipalKind.DelegatedServiceAgent, "principal-e", "write"),
        ];
        GrantFolderAccess command = new(
            "tenant-a", "organization-b", "folder-c", operations, "actor-f", "correlation-g", "task-h", "idempotency-i", "payload-j");

        Fingerprint(command, typeof(IFolderAccessCommand), [typeof(IReadOnlyList<FolderAccessOperation>)], [operations])
            .ShouldBe("02a031e53cf7755a0159c9274a2957addfcbed729b8c94b9bd711cbbbb84233e");
    }

    [Fact]
    public void FingerprintReflectsPostDedupOperations()
    {
        FolderAccessOperation group = new(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.Group, "principal-d", "query_status");
        FolderAccessOperation user = new(FolderAccessOperationIntent.Grant, FolderAccessPrincipalKind.User, "principal-e", "read_metadata");
        GrantFolderAccess canonical = new(
            "tenant-a", "tenant-a", "folder-c", [group, user], "actor-f", "correlation-g", "task-h", "idempotency-i", "payload-j");

        FolderCommandValidationResult result = FolderCommandValidator.Validate(canonical with { Operations = [user, group, user] });

        result.IsAccepted.ShouldBeTrue(result.Code.ToString());
        result.AccessOperations.ShouldBe([group, user], ignoreOrder: false);
        result.IdempotencyFingerprint.ShouldBe("20a0b53fee3e8d24b7cf3606bd443c5eb29089d4aace9cd26596f557194a3808");
        FolderCommandValidator.Validate(canonical).IdempotencyFingerprint.ShouldBe(result.IdempotencyFingerprint);
    }

    [Fact]
    public void ArchiveDecisionFingerprintKeepsItsDomainAndPolicyFields()
    {
        FolderCommandValidator.BindArchiveDecisionFingerprint(ArchiveCommand(), "command-fingerprint-i", "policy-j", "watermark-k")
            .ShouldBe("db7d96a5d0a4fa8b9bd10a97d4bf15be302f05d60d5395c0a9e6b4c764184997");
    }

    [Fact]
    public void RepositoryBindingIdentityKeepsItsPrefixTruncationAndFieldOrder()
    {
        FolderCommandValidator.DeriveRepositoryBindingId("tenant-a", "folder-b", "provider-c", "external-d", "branch-policy-e")
            .ShouldBe("repository_binding_5edc40fdec154f60f8b9a4ec3753ed2c");
    }

    [Fact]
    public void WorkspaceLockIdentityKeepsItsPrefixTruncationAndFieldOrder()
    {
        FolderCommandValidator.DeriveWorkspaceLockId(LockCommand(), "idempotency-fingerprint-k")
            .ShouldBe("workspace_lock_cacdc614cc08b9f269a1e4b68278e53d");
    }

    [Fact]
    public void WorkspaceLockOwnershipProofKeepsItsPrefixTruncationAndFieldOrder()
    {
        FolderCommandValidator.DeriveWorkspaceLockOwnershipProof("tenant-a", "folder-b", "workspace-c", "task-d", "lock-e")
            .ShouldBe("lock_proof_1affc5c28ca265c2bb495f0ee262a4ff");
    }

    [Fact]
    public void CommitReconciliationReferenceKeepsItsPrefixTruncationAndFieldOrder()
    {
        FolderCommandValidator.DeriveCommitReconciliationReference(CommitCommand())
            .ShouldBe("reconcile_08f2909db790870c1fa00a01e61d0497");
    }

    [Fact]
    public void PopulatedCapabilityProfileKeepsOperationsRoundingAndDictionaryEncoding()
    {
        ProviderCapabilityDiscoveryRequest request = new(
            "tenant-a", "organization-b", "binding-c", "credential-d", "GitHub", "GitHub-Enterprise", " V1 ",
            new ProviderTargetEvidence(" EnterpriseHost ", " Version-1 ", " API-V2 ", " Evidence-V3 ", false, null,
                new Dictionary<string, string> { ["Zeta"] = " Last ", ["alpha"] = " Cafe\u0301 " }),
            [ProviderCredentialMode.ServiceAccountReference, ProviderCredentialMode.AppInstallationReference, ProviderCredentialMode.ServiceAccountReference],
            new ProviderAuthorizationEvidenceSnapshot("authorization-e", DateTimeOffset.UnixEpoch, "fresh"),
            "correlation-f");
        ProviderCapabilityOperationRow[] operations =
        [
            ProviderCapabilityOperationRow.WithDetails("status_query", ProviderOperationSupport.Partial,
                new Dictionary<string, string> { ["zeta"] = "9", ["alpha"] = "2" },
                new Dictionary<string, string> { ["scope"] = "bounded" }, false, ProviderFailureCategory.ProviderRateLimited),
            ProviderCapabilityOperationRow.WithDetails("commit_support", ProviderOperationSupport.Supported,
                new Dictionary<string, string> { ["maximum"] = "32" },
                new Dictionary<string, string> { ["mode"] = "strict" }, true),
        ];
        ProviderCapabilityDiscoveryResult result = ProviderCapabilityProfileFactory.Create(
            request, "github", "github-enterprise", operations,
            new ProviderRateLimitPosture(" Bounded ", true, TimeSpan.FromSeconds(2.5),
                new Dictionary<string, string> { ["zeta"] = "deferred", ["alpha"] = "limited" }),
            new Dictionary<string, string> { ["zeta"] = "provider_unavailable", ["alpha"] = "provider_conflict" },
            new Dictionary<string, string> { ["zeta"] = "observed", ["alpha"] = "stable" });

        result.IsSuccess.ShouldBeTrue(result.ReasonCode);
        result.Profile.ShouldNotBeNull().Fingerprint.ShouldBe("fcc59d050591b92532b165cef8eab4a45f557991459b2faaaad7272baeb0be4a");
    }

    private static ArchiveFolder ArchiveCommand()
        => new(
            "tenant-a", "organization-b", "folder-c", "v1", "policy_retention", "actor-d", "correlation-e", "task-f",
            "idempotency-g", "payload-h", new Dictionary<string, string?> { ["Zeta"] = null, ["alpha"] = "comparison-tenant" });

    private static BindRepository BindCommand()
        => new(
            "tenant-a", "organization-b", "folder-c", "v1", "binding-d", "provider-e", "external-f", "policy-g",
            "scope-h", "actor-i", "correlation-j", "task-k", "idempotency-l", "payload-m");

    private static LockWorkspace LockCommand()
        => new(
            "tenant-a", "organization-b", "folder-c", "v1", "workspace-d", "mutation", 513,
            "actor-e", "correlation-f", "task-g", "idempotency-h", "payload-i");

    private static CommitWorkspace CommitCommand()
        => new(
            "tenant-a", "organization-b", "folder-c", "v1", "workspace-d", "operation-e", "author-metadata-f",
            "refs/heads/main", "maintenance", "changed-path-digest-g", "actor-h", "correlation-i", "task-j", "idempotency-k", "payload-l");

    private static string Fingerprint(
        IFolderCommand command,
        Type commandType,
        Type[]? additionalTypes = null,
        object?[]? additionalArguments = null)
    {
        Type[] parameterTypes = [commandType, .. additionalTypes ?? []];
        object?[] arguments = [command, .. additionalArguments ?? []];
        MethodInfo method = typeof(FolderCommandValidator).GetMethod(
            "Fingerprint", BindingFlags.NonPublic | BindingFlags.Static, parameterTypes)!;
        return (string)method.Invoke(null, arguments)!;
    }
}
