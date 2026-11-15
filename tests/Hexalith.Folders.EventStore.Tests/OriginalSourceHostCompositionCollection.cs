using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Serializes actual entry-point environment selection with every other host test in this assembly.</summary>
[CollectionDefinition("OriginalSourceHostComposition", DisableParallelization = true)]
public sealed class OriginalSourceHostCompositionCollection;
