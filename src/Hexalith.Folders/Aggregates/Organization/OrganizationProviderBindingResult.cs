namespace Hexalith.Folders.Aggregates.Organization;

public sealed record OrganizationProviderBindingResult(
    OrganizationProviderBindingResultCode Code,
    string? ManagedTenantId,
    string? OrganizationId,
    string? ProviderBindingRef,
    string? ProviderKind,
    string? CredentialReferenceId,
    string? CorrelationId,
    string? TaskId,
    string? IdempotencyKey,
    IReadOnlyList<IOrganizationEvent> Events)
{
    public static OrganizationProviderBindingResult Accepted(
        ConfigureProviderBinding command,
        IReadOnlyList<IOrganizationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);

        return From(command, OrganizationProviderBindingResultCode.Accepted, events);
    }

    public static OrganizationProviderBindingResult Rejected(
        ConfigureProviderBinding command,
        OrganizationProviderBindingResultCode code)
    {
        ArgumentNullException.ThrowIfNull(command);

        return From(command, code, []);
    }

    public static OrganizationProviderBindingResult Rejected(
        OrganizationProviderBindingResultCode code,
        string? managedTenantId,
        string? organizationId,
        string? correlationId,
        string? taskId,
        string? idempotencyKey)
        => new(
            code,
            SafePassthrough(managedTenantId),
            SafePassthrough(organizationId),
            null,
            null,
            null,
            SafePassthrough(correlationId),
            SafePassthrough(taskId),
            SafePassthrough(idempotencyKey),
            []);

    private static OrganizationProviderBindingResult From(
        ConfigureProviderBinding command,
        OrganizationProviderBindingResultCode code,
        IReadOnlyList<IOrganizationEvent> events)
    {
        bool accepted = code is OrganizationProviderBindingResultCode.Accepted or OrganizationProviderBindingResultCode.AlreadyApplied;
        return new(
            code,
            SafePassthrough(command.ManagedTenantId),
            SafePassthrough(command.OrganizationId),
            accepted ? SafePassthrough(command.ProviderBindingRef) : null,
            accepted ? SafePassthrough(command.ProviderKind) : null,
            accepted ? SafePassthrough(command.CredentialReferenceId) : null,
            SafePassthrough(command.CorrelationId),
            SafePassthrough(command.TaskId),
            SafePassthrough(command.IdempotencyKey),
            events);
    }

    private static string? SafePassthrough(string? value)
        => OrganizationProviderBindingSecretDetector.ContainsForbiddenValue(value)
            ? null
            : OrganizationAclCommandValidator.IsValidIdentifier(value) ? value : null;
}
