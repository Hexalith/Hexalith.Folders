using Hexalith.Folders.Authorization;
namespace Hexalith.Folders.Server.Tests;
internal sealed class FolderOwnerReadHeads : IOrganizationStreamSequenceReader
{
    public Dictionary<string, long> Values { get; } = new(StringComparer.Ordinal);
    public bool Unavailable { get; set; }
    public Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId, CancellationToken cancellationToken = default)
        => Unavailable ? Task.FromException<long>(new IOException("Owner evidence unavailable")) : Task.FromResult(Values.GetValueOrDefault(organizationId));
}
