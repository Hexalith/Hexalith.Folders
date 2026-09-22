using System.Security.Claims;

using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class HttpContextTenantContextAccessorTests
{
    [Fact]
    public void UnauthenticatedClaimIdentityDoesNotEstablishTenantOrPrincipal()
    {
        ClaimsIdentity identity = new(
        [
            new Claim(TenantContextOptions.EventStoreTenantClaimType, "tenant-a"),
            new Claim(TenantContextOptions.SubjectClaimType, "user-a"),
        ]);
        HttpContextTenantContextAccessor accessor = Accessor(identity);

        accessor.AuthoritativeTenantId.ShouldBeNull();
        accessor.PrincipalId.ShouldBeNull();
    }

    [Fact]
    public void AuthenticatedClaimIdentityEstablishesTenantAndPrincipal()
    {
        ClaimsIdentity identity = new(
        [
            new Claim(TenantContextOptions.EventStoreTenantClaimType, "tenant-a"),
            new Claim(TenantContextOptions.SubjectClaimType, "user-a"),
        ], authenticationType: "test");
        HttpContextTenantContextAccessor accessor = Accessor(identity);

        accessor.AuthoritativeTenantId.ShouldBe("tenant-a");
        accessor.PrincipalId.ShouldBe("user-a");
    }

    private static HttpContextTenantContextAccessor Accessor(ClaimsIdentity identity)
    {
        DefaultHttpContext context = new() { User = new ClaimsPrincipal(identity) };
        return new HttpContextTenantContextAccessor(
            new HttpContextAccessor { HttpContext = context },
            Options.Create(new TenantContextOptions()));
    }
}
