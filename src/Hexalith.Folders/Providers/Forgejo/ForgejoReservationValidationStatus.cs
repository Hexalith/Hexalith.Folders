namespace Hexalith.Folders.Providers.Forgejo;

/// <summary>
/// Describes the result of revalidating a durable provider-operation reservation.
/// </summary>
internal enum ForgejoReservationValidationStatus
{
    /// <summary>
    /// The reservation remains valid for provider access.
    /// </summary>
    Valid = 0,

    /// <summary>
    /// The reservation was durably invalidated.
    /// </summary>
    Invalidated = 1,

    /// <summary>
    /// The reservation store could not provide authoritative evidence.
    /// </summary>
    Unavailable = 2,
}
