using System.Text.Json.Serialization;

namespace Hexalith.Folders.Aggregates.Folder;

// Name-based JSON conversion is mandatory so the integer values below remain internal
// implementation detail. Wire shape, parity fixtures, and persisted projection records
// must serialize the enum NAME (e.g. "CallerRequested"), never the numeric ordinal. This
// keeps the renumbering safe across deploys: integer values can be reassigned without
// changing the wire/persistence contract.
[JsonConverter(typeof(JsonStringEnumConverter<FolderArchiveReasonCode>))]
public enum FolderArchiveReasonCode
{
    // Explicit sentinel so a TryParse miss does not silently coerce unknown input to a
    // valid reason. The zero member is "no reason supplied"; supported reasons start at 1.
    None = 0,
    CallerRequested = 1,
    PolicyRetention = 2,
    OperatorReview = 3,
}
