namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

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
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public IPlaywright Playwright =>
        _playwright ?? throw new InvalidOperationException("PlaywrightFixture has not completed InitializeAsync.");

    public IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("PlaywrightFixture has not completed InitializeAsync.");

    public async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

        try
        {
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                "Playwright Chromium browser is not installed. Run 'pwsh tests/install-playwright.ps1' once per machine before invoking the UI E2E lane.",
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
