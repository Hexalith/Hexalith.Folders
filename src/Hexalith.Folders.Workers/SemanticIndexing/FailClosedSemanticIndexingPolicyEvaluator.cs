using Hexalith.Folders.Projections.SemanticIndexing;

namespace Hexalith.Folders.Workers.SemanticIndexing;

internal sealed class FailClosedSemanticIndexingPolicyEvaluator : ISemanticIndexingPolicyEvaluator
{
    private const int MaxPathPolicyClassLength = 80;

    public ValueTask<SemanticIndexingPolicyEvaluationResult> EvaluateAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(entry.Evidence.PathPolicyClass))
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Failed(
                "authorization_evidence_unavailable",
                retryable: true));
        }

        string pathPolicyClass = entry.Evidence.PathPolicyClass.Trim();
        if (!IsValidPathPolicyClass(pathPolicyClass))
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Skipped(
                "path_policy_denied",
                retryable: false));
        }

        if (IsRedactedSensitivity(pathPolicyClass))
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Skipped(
                "sensitivity_redacted",
                retryable: false));
        }

        long? expectedLength = entry.Evidence.ByteLength ?? entry.Evidence.ObservedByteLength;
        if (expectedLength is null || string.IsNullOrWhiteSpace(entry.Evidence.MediaType))
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Failed(
                "content_descriptor_unavailable",
                retryable: true));
        }

        if (expectedLength > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes
            || entry.Evidence.ObservedByteLength > FoldersSemanticIndexingDefaults.MaxInlineIngestionBytes)
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Skipped(
                "content_too_large",
                retryable: false));
        }

        if (!IsSupportedInlineContentType(entry.Evidence.MediaType))
        {
            return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Skipped(
                "content_type_unsupported",
                retryable: false));
        }

        return ValueTask.FromResult(SemanticIndexingPolicyEvaluationResult.Allowed(
            "tenant_sensitive",
            "accepted_mutation_authorized"));
    }

    private static bool IsValidPathPolicyClass(string pathPolicyClass)
        => pathPolicyClass.Length <= MaxPathPolicyClassLength
            && pathPolicyClass.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '-');

    private static bool IsRedactedSensitivity(string pathPolicyClass)
        => pathPolicyClass.Contains("secret", StringComparison.Ordinal)
            || pathPolicyClass.Contains("credential", StringComparison.Ordinal)
            || pathPolicyClass.Contains("redacted", StringComparison.Ordinal);

    private static bool IsSupportedInlineContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType)
            && (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/x-yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(contentType, "application/markdown", StringComparison.OrdinalIgnoreCase));
}
