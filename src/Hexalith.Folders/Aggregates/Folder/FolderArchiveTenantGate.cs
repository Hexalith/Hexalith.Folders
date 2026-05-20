using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class FolderArchiveTenantGate(IFolderRepository repository, TimeProvider timeProvider)
{
    private readonly IFolderRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public FolderArchiveTenantGate(IFolderRepository repository)
        : this(repository, TimeProvider.System)
    {
    }

    public FolderResult Handle(
        ArchiveFolder command,
        TenantAccessAuthorizationResult tenantAccess,
        FolderArchiveAclEvidence aclEvidence,
        FolderArchivePolicyEvidence policyEvidence)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tenantAccess);
        ArgumentNullException.ThrowIfNull(aclEvidence);
        ArgumentNullException.ThrowIfNull(policyEvidence);

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

        ArchiveFolder authoritativeCommand = command.WithAuthoritativeTenant(tenantAccess.TenantId);

        // Schema/envelope validation runs before ACL probing so malformed payloads
        // do not leak ACL-availability signal via differential code paths.
        FolderCommandValidationResult validation = FolderCommandValidator.Validate(authoritativeCommand);
        if (!validation.IsAccepted)
        {
            return FolderResult.Rejected(authoritativeCommand, validation.Code);
        }

        FolderResultCode? aclRejection = EvaluateAcl(authoritativeCommand, aclEvidence);
        if (aclRejection is not null)
        {
            return FolderResult.Rejected(authoritativeCommand, aclRejection.Value);
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
                ? FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotentReplay)
                : FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict);
        }

        if (lookup == FolderIdempotencyLookupResult.Unavailable)
        {
            return FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyUnavailable);
        }

        FolderState state = _repository.Load(streamName);
        FolderResultCode? policyRejection = EvaluatePolicy(authoritativeCommand, policyEvidence);
        if (policyRejection is not null)
        {
            return FolderResult.Rejected(authoritativeCommand, policyRejection.Value);
        }

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
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotentReplay),
            FolderAppendOutcome.FingerprintConflict =>
                FolderResult.Rejected(authoritativeCommand, FolderResultCode.IdempotencyConflict),
            FolderAppendOutcome.AppendConflict =>
                ResolveAppendConflict(_repository, streamName, authoritativeCommand),
            // Explicit default — any future FolderAppendOutcome enum value must be wired
            // before it reaches here. Defaulting silently to MalformedEvidence would hide
            // real outcomes; fail closed but with a distinct code that tests can detect.
            _ => FolderResult.Rejected(authoritativeCommand, FolderResultCode.MalformedEvidence),
        };
    }

    private FolderResult ResolveAppendConflict(
        IFolderRepository repository,
        FolderStreamName streamName,
        ArchiveFolder command)
    {
        FolderState refreshed = repository.Load(streamName);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        FolderResult refreshedResult = FolderAggregate.Handle(refreshed, command, occurredAt);
        return refreshedResult.Events.Count == 0
            ? refreshedResult
            : FolderResult.Rejected(command, FolderResultCode.AppendConflict);
    }

    private static bool HasCompetingClientTenant(ArchiveFolder command, string authoritativeTenantId)
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

        foreach (KeyValuePair<string, string?> entry in command.ClientControlledTenantIds)
        {
            // Reject smuggled-key abuses (whitespace-only or reserved keys) regardless of value.
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return true;
            }

            if (entry.Value is null)
            {
                continue;
            }

            // A whitespace-only client tenant value is a hard validation failure, not "no opinion".
            if (string.IsNullOrEmpty(entry.Value))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                return true;
            }

            if (!string.Equals(entry.Value, authoritativeTenantId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static FolderResultCode? EvaluateAcl(ArchiveFolder command, FolderArchiveAclEvidence aclEvidence)
    {
        if (aclEvidence.Outcome == FolderArchiveAclOutcome.Allowed)
        {
            bool matches = string.Equals(aclEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.FolderId, command.FolderId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.PrincipalId, command.ActorPrincipalId, StringComparison.Ordinal)
                && string.Equals(aclEvidence.Action, FolderArchiveAclEvidence.ArchiveAction, StringComparison.Ordinal);
            return matches ? null : FolderResultCode.AclEvidenceUnavailable;
        }

        return aclEvidence.Outcome switch
        {
            FolderArchiveAclOutcome.Denied => FolderResultCode.FolderAclDenied,
            FolderArchiveAclOutcome.TenantMismatch => FolderResultCode.TenantMismatch,
            FolderArchiveAclOutcome.FolderMismatch => FolderResultCode.AclEvidenceForeignFolder,
            FolderArchiveAclOutcome.UnsupportedAction => FolderResultCode.AclEvidenceUnsupportedAction,
            FolderArchiveAclOutcome.Unavailable
                or FolderArchiveAclOutcome.Malformed
                or FolderArchiveAclOutcome.Stale => FolderResultCode.AclEvidenceUnavailable,
            _ => FolderResultCode.AclEvidenceUnavailable,
        };
    }

    private static FolderResultCode? EvaluatePolicy(ArchiveFolder command, FolderArchivePolicyEvidence policyEvidence)
    {
        if (policyEvidence.Outcome == FolderArchivePolicyOutcome.Allowed)
        {
            bool matches = string.Equals(policyEvidence.ManagedTenantId, command.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(policyEvidence.OrganizationId, command.OrganizationId, StringComparison.Ordinal)
                && string.Equals(policyEvidence.FolderId, command.FolderId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(policyEvidence.PolicyVersion);
            return matches ? null : FolderResultCode.PolicyEvidenceMalformed;
        }

        return policyEvidence.Outcome switch
        {
            FolderArchivePolicyOutcome.Denied => FolderResultCode.ArchivePolicyDenied,
            FolderArchivePolicyOutcome.ScopeMismatch => FolderResultCode.PolicyEvidenceScopeMismatch,
            FolderArchivePolicyOutcome.Unavailable => FolderResultCode.PolicyEvidenceUnavailable,
            FolderArchivePolicyOutcome.Malformed => FolderResultCode.PolicyEvidenceMalformed,
            FolderArchivePolicyOutcome.Stale => FolderResultCode.PolicyEvidenceStale,
            _ => FolderResultCode.PolicyEvidenceMalformed,
        };
    }

    private static FolderResultCode Map(TenantAccessOutcome outcome)
        => outcome switch
        {
            // An IsAllowed=false result with Outcome=Allowed is a caller invariant violation.
            // Fail closed to MalformedEvidence rather than throwing — the gate's contract is
            // to return a safe denial result, never to throw on hostile evidence shapes.
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
