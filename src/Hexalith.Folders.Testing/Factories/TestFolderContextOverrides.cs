namespace Hexalith.Folders.Testing.Factories;

public sealed record TestFolderContextOverrides(
    string? ManagedTenantId = null,
    string? OrganizationId = null,
    string? FolderId = null,
    string? TaskId = null,
    string? CorrelationId = null,
    string? IdempotencyKey = null);
