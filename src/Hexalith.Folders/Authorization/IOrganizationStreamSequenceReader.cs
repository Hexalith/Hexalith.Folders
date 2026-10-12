namespace Hexalith.Folders.Authorization;

/// <summary>Reads the current EventStore organization actor sequence for projection catch-up checks.</summary>
public interface IOrganizationStreamSequenceReader
{
    Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
        CancellationToken cancellationToken = default);
}
