using System.Runtime.CompilerServices;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessValidationResult(
    ProviderReadinessResultCode Code,
    string Status,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string RemediationCategory,
    string CorrelationId,
    string? ProviderReference,
    string? ProviderBindingRef,
    string? CapabilityProfileRef,
    ProviderReadinessCapabilityEvidence? Evidence,
    ProviderReadinessFreshness Freshness,
    ProviderFailureCategory FailureCategory,
    string CategoryCode)
{
    private static readonly ConditionalWeakTable<ProviderReadinessValidationResult, OrganizationProviderBinding> ResolvedBindings = new();

    /// <summary>
    /// Associates the freshly verified provider binding observed during a successful readiness
    /// validation without altering record equality, hash code, JSON serialization, or string formatting.
    /// </summary>
    /// <param name="binding">The freshly verified binding observed for this result.</param>
    /// <returns>This <see cref="ProviderReadinessValidationResult"/> instance.</returns>
    internal ProviderReadinessValidationResult WithResolvedBinding(OrganizationProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ResolvedBindings.AddOrUpdate(this, binding);
        return this;
    }

    /// <summary>
    /// Gets the freshly verified provider binding previously attached to this result, if any.
    /// </summary>
    /// <returns>The attached <see cref="OrganizationProviderBinding"/>, or <c>null</c> when absent.</returns>
    internal OrganizationProviderBinding? TryGetResolvedBinding()
        => ResolvedBindings.TryGetValue(this, out OrganizationProviderBinding? binding) ? binding : null;
}
