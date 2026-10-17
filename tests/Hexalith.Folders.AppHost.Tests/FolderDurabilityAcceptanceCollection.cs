using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Serializes the fixture's process environment changes with other test collections.</summary>
[CollectionDefinition("Folder durability acceptance", DisableParallelization = true)]
public sealed class FolderDurabilityAcceptanceCollection;
