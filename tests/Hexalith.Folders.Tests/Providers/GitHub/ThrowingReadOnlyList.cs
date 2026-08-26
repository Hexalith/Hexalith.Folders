using System.Collections;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class ThrowingReadOnlyList<T> : IReadOnlyList<T>
{
    public T this[int index] => throw new InvalidOperationException("hostile-indexer");

    public int Count => throw new InvalidOperationException("hostile-count");

    public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("hostile-enumerator");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
