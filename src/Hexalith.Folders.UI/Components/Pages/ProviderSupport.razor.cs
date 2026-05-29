using System.Globalization;
using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 6.7 — the tenant-scoped Provider support / capability page (FR57): a strictly read-only
/// capability-support matrix read directly from the <c>GetProviderSupportEvidenceAsync</c> projection.
/// Capability differences (GitHub vs Forgejo) are read from explicit evidence and never inferred from a
/// failed operation. Pagination uses the projection's cursor (read-only navigation via the query string,
/// within the server-accepted vocabulary); the page never client-pre-filters rows (authorization already
/// ran server-side).
/// </summary>
public partial class ProviderSupport : ComponentBase, IDisposable
{
    /// <summary>Bounded page size for the paginated support-evidence list (a status/diagnostics flow — keep it small).</summary>
    private const int PageLimit = 50;

    private ProviderSupportEvidenceList? _evidence;
    private ConsoleErrorView? _error;
    private bool _unavailable;
    private bool _loading;
    private bool _cancelled;
    private CancellationTokenSource? _cts;
    private string _correlationId = string.Empty;

    /// <summary>Opaque pagination cursor (query string). The only filter input the server accepts today (C4).</summary>
    [Parameter]
    [SupplyParameterFromQuery]
    public string? Cursor { get; set; }

    /// <summary>The Folders typed SDK client (registered by <c>AddFoldersClient</c>).</summary>
    [Inject]
    private IClient Client { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        CancellationToken token = _cts!.Token;

        // Eventually-consistent read-only browsing (concern #19).
        ReadConsistencyClass freshness = ReadConsistencyClass.Eventually_consistent;

        try
        {
            _evidence = await Client
                .GetProviderSupportEvidenceAsync(_correlationId, freshness, Cursor, PageLimit, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // F-7 cancel: operator cancelled this in-flight read — neutral cancelled state, not _error/_unavailable.
            _cancelled = true;
            _loading = false;
            return;
        }
        catch (HexalithFoldersApiException ex)
        {
            _error = ConsoleErrorPresenter.FromException(ex, _correlationId);
            _loading = false;
            return;
        }
        catch (HttpRequestException)
        {
            // The read model / API is unreachable (transport failure, not a canonical denial). Render the
            // §3.8 read-model-unavailable empty state rather than leaking a transport error (metadata-only).
            _unavailable = true;
            _loading = false;
            return;
        }
        catch (TaskCanceledException)
        {
            _unavailable = true;
            _loading = false;
            return;
        }

        _loading = false;
    }

    private void ResetState()
    {
        _loading = true;
        _error = null;
        _unavailable = false;
        _cancelled = false;
        _evidence = null;
        // One fresh CancellationTokenSource per load; dispose any prior so reloads never leak a token source.
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _correlationId = Guid.NewGuid().ToString();
    }

    /// <summary>F-7 Cancel: aborts the in-flight read; the cancelled read resolves to the neutral cancelled state.</summary>
    private Task CancelAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>The read-only reload affordance for the neutral cancelled state — the same route the user is on.</summary>
    private string ReloadHref()
        => string.IsNullOrWhiteSpace(Cursor)
            ? "/providers/support"
            : string.Create(CultureInfo.InvariantCulture, $"/providers/support?cursor={Uri.EscapeDataString(Cursor)}");

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds the next-page route preserving the projection cursor; returns <see langword="null"/> when
    /// there is no further page. Read-only navigation only (no form, no mutation, UX-DR23).
    /// </summary>
    private string? NextPageHref()
    {
        if (_evidence?.Page is not { IsTruncated: true } page || string.IsNullOrWhiteSpace(page.Cursor))
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"/providers/support?cursor={Uri.EscapeDataString(page.Cursor)}");
    }

    /// <summary>
    /// Honest freshness label for the evidence list: <c>Stale</c> / <c>Current</c>, or <c>Unknown</c> when
    /// the projection returned no freshness — never presenting absent freshness as Current (UX-DR26 / AC #8).
    /// </summary>
    private string FreshnessLabel()
        => _evidence?.Freshness is not { } freshness ? "Unknown" : freshness.Stale ? "Stale" : "Current";

    /// <summary>
    /// Observed-at label; a default/min timestamp (freshness not populated) renders <c>unknown</c>, never a
    /// fabricated <c>0001-01-01</c> date (mirrors the 6.6 Workspace page's ObservedAtOrNull discipline).
    /// </summary>
    private string ObservedAtLabel()
        => _evidence?.Freshness is { ObservedAt: var observedAt } && observedAt != default
            ? observedAt.ToString("u", CultureInfo.InvariantCulture)
            : "unknown";
}
