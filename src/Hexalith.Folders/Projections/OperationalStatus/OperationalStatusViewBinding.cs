namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>The bounded original view scope required to acknowledge one retained source position.</summary>
/// <param name="WorkspaceId">The materialized workspace at that position, if any.</param>
/// <param name="TaskId">The materialized task at that position, if any.</param>
public sealed record OperationalStatusViewBinding(string? WorkspaceId, string? TaskId);
