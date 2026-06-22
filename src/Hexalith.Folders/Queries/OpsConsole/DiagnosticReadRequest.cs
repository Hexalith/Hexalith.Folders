using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.OpsConsole;

/// <summary>
/// Authorization inputs shared by the folder/workspace-scoped ops-console diagnostics queries. Tenant and
/// principal are authoritative (from authenticated context + EventStore claim-transform evidence); the
/// client-controlled values are comparison inputs only.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the read action token.</param>
/// <param name="FolderId">Folder id (authorization scope).</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
/// <param name="ClientControlledPrincipalValues">Client-asserted principal signals (comparison inputs only).</param>
public sealed record DiagnosticReadRequest(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
