using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class FolderCreateTenantGate
{
    private readonly IFolderRepository _repository;
    private readonly TimeProvider _timeProvider;

    public FolderCreateTenantGate(IFolderRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public FolderCreateTenantGate(IFolderRepository repository)
        : this(repository, TimeProvider.System)
    {
    }

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

        if (!string.IsNullOrWhiteSpace(command.PayloadTenantId)
            && !string.Equals(command.PayloadTenantId, tenantAccess.TenantId, StringComparison.Ordinal))
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

        // Stream-name construction happens after all reject-before-construction gates
        // (tenant, payload-mismatch, ACL, metadata validation) have passed. From here on
        // the gate uses `streamName` as the single addressable identity for the ledger,
        // the state load, and the append.
        FolderStreamName streamName = _repository.CreateStreamName(
            authoritativeCommand.ManagedTenantId,
            authoritativeCommand.FolderId);

        FolderIdempotencyLookupResult lookup = _repository.TryGetIdempotencyFingerprint(
            streamName,
            authoritativeCommand.IdempotencyKey,
            out string? priorFingerprint);

        if (lookup == FolderIdempotencyLookupResult.Found)
        {
            return string.Equals(priorFingerprint, validation.IdempotencyFingerprint, StringComparison.Ordinal)
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            // Load state so an already-existing folder is surfaced as DuplicateFolder
            // rather than masked by a transient ledger outage; otherwise fail closed.
            FolderState unavailableState = _repository.Load(streamName);
            return unavailableState.IsCreated
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.DuplicateFolder)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyUnavailable);
        }

        FolderState state = _repository.Load(streamName);
        FolderResult result = FolderAggregate.Handle(state, authoritativeCommand, _timeProvider.GetUtcNow());
        if (result.Events.Count == 0)
        {
            return result;
        }

        FolderAppendOutcome outcome = _repository.AppendIfFingerprintAbsent(
            streamName,
            authoritativeCommand.IdempotencyKey,
            validation.IdempotencyFingerprint!,
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
            // Unknown outcomes from a future adapter must fail closed without throwing,
            // so the FolderResult contract is preserved at the gate's public boundary.
            _ => FolderResult.Rejected(authoritativeCommand, FolderResultCode.MalformedEvidence),
        };
    }

    // Returns:
    //   null                        → ACL evidence permits the command
    //   AclEvidenceMismatch         → ACL outcome is Allowed but evidence is for a
    //                                 different tenant/org/principal/action (replay,
    //                                 stale cache, misrouted projection event)
    //   FolderAclDenied             → genuine deny
    //   AclEvidenceUnavailable      → unavailable, malformed, stale, or unknown outcome
    private static FolderResultCode? EvaluateAcl(CreateFolder command, FolderCreateAclEvidence aclEvidence)
    {
        if (aclEvidence.Outcome == FolderCreateAclOutcome.Allowed)
        {
            bool matches = string.Equals(aclEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.PrincipalId, command.ActorPrincipalId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.Action, "create_folder", StringComparison.Ordinal);
            return matches ? null : FolderResultCode.AclEvidenceMismatch;
        }

        return aclEvidence.Outcome switch
        {
            FolderCreateAclOutcome.Denied => FolderResultCode.FolderAclDenied,
            FolderCreateAclOutcome.Unavailable
                or FolderCreateAclOutcome.Malformed
                or FolderCreateAclOutcome.Stale => FolderResultCode.AclEvidenceUnavailable,
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
