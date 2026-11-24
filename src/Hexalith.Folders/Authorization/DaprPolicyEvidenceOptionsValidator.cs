using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Authorization;

/// <summary>Rejects incomplete enabled Dapr policy configuration at host startup.</summary>
internal sealed class DaprPolicyEvidenceOptionsValidator : IValidateOptions<DaprPolicyEvidenceOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, DaprPolicyEvidenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !options.Enabled
            || (HasEntries(options.AllowedTargetAppIds) && HasEntries(options.AllowedServiceInvocationClasses))
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("Enabled Folders Dapr policy requires nonempty target and invocation-class allow-lists.");
    }

    private static bool HasEntries(string[]? values)
        => values is { Length: > 0 } && values.All(static value => !string.IsNullOrWhiteSpace(value));
}
