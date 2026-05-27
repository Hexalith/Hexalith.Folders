using System.IO;

namespace Hexalith.Folders.Cli.Infrastructure;

/// <summary>
/// Abstraction over the CLI's standard output and standard error streams. Injecting writers (rather than
/// using <see cref="System.Console"/> directly) keeps tests hermetic and parallel-safe: a test captures
/// output without mutating global <see cref="System.Console"/> state.
/// </summary>
internal interface ICliConsole
{
    /// <summary>Gets the writer for primary command output (stdout).</summary>
    TextWriter Out { get; }

    /// <summary>Gets the writer for diagnostics, correlation/idempotency echoes, and error envelopes (stderr).</summary>
    TextWriter Error { get; }
}
