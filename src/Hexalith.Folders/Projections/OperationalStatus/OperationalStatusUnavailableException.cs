namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Indicates unproven operational persistence that requires retry or recovery.</summary>
public sealed class OperationalStatusUnavailableException() : InvalidOperationException("Operational persistence is unavailable.");
