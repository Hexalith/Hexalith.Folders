using System.Globalization;
using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 10.5 — the tenant-scoped, read-only semantic-indexing status projection page (AC13). It renders the
/// metadata-only indexing status (indexed / stale / skipped / failed / tombstoned / reconciliation_required) per
/// file version, read directly from the <c>GetFolderIndexingStatusAsync</c> projection. Strictly read-only: no
/// mutation, repair, content, snippets, raw paths, or diffs; redacted entries are visually and semantically
/// distinct from unknown/missing. Mirrors the <c>ProviderSupport</c> page shape and lifecycle.
/// </summary>
public partial class IndexingStatus : ComponentBase, IDisposable
{
    private FolderIndexingStatusResult? _status;
    private ConsoleErrorView? _error;
    private bool _unavailable;
    private bool _loading;
    private bool _cancelled;
    private CancellationTokenSource? _cts;
    private string _correlationId = string.Empty;

    /// <summary>The tenant-scoped folder identifier (route parameter).</summary>
    [Parameter]
    public string FolderId { get; set; } = default!;

    /// <summary>The Folders typed SDK client (registered by <c>AddFoldersClient</c>).</summary>
    [Inject]
    private IClient Client { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        CancellationToken token = _cts!.Token;
        string folderId = FolderId;
        string correlationId = _correlationId;

        // Eventually-consistent read-only browsing; the indexing-status read is not task-scoped (console view).
        string? taskId = null;

        try
        {
            _status = await Client
                .GetFolderIndexingStatusAsync(folderId, correlationId, taskId, ReadConsistencyClass.Eventually_consistent, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsCurrentRequest(folderId, correlationId) && _cts.IsCancellationRequested)
        {
            _cancelled = true;
            _loading = false;
            return;
        }
        catch (OperationCanceledException) when (!IsCurrentRequest(folderId, correlationId))
        {
            return;
        }
        catch (OperationCanceledException) when (IsCurrentRequest(folderId, correlationId))
        {
            _unavailable = true;
            _loading = false;
            return;
        }
        catch (HexalithFoldersApiException ex)
        {
            if (IsCurrentRequest(folderId, correlationId))
            {
                _error = ConsoleErrorPresenter.FromException(ex, correlationId);
                _loading = false;
            }

            return;
        }
        catch (HttpRequestException) when (IsCurrentRequest(folderId, correlationId))
        {
            _unavailable = true;
            _loading = false;
            return;
        }
        catch (TaskCanceledException) when (IsCurrentRequest(folderId, correlationId))
        {
            _unavailable = true;
            _loading = false;
            return;
        }

        if (!IsCurrentRequest(folderId, correlationId))
        {
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
        _status = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _correlationId = Guid.NewGuid().ToString();
    }

    private bool IsCurrentRequest(string folderId, string correlationId)
        => string.Equals(FolderId, folderId, StringComparison.Ordinal)
            && string.Equals(_correlationId, correlationId, StringComparison.Ordinal);

    private Task CancelAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private string ReloadHref()
        => string.Create(CultureInfo.InvariantCulture, $"/folders/{Uri.EscapeDataString(FolderId)}/indexing-status");

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string FreshnessLabel()
        => _status?.Freshness is not { } freshness ? "Unknown" : freshness.Stale ? "Stale" : "Current";

    private string ObservedAtLabel()
        => _status?.Freshness is { ObservedAt: var observedAt } && observedAt != default
            ? observedAt.ToString("u", CultureInfo.InvariantCulture)
            : "unknown";

    // Total mapping (throws nothing; unknown -> neutral) of the indexing status to a Fluent badge slot. Color is
    // never the only signal — the label (StatusLabel) and the text accompany every badge for WCAG 2.2 AA.
    private static BadgeSlot StatusSlot(FolderIndexingStatusEntryIndexingStatus status)
        => status switch
        {
            FolderIndexingStatusEntryIndexingStatus.Indexed => BadgeSlot.Success,
            FolderIndexingStatusEntryIndexingStatus.Stale => BadgeSlot.Warning,
            FolderIndexingStatusEntryIndexingStatus.Reconciliation_required => BadgeSlot.Warning,
            FolderIndexingStatusEntryIndexingStatus.Failed => BadgeSlot.Danger,
            FolderIndexingStatusEntryIndexingStatus.Skipped => BadgeSlot.Neutral,
            FolderIndexingStatusEntryIndexingStatus.Tombstoned => BadgeSlot.Neutral,
            _ => BadgeSlot.Neutral,
        };

    private static string StatusLabel(FolderIndexingStatusEntryIndexingStatus status)
        => status switch
        {
            FolderIndexingStatusEntryIndexingStatus.Indexed => "Indexed",
            FolderIndexingStatusEntryIndexingStatus.Stale => "Stale",
            FolderIndexingStatusEntryIndexingStatus.Skipped => "Skipped",
            FolderIndexingStatusEntryIndexingStatus.Failed => "Failed",
            FolderIndexingStatusEntryIndexingStatus.Tombstoned => "Tombstoned",
            FolderIndexingStatusEntryIndexingStatus.Reconciliation_required => "Reconciliation required",
            _ => "Unknown",
        };

    private static FieldDisclosure RedactionDisclosure(FolderIndexingStatusEntryRedaction redaction)
        => redaction == FolderIndexingStatusEntryRedaction.Redacted
            ? FieldDisclosure.Redacted
            : FieldDisclosure.Visible;
}
