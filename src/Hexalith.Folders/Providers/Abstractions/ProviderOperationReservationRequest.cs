namespace Hexalith.Folders.Providers.Abstractions;

internal sealed record ProviderOperationReservationRequest(
    string OperationKind,
    string IntentFingerprint,
    string AuthorizationFingerprint,
    string CorrelationId)
{
    public override string ToString() => nameof(ProviderOperationReservationRequest);
}
