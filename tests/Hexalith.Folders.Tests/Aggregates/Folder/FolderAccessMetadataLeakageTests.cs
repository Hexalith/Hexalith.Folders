using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderAccessMetadataLeakageTests
{
    private static readonly string[] ForbiddenSentinels =
    [
        "github_pat_credential_material",
        "repository-secret-name",
        "branch-main",
        "/tmp/raw-file-path",
        "file contents",
        "diff --git a/secret b/secret",
        "generated context payload",
        "person@example.com",
        "raw-auth-header",
        "tenant-display-name",
    ];

    [Theory]
    [MemberData(nameof(ForbiddenValues))]
    public void DeniedResultsShouldNotEchoForbiddenSentinels(string sentinel)
    {
        RecordingFolderRepository repository = new();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(principalId: sentinel, payloadTenantId: sentinel),
            FolderAccessTenantEvidenceGateTests.Evidence(TenantAccessOutcome.Denied, sentinel),
            new FolderAccessAclEvidence(FolderAccessAclOutcome.Denied, sentinel, sentinel, sentinel, sentinel, sentinel));

        string evidence = string.Join(
            "|",
            result.ManagedTenantId,
            result.OrganizationId,
            result.FolderId,
            result.PrincipalId,
            result.Action,
            result.ActorPrincipalId,
            result.CorrelationId,
            result.TaskId,
            result.IdempotencyKey);

        foreach (string forbidden in ForbiddenSentinels)
        {
            evidence.ShouldNotContain(forbidden);
        }
    }

    [Fact]
    public void InvalidPrincipalShouldRejectBeforeAppendAndWithoutRawPrincipalEcho()
    {
        RecordingFolderRepository repository = FolderAccessIdempotencyTests.SeededRepository();
        FolderAccessTenantGate gate = new(repository);

        FolderResult result = gate.Handle(
            FolderCommandFactory.GrantAccess(principalId: "person@example.com"),
            FolderAccessIdempotencyTests.TenantEvidence(),
            FolderAccessIdempotencyTests.AclEvidence());

        result.Code.ShouldBe(FolderResultCode.InvalidPrincipal);
        result.PrincipalId.ShouldBeNull();
        repository.AppendsAttempted.ShouldBe(0);
        repository.EventsAppended.ShouldBe(0);
    }

    public static IEnumerable<object[]> ForbiddenValues()
        => ForbiddenSentinels.Select(value => new object[] { value });
}
