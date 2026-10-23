using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Helpers;

public sealed class ProviderTargetFingerprintCompatibilityTests
{
    // Expected hashes come from the original provider sources at
    // 591194c7766c72d8bd51398d66f814a6a1fca922, before helper extraction.
    [Theory]
    [InlineData(false, "138ce4b8e19512328a48bf01a0e74a28c654737544a0d5b32a736e482b47928c")]
    [InlineData(true, "ed22d98412e7a4f79952ff239b9729be820f8bb2e0f2d8bd602d413f220baad3")]
    public void RepositoryCreationTargetFingerprintKeepsItsBaselineFieldOrder(bool forgejo, string expected)
    {
        ProviderRepositoryCreationRequest request = CreationRequest(forgejo);
        bool created = forgejo
            ? ForgejoSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                new Uri("https://forgejo.example/authorized/"), "9.0.3",
                out ProviderTargetEvidence target, out string? reason)
            : GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                out target, out reason);

        created.ShouldBeTrue(reason);
        target.Metadata["safe_target_fingerprint"].ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, "307101dc4222f1be3e5f638206d7c4e81a8f13466e1cadc226e890d41e990ea5")]
    [InlineData(true, "b463be7bb2279fcf9ee49d96fb49b26b1914f13ba9962a41873f0edd1af8c37f")]
    public void RepositoryBindingTargetFingerprintKeepsItsBaselineFieldOrder(bool forgejo, string expected)
    {
        ProviderRepositoryBindingRequest request = BindingRequest(forgejo);
        bool created = forgejo
            ? ForgejoSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                new Uri("https://forgejo.example/authorized/"), "9.0.3",
                out ProviderTargetEvidence target, out string? reason)
            : GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                out target, out reason);

        created.ShouldBeTrue(reason);
        target.Metadata["safe_target_fingerprint"].ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, "cf8212d328f3389f8a766e5fb876b62f9fd0b409b88b3104d941f0a35963c391")]
    [InlineData(true, "ce9d6289f946702cbb0b1fbea6b0b947522fca631055e9dd6b56cd1cb618fe0f")]
    public void FileMutationTargetFingerprintKeepsItsBaselineFieldOrder(bool forgejo, string expected)
    {
        ProviderFileMutationRequest request = MutationRequest(forgejo);
        bool created = forgejo
            ? ForgejoSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                new Uri("https://forgejo.example/authorized/"), "9.0.3",
                out ProviderTargetEvidence target, out string? reason)
            : GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                out target, out reason);

        created.ShouldBeTrue(reason);
        target.Metadata["safe_target_fingerprint"].ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, "b310991f52ab1c8067d18147ca91e3723ba01e66906d210e01f09cc049ec6a8f")]
    [InlineData(true, "b346f369346d55b056a90e54bbc0bdb0f469eb2b89537919e607c601356691b0")]
    public void CommitTargetFingerprintKeepsItsBaselineFieldOrder(bool forgejo, string expected)
    {
        ProviderCommitRequest request = CommitRequest(forgejo);
        bool created = forgejo
            ? ForgejoSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                new Uri("https://forgejo.example/authorized/"), "9.0.3",
                out ProviderTargetEvidence target, out string? reason)
            : GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                out target, out reason);

        created.ShouldBeTrue(reason);
        target.Metadata["safe_target_fingerprint"].ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, "eebd41207e8dbd3d95cb5cbd5a953d8d563c7dc2754f1869810bec5642fdb6a2")]
    [InlineData(true, "2c8aaf1a3fb8e755025fc7808df7f167174f238fc06948e19487db9765246d06")]
    public void OperationStatusTargetFingerprintKeepsItsBaselineFieldOrder(bool forgejo, string expected)
    {
        ProviderOperationStatusRequest request = StatusRequest(forgejo);
        bool created = forgejo
            ? ForgejoSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                new Uri("https://forgejo.example/authorized/"), "9.0.3",
                out ProviderTargetEvidence target, out string? reason)
            : GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
                out target, out reason);

        created.ShouldBeTrue(reason);
        target.Metadata["safe_target_fingerprint"].ShouldBe(expected);
    }

    private static ProviderRepositoryCreationRequest CreationRequest(bool forgejo)
        => new(
            "tenant-a", "organization-b", "provider-binding-c", "credential-d", "repository-binding-e",
            Family(forgejo), Family(forgejo) + "-instance", Target(forgejo, ProviderOperationCatalog.RepositoryCreation),
            [ProviderCredentialMode.AppInstallationReference], Authorization(), "correlation-f", "creation-key-g",
            Admission(), "profile-h");

    private static ProviderRepositoryBindingRequest BindingRequest(bool forgejo)
        => new(
            "tenant-a", "organization-b", "provider-binding-c", "credential-d", "repository-binding-e",
            "external-reference-f", "external-fingerprint-g", "branch-policy-h", Family(forgejo), Family(forgejo) + "-instance",
            Target(forgejo, ProviderOperationCatalog.RepositoryBinding), [ProviderCredentialMode.AppInstallationReference],
            Authorization(), "correlation-i", "binding-key-j", Admission());

    private static ProviderFileMutationRequest MutationRequest(bool forgejo)
        => new(
            "tenant-a", "organization-b", "folder-c", "delegated-task-d", "provider-binding-e", "credential-f",
            "repository-binding-g", Family(forgejo), Family(forgejo) + "-instance",
            Target(forgejo, ProviderOperationCatalog.FileMutationSupport), [ProviderCredentialMode.AppInstallationReference],
            Authorization(), Lock(), RefPolicy(),
            new ProviderFilePolicyEvidence("file-policy-k", DateTimeOffset.UnixEpoch, "fresh", 2048, 3, true, true, true),
            "resolved-target-l", "changes-reference-m", "change-set-n", [], "correlation-o", "mutation-key-p", Admission());

    private static ProviderCommitRequest CommitRequest(bool forgejo)
        => new(
            "tenant-a", "organization-b", "folder-c", "delegated-task-d", "provider-binding-e", "credential-f",
            "repository-binding-g", Family(forgejo), Family(forgejo) + "-instance",
            Target(forgejo, ProviderOperationCatalog.CommitSupport), [ProviderCredentialMode.AppInstallationReference],
            Authorization(), Lock(), RefPolicy(), "resolved-target-l", "staged-reference-m", "staged-changes-n",
            "message-reference-o", "commit-message-p", "expected-head-q", "correlation-r", "commit-key-s", Admission());

    private static ProviderOperationStatusRequest StatusRequest(bool forgejo)
        => new(
            "tenant-a", "organization-b", "folder-c", "delegated-task-d", "provider-binding-e", "credential-f",
            "repository-binding-g", Family(forgejo), Family(forgejo) + "-instance",
            Target(forgejo, ProviderOperationCatalog.StatusQuery), [ProviderCredentialMode.AppInstallationReference],
            Authorization(), Lock(), RefPolicy(), "operation-k", "resolved-target-l", "full-ref-m", "expected-head-n",
            "intended-commit-o", "check-window-p", 2, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1),
            "correlation-q");

    private static string Family(bool forgejo) => forgejo ? "forgejo" : "github";

    private static ProviderTargetEvidence Target(bool forgejo, string operationScope)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["Zeta"] = "last-value",
            ["operation_scope"] = operationScope,
            ["alpha"] = "e\u0301",
            ["safe_target_fingerprint"] = "declared-target",
        };
        if (forgejo)
        {
            metadata["authorized_base_url"] = "https://forgejo.example/authorized/";
        }

        return new ProviderTargetEvidence(
            Family(forgejo), forgejo ? "9.0.3" : "github-rest",
            forgejo ? "forgejo-rest-v1" : "github-rest-2022-11-28",
            forgejo ? "forgejo-target-evidence-v2" : "target-v1", false, DateTimeOffset.UnixEpoch, metadata);
    }

    private static ProviderAuthorizationEvidenceSnapshot Authorization()
        => new("authorization-h", DateTimeOffset.UnixEpoch, "fresh");

    private static ProviderOperationLockEvidence Lock()
        => new("lock-i", DateTimeOffset.UnixEpoch, "fresh", true, false);

    private static ProviderRefPolicyEvidence RefPolicy()
        => new("ref-policy-j", DateTimeOffset.UnixEpoch, "fresh", true, true, true);

    private static ProviderIdempotencyAdmission Admission()
        => new(ProviderIdempotencyDisposition.Fresh, "intent-t");
}
