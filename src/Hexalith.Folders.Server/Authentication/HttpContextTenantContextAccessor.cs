using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server.Authentication;

public sealed class HttpContextTenantContextAccessor(
    IHttpContextAccessor httpContextAccessor,
    IOptions<TenantContextOptions> options) : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly TenantContextOptions _options = options.Value;

    public string? AuthoritativeTenantId
        => GetClaim(_options.TenantClaimType);

    public string? PrincipalId
        => GetClaim(_options.PrincipalClaimType);

    private string? GetClaim(string claimType)
    {
        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        string? value = user.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
