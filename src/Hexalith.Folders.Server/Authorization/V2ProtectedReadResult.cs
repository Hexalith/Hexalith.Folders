namespace Hexalith.Folders.Server.Authorization;

/// <summary>Result of an unrouted v2 protected observation.</summary>
/// <typeparam name="T">Protected result type.</typeparam>
/// <param name="Authorization">Authorization outcome.</param>
/// <param name="Value">Observed value, present only after authorization succeeds.</param>
internal sealed record V2ProtectedReadResult<T>(V2AuthorizationOutcome Authorization, T? Value);
