using System.Globalization;
using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 6.8 — the folder-scoped operation-timeline view (wireflow §3.4): the architecture-sanctioned
/// custom "Diagnostic Timeline" (UX-DR8). A strictly read-only, paginated, newest-first list of operation
/// timeline entries read through the direct SDK <see cref="IClient"/>. Each entry surfaces the operator
/// disposition (the PRIMARY status visual, F-4) over the technical <c>from → to</c> lifecycle transition
/// (muted secondary metadata), plus evidence timestamp, operation/task/correlation identifiers, the
/// redactable workspace reference, sanitized result, advisory retryable posture, and duration. No mutation
/// affordance, no filter UI (C4 rejection-only); tenant scope is sourced from the authenticated context via
/// <c>TenantScopeBanner</c> — never the route.
/// </summary>
public partial class OperationTimeline : ComponentBase
{
    /// <summary>Bounded page size for the paginated timeline (a diagnostics flow — keep it small; server clamps to its max).</summary>
    private const int PageLimit = 50;

    private OperationTimelinePage? _timeline;
    private EffectivePermissions? _permissions;
    private ConsoleErrorView? _error;
    private bool _unavailable;
    private bool _loading;
    private string _correlationId = string.Empty;

    /// <summary>The folder whose operation timeline is inspected (route parameter).</summary>
    [Parameter]
    public string FolderId { get; set; } = default!;

    /// <summary>Opaque pagination cursor (query string). Read-only navigation only — never a filter (UX-DR23).</summary>
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

        // Eventually-consistent read-only browsing (concern #19) — never the active snapshot_per_task probe.
        ReadConsistencyClass freshness = ReadConsistencyClass.Eventually_consistent;

        // Advisory for the scope banner; a real authorization denial surfaces on the primary read below.
        _permissions = await TryReadAsync(() =>
            Client.GetEffectivePermissionsAsync(FolderId, _correlationId, freshness)).ConfigureAwait(false);

        // Primary read. C4: the filter key vocabulary is rejection-only today, so always pass filter: null.
        try
        {
            _timeline = await Client
                .ListOperationTimelineAsync(FolderId, _correlationId, freshness, Cursor, PageLimit, filter: null)
                .ConfigureAwait(false);
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
        _timeline = null;
        _permissions = null;
        _correlationId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Builds the next-page route preserving the projection cursor; returns <see langword="null"/> when
    /// there is no further page. Read-only navigation only (no form, no mutation, UX-DR23).
    /// </summary>
    private string? NextPageHref()
    {
        if (_timeline?.Page is not { IsTruncated: true } page || string.IsNullOrWhiteSpace(page.Cursor))
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"/folders/{Uri.EscapeDataString(FolderId)}/operation-timeline?cursor={Uri.EscapeDataString(page.Cursor)}");
    }

    /// <summary>
    /// Honest freshness label: <c>Stale</c> when the projection flags staleness; <c>Current</c> only when a
    /// real observed-at is present; otherwise <c>Unknown</c> — never presenting absent/default freshness as
    /// Current (UX-DR26 / AC #10).
    /// </summary>
    private string FreshnessLabel()
        => _timeline?.Freshness is not { } freshness ? "Unknown"
            : freshness.Stale ? "Stale"
            : freshness.ObservedAt == default ? "Unknown"
            : "Current";

    /// <summary>
    /// Observed-at label; a default/min timestamp (freshness not populated) renders <c>unknown</c>, never a
    /// fabricated <c>0001-01-01</c> date (UX-DR26 / AC #10).
    /// </summary>
    private string ObservedAtLabel()
        => _timeline?.Freshness is { ObservedAt: var observedAt } && observedAt != default
            ? observedAt.ToString("u", CultureInfo.InvariantCulture)
            : "unknown";

    private static async Task<T?> TryReadAsync<T>(Func<Task<T>> read)
        where T : class
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (HexalithFoldersApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
