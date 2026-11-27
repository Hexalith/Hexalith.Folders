using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

/// <summary>
/// Collects the client-controlled tenant and principal identity values that Server endpoints compare with the
/// authenticated identity. Indexed source keys retain every supplied value for comparison.
/// </summary>
internal static class FolderClientIdentityValues
{
    /// <summary>
    /// Returns the tenant identity values the client supplied through the query string or request headers.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The supplied tenant values with their indexed source keys.</returns>
    public static IReadOnlyDictionary<string, string?> TenantIds(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return HistoricalMutationAuthorization.ClientControlledValues(
            httpContext,
            ("query_tenant_id", false, "tenantId"),
            ("query_managed_tenant_id", false, "managedTenantId"),
            ("header_hexalith_tenant_id", true, "X-Hexalith-Tenant-Id"),
            ("header_tenant_id", true, "X-Tenant-Id"),
            ("forwarded_tenant_id", true, "X-Forwarded-Tenant"));
    }

    /// <summary>
    /// Returns the principal identity values the client supplied through the query string or request headers.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The supplied principal values with their indexed source keys.</returns>
    public static IReadOnlyDictionary<string, string?> PrincipalIds(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return HistoricalMutationAuthorization.ClientControlledValues(
            httpContext,
            ("query_principal_id", false, "principalId"),
            ("header_principal_id", true, "X-Principal-Id"),
            ("forwarded_principal_id", true, "X-Forwarded-Principal"));
    }
}
