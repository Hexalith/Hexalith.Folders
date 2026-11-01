using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Checks actual owner delivery separately from terminal command and persisted administrator authority.</summary>
internal static class FolderDurabilityBootstrapEvidence
{
    /// <summary>Observes initially absent authority under the caller's existing deadline; present values retain exact validation.</summary>
    internal static async Task WaitForAuthorityAsync(Func<CancellationToken, Task<JsonObject?>> read, string principal, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            JsonObject? authority = await read(token).ConfigureAwait(false);
            if (authority is not null)
            {
                Authority(JsonSerializer.SerializeToElement(authority), principal);
                return;
            }

            await Task.Delay(1000, token).ConfigureAwait(false);
        }
    }

    /// <summary>Rejects an actual unsuccessful owner response before waiting for peer readbacks.</summary>
    internal static void AcceptedDelivery(JsonElement receipt)
    {
        if (receipt.GetProperty("Phase").GetString() != "accepted" || receipt.GetProperty("HttpStatus").GetInt32() != 202)
            throw new InvalidOperationException("The actual owner bootstrap was not accepted: phase=" + receipt.GetProperty("Phase").GetString()
                + ", HTTP=" + receipt.GetProperty("HttpStatus").GetInt32() + ".");
    }

    /// <summary>Binds a private actual response receipt to the independently committed attempt and principal.</summary>
    internal static JsonElement Receipt(ReadOnlyMemory<byte> bytes, RouteCatalogGenerationReadback catalog, string principal)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement receipt = document.RootElement;
        AcceptedDelivery(receipt);
        if (!catalog.Serving || catalog.Generation <= 0 || catalog.Epoch <= 0 || string.IsNullOrWhiteSpace(catalog.AttemptId)
            || catalog.ActivationBindingDigest is not { Length: 64 }
            || receipt.GetProperty("AttemptId").GetString() != catalog.AttemptId
            || receipt.GetProperty("RootDigest").GetString() != catalog.RootDigest
            || receipt.GetProperty("PrincipalId").GetString() != principal
            || receipt.GetProperty("Phase").GetString() != "accepted" || receipt.GetProperty("HttpStatus").GetInt32() != 202
            || string.IsNullOrWhiteSpace(receipt.GetProperty("MessageId").GetString())
            || string.IsNullOrWhiteSpace(receipt.GetProperty("CorrelationId").GetString()))
            throw new InvalidOperationException("The owner bootstrap has no accepted response bound to the actual catalog attempt and administrator.");
        return receipt.Clone();
    }

    /// <summary>Requires the original event to retain the authenticated human as both target and actor.</summary>
    internal static void Source(StreamReadPage page, string messageId, string correlationId, string principal)
    {
        if (page.Tenant != "system" || page.Domain != "global-administrators" || page.AggregateId != "global-administrators"
            || page.Metadata.IsTruncated || page.Metadata.LatestSequence != 1 || page.Metadata.EventCount != 1 || page.Events.Count != 1)
            throw new InvalidOperationException("The isolated administrator source is not the complete original bootstrap.");
        StreamReadEvent source = page.Events[0];
        if (source.SequenceNumber != 1 || source.UserId != principal || source.CausationId != messageId || source.CorrelationId != correlationId
            || source.EventTypeName.Split(',', 2)[0].Trim() != "Hexalith.Tenants.Contracts.Events.GlobalAdministratorSet"
            || source.SerializationFormat != "json" || source.MetadataVersion != 1 || source.PayloadVersion is not (null or 1))
            throw new InvalidOperationException("The persisted administrator event lost original identity or human attribution.");
        using JsonDocument payload = JsonDocument.Parse(source.Payload);
        JsonElement value = payload.RootElement;
        if (Property(value, "tenantId").GetString() != "system" || Property(value, "userId").GetString() != principal
            || Property(value, "actorUserId").GetString() != principal || Property(value, "setAt").GetDateTimeOffset() == default)
            throw new InvalidOperationException("The persisted administrator source does not authorize the verified bootstrap principal.");
    }

    /// <summary>Requires source-derived persisted projection membership before ordinary tenant creation.</summary>
    internal static void Authority(JsonElement authority, string principal)
    {
        if (authority.ValueKind != JsonValueKind.Object || Property(authority, "projectionVersion").GetString() != "tenant-sequence:1"
            || Property(authority, "projectedAt").GetDateTimeOffset() == default
            || Property(authority, "administrators").EnumerateArray().Select(item => item.GetString()).SequenceEqual([principal]) is false)
            throw new InvalidOperationException("The original administrator authority is not durably projected.");
    }

    private static JsonElement Property(JsonElement value, string name) => value.TryGetProperty(name, out JsonElement found)
        ? found : value.GetProperty(char.ToUpperInvariant(name[0]) + name[1..]);
}
