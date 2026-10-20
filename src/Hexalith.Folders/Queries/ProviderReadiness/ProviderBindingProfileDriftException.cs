namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>Indicates that private host configuration no longer matches retained provider authority.</summary>
internal sealed class ProviderBindingProfileDriftException()
    : InvalidOperationException("Private provider profile no longer matches retained binding authority.");
