namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies the exact durable terminal outcome carried by an equivalent replay.
/// </summary>
public enum ProviderPriorOutcomeDisposition
{
    /// <summary>The prior operation completed successfully.</summary>
    Success,

    /// <summary>The prior operation may have applied and requires reconciliation.</summary>
    Unknown,

    /// <summary>The prior operation ended in a known terminal failure.</summary>
    KnownFailure,
}
