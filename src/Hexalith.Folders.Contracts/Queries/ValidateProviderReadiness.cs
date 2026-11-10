using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Validate provider readiness through sanitized evidence.</summary>
/// <param name="ProviderBindingRef">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="RequestedCapability">The requestedCapability value.</param>
[Description("Validate provider readiness through sanitized evidence.")]
[HexalithQuery("Validate provider readiness through sanitized evidence.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.ValidateProviderReadiness",
    ProjectionType = "validate-provider-readiness", AggregateIdProperty = nameof(ProviderBindingRef))]
public sealed record ValidateProviderReadiness(
    [property: HexalithIdentifier, JsonPropertyName("providerBindingRef"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string ProviderBindingRef,
    [property: JsonPropertyName("requestedCapability"), Description("The requested capability."), JsonRequired, Required]
    ProviderCapabilityName RequestedCapability);
