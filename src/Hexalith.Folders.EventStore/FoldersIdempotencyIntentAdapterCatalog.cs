using System.Collections.Frozen;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.EventStore;

/// <summary>Fails closed at startup when Folders mutation adapters lack the complete owner policy.</summary>
internal sealed class FoldersIdempotencyIntentAdapterCatalog(IEnumerable<IIdempotencyIntentAdapter> adapters) : IHostedService
{
    /// <summary>Gets every existing mutation discriminator from the immutable owner inventory.</summary>
    internal static readonly IReadOnlySet<string> RequiredCommandTypes =
        FoldersCommandIntentDeclarations.All.Keys.ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, IIdempotencyIntentAdapter> registered = new(StringComparer.Ordinal);
        foreach (IIdempotencyIntentAdapter adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            if (!registered.TryAdd(adapter.CommandType, adapter))
            {
                throw new InvalidOperationException(
                    "Multiple trusted idempotency adapters are registered for one command type.");
            }
        }

        if (!RequiredCommandTypes.SetEquals(registered.Keys))
        {
            throw new InvalidOperationException(
                "Folders trusted idempotency adapters are missing or unexpected for a mutation command type.");
        }

        foreach (IIdempotencyIntentAdapter adapter in registered.Values)
        {
            CommandIntentDeclaration expected = FoldersCommandIntentDeclarations.For(adapter.CommandType);
            if (adapter is not IDeclaredIdempotencyIntentAdapter declared
                || declared.Declaration is not { } actual
                || adapter.AdapterId != expected.AdapterId
                || adapter.OperationId != expected.OperationId
                || adapter.DescriptorVersion != expected.DescriptorSchema.Version
                || adapter.RetentionTier != expected.RetentionTier
                || actual.AdapterId != expected.AdapterId
                || actual.OperationId != expected.OperationId
                || actual.PolicyVersion != expected.PolicyVersion
                || actual.RetentionTier != expected.RetentionTier
                || actual.RequiresAuthenticatedSubject != expected.RequiresAuthenticatedSubject
                || RouteCatalogCodec.ComputeDeclarationDigest(actual.Route) != RouteCatalogCodec.ComputeDeclarationDigest(expected.Route)
                || RouteCatalogCodec.ComputeDescriptorDigest(actual.DescriptorSchema) != RouteCatalogCodec.ComputeDescriptorDigest(expected.DescriptorSchema))
            {
                throw new InvalidOperationException(
                    $"A Folders trusted idempotency adapter for '{adapter.CommandType}' lacks a matching complete owner declaration.");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
