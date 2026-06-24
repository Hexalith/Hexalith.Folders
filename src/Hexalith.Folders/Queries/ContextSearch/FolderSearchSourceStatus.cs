namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>The availability outcome of a search-source call (the source never throws into the handler's circuit).</summary>
public enum FolderSearchSourceStatus
{
    /// <summary>The search executed and returned hits.</summary>
    Available,

    /// <summary>The search index is unreachable/unconfigured; degrade to a safe read-model-unavailable outcome.</summary>
    Unavailable,

    /// <summary>The search backend reported in-band degradation (e.g. the syntactic axis was unavailable).</summary>
    Degraded,

    /// <summary>The search exceeded its time budget.</summary>
    Timeout,
}
