using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Serializes tests that deliberately retain the process-wide native Forgejo operation permit.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ForgejoNativeOperationGateCollection
{
    /// <summary>
    /// Identifies the non-parallel native-operation-gate test collection.
    /// </summary>
    public const string Name = "Forgejo native operation gate";
}
