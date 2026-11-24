using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderClientIdentityValuesTests
{
    [Fact]
    public void TenantIdsShouldMapEveryQueryAndHeaderSource()
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?tenantId=query-tenant&managedTenantId=query-managed");
        context.Request.Headers["X-Hexalith-Tenant-Id"] = "hexalith-tenant";
        context.Request.Headers["X-Tenant-Id"] = "header-tenant";
        context.Request.Headers["X-Forwarded-Tenant"] = "forwarded-tenant";

        Render(FolderClientIdentityValues.TenantIds(context)).ShouldBe(
        [
            "forwarded_tenant_id=forwarded-tenant",
            "header_hexalith_tenant_id=hexalith-tenant",
            "header_tenant_id=header-tenant",
            "query_managed_tenant_id=query-managed",
            "query_tenant_id=query-tenant",
        ]);
    }

    [Fact]
    public void TenantIdsShouldKeepEverySourceKeyWhenNoValueIsSupplied()
        => Render(FolderClientIdentityValues.TenantIds(new DefaultHttpContext())).ShouldBe(
        [
            "forwarded_tenant_id=",
            "header_hexalith_tenant_id=",
            "header_tenant_id=",
            "query_managed_tenant_id=",
            "query_tenant_id=",
        ]);

    [Fact]
    public void PrincipalIdsShouldReadHeadersOnly()
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?principalId=query-principal");
        context.Request.Headers["X-Principal-Id"] = "header-principal";
        context.Request.Headers["X-Forwarded-Principal"] = "forwarded-principal";

        Render(FolderClientIdentityValues.PrincipalIds(context)).ShouldBe(
        [
            "forwarded_principal_id=forwarded-principal",
            "header_principal_id=header-principal",
        ]);
    }

    [Fact]
    public void IdentityValuesShouldSkipBlankAndControlCharacterValues()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["X-Tenant-Id"] = new StringValues([" ", "bad\u0007tenant", " good-tenant "]);
        context.Request.Headers["X-Principal-Id"] = new StringValues(["bad\r\nprincipal"]);

        FolderClientIdentityValues.TenantIds(context)["header_tenant_id"].ShouldBe("good-tenant");
        FolderClientIdentityValues.PrincipalIds(context)["header_principal_id"].ShouldBeNull();
    }

    [Fact]
    public void IdentityValuesShouldRejectANullContext()
    {
        Should.Throw<ArgumentNullException>(() => FolderClientIdentityValues.TenantIds(null!));
        Should.Throw<ArgumentNullException>(() => FolderClientIdentityValues.PrincipalIds(null!));
    }

    private static string[] Render(IReadOnlyDictionary<string, string?> values)
        => [.. values
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key}={pair.Value}")];
}
