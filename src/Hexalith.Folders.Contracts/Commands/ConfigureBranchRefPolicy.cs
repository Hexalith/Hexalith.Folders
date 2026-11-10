using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Commands.Models;

namespace Hexalith.Folders.Contracts.Commands;

/// <summary>Configure tenant-scoped branch and ref policy metadata.</summary>
/// <param name="RepositoryBindingId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="PolicyRef">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="DefaultRef">The defaultRef value.</param>
/// <param name="AllowedRefPatterns">The allowedRefPatterns value.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="IdempotencyKey">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="ProtectedRefPatterns">The protectedRefPatterns value.</param>
[Description("Configure tenant-scoped branch and ref policy metadata.")]
[HexalithCommand("Configure tenant-scoped branch and ref policy metadata.", Domain = "folders",
    WireType = "Hexalith.Folders.Commands.ConfigureBranchRefPolicy",
    AggregateIdProperty = nameof(FolderId), IdempotencyKeyProperty = nameof(IdempotencyKey))]
public sealed record ConfigureBranchRefPolicy(
    [property: HexalithIdentifier, JsonPropertyName("repositoryBindingId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string RepositoryBindingId,
    [property: HexalithIdentifier, JsonPropertyName("policyRef"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string PolicyRef,
    [property: HexalithIdentifier, JsonPropertyName("defaultRef"), Description("The default ref."), JsonRequired, Required, StringLength(91, MinimumLength = 14), RegularExpression("^branch_ref_[a-z0-9_]{3,80}$")]
    string DefaultRef,
    [property: JsonPropertyName("allowedRefPatterns"), Description("The allowed ref patterns."), JsonRequired, Required, MinLength(1), MaxLength(16)]
    string[] AllowedRefPatterns,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("idempotencyKey"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string IdempotencyKey,
    [property: JsonPropertyName("protectedRefPatterns"), Description("The protected ref patterns."), MaxLength(16)]
    string[]? ProtectedRefPatterns = null);
