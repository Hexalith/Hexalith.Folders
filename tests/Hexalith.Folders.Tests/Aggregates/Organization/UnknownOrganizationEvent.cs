using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

/// <summary>Represents a retained organization event with no supported fold handler.</summary>
/// <param name="ManagedTenantId">The stream tenant.</param>
/// <param name="OrganizationId">The stream organization.</param>
/// <param name="CorrelationId">The correlation identifier.</param>
/// <param name="TaskId">The task identifier.</param>
/// <param name="IdempotencyKey">The retained command key.</param>
/// <param name="IdempotencyFingerprint">The retained intent fingerprint.</param>
internal sealed record UnknownOrganizationEvent(string ManagedTenantId, string OrganizationId,
    string CorrelationId, string TaskId, string IdempotencyKey, string IdempotencyFingerprint) : IOrganizationEvent;
