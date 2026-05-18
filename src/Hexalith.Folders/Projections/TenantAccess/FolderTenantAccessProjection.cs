namespace Hexalith.Folders.Projections.TenantAccess;

public sealed class FolderTenantAccessProjection
{
    public string TenantId { get; init; } = string.Empty;

    public bool Enabled { get; set; }

    public Dictionary<string, FolderTenantPrincipalEvidence> Principals { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> ConfigurationKeys { get; init; } = new(StringComparer.Ordinal);

    public HashSet<string> RemovedConfigurationKeys { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, FolderTenantEventEvidence> ProcessedMessages { get; init; } = new(StringComparer.Ordinal);

    public long Watermark { get; set; }

    public string ProjectionWatermark { get; set; } = string.Empty;

    public DateTimeOffset? LastEventTimestamp { get; set; }

    public bool ReplayConflict { get; set; }

    public bool MalformedEvidence { get; set; }

    /// <summary>
    /// Optimistic concurrency token. <see cref="IFolderTenantAccessProjectionStore.SaveAsync"/> rejects writes
    /// whose <see cref="Version"/> does not match the currently-stored version and increments the stored version
    /// on success.
    /// </summary>
    public long Version { get; set; }

    public FolderTenantAccessProjection Clone()
        => new()
        {
            TenantId = TenantId,
            Enabled = Enabled,
            Principals = new Dictionary<string, FolderTenantPrincipalEvidence>(Principals, StringComparer.Ordinal),
            ConfigurationKeys = new HashSet<string>(ConfigurationKeys, StringComparer.Ordinal),
            RemovedConfigurationKeys = new HashSet<string>(RemovedConfigurationKeys, StringComparer.Ordinal),
            ProcessedMessages = new Dictionary<string, FolderTenantEventEvidence>(ProcessedMessages, StringComparer.Ordinal),
            Watermark = Watermark,
            ProjectionWatermark = ProjectionWatermark,
            LastEventTimestamp = LastEventTimestamp,
            ReplayConflict = ReplayConflict,
            MalformedEvidence = MalformedEvidence,
            Version = Version,
        };
}
