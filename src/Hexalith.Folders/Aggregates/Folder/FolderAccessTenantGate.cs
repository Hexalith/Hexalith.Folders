using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class FolderAccessTenantGate(IFolderRepository repository, TimeProvider timeProvider)
{
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public FolderAccessTenantGate(IFolderRepository repository)
        : this(repository, TimeProvider.System)
    {
    }

    public FolderResult Handle(
        IFolderAccessCommand command,
        TenantAccessAuthorizationResult tenantAccess,
        FolderAccessAclEvidence aclEvidence)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);
        ArgumentNullException.ThrowIfNull(aclEvidence);

        // Tenant-denied path: do not echo the authorizer-supplied tenant ID back to the
        // caller. The denied tenant identity is unauthorized information the caller has not
        // proven the right to see, so pass null for managedTenantId.
        if (!tenantAccess.IsAllowed)
        {
            return FolderResult.Rejected(
                Map(tenantAccess.Outcome),
                managedTenantId: null,
                organizationId: null,
                folderId: null,
                principalKind: null,
                principalId: null,
                action: null,
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
                principalKind: null,
                principalId: null,
                action: null,
                command.ActorPrincipalId,
                command.CorrelationId,
                command.TaskId,
                command.IdempotencyKey);
        }

        IFolderAccessCommand authoritativeCommand = command.WithAuthoritativeTenant(tenantAccess.TenantId);

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
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.AlreadyApplied)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyUnavailable);
        }

        FolderState state = _repository.Load(streamName);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        FolderResult result = FolderAggregate.Handle(state, authoritativeCommand, occurredAt);
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
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.AlreadyApplied),
            FolderAppendOutcome.FingerprintConflict =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict =>
                ResolveAppendConflict(_repository, streamName, authoritativeCommand, occurredAt),
            _ => FolderResult.Rejected(authoritativeCommand, FolderResultCode.MalformedEvidence),
        };
    }

    // When the append loses an optimistic-concurrency race, the gate re-reads state and
    // re-evaluates the command. Two stable outcomes are possible:
    //   - The racing event made the command a no-op (e.g., a competing grant already wrote
    //     this tuple). Return the refreshed `AlreadyApplied` / `MissingEntry` result so the
    //     caller sees the same outcome they would have on idempotent replay.
    //   - The command still has real work to do (e.g., a racing revoke beat a grant for a
    //     different tuple). Return `AppendConflict` so the caller can re-prepare against the
    //     new authorization context and retry. We deliberately do not auto-retry the append:
    //     auto-retry would silently re-execute side effects the caller may not have re-authorized,
    //     and it expands the gate's behavior beyond what the spec requires.
    private static FolderResult ResolveAppendConflict(
        IFolderRepository repository,
        FolderStreamName streamName,
        IFolderAccessCommand command,
        DateTimeOffset occurredAt)
    {
        FolderState refreshed = repository.Load(streamName);
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command, occurredAt);
        return refreshedResult.Events.Count == 0
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    // Reject any client-controlled tenant value that disagrees with the authoritative
    // tenant, including the command's own `ManagedTenantId` field. The aggregate later
    // rebinds ManagedTenantId via WithAuthoritativeTenant, but doing so silently would
    // hide a probing attack ("submit with victim-tenant; observe rebound behavior") from
    // detection. Surfacing TenantMismatch keeps the ingress matrix fully fail-loud.
    private static bool HasCompetingClientTenant(IFolderAccessCommand command, string authoritativeTenantId)
    {
        if (!string.IsNullOrWhiteSpace(command.ManagedTenantId)
            && !string.Equals(command.ManagedTenantId, authoritativeTenantId, StringComparison.Ordinal))
        {
            return true;
        }

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
            // Defense-in-depth: although FolderAccessAclEvidence's constructor rejects
            // Allowed-with-wrong-action, a future deserializer or wire format could still
            // produce a degenerate value. Treat null/whitespace Action as unavailable.
            if (string.IsNullOrWhiteSpace(aclEvidence.Action))
            {
                return FolderResultCode.AclEvidenceUnavailable;
            }

            bool matches = string.Equals(aclEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.FolderId, command.FolderId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.PrincipalId, command.ActorPrincipalId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.Action, FolderAccessAclEvidence.ManagementAction, StringComparison.Ordinal);

            // Allowed-but-scope-mismatched evidence is collapsed into `AclEvidenceUnavailable`
            // (same code as missing/stale/malformed) so denial vs scope-mismatch evidence is
            // indistinguishable to a caller probing folder/principal/tenant existence.
            return matches ? null : FolderResultCode.AclEvidenceUnavailable;
        }

        return aclEvidence.Outcome switch
        {
            FolderAccessAclOutcome.Denied => FolderResultCode.FolderAclDenied,
            FolderAccessAclOutcome.TenantMismatch => FolderResultCode.TenantMismatch,
            FolderAccessAclOutcome.FolderMismatch => FolderResultCode.AclEvidenceUnavailable,
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
            // An IsAllowed=false result with Outcome=Allowed is a caller invariant violation.
            // Match the archive gate: fail closed with safe malformed evidence instead of
            // throwing from the domain gate.
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
