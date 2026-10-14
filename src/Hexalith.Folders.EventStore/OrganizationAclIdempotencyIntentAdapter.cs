using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Organization;

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
        RequireMembers(root, ["requestSchemaVersion", "operations"]);
        if (root.EnumerateObject().Any(property =>
            property.Name.Equals("requestSchemaVersion", StringComparison.OrdinalIgnoreCase)
            && property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
        {
            throw new JsonException("Organization ACL schema version must be a string or null.");
        }
        if (!root.TryGetProperty("operations", out JsonElement operations)
            || operations.ValueKind != JsonValueKind.Array
            || operations.GetArrayLength() == 0
            || (operationId != "initialize-organization-acl-baseline" && operations.GetArrayLength() != 1))
        {
            throw new JsonException("Organization ACL canonical intent requires operations.");
        }

        if (!string.Equals(command.AggregateId, command.Tenant, StringComparison.Ordinal))
        {
            throw new JsonException("Organization ACL intent targets another actor stream.");
        }

        OrganizationAclOperationIntent intent = operationId == "revoke-organization-acl-principal"
            ? OrganizationAclOperationIntent.Revoke : OrganizationAclOperationIntent.Grant;
        List<OrganizationAclOperation> parsed = [];
        foreach (JsonElement operation in operations.EnumerateArray())
        {
            RequireMembers(operation, ["principalKind", "principalId", "action"]);
            if (operation.ValueKind != JsonValueKind.Object
                || !operation.TryGetProperty("principalKind", out JsonElement kindValue)
                || kindValue.ValueKind != JsonValueKind.String
                || !OrganizationAclPrincipalKindParser.TryParse(kindValue.GetString(), out OrganizationAclPrincipalKind kind)
                || !operation.TryGetProperty("principalId", out JsonElement principalId)
                || principalId.ValueKind != JsonValueKind.String
                || !operation.TryGetProperty("action", out JsonElement action)
                || action.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Organization ACL intent has an invalid operation.");
            }

            parsed.Add(new OrganizationAclOperation(intent, kind, principalId.GetString()!, action.GetString()!));
        }

        IOrganizationAclCommand domainCommand = operationId switch
        {
            "initialize-organization-acl-baseline" => new InitializeOrganizationAclBaseline(
                command.Tenant, command.AggregateId, parsed, "correlation", "task", "key", null),
            "grant-organization-acl-principal" => new GrantOrganizationAclPrincipal(
                command.Tenant, command.AggregateId, parsed[0].PrincipalKind, parsed[0].PrincipalId,
                parsed[0].Action, "correlation", "task", "key", null),
            "revoke-organization-acl-principal" => new RevokeOrganizationAclPrincipal(
                command.Tenant, command.AggregateId, parsed[0].PrincipalKind, parsed[0].PrincipalId,
                parsed[0].Action, "correlation", "task", "key", null),
            _ => throw new JsonException("Unknown organization ACL operation."),
        };
        OrganizationAclCommandValidationResult validation = OrganizationAclCommandValidator.Validate(domainCommand);
        if (!validation.IsAccepted)
        {
            throw new JsonException("Organization ACL intent fails domain validation.");
        }

        JsonObject normalized = JsonNode.Parse(command.Payload)?.AsObject()
            ?? throw new JsonException("Organization ACL intent must be an object.");
        normalized["operations"] = new JsonArray(validation.Operations.Select(operation =>
            (JsonNode?)new JsonObject
            {
                ["principalKind"] = operation.PrincipalKind.ToString(),
                ["principalId"] = operation.PrincipalId,
                ["action"] = operation.Action,
            }).ToArray());
        IdempotencyIntentCommand normalizedCommand = command with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(normalized),
        };
        using JsonDocument normalizedDocument = FoldersCanonicalIntentBuilder.ParsePayload(normalizedCommand);

        return FoldersCanonicalIntentBuilder.Create(normalizedCommand,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["acl_entries"] = FoldersCanonicalIntentBuilder.ReadCanonicalArray(normalizedDocument.RootElement, "operations"),
                ["effect"] = operationId,
                ["organization_id"] = command.AggregateId,
                ["request_schema_version"] = FoldersCanonicalIntentBuilder.ReadString(normalizedDocument.RootElement, "requestSchemaVersion"),
            });
    }

    private static void RequireMembers(JsonElement value, string[] members)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Organization ACL payload members require an object.");
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!members.Contains(property.Name, StringComparer.OrdinalIgnoreCase) || !seen.Add(property.Name))
            {
                throw new JsonException("Organization ACL payload contains an unknown or ambiguous member.");
            }
        }
    }

}
