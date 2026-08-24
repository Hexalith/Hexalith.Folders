namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies a read-only provider reconciliation outcome.
/// </summary>
public enum ProviderMutationStatusDisposition
{
    /// <summary>The exact intended commit is the selected ref head.</summary>
    Confirmed,

    /// <summary>The selected ref remains at the exact pre-mutation head.</summary>
    NotApplied,

    /// <summary>The selected ref contains conflicting evidence.</summary>
    Conflicting,

    /// <summary>Provider evidence could not be read.</summary>
    Unavailable,
}
