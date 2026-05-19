namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderResult(
    FolderResultCode Code,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    string? ActorPrincipalId,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey,
    IReadOnlyList<IFolderEvent> Events)
{
    public static FolderResult Accepted(CreateFolder command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Created,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    // Validation-rejection paths can carry malformed bytes (the validator may itself
    // be rejecting because an identifier is malformed). Funnel command fields through
    // `SafePassthrough` so a result for `MalformedEvidence` cannot echo the unsafe value.
    public static FolderResult Rejected(IFolderCommand command, FolderResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new(
            code,
            SafePassthrough(command.ManagedTenantId),
            SafePassthrough(command.OrganizationId),
            SafePassthrough(command.FolderId),
            SafePassthrough(command.ActorPrincipalId),
            SafePassthrough(command.CorrelationId),
            SafePassthrough(command.TaskId),
            SafePassthrough(command.IdempotencyKey),
            []);
    }

    public static FolderResult Rejected(
        FolderResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? actorPrincipalId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(
            code,
            SafePassthrough(managedTenantId),
            SafePassthrough(organizationId),
            SafePassthrough(folderId),
            SafePassthrough(actorPrincipalId),
            SafePassthrough(correlationId),
            SafePassthrough(taskId),
            SafePassthrough(idempotencyKey),
            []);

    private static string? SafePassthrough(string? value)
        => FolderCommandValidator.IsValidIdentifier(value) ? value : null;
}
