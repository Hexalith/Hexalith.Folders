using System.Diagnostics;

namespace Task36FoldObserver;

/// <summary>Tracks only opaque existing request activities with a bounded, non-waiting advisory inventory.</summary>
internal sealed class LifecycleRequestCorrelation
{
    /// <summary>Bounds retained request identities independently of the existing sink capacity.</summary>
    internal const int MaximumTrackedRequests = 64;

    private readonly object _gate = new();
    private readonly Dictionary<long, (string? Trace, string? Span, Guid Framework, Guid Related, long Started)> _active = [];
    private long _sequence;
    private long _unavailableStarts;
    private long _unmatchedEnds;
    private int _gap;
    private int _drainPending;

    /// <summary>Copies existing trace/span and framework IDs only; never retains or inspects application objects.</summary>
    internal object Observe(bool started, long inFlight, Guid frameworkActivityId, Guid relatedActivityId)
    {
        if (!started && inFlight == 0) Volatile.Write(ref _drainPending, 1);
        if (!Monitor.TryEnter(_gate))
        {
            Volatile.Write(ref _gap, 1);
            return new { status = "unavailable", reason = "observation_busy" };
        }
        try
        {
            // A prior drain may itself have contended. Reconcile it before a later request starts.
            if (started) ReconcileDrain();
            Activity? current = Activity.Current;
            string? trace = current is not null && current.TraceId != default ? current.TraceId.ToHexString() : null;
            string? span = current is not null && current.SpanId != default ? current.SpanId.ToHexString() : null;
            if (Volatile.Read(ref _gap) != 0)
            {
                string reason = GapReason();
                _active.Clear();
                if (inFlight == 0) Volatile.Write(ref _gap, 0);
                return Metadata("unavailable", reason, null, trace, span, frameworkActivityId, relatedActivityId);
            }
            if ((trace is null || span is null) && frameworkActivityId == Guid.Empty)
            {
                if (started) _unavailableStarts++; else _unmatchedEnds++;
                Volatile.Write(ref _gap, 4);
                return Metadata("unavailable", "identity_unavailable", null, trace, span, frameworkActivityId, relatedActivityId);
            }
            if (started)
            {
                if (_active.Count == MaximumTrackedRequests)
                {
                    _active.Clear();
                    Volatile.Write(ref _gap, 2);
                    return Metadata("unavailable", "tracking_capacity", null, trace, span, frameworkActivityId, relatedActivityId);
                }
                long ordinal = ++_sequence;
                _active.Add(ordinal, (trace, span, frameworkActivityId, relatedActivityId, Stopwatch.GetTimestamp()));
                if (Volatile.Read(ref _gap) != 0)
                    return Metadata("unavailable", GapReason(), null, trace, span, frameworkActivityId, relatedActivityId);
                return Metadata("tracked", "request_start", ordinal, trace, span, frameworkActivityId, relatedActivityId);
            }

            // A trace alone, related ID alone, or conflicting available trace/span pair never establishes a join.
            long[] matches = _active.Where(entry => trace is not null && span is not null
                    && entry.Value.Trace is not null && entry.Value.Span is not null
                ? entry.Value.Trace == trace && entry.Value.Span == span
                : frameworkActivityId != Guid.Empty && entry.Value.Framework == frameworkActivityId)
                .Select(entry => entry.Key).ToArray();
            if (matches.Length != 1)
            {
                _unmatchedEnds++;
                Volatile.Write(ref _gap, matches.Length > 1 ? 3 : 4);
                if (matches.Length > 1)
                {
                    _active.Clear();
                    Volatile.Write(ref _gap, 3);
                    if (inFlight == 0) Volatile.Write(ref _gap, 0);
                }
                return Metadata("unmatched", matches.Length == 0 ? "start_not_observed" : "ambiguous_identity",
                    null, trace, span, frameworkActivityId, relatedActivityId);
            }
            long matched = matches[0];
            bool traceMatched = trace is not null && span is not null
                && _active[matched].Trace is not null && _active[matched].Span is not null;
            if (Volatile.Read(ref _gap) != 0)
                return Metadata("unavailable", GapReason(), null, trace, span, frameworkActivityId, relatedActivityId);
            _active.Remove(matched);
            return Metadata("matched", traceMatched ? "trace_span" : "framework_activity",
                matched, trace, span, frameworkActivityId, relatedActivityId);
        }
        finally
        {
            ReconcileDrain();
            Monitor.Exit(_gate);
        }
    }

    /// <summary>Snapshots bounded primitive identities, explicitly reporting unavailable correlation.</summary>
    internal object Snapshot()
    {
        if (!Monitor.TryEnter(_gate)) return new { status = "unavailable", reason = "observation_busy" };
        try
        {
            ReconcileDrain();
            int gap = Volatile.Read(ref _gap);
            object[] active = gap == 0 ? _active.Select(entry => (object)new
            {
                requestOrdinal = entry.Key,
                traceId = entry.Value.Trace,
                spanId = entry.Value.Span,
                frameworkActivityId = Id(entry.Value.Framework),
                relatedActivityId = Id(entry.Value.Related),
                elapsedMilliseconds = Stopwatch.GetElapsedTime(entry.Value.Started).TotalMilliseconds,
            }).ToArray() : [];
            return new
            {
                status = gap == 0 ? "available" : "unavailable",
                reason = gap == 0 ? "observed_ids_only" : GapReason(),
                capacity = MaximumTrackedRequests,
                unavailableStarts = _unavailableStarts,
                unmatchedEnds = _unmatchedEnds,
                activeRequests = active,
            };
        }
        finally { Monitor.Exit(_gate); }
    }

    private string GapReason() => Volatile.Read(ref _gap) switch
    {
        2 => "tracking_capacity",
        3 => "ambiguous_identity",
        4 => "incomplete_observation",
        _ => "observation_busy",
    };

    private void ReconcileDrain()
    {
        if (Interlocked.Exchange(ref _drainPending, 0) == 0) return;
        _active.Clear();
        Volatile.Write(ref _gap, 0);
    }

    private static object Metadata(string status, string reason, long? ordinal, string? trace, string? span,
        Guid framework, Guid related) => new
        {
            status,
            reason,
            requestOrdinal = ordinal,
            traceId = trace,
            spanId = span,
            frameworkActivityId = Id(framework),
            relatedActivityId = Id(related),
        };

    private static string? Id(Guid value) => value == Guid.Empty ? null : value.ToString("D");
}
