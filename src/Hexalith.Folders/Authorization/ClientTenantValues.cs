namespace Hexalith.Folders.Authorization;

/// <summary>Copies client-supplied tenant comparison inputs without creating tenant authority.</summary>
internal static class ClientTenantValues
{
    /// <summary>Copies ordinal keys and replaces the payload comparison only when it is supplied.</summary>
    /// <param name="values">The existing comparison inputs.</param>
    /// <param name="payloadTenantId">The optional payload tenant comparison.</param>
    /// <returns>An independent, ordinal dictionary of comparison inputs.</returns>
    internal static IReadOnlyDictionary<string, string?> WithPayloadTenant(
        IReadOnlyDictionary<string, string?> values,
        string? payloadTenantId)
    {
        Dictionary<string, string?> merged = new(values, StringComparer.Ordinal);
        if (payloadTenantId is not null)
        {
            merged["payload_tenant_id"] = payloadTenantId;
        }

        return merged;
    }
}
