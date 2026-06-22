using System.Text.RegularExpressions;

using Hexalith.Folders.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Reads the five folder/workspace-scoped ops-console diagnostics (lock, dirty-state, failed-operation,
/// provider-status, sync-status). Authorization-before-observation via
/// <see cref="LayeredFolderAuthorizationService"/> (StrictRead, folder ACL evidence) over the folder scope,
/// mirroring <see cref="Folders.WorkspaceTransitionEvidenceQueryHandler"/>; the workspace id is a read-model
/// selector only. Metadata-only; a diagnostic owned by another tenant/folder/workspace is indistinguishable
/// from a missing one.
/// </summary>
public sealed partial class FolderScopedDiagnosticsQueryHandler(
    LayeredFolderAuthorizationService authorizationService,
    IOpsConsoleDiagnosticsReadModel readModel,
    ILogger<FolderScopedDiagnosticsQueryHandler>? logger = null)
{
    /// <summary>Action token authorizing a folder-scoped diagnostic read (folder metadata read).</summary>
    public const string ActionToken = "read_metadata";

    private const string ActorPresentIdentifier = "actor_present";

    private readonly LayeredFolderAuthorizationService _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    private readonly IOpsConsoleDiagnosticsReadModel _readModel = readModel ?? throw new ArgumentNullException(nameof(readModel));
    private readonly ILogger<FolderScopedDiagnosticsQueryHandler> _logger = logger ?? NullLogger<FolderScopedDiagnosticsQueryHandler>.Instance;

    /// <summary>Reads workspace lock diagnostics.</summary>
    public Task<OpsConsoleDiagnosticReadResult<LockDiagnosticsView>> GetLockAsync(
        DiagnosticReadRequest request,
        string workspaceId,
        CancellationToken cancellationToken = default)
        => ReadWorkspaceAsync(
            request,
            workspaceId,
            (allowed, ct) => _readModel.GetLockAsync(allowed.AuthoritativeTenantId, request.FolderId, workspaceId, ct),
            static (view, request, workspaceId) => MatchesWorkspace(view.ManagedTenantId, view.FolderId, view.WorkspaceId, request.AuthoritativeTenantId, request.FolderId, workspaceId),
            cancellationToken);

    /// <summary>Reads workspace dirty-state diagnostics.</summary>
    public Task<OpsConsoleDiagnosticReadResult<DirtyStateDiagnosticsView>> GetDirtyStateAsync(
        DiagnosticReadRequest request,
        string workspaceId,
        CancellationToken cancellationToken = default)
        => ReadWorkspaceAsync(
            request,
            workspaceId,
            (allowed, ct) => _readModel.GetDirtyStateAsync(allowed.AuthoritativeTenantId, request.FolderId, workspaceId, ct),
            static (view, request, workspaceId) => MatchesWorkspace(view.ManagedTenantId, view.FolderId, view.WorkspaceId, request.AuthoritativeTenantId, request.FolderId, workspaceId),
            cancellationToken);

    /// <summary>Reads workspace failed-operation diagnostics.</summary>
    public Task<OpsConsoleDiagnosticReadResult<FailedOperationDiagnosticsView>> GetFailedOperationAsync(
        DiagnosticReadRequest request,
        string workspaceId,
        CancellationToken cancellationToken = default)
        => ReadWorkspaceAsync(
            request,
            workspaceId,
            (allowed, ct) => _readModel.GetFailedOperationAsync(allowed.AuthoritativeTenantId, request.FolderId, workspaceId, ct),
            static (view, request, workspaceId) => MatchesWorkspace(view.ManagedTenantId, view.FolderId, view.WorkspaceId, request.AuthoritativeTenantId, request.FolderId, workspaceId),
            cancellationToken);

    /// <summary>Reads workspace sync-status diagnostics.</summary>
    public Task<OpsConsoleDiagnosticReadResult<SyncStatusDiagnosticsView>> GetSyncStatusAsync(
        DiagnosticReadRequest request,
        string workspaceId,
        CancellationToken cancellationToken = default)
        => ReadWorkspaceAsync(
            request,
            workspaceId,
            (allowed, ct) => _readModel.GetSyncStatusAsync(allowed.AuthoritativeTenantId, request.FolderId, workspaceId, ct),
            static (view, request, workspaceId) => MatchesWorkspace(view.ManagedTenantId, view.FolderId, view.WorkspaceId, request.AuthoritativeTenantId, request.FolderId, workspaceId),
            cancellationToken);

    /// <summary>Reads folder provider-status diagnostics (no workspace selector).</summary>
    public async Task<OpsConsoleDiagnosticReadResult<ProviderStatusDiagnosticsView>> GetProviderStatusAsync(
        DiagnosticReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string safeCorrelationId = SafeCorrelationId(request.CorrelationId);

        if (string.IsNullOrWhiteSpace(request.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(request.PrincipalId))
        {
            return new(DiagnosticReadResultCode.AuthenticationRequired, null, safeCorrelationId);
        }

        if (string.IsNullOrWhiteSpace(request.FolderId))
        {
            return new(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId);
        }

        LayeredFolderAuthorizationResult authorization = await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return new(MapAuthorizationDenial(authorization), null, safeCorrelationId);
        }

        LayeredFolderAuthorizationAllowedContext allowed = authorization.AllowedContext;
        ProviderStatusDiagnosticsView? view;
        try
        {
            view = await _readModel.GetProviderStatusAsync(allowed.AuthoritativeTenantId, request.FolderId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadModelFailure(ex, ex.GetType().FullName);
            return new(DiagnosticReadResultCode.ReadModelUnavailable, null, safeCorrelationId);
        }

        return view is null
            || !string.Equals(view.ManagedTenantId, allowed.AuthoritativeTenantId, StringComparison.Ordinal)
            || !string.Equals(view.FolderId, request.FolderId, StringComparison.Ordinal)
            ? new(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId)
            : new(DiagnosticReadResultCode.Allowed, view, safeCorrelationId);
    }

    private async Task<OpsConsoleDiagnosticReadResult<TView>> ReadWorkspaceAsync<TView>(
        DiagnosticReadRequest request,
        string workspaceId,
        Func<LayeredFolderAuthorizationAllowedContext, CancellationToken, Task<TView?>> read,
        Func<TView, DiagnosticReadRequest, string, bool> matches,
        CancellationToken cancellationToken)
        where TView : class
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string safeCorrelationId = SafeCorrelationId(request.CorrelationId);

        if (string.IsNullOrWhiteSpace(request.AuthoritativeTenantId) || string.IsNullOrWhiteSpace(request.PrincipalId))
        {
            return new(DiagnosticReadResultCode.AuthenticationRequired, null, safeCorrelationId);
        }

        if (string.IsNullOrWhiteSpace(request.FolderId) || string.IsNullOrWhiteSpace(workspaceId))
        {
            return new(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId);
        }

        LayeredFolderAuthorizationResult authorization = await AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!authorization.IsAllowed || authorization.AllowedContext is null)
        {
            return new(MapAuthorizationDenial(authorization), null, safeCorrelationId);
        }

        TView? view;
        try
        {
            view = await read(authorization.AllowedContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReadModelFailure(ex, ex.GetType().FullName);
            return new(DiagnosticReadResultCode.ReadModelUnavailable, null, safeCorrelationId);
        }

        return view is null || !matches(view, request, workspaceId)
            ? new(DiagnosticReadResultCode.NotFoundSafe, null, safeCorrelationId)
            : new(DiagnosticReadResultCode.Allowed, view, safeCorrelationId);
    }

    private Task<LayeredFolderAuthorizationResult> AuthorizeAsync(DiagnosticReadRequest request, CancellationToken cancellationToken)
        => _authorizationService.AuthorizeAsync(
            new LayeredFolderAuthorizationContext(
                request.AuthoritativeTenantId,
                request.PrincipalId,
                ActorSafeIdentifier: ActorPresentIdentifier,
                ActionToken,
                LayeredFolderOperationPolicy.StrictRead(),
                request.ClaimTransformEvidence,
                OperationScope: request.FolderId,
                request.CorrelationId,
                request.TaskId,
                request.ClientControlledTenantValues,
                request.ClientControlledPrincipalValues),
            cancellationToken);

    private void LogReadModelFailure(Exception ex, string? exceptionType)
        => _logger.LogWarning(
            ex,
            "Ops-console diagnostics read-model call failed; returning ReadModelUnavailable. Exception type: {ExceptionType}",
            exceptionType);

    private static bool MatchesWorkspace(
        string viewTenantId,
        string viewFolderId,
        string viewWorkspaceId,
        string? authoritativeTenantId,
        string folderId,
        string workspaceId)
        => !string.IsNullOrEmpty(viewTenantId)
            && string.Equals(viewTenantId, authoritativeTenantId, StringComparison.Ordinal)
            && string.Equals(viewFolderId, folderId, StringComparison.Ordinal)
            && string.Equals(viewWorkspaceId, workspaceId, StringComparison.Ordinal);

    private static DiagnosticReadResultCode MapAuthorizationDenial(LayeredFolderAuthorizationResult authorization)
        => authorization.Decision.OutcomeCode switch
        {
            LayeredAuthorizationOutcomeCodes.AuthenticationDenied => DiagnosticReadResultCode.AuthenticationRequired,
            LayeredAuthorizationOutcomeCodes.SafeNotFound or LayeredAuthorizationOutcomeCodes.FolderAclDenied => DiagnosticReadResultCode.NotFoundSafe,
            LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable
                or LayeredAuthorizationOutcomeCodes.FolderAclUnavailable => DiagnosticReadResultCode.ProjectionUnavailable,
            LayeredAuthorizationOutcomeCodes.TenantProjectionStale
                or LayeredAuthorizationOutcomeCodes.FolderAclStale => DiagnosticReadResultCode.ProjectionStale,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied when authorization.Decision.Retryable => DiagnosticReadResultCode.ReadModelUnavailable,
            LayeredAuthorizationOutcomeCodes.DaprPolicyDenied
                or LayeredAuthorizationOutcomeCodes.ClaimTransformDenied
                or LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied
                or LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed
                or LayeredAuthorizationOutcomeCodes.TenantAccessDenied => DiagnosticReadResultCode.AuthorizationDenied,
            _ => DiagnosticReadResultCode.ReadModelUnavailable,
        };

    private static string SafeCorrelationId(string? value)
        => !string.IsNullOrWhiteSpace(value) && CanonicalIdentifierPattern().IsMatch(value.Trim())
            ? value.Trim()
            : $"correlation_{Guid.NewGuid():N}";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();
}
