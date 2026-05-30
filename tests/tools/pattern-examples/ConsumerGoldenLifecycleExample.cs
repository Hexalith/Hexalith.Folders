// <!-- hexalith-example: compile-csharp -->
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.PatternExamples;

/// <summary>
/// Compile-checked backing for the consumer SDK reference (docs/sdk/api-reference.md). The golden-lifecycle
/// ordering is expressed with <c>nameof(IClient.*)</c> so it cannot drift from the typed client surface:
/// renaming or removing any operation breaks this build. Synthetic, metadata-only.
/// </summary>
public static class ConsumerGoldenLifecycleExample
{
    /// <summary>
    /// The canonical 9-step golden lifecycle ordering published in the API &amp; SDK reference. Step 6
    /// (UploadFile) is the convenience helper over <see cref="IClient"/>'s file-add operation.
    /// </summary>
    public static readonly string[] GoldenLifecycleOperations =
    [
        nameof(IClient.ConfigureProviderBindingAsync),
        nameof(IClient.ValidateProviderReadinessAsync),
        nameof(IClient.CreateRepositoryBackedFolderAsync),
        nameof(IClient.PrepareWorkspaceAsync),
        nameof(IClient.LockWorkspaceAsync),
        nameof(IClient.AddFileAsync),
        nameof(IClient.CommitWorkspaceAsync),
        nameof(IClient.GetWorkspaceStatusAsync),
        nameof(IClient.ListAuditTrailAsync),
    ];
}
