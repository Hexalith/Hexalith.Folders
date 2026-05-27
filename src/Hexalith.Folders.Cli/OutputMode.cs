namespace Hexalith.Folders.Cli;

/// <summary>
/// Rendering mode selected by the global <c>--output</c> option. Both modes are metadata-only: neither
/// ever prints file bytes, base64 content, diffs, provider payloads, secrets/tokens, local absolute paths,
/// or unauthorized-resource-existence hints.
/// </summary>
internal enum OutputMode
{
    /// <summary>Readable, human-oriented summary (default).</summary>
    Human,

    /// <summary>Machine-readable JSON of the SDK result or projected problem shape.</summary>
    Json,
}
