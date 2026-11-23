using System.Text.Json;
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
    public void FolderContextRejectsStreamDelimiterCharacters()
    {
        Should.Throw<ArgumentException>(() => FoldersTestDataFactory.FolderContext(
            new TestFolderContextOverrides(ManagedTenantId: "tenant:alpha")));

        Should.Throw<ArgumentException>(() => FoldersTestDataFactory.FolderContext(
            new TestFolderContextOverrides(OrganizationId: "organization:001")));

        Should.Throw<ArgumentException>(() => FoldersTestDataFactory.FolderContext(
            new TestFolderContextOverrides(FolderId: "folder:001")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fixture\nvalue")]
    public void FolderContextRejectsInvalidHeaderIdentifiers(string? value)
    {
        Should.Throw<ArgumentException>(() => new TestFolderContext(
            "tenant", "organization", "folder", value!, "correlation", "idempotency"))
            .ParamName.ShouldBe("taskId");
        Should.Throw<ArgumentException>(() => new TestFolderContext(
            "tenant", "organization", "folder", "task", value!, "idempotency"))
            .ParamName.ShouldBe("correlationId");
        Should.Throw<ArgumentException>(() => new TestFolderContext(
            "tenant", "organization", "folder", "task", "correlation", value!))
            .ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void FolderContextDeconstructsInConstructorOrder()
    {
        TestFolderContext context = new("tenant", "organization", "folder", "task", "correlation", "idempotency");

        var (tenant, organization, folder, task, correlation, idempotency) = context;

        (tenant, organization, folder, task, correlation, idempotency)
            .ShouldBe(("tenant", "organization", "folder", "task", "correlation", "idempotency"));
    }

    [Fact]
    public void FolderContextRoundTripsThroughJson()
    {
        TestFolderContext context = new("tenant", "organization", "folder", "task:1", "correlation", "idempotency");

        JsonSerializer.Deserialize<TestFolderContext>(JsonSerializer.Serialize(context)).ShouldBe(context);
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

    [Fact]
    public void AuthorizationContextJsonEscapesTenantClaimValues()
    {
        TestAuthorizationContext context = FoldersTestDataFactory.AuthorizationContext(
            new TestAuthorizationContextOverrides(ManagedTenantId: "tenant-\"alpha\\beta"));

        using JsonDocument document = JsonDocument.Parse(context.TenantClaimJson);

        JsonElement tenantClaims = document.RootElement;
        tenantClaims.ValueKind.ShouldBe(JsonValueKind.Array);
        tenantClaims.GetArrayLength().ShouldBe(1);
        tenantClaims[0].GetString().ShouldBe("tenant-\"alpha\\beta");
    }
}
