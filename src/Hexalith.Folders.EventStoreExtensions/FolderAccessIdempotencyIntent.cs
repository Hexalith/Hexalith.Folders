using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.EventStore;

/// <summary>Shared Contract Spine fields for Grant/Revoke folder access adapters.</summary>
internal static class FolderAccessIdempotencyIntent
{
    public static IdempotencyCanonicalIntent Create(IdempotencyIntentCommand command, string effect)
    {
        using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
        JsonElement root = document.RootElement;
        string? principalKind;
        string? principalId;
        string? action;
        JsonDocument? normalizedGuard = null;

        try
        {
            if (root.TryGetProperty("operations", out JsonElement operations))
            {
                if (operations.ValueKind != JsonValueKind.Array
                    || operations.GetArrayLength() != 1
                    || operations[0].ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Folder access canonical intent requires exactly one operation.");
                }

                JsonElement first = operations[0];
                principalKind = FoldersCanonicalIntentBuilder.ReadString(first, "principalKind");
                principalId = FoldersCanonicalIntentBuilder.ReadString(first, "principalId");
                action = FoldersCanonicalIntentBuilder.ReadString(first, "action");
            }
            else
            {
                if (root.TryGetProperty("requestSchemaVersion", out _))
                {
                    throw new JsonException("Decorated folder access canonical intent must not carry requestSchemaVersion.");
                }

                if (root.TryGetProperty("effect", out _))
                {
                    throw new JsonException("Folder access canonical intent rejects caller-supplied effect.");
                }

                FoldersCanonicalIntentBuilder.VerifyRequiredMatchingFolderId(root, command.AggregateId);
                string? subjectRef = FoldersCanonicalIntentBuilder.ReadString(root, "subjectRef");
                string? permissionLevel = FoldersCanonicalIntentBuilder.ReadString(root, "permissionLevel");
                string? aclEntryId = FoldersCanonicalIntentBuilder.ReadString(root, "aclEntryId");
                action = MapPermissionLevelToAction(permissionLevel);

                if (string.IsNullOrWhiteSpace(subjectRef)
                    || action is null
                    || string.IsNullOrWhiteSpace(aclEntryId)
                    || !TryParseSubjectRef(subjectRef, out principalKind, out principalId))
                {
                    throw new JsonException("Folder access canonical intent requires valid subjectRef, permissionLevel, and aclEntryId.");
                }

                string expectedAclEntryId = DeriveAclEntryId(principalKind, principalId, permissionLevel!);
                if (!string.Equals(aclEntryId, expectedAclEntryId, StringComparison.Ordinal))
                {
                    throw new JsonException("Folder access canonical intent aclEntryId does not match subjectRef and permissionLevel.");
                }

                JsonObject normalized = new()
                {
                    ["requestSchemaVersion"] = "v1",
                    ["operations"] = new JsonArray(new JsonObject
                    {
                        ["principalKind"] = principalKind,
                        ["principalId"] = principalId,
                        ["action"] = action,
                    }),
                };
                normalizedGuard = JsonDocument.Parse(normalized.ToJsonString());
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
                },
                canonicalGuardRoot: normalizedGuard?.RootElement);
        }
        finally
        {
            normalizedGuard?.Dispose();
        }
    }

    private static string? MapPermissionLevelToAction(string? permissionLevel)
        => permissionLevel switch
        {
            "read" => "read_metadata",
            "write" => "mutate_files",
            "administer" => "manage_folder_access",
            _ => null,
        };

    private static bool TryParseSubjectRef(string subjectRef, out string principalKindToken, out string principalId)
    {
        if (subjectRef.Contains(':', StringComparison.Ordinal))
        {
            int separator = subjectRef.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator >= subjectRef.Length - 1)
            {
                principalKindToken = string.Empty;
                principalId = string.Empty;
                return false;
            }

            principalKindToken = subjectRef[..separator];
            principalId = subjectRef[(separator + 1)..];
            return principalKindToken is "user" or "group" or "role" or "delegated_service_agent"
                && IsValidIdentifier(principalId);
        }

        principalKindToken = "user";
        principalId = subjectRef;
        return IsValidIdentifier(principalId);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        bool isLowercase = true;
        foreach (char c in value)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-'))
            {
                isLowercase = false;
                break;
            }
        }

        if (isLowercase)
        {
            return true;
        }

        if (value.Length < 16 || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string DeriveAclEntryId(string principalKindToken, string principalId, string permissionLevel)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{principalKindToken}|{principalId}|{permissionLevel}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
