namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Provides internal deterministic hooks for transport boundary verification.
/// </summary>
internal sealed class ForgejoSmartHttpGitTransportTestHooks
{
    /// <summary>
    /// Gets or sets an observer invoked immediately before the native fetch.
    /// </summary>
    public Action<string, ForgejoNativeOperationState>? BeforeFetch { get; init; }

    /// <summary>
    /// Gets or sets an observer invoked once when cleanup is attempted.
    /// </summary>
    public Action? CleanupAttempted { get; init; }

    /// <summary>
    /// Gets or sets a deterministic override of the real cleanup result.
    /// </summary>
    public Func<bool, bool>? CleanupResult { get; init; }

    /// <summary>
    /// Gets or sets the temporary-repository disk ceiling override.
    /// </summary>
    public long? MaximumTemporaryDiskBytes { get; init; }

    /// <summary>
    /// Gets or sets the smart-Git transfer ceiling override.
    /// </summary>
    public long? MaximumTransferBytes { get; init; }

    /// <summary>
    /// Gets or sets the caller-visible operation deadline override.
    /// </summary>
    public TimeSpan? OperationTimeout { get; init; }

    /// <summary>
    /// Gets or sets an observer invoked after native work and temporary-repository cleanup complete.
    /// </summary>
    public Action? NativeOperationCompleted { get; init; }

    /// <summary>
    /// Gets or sets an observer invoked at the receive-pack dispatch boundary.
    /// </summary>
    public Action? ReceivePackDispatched { get; init; }
}
