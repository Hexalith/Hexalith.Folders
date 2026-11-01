using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Hosting;

/// <summary>Qualifies isolated transport setup without claiming production credential validation.</summary>
public sealed class FoldersServerTestDefaultsTests
{
    /// <summary>Rejects blank explicit scheme names before registering any fixture services.</summary>
    /// <param name="scheme">An invalid scheme name.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void BlankHumanSchemeFailsAtRegistration(string scheme)
    {
        var services = new ServiceCollection();
        ArgumentException exception = Should.Throw<ArgumentException>(() => services.AddFoldersServerTestDefaults(scheme));
        exception.ParamName.ShouldBe("humanAuthenticationScheme");
        services.ShouldBeEmpty();
    }

    /// <summary>Preserves an existing fixture default scheme when isolated handler defaults are added.</summary>
    [Fact]
    public void ExistingDefaultSchemeKeepsAuthenticateAndChallengeSelection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("existing").AddCookie("existing");
        services.AddFoldersServerTestDefaults();
        using ServiceProvider provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        options.DefaultScheme.ShouldBe("existing");
        options.DefaultAuthenticateScheme.ShouldBeNull();
        options.DefaultChallengeScheme.ShouldBeNull();
    }

    /// <summary>Supplies a synthetic fixture principal while retaining the SDK human credential-kind requirements.</summary>
    [Fact]
    public async Task ExplicitFixtureSchemeSuppliesHumanTransportWhileKeepingSdkRequirements()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventStoreHttpHostSecurity();
        services.AddFoldersServerTestDefaults();
        using ServiceProvider provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        AuthenticateResult result = await context.AuthenticateAsync("FoldersIsolatedHuman");
        result.Succeeded.ShouldBeTrue();
        EventStoreHttpHostSecurityExtensions.IsHuman(result.Principal!).ShouldBeTrue();
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy)!;
        policy.Requirements.Select(item => item.GetType()).ShouldBe(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy().Requirements.Select(item => item.GetType()));
        policy.AuthenticationSchemes.ShouldBe(["FoldersIsolatedHuman"]);
    }

    /// <summary>Uses an existing fake human scheme after SDK policy setup without changing the fail-closed fallback.</summary>
    [Fact]
    public void ExplicitHumanSchemeSurvivesSdkPolicyPostConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventStoreHttpHostSecurity();
        services.AddAuthentication("existing").AddCookie("existing");
        services.AddFoldersServerTestDefaults("existing");
        using ServiceProvider provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var policy = options.GetPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy)!;
        policy.AuthenticationSchemes.ShouldBe(["existing"]);
        policy.Requirements.Select(item => item.GetType()).ShouldBe(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy().Requirements.Select(item => item.GetType()));
        options.DefaultPolicy.AuthenticationSchemes.ShouldBe(["Bearer"]);
        options.FallbackPolicy.ShouldNotBeNull().AuthenticationSchemes.ShouldBe([EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme]);
    }
}
