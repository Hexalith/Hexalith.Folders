using System.Net.Http;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Rendering;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components.Pages;

/// <summary>
/// Story 6.6 / §3.2 — the Workspace view. Composes the Tenant Scope Banner, Workspace Trust Summary,
/// the predictable UX-DR18 sections (Overview | Folder metadata | Diagnosis | Audit trail | Provider
/// readiness | Lock/task history | Access evidence), the metadata-only folder tree, and the trust
/// matrix. Reads through the SDK <see cref="IClient"/> directly (diagnostics primary + status DTOs
/// supplementary). Strictly read-only.
/// </summary>
public partial class Workspace : ComponentBase, IDisposable
{
    /// <summary>The folder identifier from the route.</summary>
    [Parameter]
    public string FolderId { get; set; } = default!;

    /// <summary>The workspace identifier from the route.</summary>
    [Parameter]
    public string WorkspaceId { get; set; } = default!;

    /// <summary>The Folders typed SDK client (registered by <c>AddFoldersClient</c>).</summary>
    [Inject]
    private IClient Client { get; set; } = default!;

    /// <summary>The authenticated-context bridge supplying the authoritative tenant.</summary>
    [Inject]
    private IUserContextAccessor UserContext { get; set; } = default!;

    private bool _loading = true;
    private bool _cancelled;
    private CancellationTokenSource? _cts;
    private ConsoleErrorView? _error;
    private EffectivePermissions? _permissions;
    private WorkspaceTrustSummaryModel? _summary;
    private IReadOnlyList<TrustMatrixCell> _trustCells = [];
    private IReadOnlyList<FileMetadataItem>? _fileItems;
    private WorkspaceLockStatus? _lock;
    private WorkspaceCleanupStatus? _cleanup;
    private DirtyStateDiagnostics? _dirty;
    private WorkspaceStatus? _status;
    private string? _taskId;
    private string _correlationId = string.Empty;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        ResetState();
        CancellationToken token = _cts!.Token;

        ReadConsistencyClass freshness = ReadConsistencyClass.Eventually_consistent;

        // Advisory for the banner; a real authorization denial surfaces on the workspace-status read.
        try
        {
            _permissions = await Client.GetEffectivePermissionsAsync(FolderId, _correlationId, freshness, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            _cancelled = true;
            _loading = false;
            return;
        }
        catch (HexalithFoldersApiException)
        {
            _permissions = null;
        }
        catch (HttpRequestException)
        {
            // Transport failure on the advisory permissions read is non-fatal; the banner degrades and the
            // primary workspace-status read below drives the page-level read-model-unavailable state.
            _permissions = null;
        }
        catch (TaskCanceledException)
        {
            _permissions = null;
        }

        // Primary read. Authorization-before-observation: a denial here is the page-level safe denial.
        try
        {
            _status = await Client.GetWorkspaceStatusAsync(FolderId, WorkspaceId, _correlationId, freshness, token).ConfigureAwait(false);
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
            // The read model / API is unreachable (transport failure, not a canonical denial). Fall through to
            // the §3.8 read-model-unavailable empty state (the same surface as a null status) rather than
            // crashing to a 500 — and never leak a transport error (metadata-only). Mirrors AuditTrail /
            // OperationTimeline / Provider / ProviderSupport / IncidentStream.
            _loading = false;
            return;
        }
        catch (TaskCanceledException)
        {
            _loading = false;
            return;
        }

        if (_status is null)
        {
            _loading = false;
            return;
        }

        _taskId = NullIfBlank(_status.AcceptedCommandState?.TaskId);
        string? operationId = NullIfBlank(_status.AcceptedCommandState?.OperationId);

        FolderLifecycleStatus? lifecycle = await TryReadAsync(ct =>
            Client.GetFolderLifecycleStatusAsync(FolderId, _correlationId, freshness, ct), token).ConfigureAwait(false);

        _lock = await TryReadAsync(ct =>
            Client.GetWorkspaceLockAsync(FolderId, WorkspaceId, _correlationId, freshness, ct), token).ConfigureAwait(false);

        _dirty = await TryReadAsync(ct =>
            Client.GetDirtyStateDiagnosticsAsync(FolderId, WorkspaceId, _correlationId, freshness, ct), token).ConfigureAwait(false);

        CommitEvidence? commit = operationId is null
            ? null
            : await TryReadAsync(ct =>
                Client.GetCommitEvidenceAsync(FolderId, WorkspaceId, operationId, _correlationId, freshness, ct), token).ConfigureAwait(false);

        // The cleanup and file-tree reads require a task id; never fabricate one when absent.
        if (_taskId is not null)
        {
            _cleanup = await TryReadAsync(ct =>
                Client.GetWorkspaceCleanupStatusAsync(FolderId, WorkspaceId, _correlationId, _taskId, freshness, ct), token).ConfigureAwait(false);

            FileTreeResult? tree = await TryReadAsync(ct =>
                Client.ListFolderFilesAsync(FolderId, WorkspaceId, _correlationId, _taskId, freshness, null, null, ct), token).ConfigureAwait(false);
            _fileItems = tree?.Items is null ? null : [.. tree.Items];
        }

        // A cancel during the supplementary reads (swallowed to null above) still lands on the neutral
        // cancelled state rather than rendering a partial workspace view.
        if (_cts.IsCancellationRequested)
        {
            _cancelled = true;
            _loading = false;
            return;
        }

        _summary = BuildSummary(lifecycle, commit);
        _trustCells = BuildTrustCells();
        _loading = false;
    }

    private void ResetState()
    {
        _loading = true;
        _cancelled = false;
        _error = null;
        _permissions = null;
        _summary = null;
        _trustCells = [];
        _fileItems = null;
        _lock = null;
        _cleanup = null;
        _dirty = null;
        _status = null;
        _taskId = null;
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

    /// <summary>The read-only reload affordance for the neutral cancelled state — the same workspace route.</summary>
    private string ReloadHref()
        => $"/folders/{Uri.EscapeDataString(FolderId)}/workspaces/{Uri.EscapeDataString(WorkspaceId)}";

    private WorkspaceTrustSummaryModel BuildSummary(FolderLifecycleStatus? lifecycle, CommitEvidence? commit)
    {
        bool stale = _status!.Freshness?.Stale ?? false;

        (FieldDisclosure commitDisclosure, string? commitText) = commit is null
            ? (FieldDisclosure.Unknown, (string?)null)
            : (ConsoleStatusText.ResolveCommitDisclosure(commit.CommitReferenceClassification),
               ConsoleStatusText.ResolveCommitReferenceText(commit.CommitReferenceClassification));

        string? repositoryBinding = NullIfBlank(lifecycle?.RepositoryBindingId);
        string? provider = NullIfBlank(lifecycle?.ProviderBindingRef);

        return new WorkspaceTrustSummaryModel
        {
            Tenant = NullIfBlank(UserContext.TenantId) ?? "Unknown",
            Folder = FolderId,
            WorkspaceId = WorkspaceId,
            CurrentState = _status.CurrentState,
            HasProjectionLagEvidence = stale,
            // AC #7: prefer the server-computed disposition from the diagnostics DTO as the primary visual;
            // the summary falls back to the canonical client mapper only when no diagnostic was returned.
            ServerDisposition = _dirty?.Disposition,
            AuthorizationPosture = TenantScopeStateMapper.Resolve(_permissions),
            LockState = _lock?.LockState,
            DirtyState = NullIfBlank(_dirty?.Status) ?? "Unknown",
            DirtyDisposition = _dirty?.Disposition,
            TaskId = _taskId ?? "Unknown",
            CorrelationId = _correlationId,
            RepositoryBindingDisclosure = repositoryBinding is null ? FieldDisclosure.Unknown : FieldDisclosure.Visible,
            RepositoryBinding = repositoryBinding,
            ProviderDisclosure = provider is null ? FieldDisclosure.Unknown : FieldDisclosure.Visible,
            Provider = provider,
            CommitReferenceDisclosure = commitDisclosure,
            CommitReferenceText = commitText,
            LatestReasonCategory = _status.LastFailureCategory,
            FreshnessObservedAt = ObservedAtOrNull(_status.Freshness),
            ProjectionWatermark = NullIfBlank(_status.Freshness?.ProjectionWatermark),
            FreshnessStale = stale,
            FreshnessReasonCode = NullIfBlank(_dirty?.Trust?.StaleReasonCode),
        };
    }

    private IReadOnlyList<TrustMatrixCell> BuildTrustCells()
    {
        TenantAccessState posture = TenantScopeStateMapper.Resolve(_permissions);
        OperatorDispositionLabel disposition = DispositionLabelMapper.ResolveDisposition(
            _status!.CurrentState,
            _status.Freshness?.Stale ?? false);
        ProviderOutcomeState? providerOutcome = _status.ProviderOutcome?.State;

        // UX-DR19 connected-evidence: every cell links to its supporting evidence. Most are same-page
        // fragment anchors (the sections already exist); the provider-readiness and audit-traceability
        // cells link out to the dedicated pages shipped by Stories 6.7 and 6.8 respectively.
        return
        [
            new TrustMatrixCell(
                "Tenant boundary",
                TrustDimensionDeriver.FromAuthorization(posture),
                TenantScopeStateMapper.ResolveLabel(posture),
                ObservedAtOrNull(_permissions?.Freshness),
                "#console-page-workspace-section-access-evidence",
                "Access evidence section"),
            new TrustMatrixCell(
                "Provider readiness",
                TrustDimensionDeriver.FromProviderOutcome(providerOutcome),
                "Provider binding, credential-reference status, readiness, and failure diagnostics.",
                ObservedAtOrNull(_status.ProviderOutcome?.Freshness),
                $"/folders/{FolderId}/provider",
                "Provider readiness page"),
            new TrustMatrixCell(
                "Workspace lifecycle",
                TrustDimensionDeriver.FromDisposition(disposition),
                ConsoleStatusText.ResolveReasonCategoryLabel(_status.LastFailureCategory),
                ObservedAtOrNull(_status.Freshness),
                "#console-page-workspace-section-diagnosis",
                "Diagnosis section"),
            new TrustMatrixCell(
                "Lock state",
                TrustDimensionDeriver.FromLockState(_lock?.LockState),
                _lock?.LockState is { } lockState ? ConsoleStatusText.ResolveLockLabel(lockState) : "Lock evidence not available.",
                ObservedAtOrNull(_lock?.Freshness),
                "#console-page-workspace-section-lock-task-history",
                "Lock/task history section"),
            new TrustMatrixCell(
                "Folder metadata visibility",
                _fileItems is null ? TrustDimensionState.Unknown : TrustDimensionState.Ready,
                _fileItems is null ? "No workspace-bound metadata in scope." : "Metadata-only evidence available below.",
                ObservedAtOrNull(_status.Freshness),
                "#console-page-workspace-section-folder-metadata",
                "Folder metadata section"),
            new TrustMatrixCell(
                "Audit traceability",
                TrustDimensionState.Unknown,
                "Metadata-only audit trail and operation timeline for this folder.",
                null,
                $"/folders/{FolderId}/audit-trail",
                "Audit trail page"),
        ];
    }

    // A default/min DateTimeOffset (read model didn't populate freshness) must render as "Unknown", never
    // as a fabricated 0001-01-01 timestamp.
    private static DateTimeOffset? ObservedAtOrNull(FreshnessMetadata? freshness)
        => freshness is { ObservedAt: var observedAt } && observedAt != default ? observedAt : null;

    private static async Task<T?> TryReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token)
        where T : class
    {
        try
        {
            return await read(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Operator cancel during a supplementary read; the post-read cancel guard drives the cancelled state.
            return null;
        }
        catch (HexalithFoldersApiException)
        {
            // A denial or unavailability on a supplementary panel must not blow up the whole page;
            // the affected panel renders its unknown/unavailable state instead.
            return null;
        }
        catch (HttpRequestException)
        {
            // A transport failure on a supplementary panel degrades that panel to unknown/unavailable, never
            // the whole page (metadata-only).
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
