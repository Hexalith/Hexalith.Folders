namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Qualifies independent Folders mutation processes sharing the same EventStore authority.</summary>
public sealed class WorkspaceContentDurabilityAcceptanceFixture : FolderDurabilityAcceptanceFixture
{
    /// <inheritdoc/>
    protected override bool QualifyContentReplica => true;
}
