namespace Hexalith.Folders.Observability;

public sealed class FolderAuditObservationBuilder
{
    private readonly Dictionary<string, string> _classifications = new(StringComparer.Ordinal);

    public FolderAuditOperationKind OperationKind { get; set; } = FolderAuditOperationKind.Unknown;

    public FolderAuditResult Result { get; set; } = FolderAuditResult.Unknown;

    public string? TenantId { get; set; }

    public string? ActorReference { get; set; }

    public string? TaskId { get; set; }

    public string? OperationId { get; set; }

    public string? CorrelationId { get; set; }

    public string? FolderId { get; set; }

    public string? WorkspaceId { get; set; }

    public string? ProviderReference { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public TimeSpan Duration { get; set; }

    public FolderAuditRedactionState RedactionState { get; set; } = FolderAuditRedactionState.MetadataOnly;

    public string? StateTransition { get; set; }

    public string? SanitizedCategory { get; set; }

    public bool IsRetry { get; set; }

    public bool IsIdempotentReplay { get; set; }

    public bool IsDuplicate { get; set; }

    public FolderAuditObservationBuilder AddClassification(string key, string value)
    {
        if (FolderAuditSanitizer.TrySanitizeClassificationKey(key, out string? safeKey)
            && FolderAuditSanitizer.TrySanitizeCategory(value, out string? safeValue)
            && safeKey is not null
            && safeValue is not null)
        {
            _classifications[safeKey] = safeValue;
        }

        return this;
    }

    public FolderAuditObservation Build()
        => FolderAuditSanitizer.Create(this, _classifications);
}
