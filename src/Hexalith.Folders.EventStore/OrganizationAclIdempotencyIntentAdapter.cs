using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Trusted command intent for each organization ACL mutation.</summary>
internal sealed class OrganizationAclIdempotencyIntentAdapter(string commandType, string operationId)
    : IIdempotencyIntentAdapter
{
    public string CommandType => commandType;

    public string AdapterId => FoldersCanonicalIntentBuilder.AdapterId;

    public string OperationId => operationId;

    public int DescriptorVersion => FoldersCanonicalIntentBuilder.DescriptorVersion;

    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("operations", out JsonElement operations)
            || operations.ValueKind != JsonValueKind.Array
            || operations.GetArrayLength() == 0
            || (operationId != "initialize-organization-acl-baseline" && operations.GetArrayLength() != 1))
        {
            throw new JsonException("Organization ACL canonical intent requires operations.");
        }

        return FoldersCanonicalIntentBuilder.Create(command,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["acl_entries"] = FoldersCanonicalIntentBuilder.ReadCanonicalArray(root, "operations"),
                ["effect"] = operationId,
                ["organization_id"] = command.AggregateId,
                ["request_schema_version"] = FoldersCanonicalIntentBuilder.ReadString(root, "requestSchemaVersion"),
            });
    }
}
