namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Bounded original source identity and chained prefix evidence for one position.</summary>
/// <param name="Sequence">The aggregate ordinal.</param>
/// <param name="MessageId">The unique retained message identity.</param>
/// <param name="EventDigest">The exact retained event digest.</param>
/// <param name="SourceDigest">The chained source digest through this position.</param>
/// <param name="RequiredViews">The original source-folded view scope required for duplicate completion.</param>
public sealed record OperationalStatusReceipt(long Sequence, string MessageId, string EventDigest, string SourceDigest,
    OperationalStatusViewBinding RequiredViews)
{
    /// <summary>Canonical integrity of original source identity and required view bindings.</summary>
    public string IntegrityDigest { get; init; } = string.Empty;

    /// <summary>Creates bounded source-derived receipt evidence.</summary>
    public static OperationalStatusReceipt Create(long sequence, string messageId, string eventDigest, string sourceDigest,
        OperationalStatusViewBinding requiredViews)
    {
        ArgumentNullException.ThrowIfNull(requiredViews);
        OperationalStatusReceipt value = new(sequence, messageId, eventDigest, sourceDigest, requiredViews);
        return value with { IntegrityDigest = value.ComputeIntegrity() };
    }

    /// <summary>Rejects damaged bindings and legacy receipts without required view evidence.</summary>
    public bool Valid()
        => Sequence > 0 && !string.IsNullOrWhiteSpace(MessageId) && EventDigest is { Length: 64 }
            && SourceDigest is { Length: 64 } && RequiredViews is not null
            && (RequiredViews.WorkspaceId is null || !string.IsNullOrWhiteSpace(RequiredViews.WorkspaceId))
            && (RequiredViews.TaskId is null || !string.IsNullOrWhiteSpace(RequiredViews.TaskId))
            && IntegrityDigest == ComputeIntegrity();

    private string ComputeIntegrity()
        => OperationalStatusIntegrity.Digest(new { Sequence, MessageId, EventDigest, SourceDigest, RequiredViews });
}
