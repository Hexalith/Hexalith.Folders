using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Hexalith.Folders.Contracts.Commands.Models;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

public sealed class ContractSpineDriftGateTests
{
    // This is the sole exemption table. Keys name the affected operation/field or mapping.
    private static readonly IReadOnlyDictionary<string, string> Exemptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["*/requestSchemaVersion"] = "The request-schema discriminator is supplied by Story 5.13 admission at the value the domain accepts (today v1), including nested requests.",
        ["C06/folderId"] = "CreateFolder requires caller-supplied routing identity equal to envelope.AggregateId.",
        ["C08/effect"] = "Grant is derived from operation identity, not a payload member (V08); Story 5.13 supplies it during translation.",
        ["C13/effect"] = "Revoke is derived from operation identity, not a payload member (V08); Story 5.13 supplies it during translation.",
        ["C10/$request"] = "MutateFiles maps to the canonical MutateFilesRequest component, using AddFile path scope.",
        ["C05/$aggregate"] = "ProviderBindingRef routes the command; reserved actor derivation and tenant separation belong to Story 5.13.",
        ["C01/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C02/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C04/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C05/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C06/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C07/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C08/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
        ["C13/X-Hexalith-Task-Id"] = "Carried by McpCli extensions[\"taskId\"]; Story 5.13/G3.",
    };

    [Fact]
    public void EveryDeclaredFieldRequiredFlagBoundAndEnumMatchesTheV2Spine()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        var differences = new List<string>();
        foreach (OperationDeclaration row in McpCliContractTestSupport.Operations)
        {
            Dictionary<string, JsonObject> fields = ExpectedFields(spine, row);
            CompareObject(spine, row.Type, fields, row.Id + "/" + row.Type.Name, differences, row.Id);
        }

        differences.ShouldBeEmpty(string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void DriftGateReportsAddedDroppedRenamedRequiredAndBoundChanges()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C09");
        foreach (string mutation in new[] { "added", "dropped", "renamed", "required", "bound", "type", "enum" })
        {
            Dictionary<string, JsonObject> fields = ExpectedFields(spine, row);
            switch (mutation)
            {
                case "added": fields.Add("newField", new JsonObject { ["type"] = "string" }); break;
                case "dropped": fields.Remove("requestedLeaseSeconds"); break;
                case "renamed": fields.Add("renamedLease", fields["requestedLeaseSeconds"]); fields.Remove("requestedLeaseSeconds"); break;
                case "required": fields["requestedLeaseSeconds"]["x-required"] = false; break;
                case "bound": fields["requestedLeaseSeconds"]["maximum"] = 86401L; break;
                case "type": fields["requestedLeaseSeconds"]["type"] = "array"; break;
                case "enum": fields["lockIntent"]["enum"]!.AsArray().Add("unexpected_intent"); break;
            }

            var differences = new List<string>();
            CompareObject(spine, row.Type, fields, "C09/LockWorkspace", differences, row.Id);
            differences.ShouldNotBeEmpty(mutation);
            differences.ShouldAllBe(message => message.Contains("LockWorkspace", StringComparison.Ordinal), mutation);
        }
    }

    [Fact]
    public void AclOperationsDeriveEffectFromIdentityAndRejectCallerSuppliedEffect()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        foreach ((string id, string effect) in new[] { ("C08", "grant"), ("C13", "revoke") })
        {
            Exemptions.ShouldContainKey(id + "/effect");
            OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == id);
            var payload = new JsonObject
            {
                ["folderId"] = "opaque_FolderAbC0123456789",
                ["aclEntryId"] = "opaque_AclEntry0123456789",
                ["subjectRef"] = "opaque_Subject0123456789",
                ["permissionLevel"] = "read",
                ["idempotencyKey"] = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                ["effect"] = effect == "grant" ? "revoke" : "grant",
            };
            JsonSerializerOptions options = McpCliContractTestSupport.CreateCanonicalPayloadOptions();
            options.GetTypeInfo(row.Type).Properties.ShouldNotContain(property => property.Name == "effect");
            row.Type.GetProperty("Effect").ShouldBeNull();
            foreach (string supplied in new[] { effect, effect == "grant" ? "revoke" : "grant" })
            {
                payload["effect"] = supplied;
                Should.Throw<JsonException>(() => JsonSerializer.Deserialize(payload, row.Type, options)).Message.ShouldContain("effect");
            }

            payload.Remove("effect");
            object omitted = JsonSerializer.Deserialize(payload, row.Type, options)!;
            JsonSerializer.SerializeToNode(omitted, row.Type, options)!.AsObject().ContainsKey("effect").ShouldBeFalse();
            JsonObject operation = McpCliContractTestSupport.FindOperation(spine, "UpdateFolderAclEntry");
            JsonObject body = RequestSchema(spine, operation);
            body["properties"]!["effect"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe(["grant", "revoke"]);
        }
    }

    [Theory]
    [InlineData("C09", "Idempotency-Key", "required")]
    [InlineData("C09", "Idempotency-Key", "bound")]
    [InlineData("C09", "X-Hexalith-Task-Id", "required")]
    [InlineData("C09", "X-Hexalith-Task-Id", "bound")]
    [InlineData("C01", "X-Hexalith-Task-Id", "required")]
    [InlineData("C01", "X-Hexalith-Task-Id", "bound")]
    [InlineData("C03", "taskId", "body-bound")]
    [InlineData("C03", "taskId", "body-required")]
    [InlineData("C03", "taskId", "body-dropped")]
    public void CommandHeaderAndDuplicateBodyDriftCannotBeHidden(string id, string name, string mutation)
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == id);
        if (mutation is "body-bound" or "body-required" or "body-dropped")
        {
            JsonObject body = spine["components"]!["schemas"]!["CommitWorkspaceRequest"]!.AsObject();
            if (mutation == "body-dropped")
            {
                body["properties"]!.AsObject().Remove("taskId");
            }
            else if (mutation == "body-required")
            {
                JsonArray required = body["required"]!.AsArray();
                required.Remove(required.Single(value => value!.GetValue<string>() == "taskId"));
            }
            else
            {
                JsonObject task = McpCliContractTestSupport.Resolve(spine, body["properties"]!["taskId"]!.AsObject());
                task["maxLength"] = 127;
                body["properties"]!["taskId"] = task;
            }
        }
        else
        {
            JsonArray parameters = McpCliContractTestSupport.FindOperation(spine, row.SpineOperation)["parameters"]!.AsArray();
            int index = Enumerable.Range(0, parameters.Count).Single(candidate =>
                McpCliContractTestSupport.Resolve(spine, parameters[candidate]!.AsObject())["name"]!.GetValue<string>() == name);
            JsonObject parameter = McpCliContractTestSupport.Resolve(spine, parameters[index]!.AsObject());
            parameters[index] = parameter;
            if (mutation == "required")
            {
                parameter["required"] = !(parameter["required"]?.GetValue<bool>() ?? false);
            }
            else
            {
                JsonObject schema = McpCliContractTestSupport.Resolve(spine, parameter["schema"]!.AsObject());
                schema["maxLength"] = 127;
                parameter["schema"] = schema;
            }
        }

        // Required task flags and duplicate body/header mismatches fail during extraction;
        // represented header members fail during the normal reflected-shape comparison.
        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() =>
        {
            var differences = new List<string>();
            CompareObject(spine, row.Type, ExpectedFields(spine, row), row.Id + "/" + row.Type.Name, differences, row.Id);
            differences.ShouldBeEmpty(string.Join(Environment.NewLine, differences));
        });
        string field = name switch
        {
            "Idempotency-Key" => "idempotencyKey",
            "taskId" => "taskId",
            _ => mutation == "required" ? "task-header" : row.TaskScoped ? "taskId" : name,
        };
        failure.Message.ShouldContain(field, Case.Sensitive, id + " " + name + " " + mutation);
    }

    [Fact]
    public void ConditionalFileMutationRulesStayPinnedUntilAdmissionImplementsThem()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        JsonNode conditions = spine["components"]!["schemas"]!["FileMutationRequest"]!["allOf"]!;
        // DataAnnotations describe each member's static shape. These conditional combinations
        // remain an admission responsibility; pin their complete tree so changes still redden this gate.
        JsonNode.DeepEquals(conditions, McpCliContractTestSupport.FileMutationConditions)
            .ShouldBeTrue("FileMutationRequest/allOf drifted; reconcile admission constraints.");
    }

    [Fact]
    public void CanonicalPayloadResolverMatchesTheMcpCliSource()
    {
        string source = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "references/Hexalith.McpCli/src/Hexalith.McpCli.Core/Serialization/McpCliJson.cs"));
        string mirror = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "tests/Hexalith.Folders.Contracts.Tests/McpCli/McpCliContractTestSupport.cs"));
        foreach (string method in new[]
        {
            @"private static DefaultJsonTypeInfoResolver CreateResolver\(\)([\s\S]*?return resolver;\s*})",
            @"private static JsonSerializerOptions CreatePayload\(JsonSerializerOptions\? provider\)([\s\S]*?return options;\s*})",
        })
        {
            Match canonical = Regex.Match(source, method);
            Match copied = Regex.Match(mirror, method);
            canonical.Success.ShouldBeTrue(method);
            copied.Success.ShouldBeTrue(method);
            Regex.Replace(copied.Value, @"//[^\r\n]*|\s+", string.Empty)
                .ShouldBe(Regex.Replace(canonical.Value, @"//[^\r\n]*|\s+", string.Empty), method);
        }
    }

    [Fact]
    public void OptionalOmissionAndPermittedWireNullsAreCheckedIndependently()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C06");
        var instance = new Contracts.Commands.CreateFolder(new FolderMetadata("Example", SensitiveMetadataTier.TenantSensitive),
            "01ARZ3NDEKTSV4RRFFQ69G5FAV", "opaque_FolderAbC0123456789");
        JsonSerializer.SerializeToNode(instance, McpCliContractTestSupport.CreateCanonicalPayloadOptions())!
            .AsObject().ContainsKey("parentFolderId").ShouldBeFalse("default optional members are omitted by canonical payload serialization");
        Dictionary<string, JsonObject> fields = ExpectedFields(spine, row);
        fields["parentFolderId"]["nullable"] = true;
        var differences = new List<string>();
        CompareObject(spine, row.Type, fields, "C06/CreateFolder", differences, row.Id);
        differences.ShouldContain(message => message.Contains("CreateFolder.parentFolderId: permitted wire null", StringComparison.Ordinal));

        // ByteLength is the single explicit nullable wire member. Removing that permission
        // must fail even though the same nullable CLR type also represents omission.
        JsonObject mutation = spine["components"]!["schemas"]!["FileMutationRequest"]!.AsObject().DeepClone().AsObject();
        mutation["properties"]!["byteLength"]!.AsObject().Remove("nullable");
        differences.Clear();
        CompareObject(spine, typeof(FileMutationRequest), ObjectFields(spine, mutation), "FileMutationRequest", differences, "C10");
        differences.ShouldContain(message => message.Contains("FileMutationRequest.byteLength: permitted wire null", StringComparison.Ordinal));
    }

    [Fact]
    public void InlineAllOfMergesFieldsAndTightensBoundsAndRejectsUnsupportedComposition()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        JsonObject request = spine["components"]!["schemas"]!["LockWorkspaceRequest"]!.AsObject();
        request["allOf"] = JsonNode.Parse("""
        [{"properties":{"requestedLeaseSeconds":{"maximum":86399},"addedScope":{"type":"string"}},"required":["addedScope"]}]
        """);
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C09");
        Dictionary<string, JsonObject> fields = ExpectedFields(spine, row);
        fields.ShouldContainKey("lockIntent");
        fields.ShouldContainKey("addedScope");
        fields["addedScope"]["x-required"]!.GetValue<bool>().ShouldBeTrue();
        var differences = new List<string>();
        CompareObject(spine, row.Type, fields, "C09/LockWorkspace", differences, row.Id);
        differences.ShouldContain(message => message.Contains("LockWorkspace.addedScope", StringComparison.Ordinal));
        differences.ShouldContain(message => message.Contains("LockWorkspace.requestedLeaseSeconds: maximum", StringComparison.Ordinal));
        request["allOf"] = JsonNode.Parse("""[{"oneOf":[{"type":"string"},{"type":"integer"}]}]""");
        Should.Throw<NotSupportedException>(() => ExpectedFields(spine, row)).Message.ShouldContain("oneOf");
    }

    [Theory]
    [InlineData("""[{"properties":{"addedScope":{"type":"string"}},"required":["addedScope"]}]""", "addedScope", "spine field is missing")]
    [InlineData("""[{"properties":{"changes":{"maxItems":99}}}]""", "changes", "maxItems")]
    public void CanonicalBatchRootAllOfFieldsAndBoundsCannotBypassTheDriftGate(string intersections, string field, string difference)
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        spine["components"]!["schemas"]!["MutateFilesRequest"]!["allOf"] = JsonNode.Parse(intersections);
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C10");
        Dictionary<string, JsonObject> fields = ExpectedFields(spine, row);
        if (field == "addedScope")
        {
            fields.ShouldContainKey(field);
            fields[field]["x-required"]!.GetValue<bool>().ShouldBeTrue();
        }

        var differences = new List<string>();
        CompareObject(spine, row.Type, fields, "C10/MutateFiles", differences, row.Id);
        differences.ShouldContain(message => message.Contains("MutateFiles." + field + ": " + difference, StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalBatchRootAllOfKeepsTheSchemaVersionExemption()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        JsonObject request = spine["components"]!["schemas"]!["MutateFilesRequest"]!.AsObject();
        JsonObject properties = request["properties"]!.AsObject();
        JsonNode discriminator = properties["requestSchemaVersion"]!.DeepClone();
        properties.Remove("requestSchemaVersion");
        JsonArray required = request["required"]!.AsArray();
        required.Remove(required.Single(value => value!.GetValue<string>() == "requestSchemaVersion"));
        request["allOf"] = new JsonArray(new JsonObject
        {
            ["properties"] = new JsonObject { ["requestSchemaVersion"] = discriminator },
            ["required"] = new JsonArray("requestSchemaVersion"),
        });

        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C10");
        DecisionExemptionCodes(spine, row).ShouldBe("E1, E3");
        var differences = new List<string>();
        CompareObject(spine, row.Type, ExpectedFields(spine, row), "C10/MutateFiles", differences, row.Id);
        differences.ShouldBeEmpty(string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void DriftGateRejectsIgnoredAndRemovedEffectiveJsonMembers()
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        OperationDeclaration row = McpCliContractTestSupport.Operations.Single(item => item.Id == "C09");
        foreach (bool remove in new[] { false, true })
        {
            JsonSerializerOptions options = McpCliContractTestSupport.CreateCanonicalPayloadOptions(typeInfo =>
            {
                if (typeInfo.Type != row.Type)
                {
                    return;
                }

                JsonPropertyInfo member = typeInfo.Properties.Single(property => property.Name == "lockIntent");
                if (remove)
                {
                    typeInfo.Properties.Remove(member);
                }
                else
                {
                    // JsonIgnore(Always) leaves metadata with neither accessor.
                    member.Get = null;
                    member.Set = null;
                    member.IsRequired = false;
                }
            });
            var differences = new List<string>();
            CompareObject(spine, row.Type, ExpectedFields(spine, row), "C09/LockWorkspace", differences, row.Id, options);
            differences.ShouldContain(message => message.Contains("LockWorkspace.lockIntent: effective JSON binding", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("contentEncoding", "hex")]
    [InlineData("x-hexalith-decoded-byte-maximum", "262145")]
    public void InlineContentEncodingAndDecodedByteMaximumArePinned(string field, string replacement)
    {
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        JsonObject inline = spine["components"]!["schemas"]!["PutFileInline"]!.AsObject();
        inline["properties"]!["contentBytes"]![field] = field == "contentEncoding"
            ? JsonValue.Create(replacement) : JsonValue.Create(int.Parse(replacement, CultureInfo.InvariantCulture));
        var differences = new List<string>();
        CompareObject(spine, typeof(PutFileInline), ObjectFields(spine, inline), "PutFileInline", differences, "C10");
        differences.ShouldContain(message => message.Contains("PutFileInline.contentBytes: " + field, StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyInlineContentIsValidButContentBytesPresenceRemainsRequired()
    {
        var inline = new PutFileInline("application/octet-stream", string.Empty);
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(inline, new ValidationContext(inline), validation, validateAllProperties: true)
            .ShouldBeTrue(string.Join(Environment.NewLine, validation));
        Convert.FromBase64String(inline.ContentBytes).ShouldBeEmpty();

        JsonSerializerOptions options = McpCliContractTestSupport.CreateCanonicalPayloadOptions();
        const string payload = """{"mediaType":"application/octet-stream","contentBytes":""}""";
        JsonSerializer.Deserialize<PutFileInline>(payload, options)!.ShouldBe(inline);
        JsonSerializer.Serialize(inline, options).ShouldBe(payload);
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<PutFileInline>(
            """{"mediaType":"application/octet-stream"}""", options)).Message.ShouldContain("contentBytes");

        PutFileInline missing = inline with { ContentBytes = null! };
        validation.Clear();
        Validator.TryValidateObject(missing, new ValidationContext(missing), validation, validateAllProperties: true).ShouldBeFalse();
        validation.ShouldContain(result => result.MemberNames.Contains(nameof(PutFileInline.ContentBytes), StringComparer.Ordinal));
    }

    private static Dictionary<string, JsonObject> ExpectedFields(JsonObject spine, OperationDeclaration row)
    {
        JsonObject operation = McpCliContractTestSupport.FindOperation(spine, row.Id == "C10" ? "AddFile" : row.SpineOperation);
        JsonObject body = row.Id == "C10"
            ? McpCliContractTestSupport.Resolve(spine, spine["components"]!["schemas"]!["MutateFilesRequest"]!.AsObject())
            : RequestSchema(spine, operation);
        if (row.Id == "C10")
        {
            Exemptions.ShouldContainKey("C10/$request");
        }

        Dictionary<string, JsonObject> fields = ObjectFields(spine, body);
        JsonObject[] parameters = operation["parameters"]!.AsArray().OfType<JsonObject>()
            .Select(raw => McpCliContractTestSupport.Resolve(spine, raw)).ToArray();
        foreach (JsonObject parameter in parameters)
        {
            string name = parameter["name"]!.GetValue<string>();
            string location = parameter["in"]!.GetValue<string>();
            if (location is not ("path" or "query") && !(row.Id.StartsWith('Q') && name == "X-Hexalith-Task-Id"))
            {
                continue;
            }

            name = name == "X-Hexalith-Task-Id" ? "taskId" : name;
            JsonObject field = McpCliContractTestSupport.Resolve(spine, parameter["schema"]!.AsObject());
            field["x-required"] = parameter["required"]?.GetValue<bool>() ?? false;
            fields[name] = field;
        }

        if (row.Id.StartsWith('C'))
        {
            JsonObject key = HeaderField(spine, parameters, "Idempotency-Key");
            fields.Add("idempotencyKey", key);
            JsonObject task = HeaderField(spine, parameters, "X-Hexalith-Task-Id");
            task["x-required"]!.GetValue<bool>().ShouldBe(row.TaskScoped, row.Id + " task-header required flag");
            if (row.TaskScoped)
            {
                if (row.Id == "C03")
                {
                    fields.ShouldContainKey("taskId", "C03/CommitWorkspace.taskId: the duplicate body input must remain declared");
                }

                if (fields.TryGetValue("taskId", out JsonObject? bodyTask))
                {
                    JsonNode.DeepEquals(bodyTask, task).ShouldBeTrue(row.Id + "/taskId: body and header schema/required flag must match");
                }
                else
                {
                    fields.Add("taskId", task);
                }
            }
            else
            {
                Exemptions.ShouldContainKey(row.Id + "/X-Hexalith-Task-Id");
                JsonObject identifier = McpCliContractTestSupport.Resolve(spine, spine["components"]!["schemas"]!["OpaqueIdentifier"]!.AsObject());
                task.Remove("x-required");
                JsonNode.DeepEquals(task, identifier).ShouldBeTrue(row.Id + "/X-Hexalith-Task-Id: optional extension schema must preserve the opaque identifier bounds");
            }

            if (row.Id == "C06")
            {
                Exemptions.ShouldContainKey("C06/folderId");
                fields["folderId"] = McpCliContractTestSupport.Resolve(spine, spine["components"]!["schemas"]!["OpaqueIdentifier"]!.AsObject());
                fields["folderId"]["x-required"] = true;
            }
        }

        if (row.Id == "C05")
        {
            Exemptions.ShouldContainKey("C05/$aggregate");
            row.AggregateIdProperty.ShouldBe("ProviderBindingRef");
        }

        if (Exemptions.ContainsKey(row.Id + "/effect"))
        {
            fields.Remove("effect").ShouldBeTrue(row.Id + "/effect: the spine field is derived from operation identity");
        }

        return fields;
    }

    private static JsonObject HeaderField(JsonObject spine, JsonObject[] parameters, string name)
    {
        JsonObject parameter = parameters.Single(item => item["in"]!.GetValue<string>() == "header"
            && item["name"]!.GetValue<string>() == name);
        JsonObject field = McpCliContractTestSupport.Resolve(spine, parameter["schema"]!.AsObject());
        field["x-required"] = parameter["required"]?.GetValue<bool>() ?? false;
        return field;
    }

    /// <summary>Gets the decision-record labels corresponding to this operation's checked exemptions.</summary>
    internal static string DecisionExemptionCodes(JsonObject spine, OperationDeclaration row)
    {
        JsonObject operation = McpCliContractTestSupport.FindOperation(spine, row.Id == "C10" ? "AddFile" : row.SpineOperation);
        JsonObject body = row.Id == "C10"
            ? McpCliContractTestSupport.Resolve(spine, spine["components"]!["schemas"]!["MutateFilesRequest"]!.AsObject())
            : RequestSchema(spine, operation);
        var codes = new List<string>();
        if (body["properties"]?.AsObject().ContainsKey("requestSchemaVersion") == true)
        {
            Exemptions.ShouldContainKey("*/requestSchemaVersion");
            codes.Add("E1");
        }

        foreach ((string key, string code) in new[] { ("effect", "E2"), ("$request", "E3"), ("folderId", "E4"), ("$aggregate", "E5"), ("X-Hexalith-Task-Id", "E6") })
        {
            if (Exemptions.ContainsKey(row.Id + "/" + key))
            {
                codes.Add(code);
            }
        }

        return codes.Count == 0 ? "—" : string.Join(", ", codes);
    }

    private static JsonObject RequestSchema(JsonObject spine, JsonObject operation) => operation["requestBody"]?["content"]?["application/json"]?["schema"] is JsonObject schema
        ? McpCliContractTestSupport.Resolve(spine, schema) : new JsonObject();

    private static Dictionary<string, JsonObject> ObjectFields(JsonObject spine, JsonObject schema)
    {
        var fields = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        string[] required = schema["required"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? [];
        foreach ((string name, JsonNode? value) in schema["properties"]?.AsObject() ?? [])
        {
            if (name == "requestSchemaVersion")
            {
                Exemptions.ShouldContainKey("*/requestSchemaVersion");
                continue;
            }

            JsonObject field = McpCliContractTestSupport.Resolve(spine, value!.AsObject());
            field["x-required"] = required.Contains(name, StringComparer.Ordinal);
            fields.Add(name, field);
        }

        return fields;
    }

    private static void CompareObject(JsonObject spine, Type type, Dictionary<string, JsonObject> expected,
        string location, List<string> differences, string operationId, JsonSerializerOptions? options = null)
    {
        options ??= McpCliContractTestSupport.CreateCanonicalPayloadOptions();
        Dictionary<string, JsonPropertyInfo> bindings = options.GetTypeInfo(type).Properties
            .Where(property => property.Get is not null && (property.Set is not null || property.AssociatedParameter is not null))
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        Dictionary<string, PropertyInfo> actual = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name, StringComparer.Ordinal);
        foreach (string field in expected.Keys.Except(actual.Keys, StringComparer.Ordinal))
        {
            differences.Add(location + "." + field + ": spine field is missing from the CLR declaration.");
        }

        foreach (string field in actual.Keys.Except(expected.Keys, StringComparer.Ordinal))
        {
            differences.Add(location + "." + field + ": CLR field is absent from the spine.");
        }

        foreach (string field in actual.Keys.Intersect(expected.Keys, StringComparer.Ordinal))
        {
            PropertyInfo property = actual[field];
            JsonObject schema = expected[field];
            string path = location + "." + field;
            bool effective = bindings.TryGetValue(field, out JsonPropertyInfo? binding)
                && binding.AttributeProvider is PropertyInfo bound && bound.Name == property.Name;
            Check(effective, path, "effective JSON binding", differences);
            bool required = schema["x-required"]?.GetValue<bool>() ?? false;
            Check(property.IsDefined(typeof(JsonRequiredAttribute)) == required, path, "required flag", differences);
            Check(property.IsDefined(typeof(RequiredAttribute)) == required, path, "DataAnnotations required flag", differences);
            Type memberType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            bool nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null
                || new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
            // Optional CLR nulls represent omission because canonical serialization omits null
            // values. That representation does not authorize JSON null on the raw Gateway wire.
            bool wireAllowsNull = property.DeclaringType == typeof(FileMutationRequest) && property.Name == "ByteLength";
            Check((schema["nullable"]?.GetValue<bool>() ?? false) == wireAllowsNull, path, "permitted wire null", differences);
            Check(nullable == (!required || wireAllowsNull), path, "CLR omission/null representation", differences);
            if (effective)
            {
                Check(binding!.IsRequired == required, path, "effective JSON required flag", differences);
            }

            CompareValue(spine, memberType, property, schema, path, differences, operationId, options);
        }
    }

    private static void CompareValue(JsonObject spine, Type type, PropertyInfo member, JsonObject schema,
        string path, List<string> differences, string operationId, JsonSerializerOptions options)
    {
        string kind = schema["type"]!.GetValue<string>();
        bool compatible = kind switch
        {
            "string" => type == typeof(string) || type.IsEnum,
            "integer" => type == (schema["format"]?.GetValue<string>() == "int64" ? typeof(long) : typeof(int)),
            "array" => type.IsArray,
            "object" => type.IsClass && type != typeof(string),
            _ => false,
        };
        Check(compatible, path, "member type", differences);
        if (!compatible)
        {
            return;
        }
        if (kind == "object")
        {
            CompareObject(spine, type, ObjectFields(spine, schema), path, differences, operationId, options);
            return;
        }

        if (kind == "array")
        {
            Check(member.GetCustomAttribute<MinLengthAttribute>()?.Length == Integer(schema, "minItems"), path, "minItems", differences);
            Check(member.GetCustomAttribute<MaxLengthAttribute>()?.Length == Integer(schema, "maxItems"), path, "maxItems", differences);
            JsonObject items = McpCliContractTestSupport.Resolve(spine, schema["items"]!.AsObject());
            PropertyInfo itemConstraints = member;
            if (items["type"]!.GetValue<string>() == "string" && items["enum"] is null)
            {
                // DefaultRef and both pattern arrays use exactly the same spine primitive.
                // Standard DataAnnotations have no element target; compare each array's item
                // constraints against that shared annotated primitive without wrapping JSON strings.
                itemConstraints = member.DeclaringType!.GetProperty("DefaultRef")!;
            }

            CompareValue(spine, type.GetElementType()!, itemConstraints, items, path + "[]", differences, operationId, options);
            return;
        }

        if (kind == "string")
        {
            if (member.DeclaringType == typeof(PutFileInline) && member.Name == "ContentBytes")
            {
                Check(schema["contentEncoding"]?.GetValue<string>() == "base64", path, "contentEncoding", differences);
                Check(Integer(schema, "x-hexalith-decoded-byte-maximum") == 262144, path, "x-hexalith-decoded-byte-maximum", differences);
            }

            StringLengthAttribute? length = member.GetCustomAttribute<StringLengthAttribute>();
            Check(length?.MaximumLength == Integer(schema, "maxLength"), path, "maxLength", differences);
            Check((length?.MinimumLength ?? 0) == (Integer(schema, "minLength") ?? 0), path, "minLength", differences);
            Check(member.GetCustomAttribute<RegularExpressionAttribute>()?.Pattern == schema["pattern"]?.GetValue<string>(), path, "pattern", differences);
            string[]? vocabulary = schema["enum"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray()
                ?? (schema["const"] is JsonNode constant ? [constant.GetValue<string>()] : null);
            if (vocabulary is not null)
            {
                Check(type.IsEnum, path, "enum declaration", differences);
                string[] values = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name).ToArray();
                Check(values.Order(StringComparer.Ordinal).SequenceEqual(vocabulary.Order(StringComparer.Ordinal), StringComparer.Ordinal), path, "enum values", differences);
            }
        }

        if (kind == "integer")
        {
            RangeAttribute? range = member.GetCustomAttribute<RangeAttribute>();
            Check(range is not null, path, "numeric range", differences);
            if (range is not null)
            {
                long maximum = schema["maximum"]?.GetValue<long>() ?? (type == typeof(long) ? long.MaxValue : int.MaxValue);
                long minimum = schema["minimum"]?.GetValue<long>() ?? (type == typeof(long) ? long.MinValue : int.MinValue);
                Check(Convert.ToInt64(range.Minimum, CultureInfo.InvariantCulture) == minimum, path, "minimum", differences);
                Check(Convert.ToInt64(range.Maximum, CultureInfo.InvariantCulture) == maximum, path, "maximum", differences);
            }
        }
    }

    private static int? Integer(JsonObject schema, string key) => schema[key]?.GetValue<int>();

    private static void Check(bool condition, string path, string difference, List<string> differences)
    {
        if (!condition)
        {
            differences.Add(path + ": " + difference + " differs from the spine.");
        }
    }
}
