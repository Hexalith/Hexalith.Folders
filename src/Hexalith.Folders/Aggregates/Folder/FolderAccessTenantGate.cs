using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class FolderAccessTenantGate(IFolderRepository repository)
{
    public FolderResult Handle(
        IFolderAccessCommand command,
        TenantAccessAuthorizationResult tenantAccess,
        FolderAccessAclEvidence aclEvidence)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);
        ArgumentNullException.ThrowIfNull(aclEvidence);

        if (!tenantAccess.IsAllowed)
        {
            return FolderResult.Rejected(
                Map(tenantAccess.Outcome),
                tenantAccess.TenantId,
                command.OrganizationId,
                command.FolderId,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        if (string.IsNullOrWhiteSpace(tenantAccess.TenantId))
        {
            return FolderResult.Rejected(command, FolderResultCode.MissingAuthoritativeTenant);
        }

        if (HasCompetingClientTenant(command, tenantAccess.TenantId))
        {
            return FolderResult.Rejected(
                FolderResultCode.TenantMismatch,
                tenantAccess.TenantId,
                command.OrganizationId,
                command.FolderId,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        IFolderAccessCommand authoritativeCommand = (IFolderAccessCommand)command.WithManagedTenantId(tenantAccess.TenantId);

        FolderResultCode? aclRejection = EvaluateAcl(authoritativeCommand, aclEvidence);
        if (aclRejection is not null)
        {
            return FolderResult.Rejected(authoritativeCommand, aclRejection.Value);
        }

        FolderCommandValidationResult validation = FolderCommandValidator.Validate(authoritativeCommand);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(authoritativeCommand, validation.Code);
        }

        FolderStreamName streamName = repository.CreateStreamName(
            authoritativeCommand.ManagedTenantId,
            authoritativeCommand.FolderId);

        FolderIdempotencyLookupResult lookup = repository.TryGetIdempotencyFingerprint(
            streamName,
            authoritativeCommand.IdempotencyKey,
            out string? priorFingerprint);

        if (lookup == FolderIdempotencyLookupResult.Found)
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.AlreadyApplied)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyUnavailable);
        }

        FolderState state = repository.Load(streamName);
        FolderResult result = FolderAggregate.Handle(state, authoritativeCommand);
        if (result.Events.Count == 0)
        {
            return result;
        }

        FolderAppendOutcome outcome = repository.AppendIfFingerprintAbsent(
            streamName,
            authoritativeCommand.IdempotencyKey,
            validation.IdempotencyFingerprint!,
            result.Events);

        return outcome switch
        {
            FolderAppendOutcome.Appended => result,
            FolderAppendOutcome.FingerprintMatched =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.AlreadyApplied),
            FolderAppendOutcome.FingerprintConflict =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict =>
                ResolveAppendConflict(repository, streamName, authoritativeCommand),
            _ => FolderResult.Rejected(authoritativeCommand, FolderResultCode.MalformedEvidence),
        };
    }

    private static FolderResult ResolveAppendConflict(
        IFolderRepository repository,
        FolderStreamName streamName,
        IFolderAccessCommand command)
    {
        FolderState refreshed = repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command);
        return refreshedResult.Events.Count == 0
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    private static bool HasCompetingClientTenant(IFolderAccessCommand command, string authoritativeTenantId)
    {
        if (!string.IsNullOrWhiteSpace(command.PayloadTenantId)
            && !string.Equals(command.PayloadTenantId, authoritativeTenantId, StringComparison.Ordinal))
        {
            return true;
        }

        return command.ClientControlledTenantIds.Values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, authoritativeTenantId, StringComparison.Ordinal));
    }

    private static FolderResultCode? EvaluateAcl(IFolderAccessCommand command, FolderAccessAclEvidence aclEvidence)
    {
        if (aclEvidence.Outcome == FolderAccessAclOutcome.Allowed)
        {
            bool matches = string.Equals(aclEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.FolderId, command.FolderId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.PrincipalId, command.ActorPrincipalId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.Action, FolderAccessAclEvidence.ManagementAction, StringComparison.Ordinal);
            return matches ? null : FolderResultCode.AclEvidenceMismatch;
        }

        return aclEvidence.Outcome switch
        {
            FolderAccessAclOutcome.Denied => FolderResultCode.FolderAclDenied,
            FolderAccessAclOutcome.TenantMismatch => FolderResultCode.TenantMismatch,
            FolderAccessAclOutcome.FolderMismatch => FolderResultCode.AclEvidenceMismatch,
            FolderAccessAclOutcome.UnsupportedAction => FolderResultCode.AclEvidenceUnavailable,
            FolderAccessAclOutcome.Unavailable
                or FolderAccessAclOutcome.Malformed
                or FolderAccessAclOutcome.Stale => FolderResultCode.AclEvidenceUnavailable,
            _ => FolderResultCode.AclEvidenceUnavailable,
        };
    }

    private static FolderResultCode Map(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Allowed => FolderResultCode.MalformedEvidence,
            TenantAccessOutcome.Denied => FolderResultCode.TenantAccessDenied,
            TenantAccessOutcome.StaleProjection => FolderResultCode.StaleProjection,
            TenantAccessOutcome.UnavailableProjection => FolderResultCode.UnavailableProjection,
            TenantAccessOutcome.UnknownTenant => FolderResultCode.UnknownTenant,
            TenantAccessOutcome.DisabledTenant => FolderResultCode.DisabledTenant,
            TenantAccessOutcome.MalformedEvidence => FolderResultCode.MalformedEvidence,
            TenantAccessOutcome.TenantMismatch => FolderResultCode.TenantMismatch,
            TenantAccessOutcome.MissingAuthoritativeTenant => FolderResultCode.MissingAuthoritativeTenant,
            TenantAccessOutcome.ReplayConflict => FolderResultCode.ReplayConflict,
            _ => FolderResultCode.MalformedEvidence,
        };
}
