using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Commands.Models;

namespace Hexalith.Folders.Contracts.Commands;

/// <summary>Accept a metadata-only workspace commit request.</summary>
/// <param name="OperationId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="TaskId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="BranchRefTarget">Opaque branch/ref target reference. Raw branch names are forbidden in requests, examples, audit metadata, and diagnostics.</param>
/// <param name="ChangedPathMetadataDigest">Digest of changed-path metadata. It is never a raw changed-path list, file content, diff, or local path.</param>
/// <param name="AuthorMetadataReference">Opaque author reference. It must not contain names, email addresses, provider users, tokens, or credential material.</param>
/// <param name="CommitMessageClassification">Commit-message classification metadata only. Raw commit-message text is forbidden.</param>
/// <param name="AuditMetadataKeys">The auditMetadataKeys value.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="WorkspaceId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="IdempotencyKey">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Accept a metadata-only workspace commit request.")]
[HexalithCommand("Accept a metadata-only workspace commit request.", Domain = "folders",
    WireType = "Hexalith.Folders.Commands.CommitWorkspace",
    AggregateIdProperty = nameof(FolderId), IdempotencyKeyProperty = nameof(IdempotencyKey))]
public sealed record CommitWorkspace(
    [property: HexalithIdentifier, JsonPropertyName("operationId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string OperationId,
    [property: HexalithIdentifier, JsonPropertyName("taskId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string TaskId,
    [property: JsonPropertyName("branchRefTarget"), Description("Opaque branch/ref target reference. Raw branch names are forbidden in requests, examples, audit metadata, and diagnostics."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^branchref_[A-Za-z0-9_-]{6,118}$")]
    string BranchRefTarget,
    [property: JsonPropertyName("changedPathMetadataDigest"), Description("Digest of changed-path metadata. It is never a raw changed-path list, file content, diff, or local path."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^digest_[A-Za-z0-9_-]{9,121}$")]
    string ChangedPathMetadataDigest,
    [property: JsonPropertyName("authorMetadataReference"), Description("Opaque author reference. It must not contain names, email addresses, provider users, tokens, or credential material."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^authorref_[A-Za-z0-9_-]{6,118}$")]
    string AuthorMetadataReference,
    [property: JsonPropertyName("commitMessageClassification"), Description("Commit-message classification metadata only. Raw commit-message text is forbidden."), JsonRequired, Required, StringLength(80, MinimumLength = 1), RegularExpression("^[a-z][a-z0-9_]{0,79}$")]
    string CommitMessageClassification,
    [property: JsonPropertyName("auditMetadataKeys"), Description("The audit metadata keys."), JsonRequired, Required, MinLength(1), MaxLength(24)]
    AuditMetadataKey[] AuditMetadataKeys,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("workspaceId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string WorkspaceId,
    [property: HexalithIdentifier, JsonPropertyName("idempotencyKey"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string IdempotencyKey);
