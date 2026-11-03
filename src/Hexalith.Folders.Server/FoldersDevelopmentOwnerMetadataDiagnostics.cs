using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Folders.Server;

/// <summary>Records only source hashes and timing around the unchanged selected private owner reader.</summary>
/// <param name="inner">The actual bound owner SDK reader.</param>
/// <param name="logger">The actual private Development host logger.</param>
internal sealed class FoldersDevelopmentOwnerMetadataDiagnostics(INativeSourceMetadataReader inner,
    ILogger<FoldersDevelopmentOwnerMetadataDiagnostics> logger) : INativeSourceMetadataReader
{
    /// <inheritdoc/>
    public async Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        string tenant = Hash(identity.TenantId), domain = Hash(identity.Domain), aggregate = Hash(identity.AggregateId);
        long started = Stopwatch.GetTimestamp();
        try
        {
            logger.LogInformation("Private owner metadata started: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}",
                tenant, domain, aggregate);
        }
        catch (Exception) { /* Diagnostic logging cannot replace an owner read. */ }
        NativeSourceObservation? result = null;
        try
        {
            result = await inner.ReadAsync(identity, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            try
            {
                logger.LogInformation("Private owner metadata completed: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}, Available={Available}, ElapsedMilliseconds={ElapsedMilliseconds}, CallerCanceled={CallerCanceled}",
                    tenant, domain, aggregate, result?.Proof is not null, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    cancellationToken.IsCancellationRequested);
            }
            catch (Exception) { /* Diagnostic logging cannot replace the owner result or cancellation. */ }
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
