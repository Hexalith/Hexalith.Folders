using System.Collections.Frozen;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Folders.Routing;

namespace Hexalith.Folders.EventStore;

/// <summary>Indexes the immutable owner declarations shared by runtime routing and legacy command admission.</summary>
internal static class FoldersCommandIntentDeclarations
{
    /// <summary>Gets the complete policy, independent of runtime adapter registrations.</summary>
    internal static IReadOnlyDictionary<string, CommandIntentDeclaration> All { get; } =
        FoldersCommandCatalog.Commands.ToFrozenDictionary(static declaration => declaration.Route.MessageType!, StringComparer.Ordinal);

    /// <summary>Returns the frozen declaration for an existing command discriminator.</summary>
    internal static CommandIntentDeclaration For(string commandType) => All[commandType];
}
