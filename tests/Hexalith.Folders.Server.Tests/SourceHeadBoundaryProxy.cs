using System.Reflection;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Counts component dependency calls without adding mocking or transport dependencies.</summary>
public class SourceHeadBoundaryProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?>? _invoke;

    /// <summary>Gets the dependency call count.</summary>
    public int Calls { get; private set; }

    /// <summary>Creates one component dependency with an explicit bounded response.</summary>
    public static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        ArgumentNullException.ThrowIfNull(invoke);
        T proxy = Create<T, SourceHeadBoundaryProxy>();
        ((SourceHeadBoundaryProxy)(object)proxy)._invoke = invoke;
        return proxy;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls++;
        return _invoke!(targetMethod ?? throw new InvalidOperationException("Missing component method."), args);
    }
}
