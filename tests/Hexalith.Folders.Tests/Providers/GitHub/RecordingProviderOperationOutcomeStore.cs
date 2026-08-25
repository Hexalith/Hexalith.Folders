using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingProviderOperationOutcomeStore : IProviderOperationOutcomeStore
{
    internal const string OperationReference = "operation-01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private readonly Queue<ProviderOperationReservationResult> _reservations;
    private readonly bool _validationResult;
    private readonly bool? _recordResult;
    private readonly bool _throwOnRecord;
    private readonly object _sync = new();

    private RecordingProviderOperationOutcomeStore(
        IEnumerable<ProviderOperationReservationResult> reservations,
        bool validationResult,
        bool? recordResult,
        bool throwOnRecord)
    {
        _reservations = new Queue<ProviderOperationReservationResult>(reservations);
        _validationResult = validationResult;
        _recordResult = recordResult;
        _throwOnRecord = throwOnRecord;
    }

    public int ReserveCalls { get; private set; }

    public int ValidateCalls { get; private set; }

    public int FinalizeCalls { get; private set; }

    public List<ProviderOperationOutcomeRecord> Records { get; } = [];

    public static RecordingProviderOperationOutcomeStore Acquired(
        bool validationResult = true,
        bool? recordResult = true,
        bool throwOnRecord = false)
        => new(
            [new ProviderOperationReservationResult(ProviderOperationReservationDisposition.Acquired, OperationReference, 1)],
            validationResult,
            recordResult,
            throwOnRecord);

    public static RecordingProviderOperationOutcomeStore WithReservations(params ProviderOperationReservationResult[] reservations)
        => new(reservations, validationResult: true, recordResult: true, throwOnRecord: false);

    public ValueTask<ProviderOperationReservationResult> ReserveAsync(
        ProviderOperationReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ReserveCalls++;
            return ValueTask.FromResult(_reservations.Count > 1 ? _reservations.Dequeue() : _reservations.Peek());
        }
    }

    public ValueTask<bool> ValidateAsync(
        ProviderOperationReservationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ValidateCalls++;
        }

        return ValueTask.FromResult(_validationResult);
    }

    public ValueTask<bool?> RecordAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_throwOnRecord)
        {
            throw new InvalidOperationException("metadata-only recorder failure");
        }

        lock (_sync)
        {
            Records.Add(record);
        }

        return ValueTask.FromResult(_recordResult);
    }

    public ValueTask FinalizeNoDispatchAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            FinalizeCalls++;
            Records.Add(record);
        }

        return ValueTask.CompletedTask;
    }
}
