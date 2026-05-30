using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Folders.Server.Authentication;

public static class FoldersAuthenticationServiceCollectionExtensions
{
    private const string LegacyJwtBearerSectionName = "Authentication:JwtBearer";

    public static IServiceCollection AddFoldersProductionAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<FoldersOidcOptions>()
            .Configure(options =>
            {
                configuration.GetSection(FoldersOidcOptions.SectionName).Bind(options);

                // Aspire local hosts already emit these non-secret keys. Keep them as a compatibility
                // fallback while the production contract moves to Folders:Authentication.
                IConfigurationSection fallback = configuration.GetSection(LegacyJwtBearerSectionName);
                options.Authority ??= fallback["Authority"];
                options.MetadataAddress ??= fallback["MetadataAddress"];
                options.Audience ??= fallback["Audience"];
                options.ValidIssuer ??= fallback["Issuer"] ?? fallback["ValidIssuer"];

                if (fallback["RequireHttpsMetadata"] is { Length: > 0 } requireHttps
                    && !configuration.GetSection(FoldersOidcOptions.SectionName).GetChildren().Any(static c => c.Key == nameof(FoldersOidcOptions.RequireHttpsMetadata)))
                {
                    options.RequireHttpsMetadata = bool.Parse(requireHttps);
                }
            })
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FoldersOidcOptions>>(static sp =>
            new FoldersOidcOptionsValidator(sp.GetRequiredService<IHostEnvironment>())));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                FoldersOidcOptions oidc = ResolveOidcOptions(configuration);
                ApplyJwtBearerOptions(options, oidc);
            });

        services.Configure<TenantContextOptions>(static options =>
        {
            options.TenantClaimType = TenantContextOptions.EventStoreTenantClaimType;
            options.PrincipalClaimType = TenantContextOptions.SubjectClaimType;
        });

        return services;
    }

    private static FoldersOidcOptions ResolveOidcOptions(IConfiguration configuration)
    {
        FoldersOidcOptions options = new();
        configuration.GetSection(FoldersOidcOptions.SectionName).Bind(options);

        IConfigurationSection fallback = configuration.GetSection(LegacyJwtBearerSectionName);
        options.Authority ??= fallback["Authority"];
        options.MetadataAddress ??= fallback["MetadataAddress"];
        options.Audience ??= fallback["Audience"];
        options.ValidIssuer ??= fallback["Issuer"] ?? fallback["ValidIssuer"];
        return options;
    }

    private static void ApplyJwtBearerOptions(JwtBearerOptions options, FoldersOidcOptions oidc)
    {
        options.Authority = BlankToNull(oidc.Authority);
        options.MetadataAddress = BlankToNull(oidc.MetadataAddress);
        options.Audience = BlankToNull(oidc.Audience);
        options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.AutomaticRefreshInterval = TimeSpan.FromMinutes(10);
        options.RefreshInterval = TimeSpan.FromMinutes(1);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = BlankToNull(oidc.ValidIssuer) ?? BlankToNull(oidc.Authority),
            ValidateAudience = true,
            ValidAudience = BlankToNull(oidc.Audience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = TenantContextOptions.SubjectClaimType,
        };
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FoldersOidcOptionsValidator(IHostEnvironment environment) : IValidateOptions<FoldersOidcOptions>
    {
        public ValidateOptionsResult Validate(string? name, FoldersOidcOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
            {
                return ValidateOptionsResult.Success;
            }

            List<string> failures = [];
            Require(nameof(options.Audience), options.Audience, failures);
            Require(nameof(options.ValidIssuer), options.ValidIssuer ?? options.Authority, failures);

            if (string.IsNullOrWhiteSpace(options.Authority) && string.IsNullOrWhiteSpace(options.MetadataAddress))
            {
                failures.Add($"{FoldersOidcOptions.SectionName}:Authority or MetadataAddress is required outside Development/Test.");
            }

            if (!options.RequireHttpsMetadata)
            {
                failures.Add($"{FoldersOidcOptions.SectionName}:RequireHttpsMetadata must be true outside Development/Test.");
            }

            ValidateHttps(nameof(options.Authority), options.Authority, failures);
            ValidateHttps(nameof(options.MetadataAddress), options.MetadataAddress, failures);

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        private static void Require(string key, string? value, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"{FoldersOidcOptions.SectionName}:{key} is required outside Development/Test.");
            }
        }

        private static void ValidateHttps(string key, string? value, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                failures.Add($"{FoldersOidcOptions.SectionName}:{key} must be an absolute HTTPS URI outside Development/Test.");
            }
        }
    }
}
