namespace Hexalith.Folders.Authorization;

public sealed record LayeredFolderOperationPolicy(
    FolderOperationPolicyClass PolicyClass,
    bool RequiresDaprPolicyEvidence,
    bool AllowBoundedStaleTenantProjection,
    bool AllowBoundedStaleFolderPermission,
    string DaprTargetAppId,
    string ServiceInvocationClass)
{
    public string PolicyClassCode => PolicyClass switch
    {
        FolderOperationPolicyClass.Mutation => "mutation",
        FolderOperationPolicyClass.StrictRead => "strict_read",
        FolderOperationPolicyClass.BoundedDiagnosticRead => "bounded_diagnostic_read",
        _ => "mutation",
    };

    public static LayeredFolderOperationPolicy Mutation(
        bool requiresDaprPolicyEvidence = false,
        string daprTargetAppId = "folders",
        string serviceInvocationClass = "domain_service")
        => new(
            FolderOperationPolicyClass.Mutation,
            requiresDaprPolicyEvidence,
            AllowBoundedStaleTenantProjection: false,
            AllowBoundedStaleFolderPermission: false,
            daprTargetAppId,
            serviceInvocationClass);

    public static LayeredFolderOperationPolicy StrictRead(
        bool requiresDaprPolicyEvidence = false,
        string daprTargetAppId = "folders",
        string serviceInvocationClass = "query")
        => new(
            FolderOperationPolicyClass.StrictRead,
            requiresDaprPolicyEvidence,
            AllowBoundedStaleTenantProjection: false,
            AllowBoundedStaleFolderPermission: false,
            daprTargetAppId,
            serviceInvocationClass);

    public static LayeredFolderOperationPolicy BoundedDiagnosticRead(
        bool requiresDaprPolicyEvidence = false,
        string daprTargetAppId = "folders",
        string serviceInvocationClass = "diagnostic_read")
        => new(
            FolderOperationPolicyClass.BoundedDiagnosticRead,
            requiresDaprPolicyEvidence,
            AllowBoundedStaleTenantProjection: true,
            AllowBoundedStaleFolderPermission: true,
            daprTargetAppId,
            serviceInvocationClass);
}
