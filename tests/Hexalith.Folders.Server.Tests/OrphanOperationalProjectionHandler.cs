using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Registers a named handler without a hosted processor to qualify discovery exclusion.</summary>
internal sealed class OrphanOperationalProjectionHandler : IAsyncDomainProjectionHandler
{
    public string Domain => "orphan";
    public string ProjectionType => "orphan-view";
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("An orphan projection must not be invoked.");
}
