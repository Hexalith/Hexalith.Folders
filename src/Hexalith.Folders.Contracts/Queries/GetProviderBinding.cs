using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Inspect a redacted provider binding reference.</summary>
/// <param name="ProviderBindingRef">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Inspect a redacted provider binding reference.")]
[HexalithQuery("Inspect a redacted provider binding reference.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.GetProviderBinding",
    ProjectionType = "get-provider-binding", AggregateIdProperty = nameof(ProviderBindingRef))]
public sealed record GetProviderBinding(
    [property: HexalithIdentifier, JsonPropertyName("providerBindingRef"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string ProviderBindingRef);
