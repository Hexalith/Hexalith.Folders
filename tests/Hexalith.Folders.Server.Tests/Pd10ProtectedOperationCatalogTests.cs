using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Proves the candidate authorization identity table is total and structural.</summary>
public sealed class Pd10ProtectedOperationCatalogTests
{
    [Fact]
    public void CatalogDeclaresEveryProtectedIdentityExactlyOnce()
    {
        IReadOnlyList<Pd10ProtectedOperationDescriptor> descriptors = Pd10ProtectedOperationCatalog.Descriptors;

        descriptors.Count.ShouldBe(49);
        descriptors.Select(item => item.OperationId).Distinct(StringComparer.Ordinal).Count().ShouldBe(49);
        descriptors.Select(item => $"{item.Method} {item.CandidateRoute}").Distinct(StringComparer.Ordinal).Count().ShouldBe(49);
        descriptors.ShouldAllBe(item => !string.IsNullOrWhiteSpace(item.ActionToken));
        descriptors.Select(item => item.OperationFamily).Distinct().Count().ShouldBe(11);

        Descriptor("ListFolderAclEntries").ActionToken.ShouldBe("manage_folder_access");
        Descriptor("ListAuditTrail").ActionToken.ShouldBe("query_audit");
        Descriptor("GetReadinessDiagnostics").ActionToken.ShouldBe("view_operations_console");
        Descriptor("SearchFolderFiles").PolicyClass.ShouldBe(FolderOperationPolicyClass.StrictRead);
        Descriptor("GetTaskStatus").TaskBinding.ShouldBe(Pd10TaskBindingRule.RouteTaskBelongsToRouteFolder);
    }

    [Fact]
    public void ResolutionUsesExactMethodAndRouteStructureBeforeOpaqueValues()
    {
        Pd10ProtectedOperationCatalog.TryResolve(
            "GET",
            "/api/v2/folders/ops-console/audit-trail",
            out Pd10ProtectedOperationDescriptor? descriptor,
            out IReadOnlyDictionary<string, string> routeValues).ShouldBeTrue();

        descriptor.ShouldNotBeNull().OperationId.ShouldBe("ListAuditTrail");
        routeValues["folderId"].ShouldBe("ops-console");
        Pd10ProtectedOperationCatalog.TryResolve(
            "POST",
            "/api/v2/folders/folder-a/workspaces/workspace-a/context/search",
            out descriptor,
            out _).ShouldBeTrue();
        descriptor.ShouldNotBeNull().OperationId.ShouldBe("SearchFolderFiles");
        Pd10ProtectedOperationCatalog.TryResolve("GET", "/api/v2/folders/folder-a/not-declared", out _, out _)
            .ShouldBeFalse();
    }

    private static Pd10ProtectedOperationDescriptor Descriptor(string operationId)
        => Pd10ProtectedOperationCatalog.Descriptors.Single(item => item.OperationId == operationId);
}
