using System.Text.Json.Serialization;

namespace Hexalith.Folders.Server.Authorization;

/// <summary>Exact six-field audit record emitted by the isolated PD10 candidate authorization boundary.</summary>
public sealed record Pd10AuthorizationAuditRecord(
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("tenant")] string Tenant,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("operation_family")] string OperationFamily,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
