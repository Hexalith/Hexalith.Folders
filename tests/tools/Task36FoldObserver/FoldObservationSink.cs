using System.Text.Json;

namespace Task36FoldObserver;

/// <summary>Retains the existing bounded callback evidence vocabulary and process identity.</summary>
internal static class FoldObservationSink
{
    private static readonly object _sync = new();

    internal static void Record(string path, string action, string? source, long? position)
    {
        lock (_sync)
            File.AppendAllText(path, JsonSerializer.Serialize(new { action, source, position, processId = Environment.ProcessId }) + "\n");
    }
}
