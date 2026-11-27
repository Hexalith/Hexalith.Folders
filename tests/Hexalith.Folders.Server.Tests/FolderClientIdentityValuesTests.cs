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
        context.Request.QueryString = new QueryString("?tenantId=query-tenant&tenantId=second-query-tenant&managedTenantId=query-managed");
        context.Request.Headers["X-Hexalith-Tenant-Id"] = "hexalith-tenant";
        context.Request.Headers["X-Tenant-Id"] = new StringValues(["header-tenant", "second-header-tenant"]);
        context.Request.Headers["X-Forwarded-Tenant"] = "forwarded-tenant";

        Render(FolderClientIdentityValues.TenantIds(context)).ShouldBe(
        [
            "forwarded_tenant_id[0]=forwarded-tenant",
            "header_hexalith_tenant_id[0]=hexalith-tenant",
            "header_tenant_id[0]=header-tenant",
            "header_tenant_id[1]=second-header-tenant",
            "query_managed_tenant_id[0]=query-managed",
            "query_tenant_id[0]=query-tenant",
            "query_tenant_id[1]=second-query-tenant",
        ]);
    }

    [Fact]
    public void IdentityValuesShouldOmitAbsentSources()
    {
        DefaultHttpContext context = new();
        FolderClientIdentityValues.TenantIds(context).ShouldBeEmpty();
        FolderClientIdentityValues.PrincipalIds(context).ShouldBeEmpty();
    }

    [Fact]
    public void PrincipalIdsShouldReadQueriesAndHeaders()
    {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?principalId=query-principal");
        context.Request.Headers["X-Principal-Id"] = "header-principal";
        context.Request.Headers["X-Forwarded-Principal"] = "forwarded-principal";

        Render(FolderClientIdentityValues.PrincipalIds(context)).ShouldBe(
        [
            "forwarded_principal_id[0]=forwarded-principal",
            "header_principal_id[0]=header-principal",
            "query_principal_id[0]=query-principal",
        ]);
    }

    [Fact]
    public void IdentityValuesShouldRetainBlankAndMalformedValuesForComparison()
    {
        const string malformedTenant = "bad\u0007tenant";
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?principalId=");
        context.Request.Headers["X-Tenant-Id"] = new StringValues([" ", malformedTenant, " tenant-a "]);

        IReadOnlyDictionary<string, string?> values = FolderClientIdentityValues.TenantIds(context);
        values["header_tenant_id[0]"].ShouldBe(" ");
        string.Equals(values["header_tenant_id[1]"], malformedTenant, StringComparison.Ordinal)
            .ShouldBeTrue("The collector must retain a malformed value for comparison.");
        values["header_tenant_id[2]"].ShouldBe(" tenant-a ");
        FolderClientIdentityValues.PrincipalIds(context)["query_principal_id[0]"].ShouldBe(string.Empty);
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
