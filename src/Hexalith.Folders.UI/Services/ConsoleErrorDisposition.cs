namespace Hexalith.Folders.UI.Services;

/// <summary>
/// Classifies a canonical v2 error for distinct, non-disclosing operator presentation.
/// </summary>
public enum ConsoleErrorDisposition
{
    /// <summary>The caller has a fresh negative authority fact; retrying cannot help.</summary>
    Denied,

    /// <summary>Authority evidence is unusable; retrying may help after projections recover.</summary>
    AuthorityUnavailable,

    /// <summary>The failure is neither a canonical safe denial nor an authority outage.</summary>
    Failure,
}
