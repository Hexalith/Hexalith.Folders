namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Provides internal deterministic hooks for transport boundary verification.
/// </summary>
internal sealed class ForgejoSmartHttpGitTransportTestHooks
{
    /// <summary>
    /// Gets or sets whether tests may bypass rejection of the test runner's ambient Git configuration.
    /// </summary>
    public bool AllowAmbientConfigurationForTests { get; init; }

    /// <summary>
    /// Gets or sets an effective configuration source used to exercise the production ambient-configuration scanner.
    /// </summary>
    public Func<string, LibGit2Sharp.Configuration>? EffectiveConfigurationFactory { get; init; }

    /// <summary>
    /// Gets or sets a deterministic temporary-repository path factory.
    /// </summary>
    public Func<string>? TemporaryRepositoryPathFactory { get; init; }

    /// <summary>
    /// Gets or sets a deterministic commit transport result used to exercise the concrete HTTP client caller boundary.
    /// </summary>
    public Func<ForgejoCommitRequest, CancellationToken, Task<ForgejoCommitResult>>? CommitOperation { get; init; }

    /// <summary>
    /// Gets or sets an observer invoked after a repository is opened and before it is configured.
    /// </summary>
    public Action<LibGit2Sharp.Repository>? RepositoryOpened { get; init; }

    /// <summary>
    /// Gets or sets an observer invoked after the private temporary directory is created and before the repository is opened.
    /// </summary>
    public Action<string>? BeforeRepositoryOpen { get; init; }

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
