namespace Hexalith.Folders.Providers.Abstractions;

public sealed record ProviderCapabilityOperationRow(
    string OperationId,
    ProviderOperationSupport Support,
    IReadOnlyDictionary<string, string> Limits,
    IReadOnlyDictionary<string, string> Constraints,
    bool Retryable,
    ProviderFailureCategory? FailureCategory)
{
    public static ProviderCapabilityOperationRow Supported(string operationId)
        => new(operationId, ProviderOperationSupport.Supported, Empty(), Empty(), true, null);

    public static ProviderCapabilityOperationRow Unsupported(string operationId)
        => new(operationId, ProviderOperationSupport.Unsupported, Empty(), Empty(), false, ProviderFailureCategory.UnsupportedProviderCapability);

    public static ProviderCapabilityOperationRow Partial(string operationId)
        => new(operationId, ProviderOperationSupport.Partial, Empty(), Empty(), true, null);

    public static ProviderCapabilityOperationRow Emulated(string operationId)
        => new(operationId, ProviderOperationSupport.Emulated, Empty(), Empty(), true, null);

    public static ProviderCapabilityOperationRow WithDetails(
        string operationId,
        ProviderOperationSupport support,
        IReadOnlyDictionary<string, string>? limits = null,
        IReadOnlyDictionary<string, string>? constraints = null,
        bool retryable = true,
        ProviderFailureCategory? failureCategory = null)
        => new(operationId, support, limits ?? Empty(), constraints ?? Empty(), retryable, failureCategory);

    private static IReadOnlyDictionary<string, string> Empty() => new Dictionary<string, string>(StringComparer.Ordinal);
}
