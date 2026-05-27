using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.Observability;

public static partial class FolderAuditSanitizer
{
    private const string UnknownTenant = "tenant_unknown";
    private const int MaxIdentifierLength = 128;
    private const int MaxCategoryLength = 96;

    public static FolderAuditObservation Create(
        FolderAuditObservationBuilder builder,
        IReadOnlyDictionary<string, string>? classifications = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return new FolderAuditObservation(
            builder.OperationKind,
            builder.Result,
            RequiredSafeIdentifier(builder.TenantId, UnknownTenant),
            OptionalSafeIdentifier(builder.ActorReference),
            OptionalSafeIdentifier(builder.TaskId),
            OptionalSafeIdentifier(builder.OperationId),
            OptionalSafeIdentifier(builder.CorrelationId),
            OptionalSafeIdentifier(builder.FolderId),
            OptionalSafeIdentifier(builder.WorkspaceId),
            OptionalSafeIdentifier(builder.ProviderReference),
            builder.Timestamp,
            builder.Duration < TimeSpan.Zero ? TimeSpan.Zero : builder.Duration,
            builder.RedactionState,
            OptionalStateTransition(builder.StateTransition),
            OptionalCategory(builder.SanitizedCategory),
            builder.IsRetry,
            builder.IsIdempotentReplay,
            builder.IsDuplicate,
            new ReadOnlyDictionary<string, string>(SanitizeClassifications(classifications)));
    }

    public static string DurationBucket(TimeSpan duration)
    {
        double milliseconds = Math.Max(0, duration.TotalMilliseconds);
        double bucket = Math.Ceiling(milliseconds / 100d) * 100d;
        return bucket.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "ms";
    }

    public static bool TrySanitizeClassificationKey(string? value, out string? sanitized)
        => TrySanitize(value, ClassificationKeyPattern(), MaxCategoryLength, out sanitized);

    public static bool TrySanitizeCategory(string? value, out string? sanitized)
        => TrySanitize(value, CategoryPattern(), MaxCategoryLength, out sanitized);

    private static string RequiredSafeIdentifier(string? value, string fallback)
        => TrySanitizeIdentifier(value, out string? sanitized) && sanitized is not null ? sanitized : fallback;

    private static string? OptionalSafeIdentifier(string? value)
        => TrySanitizeIdentifier(value, out string? sanitized) && sanitized is not null ? sanitized : null;

    private static bool TrySanitizeIdentifier(string? value, out string? sanitized)
        => TrySanitize(value, IdentifierPattern(), MaxIdentifierLength, out sanitized);

    private static string? OptionalCategory(string? value)
        => TrySanitizeCategory(value, out string? sanitized) && sanitized is not null ? sanitized : null;

    private static string? OptionalStateTransition(string? value)
        => TrySanitize(value, StateTransitionPattern(), MaxCategoryLength, out string? sanitized) && sanitized is not null ? sanitized : null;

    private static Dictionary<string, string> SanitizeClassifications(IReadOnlyDictionary<string, string>? values)
    {
        Dictionary<string, string> sanitized = new(StringComparer.Ordinal);
        if (values is null)
        {
            return sanitized;
        }

        foreach (KeyValuePair<string, string> item in values)
        {
            if (TrySanitizeClassificationKey(item.Key, out string? key)
                && TrySanitizeCategory(item.Value, out string? value)
                && key is not null
                && value is not null)
            {
                sanitized[key] = value;
            }
        }

        return sanitized;
    }

    private static bool TrySanitize(string? value, Regex pattern, int maxLength, out string? sanitized)
    {
        sanitized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength
            || !pattern.IsMatch(trimmed)
            || IsSensitiveDiagnosticValue(trimmed))
        {
            return false;
        }

        sanitized = trimmed;
        return true;
    }

    private static bool IsSensitiveDiagnosticValue(string value)
    {
        string canonical = value.ToLowerInvariant();
        return canonical.Contains("token", StringComparison.Ordinal)
            || canonical.Contains("synthetic", StringComparison.Ordinal)
            || canonical.Contains("secret", StringComparison.Ordinal)
            || canonical.Contains("password", StringComparison.Ordinal)
            || canonical.Contains("credential", StringComparison.Ordinal)
            || canonical.Contains("repository", StringComparison.Ordinal)
            || canonical.Contains("repo_", StringComparison.Ordinal)
            || canonical.Contains("repo-", StringComparison.Ordinal)
            || canonical.Contains("://", StringComparison.Ordinal)
            || canonical.Contains('@', StringComparison.Ordinal)
            || canonical.Contains('\\', StringComparison.Ordinal)
            || canonical.Contains('/', StringComparison.Ordinal)
            || canonical.Contains("diff", StringComparison.Ordinal)
            || canonical.Contains("payload", StringComparison.Ordinal)
            || canonical.Contains("privatekey", StringComparison.Ordinal)
            || canonical.Contains("private_key", StringComparison.Ordinal)
            || canonical.Contains("installation", StringComparison.Ordinal);
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.Compiled)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,95}$", RegexOptions.Compiled)]
    private static partial Regex CategoryPattern();

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,95}$", RegexOptions.Compiled)]
    private static partial Regex ClassificationKeyPattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,47}->[a-z][a-z0-9_]{0,47}$", RegexOptions.Compiled)]
    private static partial Regex StateTransitionPattern();
}
