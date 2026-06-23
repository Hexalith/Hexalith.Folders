using System.Globalization;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Projections.SemanticIndexing;

[JsonConverter(typeof(JsonStringEnumConverter<SemanticIndexingBridgeStatus>))]
public enum SemanticIndexingBridgeStatus
{
    Unknown = 0,
    Indexed,
    Stale,
    Skipped,
    Failed,
    Tombstoned,
    ReconciliationRequired,
}

public static class SemanticIndexingBridgeStatusExtensions
{
    public static string ToStatusCode(this SemanticIndexingBridgeStatus status)
        => status switch
        {
            SemanticIndexingBridgeStatus.Indexed => "indexed",
            SemanticIndexingBridgeStatus.Stale => "stale",
            SemanticIndexingBridgeStatus.Skipped => "skipped",
            SemanticIndexingBridgeStatus.Failed => "failed",
            SemanticIndexingBridgeStatus.Tombstoned => "tombstoned",
            SemanticIndexingBridgeStatus.ReconciliationRequired => "reconciliation_required",
            _ => SemanticIndexingBridgeStatus.Unknown.ToString().ToLower(CultureInfo.InvariantCulture),
        };
}
