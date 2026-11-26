using Hexalith.EventStore.DomainService.DurableTasks;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Runs a specific fixture effect or recovery lookup inside a real persisted Durable Task attempt.</summary>
internal sealed class ProvisioningQualificationEffect(
    Func<DurableTaskEffectStep, CancellationToken, Task<(DurableTaskEffectLookup, string?)>> lookup,
    Func<DurableTaskEffectStep, CancellationToken, Task<(DurableTaskEffectLookup, string?)>>? execute = null) : IDurableTaskEffect
{
    public Task<(DurableTaskEffectLookup Outcome, string? Receipt)> LookupAsync(DurableTaskEffectStep step, CancellationToken cancellationToken)
        => lookup(step, cancellationToken);

    public Task<(DurableTaskEffectLookup Outcome, string? Receipt)> ExecuteAsync(DurableTaskEffectStep step, CancellationToken cancellationToken)
        => (execute ?? throw new InvalidOperationException("Recovery tried to execute the owner again."))(step, cancellationToken);
}
