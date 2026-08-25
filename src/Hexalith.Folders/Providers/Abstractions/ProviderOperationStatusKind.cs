namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies the provider-neutral result of one read-only outcome observation.
/// </summary>
public enum ProviderOperationStatusKind
{
    /// <summary>The intended commit is confirmed at the authorized ref.</summary>
    Confirmed,

    /// <summary>The authorized ref still points to the pre-operation head.</summary>
    NotApplied,

    /// <summary>The authorized ref points to conflicting evidence.</summary>
    Conflicting,

    /// <summary>Provider evidence was unavailable without changing state.</summary>
    Unavailable,
}
