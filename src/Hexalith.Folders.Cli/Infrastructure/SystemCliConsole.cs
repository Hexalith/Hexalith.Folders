using System;
using System.IO;

namespace Hexalith.Folders.Cli.Infrastructure;

/// <summary>
/// Production <see cref="ICliConsole"/> bound to the process <see cref="Console"/> streams.
/// </summary>
internal sealed class SystemCliConsole : ICliConsole
{
    /// <inheritdoc/>
    public TextWriter Out => Console.Out;

    /// <inheritdoc/>
    public TextWriter Error => Console.Error;
}
