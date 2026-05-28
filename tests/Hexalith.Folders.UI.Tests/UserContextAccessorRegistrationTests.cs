using System.Security.Claims;

using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Rendering;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class UserContextAccessorRegistrationTests
{
    [Fact]
    public void Composition_Registers_Folders_IUserContextAccessor()
    {
        (IServiceCollection services, _, _) = CompositionRootFactory.Build(CompositionRootFactory.WithAuthority("https://example.invalid/realm"));

        ServiceDescriptor descriptor = services
            .Last(d => d.ServiceType == typeof(IUserContextAccessor));

        descriptor.ImplementationType.ShouldBe(typeof(FoldersUserContextAccessor));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        // Guard rail: a stray TryAddScoped of NullUserContextAccessor would have been replaced.
        services
            .Where(d => d.ServiceType == typeof(IUserContextAccessor))
            .Select(d => d.ImplementationType?.Name)
            .ShouldNotContain("NullUserContextAccessor");
    }

    [Fact]
    public void FoldersUserContextAccessor_ReturnsNullForUnauthenticatedUser()
    {
        AuthenticationStateProvider provider = AuthenticationStateProviderStub.Anonymous();
        FoldersUserContextAccessor accessor = new(provider);

        accessor.TenantId.ShouldBeNull();
        accessor.UserId.ShouldBeNull();
    }

    [Fact]
    public void FoldersUserContextAccessor_ReadsTenantIdAndPrincipalIdFromExpectedClaims()
    {
        AuthenticationStateProvider provider = AuthenticationStateProviderStub.WithClaims(
            ("tenant_id", "tenant-a"),
            (ClaimTypes.NameIdentifier, "user-a"));

        FoldersUserContextAccessor accessor = new(provider);

        accessor.TenantId.ShouldBe("tenant-a");
        accessor.UserId.ShouldBe("user-a");
    }

    [Fact]
    public void FoldersUserContextAccessor_TreatsWhitespaceClaimAsUnauthenticated()
    {
        AuthenticationStateProvider provider = AuthenticationStateProviderStub.WithClaims(
            ("tenant_id", "   "),
            (ClaimTypes.NameIdentifier, string.Empty));

        FoldersUserContextAccessor accessor = new(provider);

        accessor.TenantId.ShouldBeNull();
        accessor.UserId.ShouldBeNull();
    }

    private static class AuthenticationStateProviderStub
    {
        public static AuthenticationStateProvider Anonymous()
        {
            AuthenticationStateProvider provider = Substitute.For<AuthenticationStateProvider>();
            provider.GetAuthenticationStateAsync()
                .Returns(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
            return provider;
        }

        public static AuthenticationStateProvider WithClaims(params (string Type, string Value)[] claims)
        {
            ClaimsIdentity identity = new(claims.Select(c => new Claim(c.Type, c.Value)), "Test");
            AuthenticationStateProvider provider = Substitute.For<AuthenticationStateProvider>();
            provider.GetAuthenticationStateAsync()
                .Returns(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
            return provider;
        }
    }
}
