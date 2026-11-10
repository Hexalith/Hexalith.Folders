using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Get one authorized audit record.</summary>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="AuditRecordId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Get one authorized audit record.")]
[HexalithQuery("Get one authorized audit record.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.GetAuditRecord",
    ProjectionType = "get-audit-record", AggregateIdProperty = nameof(FolderId))]
public sealed record GetAuditRecord(
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("auditRecordId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string AuditRecordId);
