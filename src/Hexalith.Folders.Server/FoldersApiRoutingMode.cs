namespace Hexalith.Folders.Server;

/// <summary>Selects which public Folders API versions the server host routes.</summary>
public enum FoldersApiRoutingMode
{
    /// <summary>Routes only the historical v1 API. This is the default hold state; v2 is not routed.</summary>
    V1Only,

    /// <summary>Routes the historical v1 API and the PD10 v2 API through the candidate compatibility seam.</summary>
    Coexistence,

    /// <summary>
    /// Routes only the PD10 v2 API. External v1 requests receive the canonical 404; the seam's internal
    /// historical dispatch keeps working.
    /// </summary>
    V2Only,
}
