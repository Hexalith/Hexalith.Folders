using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Server.Tests;

internal sealed class ChangingTenantAccessProjectionStore(string failure, int failureRead)
    : IFolderTenantAccessProjectionStore
{
    internal int Reads { get; private set; }

    internal void Reset() => Reads = 0;

    public Task<FolderTenantAccessProjection?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        bool changed = ++Reads >= failureRead;
        string current = changed ? failure : "valid";
        if (current == "store")
        {
            throw new InvalidOperationException("Projection store unavailable.");
        }

        FolderTenantAccessProjection projection = new()
        {
            TenantId = current == "foreign" ? "tenant-b" : "tenant-a",
            Enabled = current != "disabled",
            LastEventTimestamp = current switch
            {
                "stale" => DateTimeOffset.UtcNow.AddMinutes(-10),
                "future" => DateTimeOffset.UtcNow.AddMinutes(10),
                "timestamp" => null,
                _ => DateTimeOffset.UtcNow,
            },
            Watermark = 1,
            ProjectionWatermark = "tenant-a:1",
            ReplayConflict = current == "replay",
            MalformedEvidence = current == "malformed",
            Principals = current == "membership" ? [] : new(StringComparer.Ordinal)
            {
                ["user-a"] = new(current == "principal" ? "user-b" : "user-a",
                    current == "owner" ? "Member" : "TenantOwner"),
            },
        };
        return Task.FromResult<FolderTenantAccessProjection?>(projection);
    }

    public Task SaveAsync(FolderTenantAccessProjection projection, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The authorization test store is read-only.");
}
