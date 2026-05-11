using Hexalith.Folders.Testing.Factories;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Unit;

public sealed class FoldersTestDataFactoryTests
{
    [Fact]
    public void FolderContextCreatesTenantScopedStreamNames()
    {
        TestFolderContext context = FoldersTestDataFactory.FolderContext(
            new TestFolderContextOverrides(
                ManagedTenantId: "tenant-alpha",
                OrganizationId: "organization-001",
                FolderId: "folder-001"));

        context.FolderStreamName.ShouldBe("tenant-alpha:folders:folder-001");
        context.OrganizationStreamName.ShouldBe("tenant-alpha:organizations:organization-001");
    }

    [Fact]
    public void AuthorizationContextDefaultsToFoldersPermissionWithoutPayloadAuthority()
    {
        TestAuthorizationContext context = FoldersTestDataFactory.AuthorizationContext(
            new TestAuthorizationContextOverrides(ManagedTenantId: "tenant-alpha"));

        context.ManagedTenantId.ShouldBe("tenant-alpha");
        context.Permissions.ShouldBe(["folders:*"]);
        context.TenantClaimJson.ShouldBe("[\"tenant-alpha\"]");
    }
}
