using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 6.7 — the Provider readiness view (wireflow §3.3): a strictly read-only, folder-scoped page
/// that surfaces provider identity, the credential-reference identifier/status (non-secret), readiness
/// disposition (primary) + reason (secondary), capability/sync metadata, and advisory failure metadata.
/// Reads through the direct SDK <see cref="IClient"/> (the as-built data path — no facade); the
/// provider-status diagnostics read is primary (its denial is the page-level safe denial), the rest are
/// supplementary and degrade to honest Unknown affordances. No mutation, no credential reveal, and the
/// active provider-readiness validation probe is never invoked (AC #5/#10).
/// </summary>
public partial class Provider : ComponentBase
{
    private EffectivePermissions? _permissions;
    private ProviderStatusDiagnostics? _diagnostics;
    private FolderLifecycleStatus? _lifecycle;
    private ProviderBinding? _binding;
    private RepositoryBinding? _repository;
    private SyncStatusDiagnostics? _sync;
    private ProviderOutcome? _outcome;
    private ProviderReadinessModel? _model;
    private ConsoleErrorView? _error;
    private bool _unavailable;
    private bool _loading;
    private string _correlationId = string.Empty;

    /// <summary>The folder whose provider binding is inspected (route parameter).</summary>
    [Parameter]
    public string FolderId { get; set; } = default!;

    /// <summary>
    /// Optional workspace context (query string). When present, the workspace-scoped sync metadata is
    /// fetched; otherwise the page shows an honest "no workspace context" affordance and never fabricates
    /// a workspace id (the same workspace-scoping discipline as the 6.6 folder tree).
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery]
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// Optional operation context (query string). When present together with <see cref="WorkspaceId"/>,
    /// the advisory provider-outcome failure metadata is fetched; otherwise the page renders an honest
    /// Unknown affordance rather than fabricating a retry/remediation value (AC #5).
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery]
    public string? OperationId { get; set; }

    /// <summary>The Folders typed SDK client (registered by <c>AddFoldersClient</c>).</summary>
    [Inject]
    private IClient Client { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ResetState();

        // Eventually-consistent read-only browsing (concern #19) — never the active snapshot_per_task probe.
        ReadConsistencyClass freshness = ReadConsistencyClass.Eventually_consistent;

        // Advisory for the banner; a real authorization denial surfaces on the diagnostics read below.
        _permissions = await TryReadAsync(() =>
            Client.GetEffectivePermissionsAsync(FolderId, _correlationId, freshness)).ConfigureAwait(false);

        // Primary read. Authorization-before-observation: a canonical denial here is the page-level safe denial.
        try
        {
            _diagnostics = await Client
                .GetProviderStatusDiagnosticsAsync(FolderId, _correlationId, freshness)
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

        if (_diagnostics is null)
        {
            _unavailable = true;
            _loading = false;
            return;
        }

        // Supplementary reads — a denial/unavailability on any of these must not blow up the page; the
        // affected zone renders its honest Unknown/unavailable state instead.
        _lifecycle = await TryReadAsync(() =>
            Client.GetFolderLifecycleStatusAsync(FolderId, _correlationId, freshness)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_lifecycle?.ProviderBindingRef))
        {
            _binding = await TryReadAsync(() =>
                Client.GetProviderBindingAsync(_lifecycle!.ProviderBindingRef, _correlationId, freshness)).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(_lifecycle?.RepositoryBindingId))
        {
            _repository = await TryReadAsync(() =>
                Client.GetRepositoryBindingAsync(FolderId, _lifecycle!.RepositoryBindingId, _correlationId, freshness)).ConfigureAwait(false);
        }

        // Workspace-scoped sync metadata only when a workspace context is resolved (do not fabricate one).
        if (!string.IsNullOrWhiteSpace(WorkspaceId))
        {
            _sync = await TryReadAsync(() =>
                Client.GetSyncStatusDiagnosticsAsync(FolderId, WorkspaceId!, _correlationId, freshness)).ConfigureAwait(false);
        }

        // Advisory failure metadata only when an operation context is resolved (display only, never an action).
        if (!string.IsNullOrWhiteSpace(WorkspaceId) && !string.IsNullOrWhiteSpace(OperationId))
        {
            _outcome = await TryReadAsync(() =>
                Client.GetProviderOutcomeAsync(FolderId, WorkspaceId!, OperationId!, _correlationId, freshness)).ConfigureAwait(false);
        }

        _model = ProviderReadinessModel.Create(FolderId, _correlationId, _diagnostics, _binding, _repository, _sync, _outcome);
        _loading = false;
    }

    private void ResetState()
    {
        _loading = true;
        _error = null;
        _unavailable = false;
        _permissions = null;
        _diagnostics = null;
        _lifecycle = null;
        _binding = null;
        _repository = null;
        _sync = null;
        _outcome = null;
        _model = null;
        _correlationId = Guid.NewGuid().ToString();
    }

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
