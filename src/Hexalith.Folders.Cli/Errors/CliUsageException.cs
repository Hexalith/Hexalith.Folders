using System;

namespace Hexalith.Folders.Cli.Errors;

/// <summary>
/// A pre-SDK usage error raised before any HTTP call is made (for example, a malformed request body or a
/// missing required input that is validated in the command body rather than by the parser). The command
/// pipeline maps this to <see cref="FoldersExitCodes.UsageError"/> (64). The message is always metadata-only.
/// </summary>
internal sealed class CliUsageException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CliUsageException"/> class.</summary>
    /// <param name="message">A metadata-only usage message (no secrets, tokens, paths, or file content).</param>
    public CliUsageException(string message)
        : base(message)
    {
    }
}
