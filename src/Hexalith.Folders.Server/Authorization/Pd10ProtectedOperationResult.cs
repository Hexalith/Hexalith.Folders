namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Holds the authorization outcome and optional protected value of a candidate v2 operation.
/// </summary>
/// <typeparam name="T">The protected value type.</typeparam>
/// <param name="Outcome">The closed authorization outcome.</param>
/// <param name="Value">The value observed only after successful authorization and binding.</param>
internal sealed record Pd10ProtectedOperationResult<T>(Pd10AuthorizationOutcome Outcome, T? Value);
