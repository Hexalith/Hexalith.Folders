using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests.Authentication;

public sealed class FoldersProductionAuthenticationTests
{
    [Fact]
    public void ProductionAuthenticationShouldConfigureFrozenS2JwtBearerOptions()
    {
        using ServiceProvider provider = Services(new Dictionary<string, string?>
        {
            ["Folders:Authentication:Authority"] = "https://oidc.example.invalid/realms/folders",
            ["Folders:Authentication:MetadataAddress"] = "https://oidc.example.invalid/realms/folders/.well-known/openid-configuration",
            ["Folders:Authentication:ValidIssuer"] = "https://oidc.example.invalid/realms/folders",
            ["Folders:Authentication:Audience"] = "api://hexalith-folders.example.invalid",
            ["Folders:Authentication:RequireHttpsMetadata"] = "true",
        }).BuildServiceProvider();

        JwtBearerOptions options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.MapInboundClaims.ShouldBeFalse();
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.AutomaticRefreshInterval.ShouldBe(TimeSpan.FromMinutes(10));
        options.RefreshInterval.ShouldBe(TimeSpan.FromMinutes(1));
        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromSeconds(30));
        options.TokenValidationParameters.RequireExpirationTime.ShouldBeTrue();
        options.TokenValidationParameters.RequireSignedTokens.ShouldBeTrue();
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidIssuer.ShouldBe("https://oidc.example.invalid/realms/folders");
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidAudience.ShouldBe("api://hexalith-folders.example.invalid");
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        options.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
        options.TokenValidationParameters.NameClaimType.ShouldBe("sub");
    }

    [Fact]
    public async Task ProductionValidatorShouldRejectNonJwtBearerScheme()
    {
        ServiceCollection services = new();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment("Production"));
        services.AddOptions<FoldersOidcOptions>().Configure(static options =>
        {
            options.Authority = "https://oidc.example.invalid/realms/folders";
            options.ValidIssuer = "https://oidc.example.invalid/realms/folders";
            options.Audience = "api://hexalith-folders.example.invalid";
            options.RequireHttpsMetadata = true;
        });
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddSingleton<IHostedService, FoldersAuthSchemeValidator>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IHostedService validator = provider.GetRequiredService<IHostedService>();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("JWT bearer scheme");
    }

    [Fact]
    public async Task ProductionValidatorShouldRejectMissingIssuerOrAudiencePins()
    {
        using ServiceProvider provider = Services(new Dictionary<string, string?>
        {
            ["Folders:Authentication:Authority"] = "https://oidc.example.invalid/realms/folders",
            ["Folders:Authentication:RequireHttpsMetadata"] = "true",
        }).BuildServiceProvider();

        IHostedService validator = provider.GetServices<IHostedService>().OfType<FoldersAuthSchemeValidator>().Single();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("audience pin");
        ex.Message.ShouldContain("issuer pin");
    }

    [Fact]
    public void ProgramShouldUseAuthenticationBeforeAuthorizationAndEndpointMapping()
    {
        string program = File.ReadAllText(RepositoryPath("src/Hexalith.Folders.Server/Program.cs"));
        int authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        int authorization = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        int endpoints = program.IndexOf("app.MapFoldersServerEndpoints();", StringComparison.Ordinal);

        authentication.ShouldBeGreaterThanOrEqualTo(0);
        authorization.ShouldBeGreaterThan(authentication);
        endpoints.ShouldBeGreaterThan(authorization);
    }

    private static ServiceCollection Services(IReadOnlyDictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment("Production"));
        services.AddFoldersProductionAuthentication(configuration, new StubEnvironment("Production"));
        services.AddSingleton<IHostedService, FoldersAuthSchemeValidator>();
        return services;
    }

    private static string RepositoryPath(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory, "Hexalith.Folders.slnx")))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Hexalith.Folders.Server.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
