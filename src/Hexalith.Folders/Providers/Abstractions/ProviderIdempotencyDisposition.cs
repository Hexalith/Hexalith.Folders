namespace Hexalith.Folders.Providers.Abstractions;

/// <summary>
/// Identifies the durable idempotency admission decision supplied to a provider mutation.
/// </summary>
public enum ProviderIdempotencyDisposition
{
    /// <summary>The intent is newly admitted and may execute once.</summary>
    Fresh,

    /// <summary>The same live intent already completed and must replay without provider access.</summary>
    EquivalentReplay,

    /// <summary>The key is live but belongs to a different intent.</summary>
    Conflict,

    /// <summary>The key has expired and must never execute as a new intent.</summary>
    Expired,
}
