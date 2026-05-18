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

        return From(command, FolderResultCode.Created, events);
    }

    public static FolderResult Rejected(IFolderCommand command, FolderResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return From(command, code, []);
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
        => new(code, managedTenantId, organizationId, folderId, actorPrincipalId, correlationId, taskId, idempotencyKey, []);

    private static FolderResult From(IFolderCommand command, FolderResultCode code, IReadOnlyList<IFolderEvent> events)
        => new(
            code,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
}
