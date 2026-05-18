namespace Hexalith.Folders.Aggregates.Organization;

public static class OrganizationAclCommandValidator
{
    public static OrganizationAclCommandValidationResult Validate(IOrganizationAclCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (OrganizationStreamName.IsReservedSystemTenant(command.ManagedTenantId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.ReservedTenant);
        }

        if (!OrganizationStreamName.IsValidSegment(command.ManagedTenantId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidTenant);
        }

        if (!OrganizationStreamName.IsValidSegment(command.OrganizationId))
        {
            return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidOrganization);
        }

        Dictionary<string, OrganizationAclOperation> unique = new(StringComparer.Ordinal);
        Dictionary<string, OrganizationAclOperationIntent> tupleIntents = new(StringComparer.Ordinal);

        foreach (OrganizationAclOperation operation in command.Operations)
        {
            if (!OrganizationAclAction.IsSupported(operation.Action))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.UnsupportedAction);
            }

            if (!IsValidPrincipalId(operation.PrincipalId))
            {
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.InvalidPrincipal);
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
                return OrganizationAclCommandValidationResult.Rejected(OrganizationAclResultCode.ReplayConflict);
            }

            tupleIntents[tupleKey] = operation.Intent;
            unique[$"{operation.Intent}|{tupleKey}"] = operation;
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

    private static bool IsValidPrincipalId(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(static character => character == ':' || char.IsControl(character));
}
