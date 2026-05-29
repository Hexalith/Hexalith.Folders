using System.Security.Claims;
using System.Text.Encodings.Web;

using Hexalith.Folders.Client;
using Hexalith.Folders.UI.Configuration;
using Hexalith.Folders.UI.Infrastructure;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Extensions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.Folders.UI;

/// <summary>
/// Service-collection composition extracted from <c>Program.cs</c> so bUnit tests can replicate the
/// host wiring deterministically without going through <c>WebApplication.CreateBuilder</c>.
/// </summary>
internal static class CompositionRoot
{
    /// <summary>
    /// The environment name used by the E2E test fixture to opt into the hermetic auth stub.
    /// Aligns with the AC #6 contract that the hermetic-test branch is permitted only when
    /// <c>ASPNETCORE_ENVIRONMENT</c> is <c>Development</c> or <c>Test</c>.
    /// </summary>
    public const string TestEnvironmentName = "Test";

    /// <summary>
    /// Static bearer token accepted by the hermetic-test auth stub. Tests stamp this on outbound
    /// requests via the <c>Authorization: Bearer</c> header; the stub maps it to
    /// <c>tenant_id=tenant-a</c>/<c>NameIdentifier=user-a</c>.
    /// </summary>
    public const string HermeticTestStaticToken = "hermetic-test-token";

    public const string HermeticTestTenantId = "tenant-a";

    public const string HermeticTestUserId = "user-a";

    public const string HermeticTestSchemeName = "HermeticTest";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddFluentUIComponents();

        // Story 6.10 / F-7 / AC #9 — the BCL System.TimeProvider (net10.0, no package) drives the
        // SkeletonState 400 ms / 2 s perceived-wait thresholds. Registered as a singleton so the same clock
        // is injected everywhere; tests substitute a controllable TimeProvider. Directory.Packages.props is
        // untouched (System.TimeProvider needs no PackageReference).
        services.AddSingleton(TimeProvider.System);

        services.AddHexalithFrontComposerQuickstart(
            o => o.ScanAssemblies(typeof(Program).Assembly));
        services.AddFrontComposerDevMode(environment);

        services.AddOptions<FoldersAuthenticationOptions>()
            .Bind(configuration.GetSection(FoldersAuthenticationOptions.SectionName));

        FoldersAuthenticationOptions authenticationOptions = new();
        configuration.GetSection(FoldersAuthenticationOptions.SectionName).Bind(authenticationOptions);

        ConfigureAuthentication(services, environment, authenticationOptions);

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenDelegatingHandler>();

        ConfigureFoldersClient(services, configuration);

        // services.Replace (rather than TryAddScoped or AddScoped) atomically swaps out
        // FrontComposer's NullUserContextAccessor (registered via TryAddScoped in AddHexalithFrontComposer
        // at Shell/Extensions/ServiceCollectionExtensions.cs:236) so the FoldersUserContextAccessor
        // wins deterministically. Decision rationale lives in the story's "Why `services.Replace`" note.
        services.Replace(new ServiceDescriptor(
            typeof(IUserContextAccessor),
            typeof(FoldersUserContextAccessor),
            ServiceLifetime.Scoped));

        return services;
    }

    private static void ConfigureAuthentication(
        IServiceCollection services,
        IHostEnvironment environment,
        FoldersAuthenticationOptions options)
    {
        bool hermetic = string.Equals(options.Mode, FoldersAuthenticationOptions.HermeticTestMode, StringComparison.OrdinalIgnoreCase);
        if (hermetic)
        {
            // Mirrors the Counter sample's P11 guard at Counter.Web/Program.cs:91-96: the
            // non-production auth path is rejected at boot outside Development or Test.
            if (!environment.IsDevelopment() && !environment.IsEnvironment(TestEnvironmentName))
            {
                throw new InvalidOperationException(
                    $"{FoldersAuthenticationOptions.SectionName}:Mode='{FoldersAuthenticationOptions.HermeticTestMode}' is only permitted when ASPNETCORE_ENVIRONMENT is Development or Test. "
                    + "Remove the configuration entry or run with a non-production environment for hermetic E2E smoke runs.");
            }

            services
                .AddAuthentication(HermeticTestSchemeName)
                .AddScheme<AuthenticationSchemeOptions, HermeticTestAuthenticationHandler>(HermeticTestSchemeName, _ => { });
            return;
        }

        // Production path: real OIDC. Authority MUST be present outside Development so a missing
        // env-var cannot silently disable auth in production.
        if (string.IsNullOrWhiteSpace(options.Authority) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{FoldersAuthenticationOptions.SectionName}:Authority is required outside Development; refusing to start with no auth in a non-dev environment.");
        }

        services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
            {
                oidc.Authority = options.Authority;
                oidc.ClientId = options.ClientId;
                oidc.RequireHttpsMetadata = options.RequireHttpsMetadata;
                oidc.ResponseType = "code";
                oidc.SaveTokens = true;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
            });
    }

    private static void ConfigureFoldersClient(IServiceCollection services, IConfiguration configuration)
    {
        string? baseAddress = configuration["Folders:Client:BaseAddress"];
        services
            .AddFoldersClient(o =>
            {
                if (!string.IsNullOrWhiteSpace(baseAddress))
                {
                    o.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
                }
            })
            .AddHttpMessageHandler<BearerTokenDelegatingHandler>();
    }

    /// <summary>
    /// Authentication handler used by the hermetic-test path. It accepts the fixed
    /// <see cref="HermeticTestStaticToken"/> bearer token and surfaces a fixed
    /// <c>tenant_id=tenant-a</c>/<c>NameIdentifier=user-a</c> principal. Strictly Development/Test only
    /// (gated above in <see cref="ConfigureAuthentication"/>).
    /// </summary>
    private sealed class HermeticTestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HermeticTestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? authorization = Request.Headers.Authorization;
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            const string prefix = "Bearer ";
            if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = authorization[prefix.Length..].Trim();
            if (!string.Equals(token, HermeticTestStaticToken, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("Hermetic-test token mismatch."));
            }

            Claim[] claims =
            [
                new Claim("tenant_id", HermeticTestTenantId),
                new Claim(ClaimTypes.NameIdentifier, HermeticTestUserId),
            ];
            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
