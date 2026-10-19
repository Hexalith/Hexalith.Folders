using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Tests.OperationalStatus;

internal sealed class OperationalStatusSequenceReader : IOrganizationStreamSequenceReader
{
    public long Head { get; set; } = 1;
    public bool Unavailable { get; set; }
    public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId, CancellationToken cancellationToken = default)
        => Unavailable ? throw new TimeoutException() : Task.FromResult(Head);
}
