using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Isolates the legacy reader's process-wide sidecar environment during refusal qualification.</summary>
[CollectionDefinition("Source head boundary", DisableParallelization = true)]
public sealed class SourceHeadBoundaryCollection;
