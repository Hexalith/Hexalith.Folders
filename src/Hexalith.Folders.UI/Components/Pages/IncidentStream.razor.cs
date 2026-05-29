using System.Globalization;
using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 6.9 / F-6 — the incident-mode last-resort read path (wireflow §3.5). A strictly read-only,
/// metadata-only operator surface reached via a runbook link when projections are degraded. It delivers the
/// three F-6 guardrails on top of the already-shipped folder-scoped <see cref="IClient.ListOperationTimelineAsync"/>
/// read (Story 6.1): (1) a PERSISTENT degraded-mode banner with the last projection checkpoint, (2) the
/// Story 6.3 operator disposition rendered BESIDE the raw technical state transition, and (3) a one-click
/// correlation-id + time-window copy affordance for incident handoff.
/// </summary>
/// <remarks>
/// <para>
/// Scope variance (documented in the Dev Agent Record): the architecture's F-6 vision of a
/// projection-INDEPENDENT authoritative event-stream read is not shippable in this MVP without touching the
/// frozen OpenAPI spine / generated client (which expose no raw-event/incident endpoint) and would violate
/// the UI's no-direct-EventStore boundary (F-2, concern #11). The deeper independence is therefore
/// reference-pending; this story delivers the F-6 operator experience on top of the existing
/// operation-timeline read, treating each timeline entry as a raw incident event row.
/// </para>
/// <para>
/// Redaction does NOT relax because projections are degraded (§3.6 / F-5): every redactable field routes
/// through the shared <c>RedactedField</c> via the reused <see cref="Hexalith.Folders.UI.Components.Models.OperationTimelineEntryView"/>
/// assembler — the identical mapping the operation-timeline page uses. Tenant scope is sourced from the
/// authenticated context (<c>TenantScopeBanner</c>), never the <c>?folder=</c> query; the incident
/// permission decision is the server's (a denial surfaces as a safe <c>ConsoleErrorPanel</c>), never a
/// client-side gate on a self-asserted claim.
/// </para>
/// </remarks>
public partial class IncidentStream : ComponentBase
{
    /// <summary>Bounded page size for the paginated incident stream (server clamps to its max).</summary>
    private const int PageLimit = 50;

    private OperationTimelinePage? _timeline;
    private EffectivePermissions? _permissions;
    private ConsoleErrorView? _error;
    private bool _unavailable;
    private bool _loading;
    private bool _folderless;
    private string _correlationId = string.Empty;

    /// <summary>The folder whose incident stream is inspected. Query param (the only data source is folder-scoped).</summary>
    [Parameter]
    [SupplyParameterFromQuery]
    public string? Folder { get; set; }

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

        // Eventually-consistent read-only browsing (concern #19). Even in "incident" framing this is still a
        // passive read — never the active snapshot_per_task probe (Dev Notes call-discipline).
        ReadConsistencyClass freshness = ReadConsistencyClass.Eventually_consistent;

        // Folderless: the page root + banner + single <h1> still render; we attempt NO read and guide the
        // operator to open the stream for a specific folder (AC #9a). The checkpoint is honestly "unknown".
        if (string.IsNullOrWhiteSpace(Folder))
        {
            _folderless = true;
            _loading = false;
            return;
        }

        // Advisory for the scope banner; a real authorization denial surfaces on the primary read below.
        _permissions = await TryReadAsync(() =>
            Client.GetEffectivePermissionsAsync(Folder, _correlationId, freshness)).ConfigureAwait(false);

        // Primary read. C4: the filter key vocabulary is rejection-only today, so always pass filter: null.
        try
        {
            _timeline = await Client
                .ListOperationTimelineAsync(Folder, _correlationId, freshness, Cursor, PageLimit, filter: null)
                .ConfigureAwait(false);
        }
        catch (HexalithFoldersApiException ex)
        {
            // The incident-permission / ACL decision is the server's. Surface the canonical category token
            // only (never raw server text, never a not_found-vs-denied existence oracle) and suppress the
            // event table; the degraded + scope banners and single <h1> still render (AC #8).
            _error = ConsoleErrorPresenter.FromException(ex, _correlationId);
            _loading = false;
            return;
        }
        catch (HttpRequestException)
        {
            // Transport failure (read model / API unreachable), not a canonical denial — render the §3.8
            // read-model-unavailable empty state rather than leaking a transport error (metadata-only).
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
        _folderless = false;
        _timeline = null;
        _permissions = null;
        _correlationId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// The "Last projection checkpoint" value for the persistent degraded banner: the timeline read's
    /// freshness observed-at (UTC, <c>"u"</c> format). Absent/default freshness renders "unknown" — never a
    /// fabricated <c>0001-01-01</c> nor "Current" (UX-DR26 freshness honesty).
    /// </summary>
    private string CheckpointLabel()
        => _timeline?.Freshness is { ObservedAt: var observedAt } && observedAt != default
            ? observedAt.ToString("u", CultureInfo.InvariantCulture)
            : "unknown";

    /// <summary>
    /// The UTC timestamp window ("minTs..maxTs") of the currently-shown events, computed from their evidence
    /// timestamps (default/min values skipped). Returns <see langword="null"/> when no real timestamp is
    /// present, in which case the copy affordance reports the window as "unknown".
    /// </summary>
    private string? TimeWindowLabel()
    {
        if (_timeline?.Entries is not { Count: > 0 } entries)
        {
            return null;
        }

        DateTimeOffset? min = null;
        DateTimeOffset? max = null;
        foreach (OperationTimelineEntry entry in entries)
        {
            if (entry.EvidenceTimestamp == default)
            {
                continue;
            }

            if (min is null || entry.EvidenceTimestamp < min)
            {
                min = entry.EvidenceTimestamp;
            }

            if (max is null || entry.EvidenceTimestamp > max)
            {
                max = entry.EvidenceTimestamp;
            }
        }

        if (min is null || max is null)
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{min.Value.ToString("u", CultureInfo.InvariantCulture)}..{max.Value.ToString("u", CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Builds the next-page route preserving the projection cursor AND the folder; returns
    /// <see langword="null"/> when there is no further page. Read-only navigation only (no form, UX-DR23).
    /// </summary>
    private string? NextPageHref()
    {
        if (_timeline?.Page is not { IsTruncated: true } page || string.IsNullOrWhiteSpace(page.Cursor)
            || string.IsNullOrWhiteSpace(Folder))
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"/_admin/incident-stream?folder={Uri.EscapeDataString(Folder)}&cursor={Uri.EscapeDataString(page.Cursor)}");
    }

    /// <summary>
    /// Honest freshness label: <c>Stale</c> when the projection flags staleness; <c>Current</c> only when a
    /// real observed-at is present; otherwise <c>Unknown</c> — never presenting absent/default freshness as
    /// Current (UX-DR26).
    /// </summary>
    private string FreshnessLabel()
        => _timeline?.Freshness is not { } freshness ? "Unknown"
            : freshness.Stale ? "Stale"
            : freshness.ObservedAt == default ? "Unknown"
            : "Current";

    /// <summary>
    /// Observed-at label; a default/min timestamp (freshness not populated) renders <c>unknown</c>, never a
    /// fabricated <c>0001-01-01</c> date (UX-DR26).
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
