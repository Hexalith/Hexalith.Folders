using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Inspect branch and ref policy metadata.</summary>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Inspect branch and ref policy metadata.")]
[HexalithQuery("Inspect branch and ref policy metadata.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.GetBranchRefPolicy",
    ProjectionType = "get-branch-ref-policy", AggregateIdProperty = nameof(FolderId))]
public sealed record GetBranchRefPolicy(
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId);
