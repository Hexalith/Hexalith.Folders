using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;
using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Story 6.6 — render-time display resolvers for the SDK status enums the diagnostic pages surface
/// (lock state, cleanup status, file-entry kind/access, commit-reference classification, canonical
/// error category). Every switch is <b>total</b> over its SDK enum and throws
/// <see cref="ArgumentOutOfRangeException"/> on an unrecognized member — never a silent default — so
/// a contract drift becomes a failing totality test rather than a mis-rendered status (the C6/F-4/F-5
/// correctness concern). Label-producing members return <see cref="string"/> so a later
/// <c>IStringLocalizer</c> wrapper is a pure refactor (FR localization deferred to 6.11).
/// </summary>
public static class ConsoleStatusText
{
    private static readonly FrozenDictionary<CanonicalErrorCategory, string> _errorTokens = BuildErrorTokens();

    // Operator-facing safe explanations keyed by the canonical wire token. A string key (not an enum
    // switch) keeps this resilient to new categories: an unmapped category falls back to the generic
    // safe message rather than forcing a 47-arm switch that would drift. Two groups are present: the 12
    // nominal categories Story 6.6 AC #10 enumerates, AND the tokens the server's
    // FolderAuthorizationDenialMapper / file-path policy actually emit on the live denial path
    // (not_found_to_caller, authorization_denied, policy_denied, policy_evidence_unavailable,
    // path_policy_denied) — without these, a real folder/workspace/audit denial would fall through to the
    // generic envelope instead of the per-category safe copy AC #10 requires. All denial copy is
    // existence-neutral (not_found_to_caller mirrors not_found) so it never becomes a resource oracle.
    private static readonly FrozenDictionary<string, string> _errorExplanations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["authentication_failure"] = "Authentication could not be established for this request.",
        ["tenant_access_denied"] = "Access to this tenant scope was denied.",
        ["cross_tenant_access_denied"] = "Access across tenant boundaries was denied.",
        ["folder_acl_denied"] = "Your effective folder permissions do not allow this view.",
        ["audit_access_denied"] = "Access to audit evidence was denied.",
        ["read_model_unavailable"] = "The read model is currently unavailable and cannot answer this query.",
        ["projection_stale"] = "The projection is stale; data shown may lag the source of truth.",
        ["projection_unavailable"] = "The projection is unavailable and cannot answer this query.",
        ["response_limit_exceeded"] = "The result exceeded the response size budget; narrow the scope.",
        ["query_timeout"] = "The query timed out before completing; try again or narrow the scope.",
        ["not_found"] = "No matching diagnostic evidence is available for this scope.",
        ["redacted"] = "The requested evidence is withheld by tenant policy.",
        ["internal_error"] = "An internal error prevented this view from rendering.",

        // Tokens the server actually emits on the live denial path (FolderAuthorizationDenialMapper /
        // file-path policy). Kept existence-neutral so *_denied and not-found read identically.
        ["not_found_to_caller"] = "No matching diagnostic evidence is available for this scope.",
        ["authorization_denied"] = "Your effective permissions do not allow this view.",
        ["policy_denied"] = "Access is denied by tenant policy for this operation or resource.",
        ["policy_evidence_unavailable"] = "Authorization evidence is temporarily unavailable; retry shortly.",
        ["path_policy_denied"] = "Path policy denied the requested operation.",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Generic safe explanation used when a category has no specific operator-facing copy.</summary>
    public const string DefaultErrorExplanation = "This view could not be rendered. Use the correlation ID below when escalating.";

    /// <summary>Maps a lock state to its operator-facing label.</summary>
    public static string ResolveLockLabel(LockState state)
        => state switch
        {
            LockState.Unlocked => "Unlocked",
            LockState.Locked => "Locked",
            LockState.Expired => "Lock expired",
            LockState.Stale => "Lock stale",
            LockState.Revoked => "Lock revoked",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown lock state."),
        };

    /// <summary>Maps a lock state to its badge slot.</summary>
    public static BadgeSlot ResolveLockSlot(LockState state)
        => state switch
        {
            LockState.Unlocked => BadgeSlot.Success,
            LockState.Locked => BadgeSlot.Warning,
            LockState.Expired => BadgeSlot.Warning,
            LockState.Stale => BadgeSlot.Warning,
            LockState.Revoked => BadgeSlot.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown lock state."),
        };

    /// <summary>Maps a cleanup status to its operator-facing label.</summary>
    public static string ResolveCleanupLabel(CleanupStatus status)
        => status switch
        {
            CleanupStatus.Pending => "Cleanup pending",
            CleanupStatus.Succeeded => "Cleanup succeeded",
            CleanupStatus.Failed => "Cleanup failed",
            CleanupStatus.Status_only => "Status only",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown cleanup status."),
        };

    /// <summary>Maps a cleanup status to its badge slot.</summary>
    public static BadgeSlot ResolveCleanupSlot(CleanupStatus status)
        => status switch
        {
            CleanupStatus.Pending => BadgeSlot.Info,
            CleanupStatus.Succeeded => BadgeSlot.Success,
            CleanupStatus.Failed => BadgeSlot.Danger,
            CleanupStatus.Status_only => BadgeSlot.Neutral,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown cleanup status."),
        };

    /// <summary>Maps a file-entry kind to its operator-facing label.</summary>
    public static string ResolveFileKindLabel(FileMetadataItemKind kind)
        => kind switch
        {
            FileMetadataItemKind.File => "File",
            FileMetadataItemKind.Directory => "Directory",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown file metadata item kind."),
        };

    /// <summary>
    /// Maps a file-entry redaction to its <b>access</b>-column label (distinct from the redaction
    /// disclosure rendered through <c>RedactedField</c>): permitted / redacted / excluded-by-policy / binary.
    /// </summary>
    public static string ResolveFileAccessLabel(FileMetadataItemRedaction redaction)
        => redaction switch
        {
            FileMetadataItemRedaction.Not_redacted => "Permitted",
            FileMetadataItemRedaction.Redacted => "Redacted",
            FileMetadataItemRedaction.Excluded => "Excluded by policy",
            FileMetadataItemRedaction.Binary_disallowed => "Binary",
            _ => throw new ArgumentOutOfRangeException(nameof(redaction), redaction, "Unknown file metadata item redaction."),
        };

    /// <summary>Maps a file-entry redaction to its access-column badge slot.</summary>
    public static BadgeSlot ResolveFileAccessSlot(FileMetadataItemRedaction redaction)
        => redaction switch
        {
            FileMetadataItemRedaction.Not_redacted => BadgeSlot.Success,
            FileMetadataItemRedaction.Redacted => BadgeSlot.Warning,
            FileMetadataItemRedaction.Excluded => BadgeSlot.Neutral,
            FileMetadataItemRedaction.Binary_disallowed => BadgeSlot.Neutral,
            _ => throw new ArgumentOutOfRangeException(nameof(redaction), redaction, "Unknown file metadata item redaction."),
        };

    /// <summary>
    /// Maps the commit-reference classification to a <see cref="FieldDisclosure"/>. The console never
    /// renders a raw commit hash (S-6); an opaque reference is a disclosed token, <c>redacted</c> is
    /// policy-hidden (F-5), and <c>unavailable</c> is not-yet-known.
    /// </summary>
    public static FieldDisclosure ResolveCommitDisclosure(CommitEvidenceCommitReferenceClassification classification)
        => classification switch
        {
            CommitEvidenceCommitReferenceClassification.Opaque_reference => FieldDisclosure.Visible,
            CommitEvidenceCommitReferenceClassification.Redacted => FieldDisclosure.Redacted,
            CommitEvidenceCommitReferenceClassification.Unavailable => FieldDisclosure.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, "Unknown commit reference classification."),
        };

    /// <summary>The disclosed value shown for a commit reference (metadata classification only, never a hash).</summary>
    public static string ResolveCommitReferenceText(CommitEvidenceCommitReferenceClassification classification)
        => classification switch
        {
            CommitEvidenceCommitReferenceClassification.Opaque_reference => "Opaque reference present",
            CommitEvidenceCommitReferenceClassification.Redacted => "Opaque reference present",
            CommitEvidenceCommitReferenceClassification.Unavailable => "Opaque reference present",
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, "Unknown commit reference classification."),
        };

    /// <summary>
    /// Resolves a coarse, non-sensitive size class label from a byte length (a presentation bucket, not a
    /// wire contract). Directories pass <see langword="null"/> and render the not-applicable marker.
    /// </summary>
    public static string ResolveSizeClass(long? byteLength)
    {
        if (byteLength is not long bytes)
        {
            return "—";
        }

        const long kib = 1024L;
        const long mib = kib * 1024L;
        const long gib = mib * 1024L;

        return bytes switch
        {
            <= 0 => "empty",
            < kib => "≤ 1 KiB",
            < mib => "≤ 1 MiB",
            < gib => "≤ 1 GiB",
            _ => "> 1 GiB",
        };
    }

    /// <summary>
    /// Resolves the snake_case canonical wire token for an error category (e.g. <c>tenant_access_denied</c>),
    /// read from the generated <c>[EnumMember]</c> attribute so it is total by construction.
    /// </summary>
    public static string ResolveErrorReasonToken(CanonicalErrorCategory category)
        => _errorTokens.TryGetValue(category, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown canonical error category.");

    /// <summary>
    /// Resolves the operator-facing safe explanation for a canonical error reason token. Unmapped tokens
    /// fall back to <see cref="DefaultErrorExplanation"/> — never an existence-revealing message.
    /// </summary>
    public static string ResolveErrorExplanation(string reasonToken)
        => !string.IsNullOrEmpty(reasonToken) && _errorExplanations.TryGetValue(reasonToken, out string? text)
            ? text
            : DefaultErrorExplanation;

    /// <summary>
    /// Whether <paramref name="reasonToken"/> is a known, vetted canonical category — the union of the
    /// generated <see cref="CanonicalErrorCategory"/> wire tokens and the live-path denial tokens the
    /// pages render. Used by the error presenter so an arbitrary Problem Details <c>category</c> string is
    /// never echoed into the operator's DOM verbatim (§3.9 — surface only vetted metadata).
    /// </summary>
    public static bool IsKnownReasonToken(string? reasonToken)
        => !string.IsNullOrEmpty(reasonToken)
            && (_errorExplanations.ContainsKey(reasonToken) || _errorTokens.Values.Contains(reasonToken));

    /// <summary>
    /// Resolves an operator-facing label for a workspace's latest reason category. <c>Success</c> renders
    /// as <c>"None"</c> (a healthy workspace has no failure reason) rather than the raw <c>success</c> wire
    /// token; every other category renders its canonical token.
    /// </summary>
    public static string ResolveReasonCategoryLabel(CanonicalErrorCategory category)
        => category == CanonicalErrorCategory.Success ? "None" : ResolveErrorReasonToken(category);

    private static FrozenDictionary<CanonicalErrorCategory, string> BuildErrorTokens()
    {
        Dictionary<CanonicalErrorCategory, string> map = [];
        foreach (CanonicalErrorCategory value in Enum.GetValues<CanonicalErrorCategory>())
        {
            string name = Enum.GetName(value)
                ?? throw new InvalidOperationException($"CanonicalErrorCategory value {value} has no declared name.");
            FieldInfo field = typeof(CanonicalErrorCategory).GetField(name)
                ?? throw new InvalidOperationException($"CanonicalErrorCategory field '{name}' is missing.");
            EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()
                ?? throw new InvalidOperationException($"CanonicalErrorCategory.{name} is missing the [EnumMember] attribute.");
            map[value] = attribute.Value
                ?? throw new InvalidOperationException($"CanonicalErrorCategory.{name} has a null EnumMember.Value.");
        }

        return map.ToFrozenDictionary();
    }
}
