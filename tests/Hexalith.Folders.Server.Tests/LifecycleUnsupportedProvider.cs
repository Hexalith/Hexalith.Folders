namespace Hexalith.Folders.Server.Tests;

/// <summary>Proves the observer refuses a noncanonical provider before any resolution.</summary>
internal sealed class LifecycleUnsupportedProvider : IServiceProvider
{
    /// <summary>Gets the number of service resolution attempts.</summary>
    internal int Calls { get; private set; }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        Calls++;
        throw new InvalidOperationException("private-provider-message");
    }
}
