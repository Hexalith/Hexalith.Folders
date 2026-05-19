using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

internal static class EmptyClientTenantIds
{
    internal static IReadOnlyDictionary<string, string?> Value { get; } =
        new Dictionary<string, string?>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);
}
