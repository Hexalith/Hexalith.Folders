using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Organization;

public static partial class OrganizationAclCommandValidator
{
    internal const int MaxIdentifierLength = 256;

    public static OrganizationAclCommandValidationResult Validate(IOrganizationAclCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (OrganizationStreamName.IsReservedSystemTenant(command.ManagedTenantId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.ReservedTenant);
        }

        if (!OrganizationStreamName.IsValidManagedTenant(command.ManagedTenantId) || !IsSafeMetadataIdentifier(command.ManagedTenantId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidTenant);
        }

        if (!OrganizationStreamName.IsValidSegment(command.OrganizationId) || !IsSafeMetadataIdentifier(command.OrganizationId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidOrganization);
        }

        if (!IsSafeMetadataIdentifier(command.CorrelationId)
            || !IsSafeMetadataIdentifier(command.TaskId)
            || !IsSafeMetadataIdentifier(command.IdempotencyKey))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.MalformedEvidence);
        }

        Dictionary<string, OrganizationAclOperation> unique = new(StringComparer.Ordinal);
        Dictionary<string, OrganizationAclOperationIntent> tupleIntents = new(StringComparer.Ordinal);

        foreach (OrganizationAclOperation operation in command.Operations)
        {
            if (!Enum.IsDefined(operation.PrincipalKind))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidPrincipal, operation);
            }

            if (!Enum.IsDefined(operation.Intent))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.MalformedEvidence, operation);
            }

            if (!OrganizationAclAction.IsSupported(operation.Action))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.UnsupportedAction, operation);
            }

            if (!IsValidPrincipalId(operation.PrincipalId))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidPrincipal, operation);
            }

            OrganizationAclEntryKey key = new(
                command.ManagedTenantId,
                command.OrganizationId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);
            string tupleKey = key.CanonicalValue;
            if (tupleIntents.TryGetValue(tupleKey, out OrganizationAclOperationIntent priorIntent)
                && priorIntent != operation.Intent)
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.ReplayConflict, operation);
            }

            tupleIntents[tupleKey] = operation.Intent;
            string operationKey = $"{operation.Intent}|{tupleKey}";
            if (!unique.TryAdd(operationKey, operation) && unique[operationKey] != operation)
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.ReplayConflict, operation);
            }
        }

        OrganizationAclOperation[] ordered = unique
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();

        return OrganizationAclCommandValidationResult.Accepted(ordered, Fingerprint(command, ordered));
    }

    private static string Fingerprint(IOrganizationAclCommand command, IReadOnlyList<OrganizationAclOperation> operations)
    {
        IEnumerable<string> operationTokens = operations.Select(operation =>
        {
            OrganizationAclEntryKey key = new(
                command.ManagedTenantId,
                command.OrganizationId,
                operation.PrincipalKind,
                operation.PrincipalId,
                operation.Action);
            return $"{operation.Intent}|{key.CanonicalValue}";
        });

        return string.Join("|", [command.CommandType, .. operationTokens]);
    }

    internal static bool IsValidPrincipalId(string? value) => IsSafeMetadataIdentifier(value);

    /// <summary>Checks identifier evidence before it can appear in an ACL event or rejection.</summary>
    internal static bool IsSafeMetadataIdentifier(string? value)
        => IsValidIdentifier(value)
            && !OrganizationProviderBindingSecretDetector.ContainsForbiddenValue(value);

    internal static bool IsValidIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxIdentifierLength
            && (CanonicalIdentifierPattern().IsMatch(value)
                || StreamNameSegment.IsCandidateOpaqueIdentifier(value));

    [GeneratedRegex("\\A[a-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentifierPattern();
}
