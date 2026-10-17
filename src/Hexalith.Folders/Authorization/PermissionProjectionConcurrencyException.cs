namespace Hexalith.Folders.Authorization;

/// <summary>A delivery can be reevaluated after another projection writer advances the row.</summary>
internal sealed class PermissionProjectionConcurrencyException(string message) : InvalidOperationException(message);
