using System.Collections;

using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Tests.Projections.OperationalStatus;

/// <summary>Cancels during actual enumeration, after admission and before the full history is folded.</summary>
internal sealed class CancelingOperationalEvents(IReadOnlyList<OperationalStatusEvent> events,
    CancellationTokenSource cancellation, int cancelAt) : IReadOnlyList<OperationalStatusEvent>
{
    internal int Visited { get; private set; }
    public int Count => events.Count;
    public OperationalStatusEvent this[int index] => events[index];
    public IEnumerator<OperationalStatusEvent> GetEnumerator()
    {
        foreach (OperationalStatusEvent item in events)
        {
            if (++Visited == cancelAt) cancellation.Cancel();
            yield return item;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
