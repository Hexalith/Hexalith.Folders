using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Queries.Models;

namespace Hexalith.Folders.Contracts.Queries;

/// <summary>Inspect provider-neutral support and capability evidence.</summary>
/// <param name="Cursor">The cursor value.</param>
/// <param name="Limit">The limit value.</param>
[Description("Inspect provider-neutral support and capability evidence.")]
[HexalithQuery("Inspect provider-neutral support and capability evidence.", Domain = "folders",
    WireType = "Hexalith.Folders.Queries.GetProviderSupportEvidence",
    ProjectionType = "get-provider-support-evidence")]
public sealed record GetProviderSupportEvidence(
    [property: JsonPropertyName("cursor"), Description("The cursor."), StringLength(256, MinimumLength = 1)]
    string? Cursor = null,
    [property: JsonPropertyName("limit"), Description("The limit."), Range(1, 1000)]
    int? Limit = null);
