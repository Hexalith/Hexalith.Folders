using Microsoft.Extensions.Configuration;

namespace Hexalith.Folders.Server;

/// <summary>Reads and validates the server's API routing mode setting.</summary>
public static class FoldersApiRouting
{
    /// <summary>The configuration key that selects the <see cref="FoldersApiRoutingMode"/>.</summary>
    public const string ModeConfigurationKey = "Folders:ApiRouting:Mode";

    /// <summary>
    /// Resolves the configured routing mode. An absent setting selects <see cref="FoldersApiRoutingMode.V1Only"/>.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The validated routing mode.</returns>
    /// <exception cref="InvalidOperationException">The setting is present but empty or not an exact mode name.</exception>
    public static FoldersApiRoutingMode ResolveMode(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? value = configuration.GetSection(ModeConfigurationKey).Value;
        return value switch
        {
            null => FoldersApiRoutingMode.V1Only,
            nameof(FoldersApiRoutingMode.V1Only) => FoldersApiRoutingMode.V1Only,
            nameof(FoldersApiRoutingMode.Coexistence) => FoldersApiRoutingMode.Coexistence,
            nameof(FoldersApiRoutingMode.V2Only) => FoldersApiRoutingMode.V2Only,
            _ => throw new InvalidOperationException(
                $"{ModeConfigurationKey} must be one of {nameof(FoldersApiRoutingMode.V1Only)}, {nameof(FoldersApiRoutingMode.Coexistence)}, or {nameof(FoldersApiRoutingMode.V2Only)}."),
        };
    }
}
