using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Organization;

public sealed class OrganizationAclTenantGate(IOrganizationAclRepository repository)
{
    public OrganizationAclResult Handle(
        IOrganizationAclCommand command,
        TenantAccessAuthorizationResult tenantAccess)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);

        if (!tenantAccess.IsAllowed)
        {
            return OrganizationAclResult.Rejected(
                Map(tenantAccess.Outcome),
                SafePassthrough(tenantAccess.TenantId),
                SafePassthrough(command.OrganizationId),
                SafePassthrough(command.CorrelationId),
                SafePassthrough(command.TaskId),
                SafePassthrough(command.IdempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(tenantAccess.TenantId))
        {
            return OrganizationAclResult.Rejected(command, OrganizationAclResultCode.MissingAuthoritativeTenant);
        }

        if (!string.IsNullOrWhiteSpace(command.PayloadTenantId)
            && !string.Equals(command.PayloadTenantId, tenantAccess.TenantId, StringComparison.Ordinal))
        {
            return OrganizationAclResult.Rejected(
                OrganizationAclResultCode.TenantMismatch,
                tenantAccess.TenantId,
                SafePassthrough(command.OrganizationId),
                SafePassthrough(command.CorrelationId),
                SafePassthrough(command.TaskId),
                SafePassthrough(command.IdempotencyKey));
        }

        IOrganizationAclCommand authoritativeCommand = command.WithManagedTenantId(tenantAccess.TenantId);
        OrganizationAclCommandValidationResult validation = OrganizationAclCommandValidator.Validate(authoritativeCommand);
        if (!validation.IsAccepted)
        {
            return OrganizationAclResult.Rejected(authoritativeCommand, validation.Code, validation.FailingOperation);
        }

        // Fast-path optimistic idempotency check. Atomicity is enforced by AppendIfFingerprintAbsent below.
        if (repository.TryGetIdempotencyFingerprint(
            authoritativeCommand.ManagedTenantId,
            authoritativeCommand.OrganizationId,
            authoritativeCommand.IdempotencyKey,
            out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationAclResult.Rejected(authoritativeCommand, OrganizationAclResultCode.AlreadyApplied)
                : OrganizationAclResult.Rejected(authoritativeCommand, OrganizationAclResultCode.IdempotencyConflict);
        }

        OrganizationStreamName streamName = repository.CreateStreamName(authoritativeCommand.ManagedTenantId, authoritativeCommand.OrganizationId);
        OrganizationState state = repository.Load(streamName);
        OrganizationAclResult result = OrganizationAggregate.Handle(state, authoritativeCommand);
        if (result.Events.Count == 0)
        {
            return result;
        }

        OrganizationAclAppendOutcome outcome = repository.AppendIfFingerprintAbsent(
            streamName,
            authoritativeCommand.IdempotencyKey,
            validation.IdempotencyFingerprint,
            result.Events);

        return outcome switch
        {
            OrganizationAclAppendOutcome.Appended => result,
            OrganizationAclAppendOutcome.FingerprintMatched =>
                OrganizationAclResult.Rejected(authoritativeCommand, OrganizationAclResultCode.AlreadyApplied),
            OrganizationAclAppendOutcome.FingerprintConflict =>
                OrganizationAclResult.Rejected(authoritativeCommand, OrganizationAclResultCode.IdempotencyConflict),
            _ => throw new InvalidOperationException($"Unhandled OrganizationAclAppendOutcome: {outcome}."),
        };
    }

    private static string? SafePassthrough(string? value)
        => OrganizationAclCommandValidator.IsValidIdentifier(value) ? value : null;

    private static OrganizationAclResultCode Map(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Denied => OrganizationAclResultCode.TenantAccessDenied,
            TenantAccessOutcome.StaleProjection => OrganizationAclResultCode.StaleProjection,
            TenantAccessOutcome.UnavailableProjection => OrganizationAclResultCode.UnavailableProjection,
            TenantAccessOutcome.UnknownTenant => OrganizationAclResultCode.UnknownTenant,
            TenantAccessOutcome.DisabledTenant => OrganizationAclResultCode.DisabledTenant,
            TenantAccessOutcome.MalformedEvidence => OrganizationAclResultCode.MalformedEvidence,
            TenantAccessOutcome.TenantMismatch => OrganizationAclResultCode.TenantMismatch,
            TenantAccessOutcome.MissingAuthoritativeTenant => OrganizationAclResultCode.MissingAuthoritativeTenant,
            TenantAccessOutcome.ReplayConflict => OrganizationAclResultCode.ReplayConflict,
            _ => throw new InvalidOperationException($"Unhandled TenantAccessOutcome: {outcome}."),
        };
}
