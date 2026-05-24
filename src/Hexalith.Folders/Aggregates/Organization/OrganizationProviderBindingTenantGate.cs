using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Organization;

public sealed class OrganizationProviderBindingTenantGate(IOrganizationProviderBindingRepository repository)
{
    public OrganizationProviderBindingResult Handle(
        ConfigureProviderBinding command,
        TenantAccessAuthorizationResult tenantAccess)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);

        if (!tenantAccess.IsAllowed)
        {
            return OrganizationProviderBindingResult.Rejected(
                Map(tenantAccess.Outcome),
                tenantAccess.TenantId,
                command.OrganizationId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        if (string.IsNullOrWhiteSpace(tenantAccess.TenantId))
        {
            return OrganizationProviderBindingResult.Rejected(command, OrganizationProviderBindingResultCode.MissingAuthoritativeTenant);
        }

        if (!string.IsNullOrWhiteSpace(command.PayloadTenantId)
            && !string.Equals(command.PayloadTenantId, tenantAccess.TenantId, StringComparison.Ordinal))
        {
            return OrganizationProviderBindingResult.Rejected(
                OrganizationProviderBindingResultCode.TenantMismatch,
                tenantAccess.TenantId,
                command.OrganizationId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        if (!OrganizationStreamName.TryCreate(tenantAccess.TenantId, command.OrganizationId, out _, out _))
        {
            ConfigureProviderBinding invalidEnvelopeCommand = command.WithManagedTenantId(tenantAccess.TenantId);
            OrganizationProviderBindingCommandValidationResult invalidEnvelope =
                OrganizationProviderBindingCommandValidator.Validate(invalidEnvelopeCommand);
            return OrganizationProviderBindingResult.Rejected(invalidEnvelopeCommand, invalidEnvelope.Code);
        }

        ConfigureProviderBinding authoritativeCommand = command.WithManagedTenantId(tenantAccess.TenantId);
        OrganizationStreamName streamName = repository.CreateStreamName(authoritativeCommand.ManagedTenantId, authoritativeCommand.OrganizationId);
        OrganizationState state = repository.Load(streamName);

        if (!state.HasPermission(
            authoritativeCommand.ManagedTenantId,
            authoritativeCommand.OrganizationId,
            authoritativeCommand.ActorPrincipalKind,
            authoritativeCommand.ActorPrincipalId,
            "configure_provider_binding"))
        {
            return OrganizationProviderBindingResult.Rejected(authoritativeCommand, OrganizationProviderBindingResultCode.MissingPermission);
        }

        OrganizationProviderBindingCommandValidationResult validation =
            OrganizationProviderBindingCommandValidator.Validate(authoritativeCommand);
        if (!validation.IsAccepted)
        {
            return OrganizationProviderBindingResult.Rejected(authoritativeCommand, validation.Code);
        }

        if (repository.TryGetIdempotencyFingerprint(streamName, authoritativeCommand.IdempotencyKey, out string? priorFingerprint))
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? OrganizationProviderBindingResult.Rejected(authoritativeCommand, OrganizationProviderBindingResultCode.AlreadyApplied)
                : OrganizationProviderBindingResult.Rejected(authoritativeCommand, OrganizationProviderBindingResultCode.IdempotencyConflict);
        }

        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(state, authoritativeCommand);
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
                OrganizationProviderBindingResult.Rejected(authoritativeCommand, OrganizationProviderBindingResultCode.AlreadyApplied),
            OrganizationAclAppendOutcome.FingerprintConflict =>
                OrganizationProviderBindingResult.Rejected(authoritativeCommand, OrganizationProviderBindingResultCode.IdempotencyConflict),
            _ => throw new InvalidOperationException($"Unhandled OrganizationAclAppendOutcome: {outcome}."),
        };
    }

    private static OrganizationProviderBindingResultCode Map(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Allowed => OrganizationProviderBindingResultCode.MalformedEvidence,
            TenantAccessOutcome.Denied => OrganizationProviderBindingResultCode.TenantAccessDenied,
            TenantAccessOutcome.StaleProjection => OrganizationProviderBindingResultCode.StaleProjection,
            TenantAccessOutcome.UnavailableProjection => OrganizationProviderBindingResultCode.UnavailableProjection,
            TenantAccessOutcome.UnknownTenant => OrganizationProviderBindingResultCode.UnknownTenant,
            TenantAccessOutcome.DisabledTenant => OrganizationProviderBindingResultCode.DisabledTenant,
            TenantAccessOutcome.MalformedEvidence => OrganizationProviderBindingResultCode.MalformedEvidence,
            TenantAccessOutcome.TenantMismatch => OrganizationProviderBindingResultCode.TenantMismatch,
            TenantAccessOutcome.MissingAuthoritativeTenant => OrganizationProviderBindingResultCode.MissingAuthoritativeTenant,
            TenantAccessOutcome.ReplayConflict => OrganizationProviderBindingResultCode.ReplayConflict,
            _ => OrganizationProviderBindingResultCode.MalformedEvidence,
        };
}
