using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.EventStore;

/// <summary>Fails closed at startup when Folders mutation adapters are missing or duplicated.</summary>
internal sealed class FoldersIdempotencyIntentAdapterCatalog(IEnumerable<IIdempotencyIntentAdapter> adapters) : IHostedService
{
    internal static readonly IReadOnlySet<string> RequiredCommandTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Hexalith.Folders.Commands.CreateFolder",
        "Hexalith.Folders.Commands.ArchiveFolder",
        "Hexalith.Folders.Commands.GrantFolderAccess",
        "Hexalith.Folders.Commands.RevokeFolderAccess",
        "Hexalith.Folders.Commands.ConfigureProviderBinding",
        "Hexalith.Folders.Commands.CreateRepositoryBackedFolder",
        "Hexalith.Folders.Commands.BindRepository",
        "Hexalith.Folders.Commands.ConfigureBranchRefPolicy",
        "Hexalith.Folders.Commands.PrepareWorkspace",
        "Hexalith.Folders.Commands.LockWorkspace",
        "Hexalith.Folders.Commands.ReleaseWorkspaceLock",
        "Hexalith.Folders.Commands.MutateFiles",
        "Hexalith.Folders.Commands.CommitWorkspace",
    };

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

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
