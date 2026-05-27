using System.IO;

using Hexalith.Folders.Cli.Infrastructure;

namespace Hexalith.Folders.Cli.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="ICliConsole"/> capturing stdout and stderr so tests assert exact output without
/// mutating global <see cref="System.Console"/> state.
/// </summary>
internal sealed class TestCliConsole : ICliConsole
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    /// <inheritdoc/>
    public TextWriter Out => _out;

    /// <inheritdoc/>
    public TextWriter Error => _error;

    /// <summary>Gets the captured stdout text.</summary>
    public string StdOut => _out.ToString();

    /// <summary>Gets the captured stderr text.</summary>
    public string StdErr => _error.ToString();
}
