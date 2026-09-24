using Hexalith.Folders.Server.Authorization;

namespace Hexalith.Folders.IntegrationTests.Routing;

/// <summary>Records PD10 authorization audit decisions written by the composed host.</summary>
internal sealed class RecordingRoutingAuditSink : IPd10AuthorizationAuditSink
{
    private readonly List<Pd10AuthorizationAuditRecord> _records = [];

    /// <summary>Gets a snapshot of the recorded decisions.</summary>
    public IReadOnlyList<Pd10AuthorizationAuditRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(Pd10AuthorizationAuditRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_records)
        {
            _records.Add(record);
        }

        return ValueTask.CompletedTask;
    }
}
