using System.Security.Claims;

using Hexalith.FrontComposer.Contracts.Rendering;

using Microsoft.AspNetCore.Components.Authorization;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Folders adapter for <see cref="IUserContextAccessor"/> backed by the Blazor circuit's
/// <see cref="AuthenticationStateProvider"/>. Replaces FrontComposer's fail-closed
/// <c>NullUserContextAccessor</c> default so tenant-scoped projection queries see the
/// authenticated principal instead of silently no-opping.
/// </summary>
internal sealed class FoldersUserContextAccessor : IUserContextAccessor
{
    // Wire-contract claim name owned by Hexalith.Folders.Server.Authentication.TenantContextOptions.
    // Copied as a string constant (not a project reference) to keep the UI off the server-only types.
    private const string TenantClaimType = "tenant_id";

    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public FoldersUserContextAccessor(AuthenticationStateProvider authenticationStateProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticationStateProvider);
        _authenticationStateProvider = authenticationStateProvider;
    }

    public string? TenantId => ReadClaim(TenantClaimType);

    public string? UserId => ReadClaim(ClaimTypes.NameIdentifier);

    private string? ReadClaim(string claimType)
    {
        AuthenticationState state = _authenticationStateProvider
            .GetAuthenticationStateAsync()
            .GetAwaiter()
            .GetResult();

        string? value = state.User.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
