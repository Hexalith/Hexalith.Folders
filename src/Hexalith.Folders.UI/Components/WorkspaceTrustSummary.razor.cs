using System.Globalization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Models;
using Hexalith.Folders.UI.Services;
using Hexalith.FrontComposer.Contracts.Attributes;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.6 / UX-DR5 / F-4 — the Workspace Trust Summary. Renders the full UX-DR5 field set with the
/// operator-disposition badge as the <b>primary</b> visual and the technical state as <b>secondary</b>
/// muted metadata, composing the shipped <see cref="OperatorDispositionBadge"/>,
/// <see cref="TechnicalStateMetadata"/>, and <see cref="RedactedField"/>. Tenant-sensitive fields
/// (repository binding, provider, commit reference) render through <see cref="RedactedField"/>;
/// identifiers render monospace with safe-copy only (UX-DR27).
/// </summary>
public partial class WorkspaceTrustSummary : ComponentBase
{
    private OperatorDispositionLabel _disposition;
    private TenantAccessState _authorizationPosture;
    private BadgeSlot _authorizationSlot;
    private string _authorizationLabel = string.Empty;
    private string _reasonCategoryToken = string.Empty;
    private string _freshnessObservedAt = "Unknown";
    private string? _freshnessObservedAtMachine;

    /// <summary>Gets or sets the assembled trust-summary view-model.</summary>
    [Parameter]
    [EditorRequired]
    public WorkspaceTrustSummaryModel Model { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Model);

        // AC #7: prefer the server-computed disposition when a diagnostics DTO supplied one; otherwise
        // derive it from the current lifecycle state using the boolean projection-lag signal (never a
        // hardcoded numeric C2 lag threshold).
        _disposition = Model.ServerDisposition
            ?? DispositionLabelMapper.ResolveDisposition(Model.CurrentState, Model.HasProjectionLagEvidence);

        _authorizationPosture = Model.AuthorizationPosture;
        _authorizationSlot = TenantScopeStateMapper.ResolveSlot(_authorizationPosture);
        _authorizationLabel = TenantScopeStateMapper.ResolveLabel(_authorizationPosture);

        // 'success' is not an operator-facing failure reason — render "None" for a healthy workspace.
        _reasonCategoryToken = ConsoleStatusText.ResolveReasonCategoryLabel(Model.LatestReasonCategory);

        if (Model.FreshnessObservedAt is { } observedAt)
        {
            _freshnessObservedAt = observedAt.ToString("u", CultureInfo.InvariantCulture);
            _freshnessObservedAtMachine = observedAt.ToString("o", CultureInfo.InvariantCulture);
        }
        else
        {
            _freshnessObservedAt = "Unknown";
            _freshnessObservedAtMachine = null;
        }
    }
}
