using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

/// <summary>
/// Classifies a durable <see cref="ProviderIdempotencyAdmission"/> disposition into the shared
/// dispatch/replay/reject decision every GitHub <c>ReplayOrReject</c> gate must apply identically.
/// Story 3.10's create/bind gates and Story 3.11's mutation/commit gates previously repeated this
/// disposition switch four times; centralizing it here removes the drift risk DW-299 identified
/// without changing any gate's public result type.
/// </summary>
internal static class GitHubReplayAdmissionClassifier
{
    /// <summary>
    /// Determines whether the admission's disposition requires the caller to return the exact prior
    /// terminal outcome instead of dispatching or rejecting.
    /// </summary>
    /// <param name="disposition">The caller-supplied durable admission disposition.</param>
    /// <returns><see langword="true"/> when equivalent replay must return the prior terminal outcome.</returns>
    internal static bool RequiresReplay(ProviderIdempotencyDisposition disposition)
        => disposition == ProviderIdempotencyDisposition.EquivalentReplay;

    /// <summary>
    /// Classifies a non-replay disposition into its shared rejection category and reason code.
    /// Returns <see langword="null"/> for <see cref="ProviderIdempotencyDisposition.Fresh"/> and
    /// <see cref="ProviderIdempotencyDisposition.Execute"/>, which means the caller must dispatch.
    /// Every other non-replay disposition (conflict, expired, or any future undefined value) is a
    /// terminal <see cref="ProviderFailureCategory.ProviderConflict"/> rejection before source,
    /// credential, or provider access.
    /// </summary>
    /// <param name="disposition">The caller-supplied durable admission disposition.</param>
    /// <returns>
    /// <see langword="null"/> to dispatch, or the shared conflict category and reason code to reject.
    /// </returns>
    internal static (ProviderFailureCategory Category, string ReasonCode)? ClassifyRejection(
        ProviderIdempotencyDisposition disposition)
        => disposition switch
        {
            ProviderIdempotencyDisposition.Fresh
                or ProviderIdempotencyDisposition.Execute => null,
            ProviderIdempotencyDisposition.Conflict => (ProviderFailureCategory.ProviderConflict, "idempotency_conflict"),
            _ => (ProviderFailureCategory.ProviderConflict, "idempotency_key_expired"),
        };
}
