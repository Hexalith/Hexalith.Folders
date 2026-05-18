using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class FolderCreateTenantGate(IFolderRepository repository)
{
    public FolderResult Handle(
        CreateFolder command,
        TenantAccessAuthorizationResult tenantAccess,
        FolderCreateAclEvidence aclEvidence)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);
        ArgumentNullException.ThrowIfNull(aclEvidence);

        if (!tenantAccess.IsAllowed)
        {
            return FolderResult.Rejected(
                Map(tenantAccess.Outcome),
                SafePassthrough(tenantAccess.TenantId),
                SafePassthrough(command.OrganizationId),
                SafePassthrough(command.FolderId),
                SafePassthrough(command.ActorPrincipalId),
                SafePassthrough(command.CorrelationId),
                SafePassthrough(command.TaskId),
                SafePassthrough(command.IdempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(tenantAccess.TenantId))
        {
            return FolderResult.Rejected(command, FolderResultCode.MissingAuthoritativeTenant);
        }

        if (!string.IsNullOrWhiteSpace(command.PayloadTenantId)
            && !string.Equals(command.PayloadTenantId, tenantAccess.TenantId, StringComparison.Ordinal))
        {
            return FolderResult.Rejected(
                FolderResultCode.TenantMismatch,
                tenantAccess.TenantId,
                SafePassthrough(command.OrganizationId),
                SafePassthrough(command.FolderId),
                SafePassthrough(command.ActorPrincipalId),
                SafePassthrough(command.CorrelationId),
                SafePassthrough(command.TaskId),
                SafePassthrough(command.IdempotencyKey));
        }

        CreateFolder authoritativeCommand = (CreateFolder)command.WithManagedTenantId(tenantAccess.TenantId);

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

        FolderIdempotencyLookupResult lookup = repository.TryGetIdempotencyFingerprint(
            authoritativeCommand.ManagedTenantId,
            authoritativeCommand.FolderId,
            authoritativeCommand.IdempotencyKey,
            out string? priorFingerprint);

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyUnavailable);
        }

        if (lookup == FolderIdempotencyLookupResult.Found)
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict);
        }

        FolderStreamName streamName = repository.CreateStreamName(authoritativeCommand.ManagedTenantId, authoritativeCommand.FolderId);
        FolderState state = repository.Load(streamName);
        FolderResult result = FolderAggregate.Handle(state, authoritativeCommand);
        if (result.Events.Count == 0)
        {
            return result;
        }

        FolderAppendOutcome outcome = repository.AppendIfFingerprintAbsent(
            streamName,
            authoritativeCommand.IdempotencyKey,
            validation.IdempotencyFingerprint,
            result.Events);

        return outcome switch
        {
            FolderAppendOutcome.Appended => result,
            FolderAppendOutcome.FingerprintMatched =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotentReplay),
            FolderAppendOutcome.FingerprintConflict =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.AppendConflict),
            _ => throw new InvalidOperationException($"Unhandled FolderAppendOutcome: {outcome}."),
        };
    }

    private static FolderResultCode? EvaluateAcl(CreateFolder command, FolderCreateAclEvidence aclEvidence)
    {
        if (aclEvidence.Outcome == FolderCreateAclOutcome.Allowed
            && string.Equals(aclEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
            && string.Equals(aclEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
            && string.Equals(aclEvidence.PrincipalId, command.ActorPrincipalId, StringComparison.Ordinal)
            && string.Equals(aclEvidence.Action, "create_folder", StringComparison.Ordinal))
        {
            return null;
        }

        return aclEvidence.Outcome switch
        {
            FolderCreateAclOutcome.Denied or FolderCreateAclOutcome.Allowed => FolderResultCode.FolderAclDenied,
            FolderCreateAclOutcome.Unavailable or FolderCreateAclOutcome.Malformed or FolderCreateAclOutcome.Stale =>
                FolderResultCode.AclEvidenceUnavailable,
            _ => throw new InvalidOperationException($"Unhandled FolderCreateAclOutcome: {aclEvidence.Outcome}."),
        };
    }

    private static string? SafePassthrough(string? value)
        => FolderCommandValidator.IsValidIdentifier(value) ? value : null;

    private static FolderResultCode Map(TenantAccessOutcome outcome)
        => outcome switch
        {
            TenantAccessOutcome.Denied => FolderResultCode.TenantAccessDenied,
            TenantAccessOutcome.StaleProjection => FolderResultCode.StaleProjection,
            TenantAccessOutcome.UnavailableProjection => FolderResultCode.UnavailableProjection,
            TenantAccessOutcome.UnknownTenant => FolderResultCode.UnknownTenant,
            TenantAccessOutcome.DisabledTenant => FolderResultCode.DisabledTenant,
            TenantAccessOutcome.MalformedEvidence => FolderResultCode.MalformedEvidence,
            TenantAccessOutcome.TenantMismatch => FolderResultCode.TenantMismatch,
            TenantAccessOutcome.MissingAuthoritativeTenant => FolderResultCode.MissingAuthoritativeTenant,
            TenantAccessOutcome.ReplayConflict => FolderResultCode.ReplayConflict,
            _ => throw new InvalidOperationException($"Unhandled TenantAccessOutcome: {outcome}."),
        };
}
