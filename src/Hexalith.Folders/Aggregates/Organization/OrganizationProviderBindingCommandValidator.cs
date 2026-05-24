using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Organization;

public static partial class OrganizationProviderBindingCommandValidator
{
    private static readonly HashSet<string> SupportedProviderKinds = new(StringComparer.Ordinal)
    {
        "github",
        "forgejo",
        "gitlab",
        "bitbucket",
        "azure-devops",
    };

    public static OrganizationProviderBindingCommandValidationResult Validate(ConfigureProviderBinding command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (OrganizationStreamName.IsReservedSystemTenant(command.ManagedTenantId))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.ReservedTenant);
        }

        if (!OrganizationStreamName.IsValidSegment(command.ManagedTenantId))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.InvalidTenant);
        }

        if (!OrganizationStreamName.IsValidSegment(command.OrganizationId))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.InvalidOrganization);
        }

        if (!Enum.IsDefined(command.ActorPrincipalKind)
            || !OrganizationAclCommandValidator.IsValidPrincipalId(command.ActorPrincipalId)
            || !OrganizationAclCommandValidator.IsValidIdentifier(command.CorrelationId)
            || !OrganizationAclCommandValidator.IsValidIdentifier(command.TaskId)
            || !OrganizationAclCommandValidator.IsValidIdentifier(command.IdempotencyKey))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.MalformedEvidence);
        }

        if (!IsSafeIdentifier(command.ProviderBindingRef))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.InvalidProviderBindingReference);
        }

        if (!IsSafeIdentifier(command.CredentialReferenceId))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.InvalidCredentialReference);
        }

        if (ContainsForbidden(command.ProviderBindingRef)
            || ContainsForbidden(command.ProviderKind)
            || ContainsForbidden(command.CredentialReferenceId)
            || ContainsForbidden(command.CorrelationId)
            || ContainsForbidden(command.TaskId)
            || ContainsForbidden(command.IdempotencyKey)
            || ContainsForbidden(command.NamingPolicy)
            || ContainsForbidden(command.BranchPolicy))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.ForbiddenCredentialMaterial);
        }

        if (!SupportedProviderKinds.Contains(command.ProviderKind))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.UnsupportedProviderKind);
        }

        if (!IsValidPolicy(command.NamingPolicy) || !IsValidPolicy(command.BranchPolicy))
        {
            return OrganizationProviderBindingCommandValidationResult.Rejected(OrganizationProviderBindingResultCode.InvalidPolicy);
        }

        return OrganizationProviderBindingCommandValidationResult.Accepted(Fingerprint(command));
    }

    internal static string Fingerprint(ConfigureProviderBinding command)
        => string.Join(
            "|",
            [
                command.CommandType,
                command.ManagedTenantId,
                command.OrganizationId,
                command.ProviderBindingRef,
                command.ProviderKind,
                command.CredentialReferenceId,
                CanonicalPolicy(command.NamingPolicy),
                CanonicalPolicy(command.BranchPolicy),
            ]);

    internal static bool IsSafeIdentifier(string? value)
        => OrganizationAclCommandValidator.IsValidIdentifier(value)
            && !ContainsForbidden(value);

    private static bool IsValidPolicy(OrganizationProviderBindingPolicy? policy)
    {
        if (policy is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(policy.PolicyRef) && !IsSafeIdentifier(policy.PolicyRef))
        {
            return false;
        }

        foreach (KeyValuePair<string, string> entry in policy.Metadata)
        {
            if (!IsSafePolicyKey(entry.Key) || !IsSafePolicyValue(entry.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static string CanonicalPolicy(OrganizationProviderBindingPolicy policy)
    {
        string policyRef = string.IsNullOrWhiteSpace(policy.PolicyRef) ? "-" : policy.PolicyRef;
        IEnumerable<string> entries = policy.Metadata
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => $"{pair.Key.Length}:{pair.Key}={pair.Value.Length}:{pair.Value}");

        return $"{policyRef.Length}:{policyRef}[{string.Join(",", entries)}]";
    }

    private static bool IsSafePolicyKey(string? value)
        => IsSafeIdentifier(value)
            && !OrganizationProviderBindingSecretDetector.IsSensitiveKey(value);

    private static bool IsSafePolicyValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= OrganizationAclCommandValidator.MaxIdentifierLength
            && !ContainsForbidden(value)
            && SafePolicyValuePattern().IsMatch(value);

    private static bool ContainsForbidden(OrganizationProviderBindingPolicy? policy)
        => policy is not null
            && (ContainsForbidden(policy.PolicyRef)
                || policy.Metadata.Any(pair =>
                    OrganizationProviderBindingSecretDetector.IsSensitiveKey(pair.Key)
                    || ContainsForbidden(pair.Key)
                    || ContainsForbidden(pair.Value)));

    private static bool ContainsForbidden(string? value)
        => OrganizationProviderBindingSecretDetector.ContainsForbiddenValue(value);

    [GeneratedRegex("^[a-z0-9._/*:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePolicyValuePattern();
}
