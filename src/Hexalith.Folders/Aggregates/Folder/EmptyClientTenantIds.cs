using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

internal static class EmptyClientTenantIds
{
    internal static IReadOnlyDictionary<string, string?> Value { get; } =
        FrozenDictionary<string, string?>.Empty;
}
