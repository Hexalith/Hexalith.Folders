using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies independent source positions for the durable projection reads in the callback tests.</summary>
internal sealed class McpCliGatewayQuerySequenceReader : IOrganizationStreamSequenceReader
{
    /// <summary>Gets the committed stream positions.</summary>
    internal Dictionary<(string Tenant, string Stream), long> Heads { get; } = [];

    /// <inheritdoc/>
    public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Heads.GetValueOrDefault((tenantId, organizationId)));
    }
}
