namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderAccessAclEvidence
{
    // Story 2.4 reuses the Story 2.2 organization ACL `configure_provider_binding`
    // action as folder ACL management proof rather than minting a new manage-access token.
    public const string ManagementAction = "configure_provider_binding";

    public FolderAccessAclEvidence(
        FolderAccessAclOutcome outcome,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? principalId,
        string action)
    {
        // `Allowed` evidence is the only outcome whose Action is load-bearing for authorization.
        // Reject mis-constructed Allowed evidence at the boundary so a future code path that
        // bypasses EvaluateAcl's Action equality check cannot silently treat foreign-action
        // evidence as a valid management proof.
        if (outcome == FolderAccessAclOutcome.Allowed
            && !string.Equals(action, ManagementAction, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Allowed FolderAccessAclEvidence must carry the management action '{ManagementAction}'.",
                nameof(action));
        }

        Outcome = outcome;
        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        PrincipalId = principalId;
        Action = action;
    }

    public FolderAccessAclOutcome Outcome { get; init; }

    public string? ManagedTenantId { get; init; }

    public string? OrganizationId { get; init; }

    public string? FolderId { get; init; }

    public string? PrincipalId { get; init; }

    public string Action { get; init; }

    public static FolderAccessAclEvidence Allowed(
        string managedTenantId,
        string organizationId,
        string folderId,
        string principalId)
        => new(FolderAccessAclOutcome.Allowed, managedTenantId, organizationId, folderId, principalId, ManagementAction);
}
