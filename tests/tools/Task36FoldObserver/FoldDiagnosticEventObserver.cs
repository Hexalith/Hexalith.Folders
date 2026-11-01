namespace Task36FoldObserver;

/// <summary>Accepts only the exact source-stream and position tuple emitted at handler entry.</summary>
internal sealed class FoldDiagnosticEventObserver(string path) : IObserver<KeyValuePair<string, object?>>
{
    /// <inheritdoc/>
    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == FoldDiagnosticObserver.EventName && value.Value is KeyValuePair<string, long> metadata)
            FoldObservationSink.Record(path, "fold", metadata.Key, metadata.Value);
    }

    /// <inheritdoc/>
    public void OnError(Exception error) { }

    /// <inheritdoc/>
    public void OnCompleted() { }
}
