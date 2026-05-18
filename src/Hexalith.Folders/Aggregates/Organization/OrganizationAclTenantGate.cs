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
                tenantAccess.TenantId,
                command.OrganizationId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
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
                command.OrganizationId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        IOrganizationAclCommand authoritativeCommand = command.WithManagedTenantId(tenantAccess.TenantId);
        OrganizationAclCommandValidationResult validation = OrganizationAclCommandValidator.Validate(authoritativeCommand);
        if (!validation.IsAccepted)
        {
            return OrganizationAclResult.Rejected(authoritativeCommand, validation.Code);
        }

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
        if (result.Events.Count > 0)
        {
            repository.Append(streamName, result.Events);
        }

        return result;
    }

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
            _ => OrganizationAclResultCode.TenantAccessDenied,
        };
}
