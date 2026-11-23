namespace Hexalith.Folders.Tests.Shared.AuditLeakage;

/// <summary>A labelled synthetic sample and its declared scan scope.</summary>
/// <param name="Id">The safe label used in diagnostics.</param>
/// <param name="Value">The synthetic value to detect.</param>
/// <param name="Classification">The corpus classification.</param>
/// <param name="Category">The corpus category.</param>
/// <param name="ForbiddenOutputSurfaces">The channels where the value is forbidden.</param>
/// <param name="ParticipatesIn">The kinds of scans that use the sample.</param>
internal sealed record AuditLeakageSample(
    string Id,
    string Value,
    string Classification,
    string Category,
    string[] ForbiddenOutputSurfaces,
    string[] ParticipatesIn);
