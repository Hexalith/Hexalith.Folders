using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authorization;

using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

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
        descriptors.ShouldAllBe(item => !string.IsNullOrWhiteSpace(item.HistoricalActionToken));
        Enum.GetValues<V2ProtectedOperationFamily>().Length.ShouldBe(11);
        descriptors.Select(item => item.OperationFamily).Distinct().Count().ShouldBe(10);

        Descriptor("ListFolderAclEntries").ActionToken.ShouldBe("manage_folder_access");
        Descriptor("ListAuditTrail").ActionToken.ShouldBe("query_audit");
        Descriptor("GetReadinessDiagnostics").ActionToken.ShouldBe("view_operations_console");
        Descriptor("SearchFolderFiles").PolicyClass.ShouldBe(FolderOperationPolicyClass.StrictRead);
        Descriptor("GetTaskStatus").TaskBinding.ShouldBe(Pd10TaskBindingRule.RouteTaskBelongsToRouteFolder);
        Descriptor("GetProviderOutcome").OperationFamily.ShouldBe(V2ProtectedOperationFamily.StatusPermissionAndLockInspection);
        Descriptor("GetReconciliationStatus").OperationFamily.ShouldBe(V2ProtectedOperationFamily.StatusPermissionAndLockInspection);
        descriptors.Count(item => item.OperationFamily == V2ProtectedOperationFamily.IncidentEvidence).ShouldBe(0);
        Descriptor("GetFolderFileMetadata").ActionToken.ShouldBe("mutate_files");
        Descriptor("CreateRepositoryBackedFolder").ActionToken.ShouldBe("manage_folder_access");
        Descriptor("BindRepository").ActionToken.ShouldBe("manage_folder_access");
        Descriptor("ValidateProviderReadiness").ActionToken.ShouldBe("create_folder");
    }

    [Fact]
    public void EveryCandidateActionIsBoundToTheExactHistoricalHandlerAction()
    {
        Dictionary<string, string> differingActions = new(StringComparer.Ordinal)
        {
            ["ListFolderAclEntries"] = "read_metadata",
            ["ValidateProviderReadiness"] = "provider_readiness_read",
            ["CreateRepositoryBackedFolder"] = "create_repository_backed_folder",
            ["BindRepository"] = "bind_repository",
            ["GetWorkspaceRetryEligibility"] = "read_workspace_lock",
            ["ListFolderFiles"] = "read_metadata",
            ["GetFolderFileMetadata"] = "read_metadata",
            ["SearchFolderFiles"] = "read_metadata",
            ["GlobFolderFiles"] = "read_metadata",
            ["ReadFileRange"] = "read_file_content",
            ["GetTaskStatus"] = "read_task_status",
            ["GetCommitEvidence"] = "read_workspace_status",
            ["GetProviderOutcome"] = "read_workspace_status",
            ["GetReconciliationStatus"] = "read_workspace_status",
            ["ListAuditTrail"] = "read_metadata",
            ["GetAuditRecord"] = "read_metadata",
            ["ListOperationTimeline"] = "read_metadata",
            ["GetOperationTimelineEntry"] = "read_metadata",
            ["GetReadinessDiagnostics"] = "tenant-context-and-ops-console-diagnostic-read",
            ["GetLockDiagnostics"] = "read_metadata",
            ["GetDirtyStateDiagnostics"] = "read_metadata",
            ["GetFailedOperationDiagnostics"] = "read_metadata",
            ["GetProviderStatusDiagnostics"] = "read_metadata",
            ["GetSyncStatusDiagnostics"] = "read_metadata",
            ["GetProjectionFreshness"] = "tenant-context-and-ops-console-diagnostic-read",
        };

        foreach (Pd10ProtectedOperationDescriptor descriptor in Pd10ProtectedOperationCatalog.Descriptors)
        {
            string expected = differingActions.GetValueOrDefault(descriptor.OperationId, descriptor.ActionToken);
            descriptor.HistoricalActionToken.ShouldBe(expected, descriptor.OperationId);
        }
    }

    [Fact]
    public void EveryCatalogIdentityMatchesTheCanonicalAuthorizationMatrix()
    {
        string matrixPath = Path.Combine(LocateRepositoryRoot(), "docs", "contract", "authorization-matrix.md");
        string operationMapping = File.ReadAllText(matrixPath)
            .Split("## Operation Mapping", StringSplitOptions.None)[1]
            .Split("### Scope Dimension Rules", StringSplitOptions.None)[0];
        Regex row = new(
            @"^\| `(?<operation>[^`]+)` \| (?<method>[A-Z]+) \| `(?<path>[^`]+)` \| `(?<family>[^`]+)` \|",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        HashSet<string> expected = row.Matches(operationMapping)
            .Select(match => string.Join(
                '\u001f',
                match.Groups["operation"].Value,
                match.Groups["method"].Value,
                match.Groups["path"].Value,
                match.Groups["family"].Value))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = Pd10ProtectedOperationCatalog.Descriptors
            .Select(descriptor => string.Join(
                '\u001f',
                descriptor.OperationId,
                descriptor.Method,
                descriptor.CandidateRoute,
                ToKebabCase(descriptor.OperationFamily.ToString())))
            .ToHashSet(StringComparer.Ordinal);

        expected.Count.ShouldBe(49, "the matrix must keep the complete operation denominator");
        actual.ShouldBe(expected, "every executable operation identity and family must match the governing matrix row exactly");
    }

    [Fact]
    public void EveryCatalogEntryHasTheReviewedActionPolicyScopeAndTaskBinding()
    {
        string[] expected =
        [
            "CreateFolder|create_folder|Mutation|None|None",
            "GetFolderLifecycleStatus|read_metadata|StrictRead|RouteFolder|None",
            "ArchiveFolder|archive_folder|Mutation|RouteFolder|None",
            "ListFolderAclEntries|manage_folder_access|StrictRead|RouteFolder|None",
            "UpdateFolderAclEntry|manage_folder_access|Mutation|RouteFolder|None",
            "GetEffectivePermissions|read_metadata|StrictRead|RouteFolder|None",
            "ConfigureProviderBinding|configure_provider_binding|Mutation|None|None",
            "GetProviderBinding|tenant-context-and-provider-binding-read|StrictRead|None|None",
            "ValidateProviderReadiness|create_folder|StrictRead|None|None",
            "GetProviderSupportEvidence|tenant-context-and-provider-support-read|StrictRead|None|None",
            "CreateRepositoryBackedFolder|manage_folder_access|Mutation|RequestFolder|None",
            "BindRepository|manage_folder_access|Mutation|RouteFolder|None",
            "GetRepositoryBinding|read_metadata|StrictRead|RouteFolder|None",
            "ConfigureBranchRefPolicy|configure_branch_ref_policy|Mutation|RouteFolder|None",
            "GetBranchRefPolicy|read_branch_ref_policy|StrictRead|RouteFolder|None",
            "PrepareWorkspace|prepare_workspace|Mutation|RouteFolder|None",
            "LockWorkspace|lock_workspace|Mutation|RouteFolder|None",
            "GetWorkspaceLock|read_workspace_lock|StrictRead|RouteFolder|None",
            "ReleaseWorkspaceLock|lock_workspace|Mutation|RouteFolder|None",
            "GetWorkspaceRetryEligibility|read_workspace_status|StrictRead|RouteFolder|None",
            "GetWorkspaceTransitionEvidence|read_metadata|StrictRead|RouteFolder|None",
            "AddFile|mutate_files|Mutation|RouteFolder|None",
            "ChangeFile|mutate_files|Mutation|RouteFolder|None",
            "RemoveFile|mutate_files|Mutation|RouteFolder|None",
            "ListFolderFiles|mutate_files|StrictRead|RouteFolder|None",
            "GetFolderFileMetadata|mutate_files|StrictRead|RouteFolder|None",
            "SearchFolderFiles|mutate_files|StrictRead|RouteFolder|None",
            "SearchFolderIndexedFiles|read_context_search|StrictRead|RouteFolder|None",
            "GetFolderIndexingStatus|read_context_search|StrictRead|RouteFolder|None",
            "GlobFolderFiles|mutate_files|StrictRead|RouteFolder|None",
            "ReadFileRange|mutate_files|StrictRead|RouteFolder|None",
            "CommitWorkspace|commit|Mutation|RouteFolder|None",
            "GetWorkspaceStatus|read_workspace_status|StrictRead|RouteFolder|None",
            "GetWorkspaceCleanupStatus|read_workspace_cleanup_status|StrictRead|RouteFolder|None",
            "GetTaskStatus|query_status|StrictRead|RouteFolder|RouteTaskBelongsToRouteFolder",
            "GetCommitEvidence|query_status|StrictRead|RouteFolder|None",
            "GetProviderOutcome|query_status|StrictRead|RouteFolder|None",
            "GetReconciliationStatus|query_status|StrictRead|RouteFolder|None",
            "ListAuditTrail|query_audit|StrictRead|RouteFolder|None",
            "GetAuditRecord|query_audit|StrictRead|RouteFolder|None",
            "ListOperationTimeline|query_audit|StrictRead|RouteFolder|None",
            "GetOperationTimelineEntry|query_audit|StrictRead|RouteFolder|None",
            "GetReadinessDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetLockDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetDirtyStateDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetFailedOperationDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetProviderStatusDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetSyncStatusDiagnostics|view_operations_console|StrictRead|RouteFolder|None",
            "GetProjectionFreshness|view_operations_console|StrictRead|RouteFolder|None",
        ];
        string[] actual = Pd10ProtectedOperationCatalog.Descriptors
            .Select(descriptor => string.Join(
                '|',
                descriptor.OperationId,
                descriptor.ActionToken,
                descriptor.PolicyClass,
                descriptor.FolderScope,
                descriptor.TaskBinding))
            .ToArray();

        actual.ShouldBe(expected);
    }

    [Fact]
    public void ResolutionUsesExactMethodAndRouteStructureBeforeOpaqueValues()
    {
        Pd10ProtectedOperationCatalog.TryResolve(
            "GET",
            "/api/v2/folders/Folder_000000001/audit-trail",
            out Pd10ProtectedOperationDescriptor? descriptor,
            out IReadOnlyDictionary<string, string> routeValues).ShouldBeTrue();

        descriptor.ShouldNotBeNull().OperationId.ShouldBe("ListAuditTrail");
        routeValues["folderId"].ShouldBe("Folder_000000001");
        Pd10ProtectedOperationCatalog.TryResolve(
            "POST",
            "/api/v2/folders/folder_000000001/workspaces/workspace_000001/context/search",
            out descriptor,
            out _).ShouldBeTrue();
        descriptor.ShouldNotBeNull().OperationId.ShouldBe("SearchFolderFiles");
        Pd10ProtectedOperationCatalog.TryResolve("GET", "/api/v2/folders/folder_000000001/not-declared", out _, out _)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("folder.with.dot_001")]
    [InlineData("folder:with:colon1")]
    [InlineData("folder%2Fsegment_001")]
    public void ResolutionRejectsRouteValuesOutsideTheExactV2OpaqueIdentifierGrammar(string folderId)
        => Pd10ProtectedOperationCatalog.TryResolve(
            "GET",
            $"/api/v2/folders/{folderId}/lifecycle-status",
            out _,
            out _).ShouldBeFalse();

    [Fact]
    public void EveryCatalogHistoricalTargetMatchesTheV1Spine()
    {
        string contractPath = Path.Combine(
            LocateRepositoryRoot(),
            "src",
            "Hexalith.Folders.Contracts",
            "openapi",
            "hexalith.folders.v1.yaml");
        using StreamReader reader = File.OpenText(contractPath);
        YamlStream stream = new();
        stream.Load(reader);
        YamlMappingNode root = (YamlMappingNode)stream.Documents[0].RootNode;
        YamlMappingNode paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        Dictionary<string, string> expected = new(StringComparer.Ordinal);
        HashSet<string> methods = new(StringComparer.Ordinal) { "get", "post", "put", "patch", "delete" };
        foreach ((YamlNode pathNode, YamlNode pathValue) in paths.Children)
        {
            string path = ((YamlScalarNode)pathNode).Value.ShouldNotBeNull();
            foreach ((YamlNode methodNode, YamlNode operationValue) in ((YamlMappingNode)pathValue).Children)
            {
                string method = ((YamlScalarNode)methodNode).Value ?? string.Empty;
                if (!methods.Contains(method))
                {
                    continue;
                }

                YamlMappingNode operation = (YamlMappingNode)operationValue;
                string operationId = ((YamlScalarNode)operation.Children[new YamlScalarNode("operationId")]).Value.ShouldNotBeNull();
                expected[operationId] = $"{method.ToUpperInvariant()} {path}";
            }
        }

        Dictionary<string, string> actual = Pd10ProtectedOperationCatalog.Descriptors.ToDictionary(
            descriptor => descriptor.OperationId,
            descriptor => $"{descriptor.Method} {descriptor.HistoricalRoute}",
            StringComparer.Ordinal);

        actual.Count.ShouldBe(49);
        actual.ShouldBe(expected, "all candidate dispatch targets must remain exact identities in the historical v1 spine");
    }

    [Fact]
    public void DelegationPolicyIsExactForEveryProtectedFamily()
    {
        V2ProtectedOperationFamily[] delegable =
        [
            V2ProtectedOperationFamily.FolderAdministration,
            V2ProtectedOperationFamily.TaskMutation,
            V2ProtectedOperationFamily.ContextRead,
            V2ProtectedOperationFamily.StatusPermissionAndLockInspection,
            V2ProtectedOperationFamily.IndexSearch,
        ];

        foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
        {
            Pd10V2CandidateCompatibilitySeam.IsDelegable(family)
                .ShouldBe(delegable.Contains(family), family.ToString());
        }
    }

    [Fact]
    public void RequestCompatibilityTranslationTouchesOnlySchemaOwnedDiscriminatorLocations()
    {
        JsonObject arbitraryNested = new()
        {
            ["requestSchemaVersion"] = "v2",
        };
        JsonObject archive = new()
        {
            ["requestSchemaVersion"] = "v2",
            ["clientMetadata"] = arbitraryNested,
        };

        Pd10V2CandidateCompatibilitySeam.RewriteSchemaDiscriminators(archive, Descriptor("ArchiveFolder"));

        archive["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v1");
        arbitraryNested["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v2");

        JsonObject repository = new()
        {
            ["requestSchemaVersion"] = "v2",
            ["branchRefPolicy"] = new JsonObject { ["requestSchemaVersion"] = "v2" },
            ["clientMetadata"] = new JsonObject { ["requestSchemaVersion"] = "v2" },
        };
        Pd10V2CandidateCompatibilitySeam.RewriteSchemaDiscriminators(
            repository,
            Descriptor("CreateRepositoryBackedFolder"));

        repository["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v1");
        repository["branchRefPolicy"]!["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v1");
        repository["clientMetadata"]!["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v2");
    }

    [Fact]
    public void SuccessCompatibilityTranslationTouchesOnlyGetBranchRefPolicyRootDiscriminator()
    {
        JsonObject branchPolicy = new()
        {
            ["requestSchemaVersion"] = "v1",
            ["clientMetadata"] = new JsonObject { ["requestSchemaVersion"] = "v1" },
        };

        Pd10V2CandidateCompatibilitySeam.RewriteSuccessSchemaDiscriminator(branchPolicy, "GetBranchRefPolicy")
            .ShouldBeTrue();
        branchPolicy["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v2");
        branchPolicy["clientMetadata"]!["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v1");

        JsonObject other = new() { ["requestSchemaVersion"] = "v1" };
        Pd10V2CandidateCompatibilitySeam.RewriteSuccessSchemaDiscriminator(other, "GetRepositoryBinding")
            .ShouldBeFalse();
        other["requestSchemaVersion"]!.GetValue<string>().ShouldBe("v1");
    }

    private static Pd10ProtectedOperationDescriptor Descriptor(string operationId)
        => Pd10ProtectedOperationCatalog.Descriptors.Single(item => item.OperationId == operationId);

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ToKebabCase(string value)
    {
        StringBuilder builder = new(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
