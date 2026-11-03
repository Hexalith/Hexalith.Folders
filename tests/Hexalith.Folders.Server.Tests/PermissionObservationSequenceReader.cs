using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Controls independent source observations without supplying permission decisions.</summary>
/// <param name="read">The current exact aggregate head observation.</param>
internal sealed class PermissionObservationSequenceReader(Func<string, CancellationToken, Task<long>> read)
    : IOrganizationStreamSequenceReader
{
    /// <inheritdoc/>
    public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
        CancellationToken cancellationToken = default) => read(organizationId, cancellationToken);
}
