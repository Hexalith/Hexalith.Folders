namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class UnconfiguredProviderOperationOutcomeStore : IProviderOperationOutcomeStore
{
    public ValueTask<ProviderOperationReservationResult> ReserveAsync(
        ProviderOperationReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ProviderOperationReservationResult(
            ProviderOperationReservationDisposition.Unavailable,
            OperationReference: null,
            Generation: 0,
            FailureCategory: ProviderFailureCategory.ProviderConfigurationMissing,
            ReasonCode: "provider_operation_outcome_store_unconfigured"));
    }

    public ValueTask<bool> ValidateAsync(
        ProviderOperationReservationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool?> RecordAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<bool?>(false);
    }

    public ValueTask FinalizeNoDispatchAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
