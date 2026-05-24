namespace Hexalith.Folders.Client;

/// <summary>
/// Options that configure the Hexalith.Folders typed SDK client.
/// </summary>
/// <remarks>
/// The typed client is generated with <c>useBaseUrl: false</c>, so the absolute base address of the
/// Folders server must be supplied here (or bound from the <see cref="DefaultConfigurationSectionName"/>
/// configuration section). Authentication is intentionally not configured here: attach a bearer token via
/// a <see cref="System.Net.Http.DelegatingHandler"/> on the returned <see cref="Microsoft.Extensions.DependencyInjection.IHttpClientBuilder"/>.
/// </remarks>
public sealed class FoldersClientOptions
{
    /// <summary>
    /// The default configuration section name bound by the parameterless registration overload.
    /// </summary>
    public const string DefaultConfigurationSectionName = "Folders";

    /// <summary>
    /// Gets or sets the absolute base address of the Folders server (for example, <c>https://folders.internal/</c>).
    /// </summary>
    /// <remarks>
    /// This is the transport endpoint only; it is never a tenant authority. Tenant authority comes from the
    /// authenticated principal claims carried on each request.
    /// </remarks>
    public Uri? BaseAddress { get; set; }
}
