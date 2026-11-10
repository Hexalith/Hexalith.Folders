using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hexalith.McpCli.Abstractions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

public sealed class FoldersIdentifierRoundTripTests
{
    [Theory]
    [InlineData("opaque_FolderAbC0123456789")]
    [InlineData("workspace_01HZY7Z6N7J4Q2X8Y9V0A1B2C3")]
    [InlineData("task_01HZY7Z6N7J4Q2X8Y9V0A1B2C3")]
    [InlineData("providerBinding_01HZY7Z6N7J4Q2X8Y9V0A1B2C3")]
    [InlineData("legacy.folder-01")]
    [InlineData("legacy_workspace_01")]
    [InlineData("legacy-task-01")]
    [InlineData("legacy.provider_binding-01")]
    [InlineData("opaque_FolderAbC012345678_")]
    [InlineData("opaque_FolderAbC012345678-")]
    [InlineData(".legacy-folder-01")]
    [InlineData("_legacy-workspace-01")]
    [InlineData("-legacy-provider-01")]
    [InlineData("legacy-folder-01.")]
    [InlineData("legacy-workspace-01_")]
    [InlineData("legacy-provider-01-")]
    public void AllIdentifierMembersPreserveExactBytesAndCase(string identifier)
    {
        JsonSerializerOptions options = McpCliContractTestSupport.CreateCanonicalPayloadOptions();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperationDeclaration row in McpCliContractTestSupport.Operations)
        {
            object original = Sample(row.Type, identifier);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(original, row.Type, options);
            object roundTrip = JsonSerializer.Deserialize(bytes, row.Type, options)!;
            JsonSerializer.SerializeToUtf8Bytes(roundTrip, row.Type, options).ShouldBe(bytes, row.Id);
            AssertIdentifiers(roundTrip, identifier, visited);
        }

        foreach (string member in new[] { "FolderId", "WorkspaceId", "TaskId", "ProviderBindingRef" })
        {
            visited.ShouldContain(member);
        }
    }

    [Fact]
    public void IndependentJsonFixturesPinDistinctIdentifiersToEveryClrMemberAndRoutingKey()
    {
        // These bindings are literal fixtures, independent of JsonPropertyName attributes and
        // constructor order. Each member has its own value so exchanging two JSON names fails.
        var fixtures = new Dictionary<string, (string Key, string Value)>(StringComparer.Ordinal)
        {
            ["FolderId"] = ("folderId", "opaque_FolderIdAbC0123456789"),
            ["WorkspaceId"] = ("workspaceId", "opaque_WorkspaceIdAbC0123456789"),
            ["TaskId"] = ("taskId", "opaque_TaskIdAbC0123456789"),
            ["ProviderBindingRef"] = ("providerBindingRef", "opaque_ProviderBindingRefAbC0123456789"),
            ["RepositoryBindingId"] = ("repositoryBindingId", "opaque_RepositoryBindingIdAbC0123456789"),
            ["OperationId"] = ("operationId", "opaque_OperationIdAbC0123456789"),
            ["AuditRecordId"] = ("auditRecordId", "opaque_AuditRecordIdAbC0123456789"),
            ["ReconciliationId"] = ("reconciliationId", "opaque_ReconciliationIdAbC0123456789"),
            ["TimelineEntryId"] = ("timelineEntryId", "opaque_TimelineEntryIdAbC0123456789"),
            ["IdempotencyKey"] = ("idempotencyKey", "01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            ["SubjectRef"] = ("subjectRef", "opaque_SubjectRefAbC0123456789"),
            ["AclEntryId"] = ("aclEntryId", "opaque_AclEntryIdAbC0123456789"),
            ["ExternalRepositoryRef"] = ("externalRepositoryRef", "opaque_ExternalRepositoryRefAbC0123456789"),
            ["RepositoryProfileRef"] = ("repositoryProfileRef", "opaque_RepositoryProfileRefAbC0123456789"),
            ["PolicyRef"] = ("policyRef", "opaque_PolicyRefAbC0123456789"),
            ["DefaultRef"] = ("defaultRef", "opaque_DefaultRefAbC0123456789"),
            ["LockId"] = ("lockId", "opaque_LockIdAbC0123456789"),
            ["LockOwnershipProof"] = ("lockOwnershipProof", "opaque_LockOwnershipProofAbC0123456789"),
            ["BranchRefPolicyRef"] = ("branchRefPolicyRef", "opaque_BranchRefPolicyRefAbC0123456789"),
            ["WorkspacePolicyRef"] = ("workspacePolicyRef", "opaque_WorkspacePolicyRefAbC0123456789"),
            ["ProviderFamilyRef"] = ("providerFamilyRef", "opaque_ProviderFamilyRefAbC0123456789"),
            ["CapabilityProfileRef"] = ("capabilityProfileRef", "opaque_CapabilityProfileRefAbC0123456789"),
            ["NonSecretCredentialReference"] = ("nonSecretCredentialReference", "opaque_NonSecretCredentialReferenceAbC0123456789"),
            ["ParentFolderId"] = ("parentFolderId", "opaque_ParentFolderIdAbC0123456789"),
            ["OwnerFileId"] = ("ownerFileId", "opaque_OwnerFileIdAbC0123456789"),
            ["StagingReference"] = ("stagingReference", "opaque_StagingReferenceAbC0123456789"),
        };
        JsonSerializerOptions options = McpCliContractTestSupport.CreateCanonicalPayloadOptions();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperationDeclaration row in McpCliContractTestSupport.Operations)
        {
            object template = Sample(row.Type, "opaque_Unassigned0123456789");
            JsonObject fixture = JsonSerializer.SerializeToNode(template, row.Type, options)!.AsObject();
            SetIndependentFixture(template, fixture, fixtures);
            object instance = JsonSerializer.Deserialize(fixture, row.Type, options)!;
            JsonObject serialized = JsonSerializer.SerializeToNode(instance, row.Type, options)!.AsObject();
            AssertIndependentFixture(instance, serialized, fixtures, covered);
            if (row.AggregateIdProperty is string route)
            {
                (string key, string value) = fixtures[route];
                row.Type.GetProperty(route)!.GetValue(instance).ShouldBe(value, row.Id + " aggregate CLR value");
                serialized[key]!.GetValue<string>().ShouldBe(value, row.Id + " aggregate JSON key");
            }
        }

        covered.Order(StringComparer.Ordinal).ShouldBe(fixtures.Keys.Order(StringComparer.Ordinal));
    }

    private static void SetIndependentFixture(object template, JsonObject payload,
        IReadOnlyDictionary<string, (string Key, string Value)> fixtures)
    {
        foreach (PropertyInfo member in template.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (member.IsDefined(typeof(HexalithIdentifierAttribute)))
            {
                (string key, string value) = fixtures[member.Name];
                payload[key] = value;
                continue;
            }

            string name = member.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
            object? valueObject = member.GetValue(template);
            if (valueObject is Array array && payload[name] is JsonArray jsonArray)
            {
                for (int index = 0; index < array.Length; index++)
                {
                    if (jsonArray[index] is JsonObject child)
                    {
                        SetIndependentFixture(array.GetValue(index)!, child, fixtures);
                    }
                }
            }
            else if (valueObject is not null && payload[name] is JsonObject child)
            {
                SetIndependentFixture(valueObject, child, fixtures);
            }
        }
    }

    private static void AssertIndependentFixture(object instance, JsonObject serialized,
        IReadOnlyDictionary<string, (string Key, string Value)> fixtures, HashSet<string> covered)
    {
        foreach (PropertyInfo member in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (member.IsDefined(typeof(HexalithIdentifierAttribute)))
            {
                (string key, string value) = fixtures[member.Name];
                member.GetValue(instance).ShouldBe(value, instance.GetType().Name + "." + member.Name);
                serialized[key]!.GetValue<string>().ShouldBe(value, instance.GetType().Name + "." + key);
                covered.Add(member.Name);
                continue;
            }

            string name = member.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
            object? valueObject = member.GetValue(instance);
            if (valueObject is Array array && serialized[name] is JsonArray jsonArray)
            {
                for (int index = 0; index < array.Length; index++)
                {
                    if (jsonArray[index] is JsonObject child)
                    {
                        AssertIndependentFixture(array.GetValue(index)!, child, fixtures, covered);
                    }
                }
            }
            else if (valueObject is not null && serialized[name] is JsonObject child)
            {
                AssertIndependentFixture(valueObject, child, fixtures, covered);
            }
        }
    }

    [Fact]
    public void AggregateGrammarGapsAreNamedAndRemainUnconverted()
    {
        var gaps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["opaque_FolderAbC012345678_"] = "aggregate-trailing-underscore",
            ["opaque_FolderAbC012345678-"] = "aggregate-trailing-hyphen",
            [".legacy-folder-01"] = "legacy-leading-dot",
            ["_legacy-workspace-01"] = "legacy-leading-underscore",
            ["-legacy-provider-01"] = "legacy-leading-hyphen",
            ["legacy-folder-01."] = "legacy-trailing-dot",
            ["legacy-workspace-01_"] = "legacy-trailing-underscore",
            ["legacy-provider-01-"] = "legacy-trailing-hyphen",
        };
        string decision = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "docs/contract/mcpcli-folders-operation-declarations.md"));
        const string aggregateGrammar = "^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,254}[A-Za-z0-9])?$";
        foreach ((string identifier, string gap) in gaps)
        {
            Regex.IsMatch(identifier, aggregateGrammar).ShouldBeFalse(gap);
            decision.ShouldContain(gap);
            decision.ShouldContain(identifier);
            bool v2 = !gap.StartsWith("legacy-", StringComparison.Ordinal);
            Regex.IsMatch(identifier, v2 ? "^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$" : "^[a-z0-9._-]+$").ShouldBeTrue(gap);
        }

        foreach (string identifier in new[] { "a", "opaque_FolderAbC0123456789", "legacy.folder-01", new string('A', 256) })
        {
            Regex.IsMatch(identifier, aggregateGrammar).ShouldBeTrue(identifier);
        }
    }

    private static object Sample(Type type, string identifier)
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;
        if (actual == typeof(string))
        {
            return identifier;
        }

        if (actual.IsEnum || actual.IsPrimitive)
        {
            return Activator.CreateInstance(actual)!;
        }

        if (actual.IsArray)
        {
            Array array = Array.CreateInstance(actual.GetElementType()!, 1);
            array.SetValue(Sample(actual.GetElementType()!, identifier), 0);
            return array;
        }

        ConstructorInfo constructor = actual.GetConstructors().Single();
        return constructor.Invoke(constructor.GetParameters().Select(parameter =>
            parameter.Name == "IdempotencyKey" ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : Sample(parameter.ParameterType, identifier)).ToArray());
    }

    private static void AssertIdentifiers(object instance, string identifier, HashSet<string> visited)
    {
        foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            object? value = property.GetValue(instance);
            if (property.IsDefined(typeof(HexalithIdentifierAttribute)) && property.Name != "IdempotencyKey")
            {
                value.ShouldBe(identifier, instance.GetType().Name + "." + property.Name);
                visited.Add(property.Name);
            }

            if (value is Array array)
            {
                foreach (object element in array)
                {
                    if (element.GetType().IsClass && element is not string)
                    {
                        AssertIdentifiers(element, identifier, visited);
                    }
                }
            }
            else if (value is not null && value is not string && value.GetType().IsClass)
            {
                AssertIdentifiers(value, identifier, visited);
            }
        }
    }
}
