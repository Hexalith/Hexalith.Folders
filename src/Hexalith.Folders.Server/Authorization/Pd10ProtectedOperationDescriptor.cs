using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Authorization;

/// <summary>Describes one and only one protected PD10 v2 method-and-route identity.</summary>
/// <param name="OperationId">Stable OpenAPI operation identifier.</param>
/// <param name="Method">Exact HTTP method.</param>
/// <param name="CandidateRoute">Exact candidate route template.</param>
/// <param name="HistoricalRoute">Exact historical route template used only by the isolated seam.</param>
/// <param name="OperationFamily">The protected family grant required by the operation.</param>
/// <param name="ActionToken">The exact authorization action token.</param>
/// <param name="PolicyClass">Whether authorization uses mutation or strict-read freshness.</param>
/// <param name="FolderScope">How the folder authorization scope is established.</param>
/// <param name="TaskBinding">Whether task-to-folder binding must be proven.</param>
internal sealed record Pd10ProtectedOperationDescriptor(
    string OperationId,
    string Method,
    string CandidateRoute,
    string HistoricalRoute,
    V2ProtectedOperationFamily OperationFamily,
    string ActionToken,
    FolderOperationPolicyClass PolicyClass,
    Pd10FolderScopeRule FolderScope,
    Pd10TaskBindingRule TaskBinding);
