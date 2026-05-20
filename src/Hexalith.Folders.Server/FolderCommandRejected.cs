using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Folders.Server;

// FolderCommandRejected travels on the /process wire response and into downstream
// log/trace/audit surfaces. The Create factory canonicalizes caller-supplied identifiers
// so an internal /process caller cannot inject CR/LF or oversized strings via the
// rejection payload. Values that fail canonical validation are dropped to null rather
// than passed through, matching the safe-denial principle.
public sealed partial record FolderCommandRejected : IRejectionEvent
{
    private const int MaxIdentifierLength = 128;

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled)]
    private static partial Regex SafeIdentifierRegex();

    private FolderCommandRejected(
        string code,
        string commandType,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? actorPrincipalId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
    {
        Code = code;
        CommandType = commandType;
        ManagedTenantId = managedTenantId;
        OrganizationId = organizationId;
        FolderId = folderId;
        ActorPrincipalId = actorPrincipalId;
        CorrelationId = correlationId;
        TaskId = taskId;
        IdempotencyKey = idempotencyKey;
    }

    public string Code { get; init; }

    public string CommandType { get; init; }

    public string? ManagedTenantId { get; init; }

    public string? OrganizationId { get; init; }

    public string? FolderId { get; init; }

    public string? ActorPrincipalId { get; init; }

    public string? CorrelationId { get; init; }

    public string? TaskId { get; init; }

    public string? IdempotencyKey { get; init; }

    public static FolderCommandRejected Create(
        string code,
        string commandType,
        string? managedTenantId,
        string? organizationId,
        string? folderId,
        string? actorPrincipalId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey) =>
        new(
            code: Canonical(code) ?? "unknown",
            commandType: Canonical(commandType) ?? "unknown",
            managedTenantId: Canonical(managedTenantId),
            organizationId: Canonical(organizationId),
            folderId: Canonical(folderId),
            actorPrincipalId: Canonical(actorPrincipalId),
            correlationId: Canonical(correlationId),
            taskId: Canonical(taskId),
            idempotencyKey: Canonical(idempotencyKey));

    private static string? Canonical(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxIdentifierLength
            || !SafeIdentifierRegex().IsMatch(value))
        {
            return null;
        }

        return value;
    }
}
