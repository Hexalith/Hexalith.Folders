namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderOperationCapability(
    string OperationId,
    ProviderOperationSupport Support,
    IReadOnlyDictionary<string, string> Limits,
    IReadOnlyDictionary<string, string> Constraints,
    bool Retryable,
    ProviderFailureCategory? FailureCategory);
