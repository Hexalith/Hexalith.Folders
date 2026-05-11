namespace Hexalith.Folders.Testing.Factories;

public sealed record TestAuthorizationContextOverrides(
    string? Subject = null,
    string? ManagedTenantId = null,
    IReadOnlyList<string>? Permissions = null);
