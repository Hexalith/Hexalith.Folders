using System.Diagnostics;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server;

// FolderCommandRejected travels on the /process wire response and into downstream
// log/trace/audit surfaces. The Create factory canonicalizes caller-supplied identifiers
// so an internal /process caller cannot inject CR/LF or oversized strings via the
// rejection payload. Values that fail canonical validation are dropped to null rather
// than passed through, matching the safe-denial principle.
//
// All properties expose only private init setters so `with`-mutation cannot bypass the
// canonicalization performed by Create(...). The static factory remains the only entry
// point for constructing a canonicalized rejection event.
public sealed partial record FolderCommandRejected : IRejectionEvent
{
    // Shared with TryReadCanonicalExtension and IsCanonicalIdentifier — keep the three
    // sites in lock-step so a length bump cannot drift.
    public const int MaxCanonicalIdentifierLength = FoldersServerModule.MaxCanonicalIdentifierLength;

    private const int MaxIdentifierLength = MaxCanonicalIdentifierLength;

    // Lowercase canonical form, mirroring CanonicalSegmentRegex in FolderDomainProcessor
    // and FoldersDomainServiceEndpoints. The gateway-corrected echo at the REST surface is
    // allowed to be uppercase (ULID-friendly) via IsSafeGatewayCorrelationId, but rejection
    // event payloads must not weaken the canonical contract.
    [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.Compiled)]
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

    public string Code { get; private init; }

    public string CommandType { get; private init; }

    public string? ManagedTenantId { get; private init; }

    public string? OrganizationId { get; private init; }

    public string? FolderId { get; private init; }

    public string? ActorPrincipalId { get; private init; }

    public string? CorrelationId { get; private init; }

    public string? TaskId { get; private init; }

    public string? IdempotencyKey { get; private init; }

    // Sentinel emitted when commandType is neither a known canonical constant nor matches
    // the safe-identifier regex. Keeps unknown command-type rejections out of downstream
    // log/alert keyspaces while preserving the rejection event itself.
    public const string UnknownCommandTypeSentinel = "unknown_command_type";

    public static FolderCommandRejected Create(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(commandType);

        // Whitelist: `code` must round-trip from a FolderResultCode enum name so downstream
        // log/alert systems see only known canonical values. This is a strict invariant —
        // every rejection emit-site passes `enumValue.ToString()` so a non-roundtripping
        // value here means a programming bug.
        if (!Enum.TryParse<FolderResultCode>(code, out _))
        {
            throw new ArgumentException(
                $"Unknown FolderResultCode '{code}'. Rejection events must carry a code that round-trips through FolderResultCode.",
                nameof(code));
        }

        // commandType is intentionally not whitelisted: the UnsupportedCommandType rejection
        // path is legitimately invoked with arbitrary caller-supplied command types. Use the
        // canonical safe-identifier filter; replace unknown shapes with a fixed sentinel so
        // the wire payload never carries arbitrary strings that downstream systems may key on.
        string normalizedCommandType = NormalizeCommandType(commandType);

        string? canonicalCorrelationId = Canonical(correlationId);
        string? canonicalTaskId = Canonical(taskId);
        string? canonicalIdempotencyKey = Canonical(idempotencyKey);

        // Emit a metadata-only trace tag when an identifier was supplied but failed the
        // canonical filter. Operators reading the trace can correlate the dropped-value
        // case without the offending identifier text leaking into the rejection payload.
        if (!string.IsNullOrWhiteSpace(correlationId) && canonicalCorrelationId is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.correlation_id_dropped", true);
        }

        if (!string.IsNullOrWhiteSpace(taskId) && canonicalTaskId is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.task_id_dropped", true);
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && canonicalIdempotencyKey is null)
        {
            Activity.Current?.SetTag("hexalith.folders.rejection.idempotency_key_dropped", true);
        }

        return new FolderCommandRejected(
            code: code,
            commandType: normalizedCommandType,
            managedTenantId: Canonical(managedTenantId),
            organizationId: Canonical(organizationId),
            folderId: Canonical(folderId),
            actorPrincipalId: Canonical(actorPrincipalId),
            correlationId: canonicalCorrelationId,
            taskId: canonicalTaskId,
            idempotencyKey: canonicalIdempotencyKey);
    }

    private static string NormalizeCommandType(string commandType)
    {
        // Known canonical command types pass through untouched. Add to this guard when new
        // domain command types are wired through FolderDomainProcessor.
        if (string.Equals(commandType, FoldersServerModule.ArchiveFolderCommandType, StringComparison.Ordinal))
        {
            return commandType;
        }

        // For unknown command types (legitimately reached via UnsupportedCommandType
        // rejections), require the value to fit a relaxed dotted-canonical shape that
        // matches actual command-type names (Hexalith.Folders.Commands.Foo) before allowing
        // it on the wire. Otherwise collapse to a fixed sentinel.
        if (!string.IsNullOrWhiteSpace(commandType)
            && commandType.Length <= MaxIdentifierLength
            && CommandTypeRegex().IsMatch(commandType))
        {
            return commandType;
        }

        Activity.Current?.SetTag("hexalith.folders.rejection.command_type_dropped", true);
        return UnknownCommandTypeSentinel;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled)]
    private static partial Regex CommandTypeRegex();

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
