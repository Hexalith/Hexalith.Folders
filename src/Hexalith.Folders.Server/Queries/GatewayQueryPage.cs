namespace Hexalith.Folders.Queries.Gateway;

/// <summary>The merged cursor and effective page size for a paged Folders query.</summary>
/// <param name="Cursor">The supplied opaque cursor, if any.</param>
/// <param name="Limit">The effective limit including the operation default.</param>
internal readonly record struct GatewayQueryPage(string? Cursor, int Limit);
