namespace Hexalith.Folders.Server.Authorization;

/// <summary>Closed eleven-family PD10 authorization vocabulary.</summary>
internal enum V2ProtectedOperationFamily
{
    ProviderConfiguration,
    ReadinessAndProviderEvidence,
    FolderCreation,
    IncidentEvidence,
    FolderAdministration,
    TaskMutation,
    ContextRead,
    StatusPermissionAndLockInspection,
    AuditRead,
    ConsoleView,
    IndexSearch,
}
