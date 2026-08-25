namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies a provider-neutral file change operation.
/// </summary>
public enum ProviderFileChangeKind
{
    /// <summary>Adds a file that must not already exist.</summary>
    Add,

    /// <summary>Changes a file that is expected to exist.</summary>
    Change,

    /// <summary>Removes a file that is expected to exist.</summary>
    Remove,
}
