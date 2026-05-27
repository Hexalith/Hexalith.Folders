using System;

using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Cli.Infrastructure;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Composition;

/// <summary>
/// Injectable collaborators for the CLI. Production wires the real console, credential resolver, and SDK
/// client factory; tests substitute a capturing console, a temp-path credential resolver, and a fake
/// <see cref="IClient"/> so no socket is opened and <c>~/.hexalith</c> is never read.
/// </summary>
internal sealed class CliDependencies
{
    /// <summary>Gets the console (stdout/stderr) sink.</summary>
    public required ICliConsole Console { get; init; }

    /// <summary>Gets the credential resolver applying token precedence and the pre-SDK exit-65 rule.</summary>
    public required CredentialResolver Credentials { get; init; }

    /// <summary>
    /// Gets the factory that builds an <see cref="IClient"/> from the resolved base address and bearer token.
    /// Only invoked after credentials resolve successfully, so the token is always non-blank.
    /// </summary>
    public required Func<Uri, string, IClient> ClientFactory { get; init; }

    /// <summary>
    /// Gets the generator used for <c>--allow-auto-key</c>. Defaults to the SDK's self-contained ULID
    /// generator (a valid <c>OpaqueIdentifier</c>); there is no second ULID generator in the CLI.
    /// </summary>
    public Func<string> IdempotencyKeyGenerator { get; init; } = CorrelationAndTaskId.NewCorrelationId;
}
