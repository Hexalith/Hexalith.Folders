using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server;

/// <summary>
/// Collects the client-controlled tenant and principal identity values that Server endpoints compare with the
/// authenticated identity. Each value is keyed by its source so a mismatch can be reported without echoing it.
/// </summary>
internal static class FolderClientIdentityValues
{
    /// <summary>
    /// Returns the tenant identity values the client supplied through the query string or request headers.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>Every tenant source key, mapped to its first safe value or <see langword="null"/>.</returns>
    public static IReadOnlyDictionary<string, string?> TenantIds(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query_tenant_id"] = FolderHttpHeaderReader.ReadQuery(httpContext, "tenantId"),
            ["query_managed_tenant_id"] = FolderHttpHeaderReader.ReadQuery(httpContext, "managedTenantId"),
            ["header_hexalith_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Hexalith-Tenant-Id"),
            ["header_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Tenant-Id"),
            ["forwarded_tenant_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Forwarded-Tenant"),
        };
    }

    /// <summary>
    /// Returns the principal identity values the client supplied through request headers. Principal identity is
    /// header-only: query-string sources are not read, to reduce the attack surface.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>Every principal source key, mapped to its first safe value or <see langword="null"/>.</returns>
    public static IReadOnlyDictionary<string, string?> PrincipalIds(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["header_principal_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Principal-Id"),
            ["forwarded_principal_id"] = FolderHttpHeaderReader.ReadHeader(httpContext, "X-Forwarded-Principal"),
        };
    }
}
