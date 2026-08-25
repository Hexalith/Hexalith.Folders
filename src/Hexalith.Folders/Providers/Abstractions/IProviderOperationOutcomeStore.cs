namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderOperationOutcomeStore
{
    ValueTask<ProviderOperationReservationResult> ReserveAsync(
        ProviderOperationReservationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ValidateAsync(
        ProviderOperationReservationValidationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<bool?> RecordAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default);

    ValueTask<bool?> FinalizeNoDispatchAsync(
        ProviderOperationOutcomeRecord record,
        CancellationToken cancellationToken = default);
}
