using System.Text.Json;

using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Shared Contract Spine fields for Grant/Revoke folder access adapters.</summary>
internal static class FolderAccessIdempotencyIntent
{
    public static IdempotencyCanonicalIntent Create(IdempotencyIntentCommand command, string effect)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        string? principalKind = null;
        string? principalId = null;
        string? action = null;
        if (root.TryGetProperty("operations", out JsonElement operations)
            && operations.ValueKind == JsonValueKind.Array
            && operations.GetArrayLength() > 0)
        {
            JsonElement first = operations[0];
            principalKind = FoldersCanonicalIntentBuilder.ReadString(first, "principalKind");
            principalId = FoldersCanonicalIntentBuilder.ReadString(first, "principalId");
            action = FoldersCanonicalIntentBuilder.ReadString(first, "action");
        }

        return FoldersCanonicalIntentBuilder.Create(
            command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["acl_entry_id"] = string.Join('|', principalKind, principalId, action),
                ["effect"] = effect,
                ["folder_id"] = command.AggregateId,
                ["permission_level"] = action,
                ["subject_ref"] = string.Join(':', principalKind, principalId),
            });
    }
}
