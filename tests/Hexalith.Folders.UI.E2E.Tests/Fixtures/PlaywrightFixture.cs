namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

using Hexalith.Folders.UI;

using Microsoft.Playwright;

using Xunit;

/// <summary>
/// xUnit collection fixture that owns a single <see cref="IPlaywright"/> and
/// headless <see cref="IBrowser"/> instance for the entire UI E2E lane.
/// </summary>
/// <remarks>
/// Browsers must be installed once per machine via <c>pwsh tests/install-playwright.ps1</c>.
/// A missing browser surfaces as an actionable <see cref="InvalidOperationException"/>
/// rather than a hung process.
/// </remarks>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private const string BrowserEnvironmentVariable = "FOLDERS_PLAYWRIGHT_BROWSER";

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public IPlaywright Playwright =>
        _playwright ?? throw new InvalidOperationException("PlaywrightFixture has not completed InitializeAsync.");

    public IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("PlaywrightFixture has not completed InitializeAsync.");

    /// <summary>
    /// Creates a browser context authenticated against the hermetic console host. External requests are
    /// blocked so the fixture's bearer token stays within the local test host, including its Blazor circuit.
    /// </summary>
    /// <param name="baseAddress">The loopback address of the running hermetic console host.</param>
    /// <param name="options">Optional context settings, such as the dense-identifier viewport.</param>
    /// <returns>An authenticated context owned by the calling test.</returns>
    public async Task<IBrowserContext> CreateAuthenticatedContextAsync(
        Uri baseAddress,
        BrowserNewContextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (!baseAddress.IsAbsoluteUri || !baseAddress.IsLoopback
            || (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Hermetic browser contexts require a loopback HTTP host.", nameof(baseAddress));
        }

        options ??= new BrowserNewContextOptions();
        Dictionary<string, string> headers = options.ExtraHTTPHeaders?.ToDictionary(
            static header => header.Key,
            static header => header.Value,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        headers["Authorization"] = $"Bearer {CompositionRoot.HermeticTestStaticToken}";
        options.ExtraHTTPHeaders = headers;
        options.ServiceWorkers = ServiceWorkerPolicy.Block;

        IBrowserContext context = await Browser.NewContextAsync(options).ConfigureAwait(false);
        try
        {
            await context.RouteAsync("**/*", async route =>
            {
                Uri requestAddress = new(route.Request.Url);
                if (!HasSameOrigin(baseAddress, requestAddress))
                {
                    await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
                    return;
                }

                IAPIResponse response = await route.FetchAsync(new RouteFetchOptions
                {
                    MaxRedirects = 0,
                    Headers = route.Request.Headers.Concat(headers)
                        .GroupBy(static header => header.Key, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.OrdinalIgnoreCase),
                }).ConfigureAwait(false);
                try
                {
                    // These hermetic routes have no redirects. Reject the whole chain: Playwright
                    // otherwise routes only its first request, allowing a later hop to escape this origin.
                    if (response.Status is >= 300 and < 400 && response.Headers.ContainsKey("location"))
                    {
                        await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
                        return;
                    }

                    await route.FulfillAsync(new RouteFulfillOptions { Response = response }).ConfigureAwait(false);
                }
                finally
                {
                    await response.DisposeAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            string socketScheme = baseAddress.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            await context.RouteWebSocketAsync(url =>
            {
                Uri socketAddress = new(url);
                return socketAddress.Scheme != socketScheme
                    || socketAddress.Host != baseAddress.Host
                    || socketAddress.Port != baseAddress.Port;
            }, socket => _ = socket.CloseAsync()).ConfigureAwait(false);

            return context;
        }
        catch
        {
            await context.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool HasSameOrigin(Uri expected, Uri actual) => actual.Scheme == expected.Scheme
        && actual.Host == expected.Host && actual.Port == expected.Port;

    public async ValueTask InitializeAsync()
    {
        string browserName = Environment.GetEnvironmentVariable(BrowserEnvironmentVariable)?.Trim().ToLowerInvariant() ?? "chromium";
        if (browserName is not ("" or "chromium" or "firefox"))
        {
            throw new InvalidOperationException($"{BrowserEnvironmentVariable} must be chromium or firefox.");
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        IBrowserType browserType = browserName switch
        {
            "" or "chromium" => _playwright.Chromium,
            "firefox" => _playwright.Firefox,
            _ => throw new InvalidOperationException($"{BrowserEnvironmentVariable} must be chromium or firefox."),
        };

        try
        {
            _browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                $"Playwright {browserType.Name} browser is not installed. Run 'pwsh tests/install-playwright.ps1 -Browser {browserType.Name}' once per machine before invoking the UI E2E lane.",
                ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
    }
}
