using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed record FolderResult(
    FolderResultCode Code,
    string? ManagedTenantId,
    string? OrganizationId,
    string? FolderId,
    FolderAccessPrincipalKind? PrincipalKind,
    string? PrincipalId,
    string? Action,
    string? ActorPrincipalId,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey,
    IReadOnlyList<IFolderEvent> Events)
{
    /// <summary>Gets whether the command achieved its requested outcome, including an equivalent replay.</summary>
    [JsonIgnore]
    public bool IsAccepted => IsAcceptedCode(Code);

    /// <summary>Creates an accepted replay result with safe metadata and no new events.</summary>
    /// <param name="command">The equivalent command submitted again.</param>
    /// <returns>The replay result.</returns>
    public static FolderResult Replayed(IFolderCommand command)
        => WithoutEvents(command, FolderResultCode.IdempotentReplay);

    /// <summary>Identifies result codes that represent a successful command outcome.</summary>
    /// <param name="code">The folder result code.</param>
    /// <returns>Whether the code represents a successful outcome.</returns>
    internal static bool IsAcceptedCode(FolderResultCode code)
        => code is FolderResultCode.Accepted or FolderResultCode.Created
            or FolderResultCode.IdempotentReplay or FolderResultCode.AlreadyApplied;

    public static FolderResult Accepted(CreateFolder command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Created,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(
        IFolderAccessCommand command,
        IReadOnlyList<IFolderEvent> events,
        FolderAccessOperation? displayOperation)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        // Accepted commands have already passed FolderCommandValidator, which guarantees the
        // operation's Action is in the supported vocabulary. Echo it directly rather than
        // round-tripping through IsSupported, which used to silently null borderline values
        // in the result payload.
        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            displayOperation?.PrincipalKind,
            SafePassthrough(displayOperation?.PrincipalId),
            displayOperation?.Action,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(ArchiveFolder command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(CreateRepositoryBackedFolder command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(BindRepository command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(ConfigureBranchRefPolicy command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(PrepareWorkspace command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(LockWorkspace command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(ReleaseWorkspaceLock command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(MutateWorkspaceFile command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Accepted(CommitWorkspace command, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return new(
            FolderResultCode.Accepted,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey,
            events);
    }

    public static FolderResult Rejected(IFolderCommand command, FolderResultCode code)
        => WithoutEvents(command, code);

    private static FolderResult WithoutEvents(IFolderCommand command, FolderResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return WithoutEvents(
            code,
            command.ManagedTenantId,
            command.OrganizationId,
            command.FolderId,
            null,
            null,
            null,
            command.ActorPrincipalId,
            command.CorrelationId,
            command.TaskId,
            command.IdempotencyKey);
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
        => Rejected(
            code,
            managedTenantId,
            organizationId,
            folderId,
            null,
            null,
            null,
            actorPrincipalId,
            correlationId,
            taskId,
            idempotencyKey);

    public static FolderResult Rejected(
        FolderResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        FolderAccessPrincipalKind? principalKind,
        string? principalId,
        string? action,
        string? actorPrincipalId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => WithoutEvents(code, managedTenantId, organizationId, folderId, principalKind, principalId, action,
            actorPrincipalId, correlationId, taskId, idempotencyKey);

    private static FolderResult WithoutEvents(
        FolderResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        FolderAccessPrincipalKind? principalKind,
        string? principalId,
        string? action,
        string? actorPrincipalId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(
            code,
            SafePassthrough(managedTenantId),
            SafePassthrough(organizationId),
            SafePassthrough(folderId),
            principalKind,
            SafePassthrough(principalId),
            // Echo action only when it is in the supported vocabulary. Rejected paths can
            // include malformed/unsupported action strings, and we do not want to echo
            // attacker-controlled payload bytes back to the caller.
            FolderAccessAction.IsSupported(action) ? action : null,
            SafePassthrough(actorPrincipalId),
            SafePassthrough(correlationId),
            SafePassthrough(taskId),
            SafePassthrough(idempotencyKey),
            []);

    private static string? SafePassthrough(string? value)
        => FolderCommandValidator.IsSafeEvidenceIdentifier(value) ? value : null;
}
