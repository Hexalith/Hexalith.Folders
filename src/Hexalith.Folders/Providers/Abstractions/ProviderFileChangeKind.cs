namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies the provider-neutral effect of one ordered file change.
/// </summary>
public enum ProviderFileChangeKind
{
    /// <summary>Adds a file that is not expected to exist.</summary>
    Add,

    /// <summary>Replaces the content of an existing file.</summary>
    Change,

    /// <summary>Removes an existing file.</summary>
    Remove,
}
