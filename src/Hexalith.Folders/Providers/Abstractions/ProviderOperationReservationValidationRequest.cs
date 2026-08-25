namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationReservationValidationRequest(
    string OperationReference,
    long Generation,
    string IntentFingerprint)
{
    public override string ToString() => nameof(ProviderOperationReservationValidationRequest);
}
