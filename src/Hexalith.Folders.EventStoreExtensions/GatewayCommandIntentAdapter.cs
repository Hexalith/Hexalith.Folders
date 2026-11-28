using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates;
using Hexalith.Folders.Contracts.Commands;
using Hexalith.Folders.Contracts.Commands.Models;
using Hexalith.Folders.Contracts.Projections.Audit;

namespace Hexalith.Folders.EventStore;

/// <summary>Validates decorated input before EventStore can resolve a retained intent.</summary>
internal sealed class GatewayCommandIntentAdapter<TPayload>(IDeclaredIdempotencyIntentAdapter adapter)
    : IDeclaredIdempotencyIntentAdapter
    where TPayload : class
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <inheritdoc />
    public string CommandType => adapter.CommandType;

    /// <inheritdoc />
    public string AdapterId => adapter.AdapterId;

    /// <inheritdoc />
    public string OperationId => adapter.OperationId;

    /// <inheritdoc />
    public int DescriptorVersion => adapter.DescriptorVersion;

    /// <inheritdoc />
    public IdempotencyReplayRetentionTier RetentionTier => adapter.RetentionTier;

    /// <inheritdoc />
    public CommandIntentDeclaration Declaration => adapter.Declaration;

    /// <inheritdoc />
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        try
        {
            using JsonDocument document = FoldersCanonicalIntentBuilder.ParsePayload(command);
            JsonElement root = document.RootElement;
            if (IsDecorated(root))
            {
                ValidateDecorated(command, root);
            }

            return adapter.CreateIntent(command);
        }
        catch (JsonException)
        {
            // The SDK maps invalid intent arguments to a non-retryable validation response.
            throw new ArgumentException("Invalid Folders command intent.", nameof(command));
        }
    }

    private static bool IsDecorated(JsonElement root)
        => root.TryGetProperty("idempotencyKey", out _) || root.TryGetProperty("changes", out _)
            || (!root.TryGetProperty("requestSchemaVersion", out _)
                && !((typeof(TPayload) == typeof(GrantFolderAccess) || typeof(TPayload) == typeof(RevokeFolderAccess))
                    && root.TryGetProperty("operations", out _)));

    private static void ValidateDecorated(IdempotencyIntentCommand command, JsonElement root)
    {
        RejectExplicitNulls(root, string.Empty);
        TPayload payload = root.Deserialize<TPayload>(StrictJson) ?? throw new JsonException("Missing decorated command payload.");
        if (command.Extensions is null || !command.Extensions.TryGetValue("taskId", out string? taskId)
            || !IsProcessIdentifier(taskId) || !IsProcessIdentifier(FoldersCanonicalIntentBuilder.ReadString(root, "idempotencyKey")))
        {
            throw new JsonException("Decorated commands require task context and a caller key.");
        }

        if (root.TryGetProperty("taskId", out JsonElement payloadTask) && payloadTask.GetString() != taskId)
        {
            throw new JsonException("Decorated payload and envelope task context differ.");
        }

        if (payload is not ConfigureProviderBinding)
        {
            FoldersCanonicalIntentBuilder.VerifyRequiredMatchingFolderId(root, command.AggregateId);
        }

        switch (payload)
        {
            case ArchiveFolder archive:
                ValidateEnumToken(root.GetProperty("archiveReasonCode"), archive.ArchiveReasonCode);
                break;
            case CreateFolder create:
                ValidateEnumToken(root.GetProperty("folderMetadata").GetProperty("metadataClass"), create.FolderMetadata.MetadataClass);
                break;
            case CreateRepositoryBackedFolder create:
                ValidateEnumToken(root.GetProperty("folderMetadata").GetProperty("metadataClass"), create.FolderMetadata.MetadataClass);
                break;
            case LockWorkspace locked:
                ValidateEnumToken(root.GetProperty("lockIntent"), locked.LockIntent);
                break;
            case ReleaseWorkspaceLock released:
                ValidateEnumToken(root.GetProperty("releaseReasonCode"), released.ReleaseReasonCode);
                break;
            case CommitWorkspace commit:
                ValidateAuditKeys(commit, root.GetProperty("auditMetadataKeys"));
                break;
            case MutateFiles batch:
                ValidateFileBatch(batch, root.GetProperty("changes"));
                break;
        }
    }

    private static void ValidateAuditKeys(CommitWorkspace commit, JsonElement keys)
    {
        if (commit.AuditMetadataKeys.Length is < 1 or > 24
            || commit.AuditMetadataKeys.Distinct().Count() != commit.AuditMetadataKeys.Length)
        {
            throw new JsonException("Invalid decorated commit audit metadata keys.");
        }

        for (int index = 0; index < commit.AuditMetadataKeys.Length; index++)
        {
            ValidateEnumToken(keys[index], commit.AuditMetadataKeys[index]);
        }
    }

    private static void ValidateEnumToken<TEnum>(JsonElement raw, TEnum value) where TEnum : struct, Enum
    {
        if (raw.ValueKind != JsonValueKind.String || !Enum.IsDefined(value)
            || !string.Equals(raw.GetString(), JsonSerializer.SerializeToElement(value, StrictJson).GetString(), StringComparison.Ordinal))
        {
            throw new JsonException("Decorated command requires the canonical enum token.");
        }
    }

    private static bool IsProcessIdentifier(string? value)
        => value is { Length: <= 128 } && StreamNameSegment.IsValid(value) && !ProviderCredentialShape.IsPresent(value);

    private static void RejectExplicitNulls(JsonElement node, string path)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in node.EnumerateObject())
                {
                    string child = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    RejectExplicitNulls(property.Value, child);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in node.EnumerateArray())
                {
                    RejectExplicitNulls(item, path + "[]");
                }

                break;
            case JsonValueKind.Null when path != "changes[].byteLength":
                throw new JsonException("Decorated command properties cannot explicitly be null.");
        }
    }

    private static void ValidateFileBatch(MutateFiles batch, JsonElement rawChanges)
    {
        if (batch.Changes.Length is < 1 or > DecoratedCommandContentEvidence.MaximumBatchItems)
        {
            throw new JsonException("Invalid decorated file batch size.");
        }

        long totalBytes = 0;
        HashSet<string> operations = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < batch.Changes.Length; index++)
        {
            FileMutationRequest change = batch.Changes[index];
            JsonElement rawChange = rawChanges[index];
            JsonElement path = rawChange.GetProperty("pathMetadata");
            ValidateEnumToken(path.GetProperty("pathPolicyClass"), change.PathMetadata.PathPolicyClass);
            ValidateEnumToken(path.GetProperty("unicodeNormalization"), change.PathMetadata.UnicodeNormalization);
            if (change.ExpectedFileVersion < 0 || change.ExpectedLockFence < 1
                || !IsProcessIdentifier(change.OperationId) || !operations.Add(change.OperationId))
            {
                throw new JsonException("Invalid decorated file version, fence or operation identifier.");
            }

            string? kind = FoldersCanonicalIntentBuilder.ReadString(rawChange, "fileOperationKind");
            string? transport = FoldersCanonicalIntentBuilder.ReadString(rawChange, "transportOperation");
            if (kind == "remove" && transport == "metadataOnlyRemoval" && change.ContentHashReference is null
                && change.ByteLength is null && change.InlineContent is null && change.StreamDescriptor is null)
            {
                continue;
            }

            if (kind is not ("add" or "change") || change.ByteLength is not int length
                || !DecoratedCommandContentEvidence.IsValidHashReference(change.ContentHashReference))
            {
                throw new JsonException("Invalid decorated file operation evidence.");
            }

            totalBytes += length;
            if (totalBytes > DecoratedCommandContentEvidence.MaximumBatchBytes
                || !ValidTransportEvidence(change, transport, length, rawChange))
            {
                throw new JsonException("Invalid decorated file transport evidence.");
            }
        }
    }

    private static bool ValidTransportEvidence(FileMutationRequest change, string? transport, int length, JsonElement rawChange)
    {
        if (transport == "PutFileInline" && change.InlineContent is { } inline && change.StreamDescriptor is null)
        {
            if (!DecoratedCommandContentEvidence.IsValidMediaType(inline.MediaType)
                || (inline.ContentMediaType is not null && !DecoratedCommandContentEvidence.IsValidMediaType(inline.ContentMediaType))
                || !DecoratedCommandContentEvidence.TryDecodeInline(inline.ContentBytes, length, change.ContentHashReference, out byte[]? bytes))
            {
                return false;
            }

            CryptographicOperations.ZeroMemory(bytes!);
            return true;
        }

        return transport == "PutFileStream" && change.StreamDescriptor is { } stream && change.InlineContent is null
            && length is > DecoratedCommandContentEvidence.InlineByteLimit and <= DecoratedCommandContentEvidence.MaximumFileBytes
            && DecoratedCommandContentEvidence.IsValidMediaType(stream.MediaType) && IsProcessIdentifier(stream.StagingReference)
            && stream.DeclaredLength == length && stream.ObservedLength == length
            && stream.ObservedContentHashReference == change.ContentHashReference
            && FoldersCanonicalIntentBuilder.ReadString(rawChange, "streamDescriptor", "uploadMode") == "request_body_stream";
    }
}
