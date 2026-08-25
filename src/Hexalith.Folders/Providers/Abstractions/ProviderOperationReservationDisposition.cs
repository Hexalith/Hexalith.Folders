namespace Hexalith.Folders.Providers.Abstractions;

internal enum ProviderOperationReservationDisposition
{
    Acquired,
    Pending,
    ReplaySuccess,
    ReplayUnknown,
    ReplayKnownFailure,
    Conflict,
    Unavailable,
}
