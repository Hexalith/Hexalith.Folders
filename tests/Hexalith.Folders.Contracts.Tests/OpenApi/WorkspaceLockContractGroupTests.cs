using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class WorkspaceLockContractGroupTests
{
    private const string OpenApiResource = "WorkspaceLockContract.Spine.yaml";
    private const string ContractNotesResource = "WorkspaceLockContract.Notes.md";

    private static readonly string[] _workspaceLockOperationIdList =
    [
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
        "GetWorkspaceLock",
        "GetWorkspaceRetryEligibility",
        "GetWorkspaceTransitionEvidence",
    ];

    private static readonly string[] _mutatingOperationIdList =
    [
        "PrepareWorkspace",
        "LockWorkspace",
        "ReleaseWorkspaceLock",
    ];

    [Fact]
    public void WorkspaceLockOperations_MatchStoryAllowListAndResolveRefs()
    {
        YamlMappingNode root = LoadYamlMapping();
        Operation[] operations = EnumerateOperations(root).Where(o => _workspaceLockOperationIdList.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();

        HashSet<string> actual = operations.Select(o => o.OperationId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> expected = _workspaceLockOperationIdList.ToHashSet(StringComparer.Ordinal);
        actual.SetEquals(expected).ShouldBeTrue($"workspace/lock operation allow-list drift. Expected {string.Join(", ", expected)}, found {string.Join(", ", actual)}.");
        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.Select(o => o.Path).All(p => p.StartsWith("/api/v1/folders/{folderId}/workspaces", StringComparison.Ordinal)).ShouldBeTrue();

        string[] forbiddenPathFragments =
        [
            "/files",
            "/context",
            "/commits",
            "/audit",
            "/ops-console",
        ];

        foreach (Operation operation in operations)
        {
            foreach (string forbidden in forbiddenPathFragments)
            {
                operation.Path.ShouldNotContain(forbidden, Case.Sensitive, $"{operation.OperationId} belongs to a downstream story.");
            }
        }

        ResolveRefs(root);
    }

    [Theory]
    [InlineData("cursor")]
    [InlineData("{type: string}")]
    public void ParameterReferenceRequiresAParameterObject(string target)
    {
        YamlMappingNode root = ParseYamlMapping($$"""
            paths:
              /lookup:
                get:
                  parameters:
                    - $ref: '#/components/parameters/Cursor'
            components:
              parameters:
                Cursor: {{target}}
            """);

        Should.Throw<ShouldAssertException>(() => ResolveRefs(root)).Message
            .ShouldContain("must resolve to a Parameter Object", Case.Sensitive);
    }

    [Fact]
    public void WorkspaceLockOperations_DeclareRequiredMetadataAndIdempotencyRules()
    {
        YamlMappingNode root = LoadYamlMapping();
        Operation[] operations = EnumerateOperations(root).Where(o => _workspaceLockOperationIdList.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();
        HashSet<string> mutating = _mutatingOperationIdList.ToHashSet(StringComparer.Ordinal);

        foreach (Operation operation in operations)
        {
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-correlation")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-parity-dimensions")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-audit-metadata-keys")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-authorization")).ShouldBeTrue(operation.OperationId);
            operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-canonical-error-categories")).ShouldBeTrue(operation.OperationId);

            YamlMappingNode[] parameters = EnumerateParameters(operation.PathItem, operation.Node).ToArray();
            bool hasIdempotencyHeader = parameters.Any(parameter =>
                (GetOptionalScalar(parameter, "name") == "Idempotency-Key" && GetOptionalScalar(parameter, "in") == "header")
                || GetOptionalScalar(parameter, "$ref") == "#/components/parameters/IdempotencyKey");

            if (mutating.Contains(operation.OperationId))
            {
                hasIdempotencyHeader.ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-key")).ShouldBeTrue(operation.OperationId);
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-idempotency-ttl-tier")).ShouldBeTrue(operation.OperationId);

                string[] equivalence = RequiredSequence(operation.Node, "x-hexalith-idempotency-equivalence")
                    .OfType<YamlScalarNode>()
                    .Select(value => value.Value ?? string.Empty)
                    .ToArray();

                equivalence.ShouldBe(equivalence.Order(StringComparer.Ordinal).ToArray(), $"{operation.OperationId} idempotency equivalence fields must be lexicographically ordered.");
                equivalence.ShouldBeUnique();
                equivalence.ShouldContain("task_id", $"{operation.OperationId} must include task_id in idempotency equivalence.");
                equivalence.ShouldNotContain("request_schema_version", $"{operation.OperationId} should not gate equivalence on a single-value schema version constant.");

                bool hasRequiredTaskIdHeader = parameters.Any(parameter =>
                    GetOptionalScalar(parameter, "name") == "X-Hexalith-Task-Id"
                    && GetOptionalScalar(parameter, "required") == "true");
                hasRequiredTaskIdHeader.ShouldBeTrue($"{operation.OperationId} must declare X-Hexalith-Task-Id as a required header because task_id participates in idempotency equivalence.");
            }
            else
            {
                hasIdempotencyHeader.ShouldBeFalse($"{operation.OperationId} is non-mutating and must not accept Idempotency-Key.");
                operation.Node.Children.ContainsKey(new YamlScalarNode("x-hexalith-read-consistency")).ShouldBeTrue(operation.OperationId);
            }
        }
    }

    [Fact]
    public void WorkspaceLockOperations_ExposeC6LeaseRetryAndSafeProblemDetails()
    {
        YamlMappingNode root = LoadYamlMapping();
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("locked");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("unknown_provider_outcome");
        RequiredEnumValues(schemas, "LifecycleState").ShouldContain("reconciliation_required");
        RequiredEnumValues(schemas, "LockLeaseStatus").ShouldBe(["active", "stale", "expired", "released", "revoked"]);
        RequiredEnumValues(schemas, "LockState").ShouldBe(["unlocked", "locked", "expired", "stale", "revoked"]);
        RequiredEnumValues(schemas, "WorkspaceTransitionResult").ShouldContain("state_transition_invalid");
        RequiredEnumValues(schemas, "WorkspaceTransitionResult").ShouldContain("authorization_revoked");

        string requestedLeaseDescription = GetScalar(
            RequiredMapping(RequiredMapping(RequiredMapping(schemas, "LockWorkspaceRequest"), "properties"), "requestedLeaseSeconds"),
            "description");
        requestedLeaseDescription.ShouldContain("expiresAt` equals the lock `effectiveAt` plus `requestedLeaseSeconds", Case.Sensitive);
        requestedLeaseDescription.ShouldContain("expired at `now >= expiresAt`", Case.Sensitive);
        requestedLeaseDescription.ShouldContain("successful authorized renewal", Case.Sensitive);
        requestedLeaseDescription.ShouldNotContain("metadata only", Case.Insensitive);

        string[] requiredExamples =
        [
            "WorkspaceLockStatus",
            "WorkspaceLockStatusUnlocked",
            "WorkspaceLockStatusExpired",
            "WorkspaceTransitionEvidenceValid",
            "WorkspaceTransitionEvidenceInvalid",
            "WorkspaceTransitionEvidenceAuthorizationRevoked",
            "WorkspaceTransitionInvalidProblem",
            "WorkspaceAuthorizationRevokedProblem",
            "WorkspaceLockExpiredProblem",
            "WorkspaceLockConflictProblem",
            "WorkspaceRetryEligible",
            "WorkspaceRetryBlocked",
            "IdempotencyConflictGeneric",
            "ReconciliationRequiredProblem",
            "ProviderReadinessFailedProblem",
            "WorkspacePreparationFailedProblem",
            "UnknownProviderOutcomeProblem",
            "ProviderUnavailableProblem",
        ];

        foreach (string exampleName in requiredExamples)
        {
            examples.Children.ContainsKey(new YamlScalarNode(exampleName)).ShouldBeTrue(exampleName);
        }

        Operation[] operations = EnumerateOperations(root).Where(o => _workspaceLockOperationIdList.Contains(o.OperationId, StringComparer.Ordinal)).ToArray();
        HashSet<string> mutating = _mutatingOperationIdList.ToHashSet(StringComparer.Ordinal);
        foreach (Operation operation in operations)
        {
            string[] categories = RequiredSequence(operation.Node, "x-hexalith-canonical-error-categories")
                .OfType<YamlScalarNode>()
                .Select(value => value.Value ?? string.Empty)
                .ToArray();

            categories.ShouldContain("tenant_access_denied", operation.OperationId);
            categories.ShouldContain("folder_acl_denied", operation.OperationId);

            if (mutating.Contains(operation.OperationId))
            {
                categories.ShouldContain("state_transition_invalid", $"{operation.OperationId} is mutating and must surface state_transition_invalid as a canonical category.");
            }
            else
            {
                categories.ShouldNotContain("state_transition_invalid", $"{operation.OperationId} is a query and cannot cause a state transition; state_transition_invalid is body-encoded in WorkspaceTransitionEvidence.result instead.");
            }
        }

        string[] preparationCategories = RequiredSequence(operations.Single(o => o.OperationId == "PrepareWorkspace").Node, "x-hexalith-canonical-error-categories")
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();
        preparationCategories.ShouldContain("provider_readiness_failed");
        preparationCategories.ShouldContain("workspace_preparation_failed");
        preparationCategories.ShouldContain("unknown_provider_outcome");
        preparationCategories.ShouldContain("reconciliation_required");
        preparationCategories.ShouldContain("idempotency_conflict");
        preparationCategories.ShouldContain("lock_conflict");
    }

    [Fact]
    public void WorkspaceLockOperations_DoNotExposeTenantAuthoritySecretMaterialOrForbiddenScope()
    {
        YamlMappingNode root = LoadYamlMapping();
        YamlMappingNode schemas = RequiredMapping(RequiredMapping(root, "components"), "schemas");
        string serialized = ReadArtifact(OpenApiResource);
        string notes = ReadArtifact(ContractNotesResource);
        string combined = string.Concat(serialized, "\n", notes);

        string[] namedFields = EnumerateNamedFields(root).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string named in namedFields)
        {
            named.Equals("tenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("tenant_id", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
            named.Equals("managedTenantId", StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"tenant authority must not appear as a client-controlled field; found '{named}'.");
        }

        foreach (string named in namedFields)
        {
            bool allowedNonSecretReference = named is "nonSecretCredentialReference" or "lockOwnershipProof";
            if (allowedNonSecretReference)
            {
                continue;
            }

            string[] forbiddenFieldTerms = ["token", "secret", "password", "privateKey", "accessToken", "credential", "apiKey"];
            foreach (string forbidden in forbiddenFieldTerms)
            {
                named.ShouldNotContain(forbidden, Case.Insensitive, $"secret-shaped field name '{named}' is forbidden.");
            }
        }

        string lockProofDescription = SerializeYaml(RequiredMapping(RequiredMapping(RequiredMapping(schemas, "ReleaseWorkspaceLockRequest"), "properties"), "lockOwnershipProof"));
        lockProofDescription.ShouldContain("non-secret opaque lock proof", Case.Insensitive);
        lockProofDescription.ShouldNotContain("bearer", Case.Insensitive);
        lockProofDescription.ShouldNotContain("provider credential", Case.Insensitive);

        string[] tokenShapePatterns =
        [
            @"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
            @"sk_(live|test)_[A-Za-z0-9]{16,}",
            @"ghp_[A-Za-z0-9]{20,}",
            @"gho_[A-Za-z0-9]{20,}",
            @"-----BEGIN",
        ];

        foreach (string pattern in tokenShapePatterns)
        {
            Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase).ShouldBeFalse($"contract artifacts must not contain token-shaped strings matching {pattern}.");
        }

        string[] forbiddenTexts =
        [
            "src/Hexalith.Folders.Server",
            "src/Hexalith.Folders.Client/Generated",
            "src/Hexalith.Folders.Cli",
            "src/Hexalith.Folders.Mcp",
            "src/Hexalith.Folders.Workers",
            string.Concat("git submodule update --init ", "--recursive"),
        ];

        foreach (string forbidden in forbiddenTexts)
        {
            combined.ShouldNotContain(forbidden, Case.Sensitive);
        }
    }

    [Fact]
    public void WorkspaceLockSafeDenialEnvelopes_AreExternallyIndistinguishable()
    {
        YamlMappingNode root = LoadYamlMapping();
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        // 403 and 404 must be byte-identical so callers cannot distinguish absent-from-forbidden,
        // cross-tenant-from-missing, or missing-resource-from-stale-read. 401 carries a different
        // legitimate signal (no token) and is intentionally distinguishable from authenticated denials.
        string[] indistinguishableExamples = ["SafeDenial403Forbidden", "SafeDenial404NotFound"];
        Dictionary<string, YamlMappingNode> envelopes = indistinguishableExamples
            .ToDictionary(name => name, name => RequiredMapping(RequiredMapping(examples, name), "value"));

        string[] requiredKeys = ["type", "title", "category", "code", "message", "retryable", "clientAction", "details"];
        foreach (string key in requiredKeys)
        {
            HashSet<string> values = envelopes.Values
                .Select(envelope => GetOptionalScalar(envelope, key) ?? SerializeYaml(envelope.Children[new YamlScalarNode(key)]).Trim())
                .ToHashSet(StringComparer.Ordinal);
            values.Count.ShouldBe(1, $"safe-denial envelope key '{key}' must carry the same value across 403/404 examples; found {values.Count} distinct values.");
        }

        // 401 must still redact details and carry no resource-existence hint, even though its title/category differ.
        YamlMappingNode envelope401 = RequiredMapping(RequiredMapping(examples, "SafeDenial401Unauthorized"), "value");
        YamlMappingNode details401 = RequiredMapping(envelope401, "details");
        GetOptionalScalar(details401, "visibility").ShouldBe("redacted");
        details401.Children.Count.ShouldBe(1, "SafeDenial401Unauthorized.details must contain only the redaction marker.");

        foreach ((string name, YamlMappingNode envelope) in envelopes)
        {
            YamlMappingNode details = RequiredMapping(envelope, "details");
            GetOptionalScalar(details, "visibility").ShouldBe("redacted", name);
            details.Children.Count.ShouldBe(1, $"{name}.details must contain only the redaction marker; extra keys leak case-specific information.");
        }
    }

    [Fact]
    public void WorkspaceLockOwnershipProof_NeverAppearsInUnauthorizedOrLeakyProblemDetails()
    {
        YamlMappingNode root = LoadYamlMapping();
        YamlMappingNode examples = RequiredMapping(RequiredMapping(root, "components"), "examples");

        YamlMappingNode releaseRequest = RequiredMapping(examples, "ReleaseWorkspaceLockRequest");
        YamlMappingNode releaseValue = RequiredMapping(releaseRequest, "value");
        string proofValue = GetOptionalScalar(releaseValue, "lockOwnershipProof")
            ?? throw new InvalidOperationException("ReleaseWorkspaceLockRequest example must include a synthetic lockOwnershipProof value.");

        string[] unauthorizedOrLeakyExamples =
        [
            "SafeDenial401Unauthorized",
            "SafeDenial403Forbidden",
            "SafeDenial404NotFound",
            "WorkspaceLockExpiredProblem",
            "WorkspaceLockConflictProblem",
            "WorkspaceAuthorizationRevokedProblem",
            "WorkspaceTransitionInvalidProblem",
            "ProviderReadinessFailedProblem",
            "WorkspacePreparationFailedProblem",
            "UnknownProviderOutcomeProblem",
            "ProviderUnavailableProblem",
            "IdempotencyConflictGeneric",
            "ReconciliationRequiredProblem",
        ];

        foreach (string exampleName in unauthorizedOrLeakyExamples)
        {
            string serializedExample = SerializeYaml(RequiredMapping(examples, exampleName));
            serializedExample.ShouldNotContain(proofValue, Case.Sensitive, $"Problem Details example '{exampleName}' must not echo the lockOwnershipProof value '{proofValue}'.");
            serializedExample.ShouldNotContain("lockOwnershipProof", Case.Sensitive, $"Problem Details example '{exampleName}' must not include the lockOwnershipProof field at all.");
        }
    }

    [Theory]
    [InlineData("#/components/parameters/Cursor", "items/$ref", "schemas")]
    [InlineData("#/components/schemas/Item/type", "items/$ref", "schemas")]
    [InlineData("#/components/schemas/Item", "parameters/0/$ref", "parameters")]
    public void ReferencesMustTargetTheComponentKindRequiredByTheirLocation(string reference, string location, string component)
    {
        string schemaRef = component == "schemas" ? reference : "#/components/schemas/Item";
        string parameterRef = component == "parameters" ? reference : "#/components/parameters/Cursor";
        YamlMappingNode root = ParseYamlMapping($$"""
            paths:
              /lookup:
                get:
                  parameters:
                    - $ref: '{{parameterRef}}'
                  responses:
                    '200':
                      content:
                        application/json:
                          schema:
                            items: {$ref: '{{schemaRef}}'}
            components:
              schemas:
                Item: {type: string}
              parameters:
                Cursor:
                  name: cursor
                  in: query
                  schema: {type: string}
            """);

        Should.Throw<ShouldAssertException>(() => ResolveRefs(root)).Message
            .ShouldContain(location, Case.Sensitive);
    }

    [Fact]
    public void ExampleFieldEnumerationVisitsPayloadKeysWithoutTreatingLabelsAndValuesAsFields()
    {
        YamlMappingNode root = ParseYamlMapping("""
            components:
              schemas:
                Result:
                  properties:
                    example: {type: string}
                  example: {singleSchemaCredentialField: synthetic}
                  examples:
                    - result: [{exampleCredentialField: synthetic}]
              examples:
                tokenLabel:
                  summary: Describes a credential
                  value: {name: credentialValue, $ref: ordinary data, componentCredentialField: synthetic}
              responses:
                Result:
                  content:
                    application/json:
                      example: {items: [{inlineCredentialField: synthetic}]}
                    application/problem+json:
                      examples:
                        tokenLabel:
                          value: {namedCredentialField: synthetic}
            """);

        string[] fields = EnumerateNamedFields(root).ToArray();
        fields.ShouldContain("singleSchemaCredentialField");
        fields.ShouldContain("exampleCredentialField");
        fields.ShouldContain("componentCredentialField");
        fields.ShouldContain("inlineCredentialField");
        fields.ShouldContain("namedCredentialField");
        fields.ShouldNotContain("tokenLabel");
        fields.ShouldNotContain("credentialValue");
        ResolveRefs(root);
    }

    private sealed record Operation(string Path, string Method, string OperationId, YamlMappingNode Node, YamlMappingNode PathItem);

    private static IEnumerable<Operation> EnumerateOperations(YamlMappingNode root)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
        {
            string path = pathEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            YamlMappingNode pathItem = pathEntry.Value.ShouldBeOfType<YamlMappingNode>();

            foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
            {
                string method = methodEntry.Key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                if (method is "get" or "put" or "post" or "patch" or "delete")
                {
                    YamlMappingNode operation = methodEntry.Value.ShouldBeOfType<YamlMappingNode>();
                    yield return new Operation(path, method, GetScalar(operation, "operationId"), operation, pathItem);
                }
            }
        }
    }

    private static IEnumerable<YamlMappingNode> EnumerateParameters(YamlMappingNode pathItem, YamlMappingNode operation)
    {
        if (pathItem.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? pathParametersNode))
        {
            foreach (YamlNode parameter in pathParametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }

        if (operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
        {
            foreach (YamlNode parameter in parametersNode.ShouldBeOfType<YamlSequenceNode>())
            {
                yield return parameter.ShouldBeOfType<YamlMappingNode>();
            }
        }
    }

    private static IEnumerable<string> EnumerateNamedFields(YamlNode node)
    {
        foreach ((_, YamlNode objectNode, string? component) in WalkContract(node, "#"))
        {
            if (objectNode is not YamlMappingNode mapping)
            {
                continue;
            }

            if (GetOptionalScalar(mapping, "name") is { } name)
            {
                yield return name;
            }

            if (component == "schemas" && FindChild(mapping, "properties") is YamlMappingNode properties)
            {
                foreach (YamlScalarNode property in properties.Children.Keys.OfType<YamlScalarNode>())
                {
                    yield return property.Value ?? string.Empty;
                }
            }

            foreach (string field in EnumeratePayloadKeys(FindChild(mapping, component == "examples" ? "value" : "example")))
            {
                yield return field;
            }

            if (component == "schemas")
            {
                foreach (string field in EnumeratePayloadKeys(FindChild(mapping, "examples")))
                {
                    yield return field;
                }
            }
        }
    }

    private static IEnumerable<string> EnumeratePayloadKeys(YamlNode? node)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach ((YamlNode key, YamlNode child) in mapping.Children)
            {
                if (key is YamlScalarNode { Value: { } field })
                {
                    yield return field;
                }

                foreach (string nested in EnumeratePayloadKeys(child))
                {
                    yield return nested;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            foreach (string nested in sequence.Children.SelectMany(EnumeratePayloadKeys))
            {
                yield return nested;
            }
        }
    }

    private static string[] RequiredEnumValues(YamlMappingNode schemas, string schemaName)
    {
        YamlSequenceNode values = RequiredSequence(RequiredMapping(schemas, schemaName), "enum");

        return values
            .OfType<YamlScalarNode>()
            .Select(value => value.Value ?? string.Empty)
            .ToArray();
    }

    private static void ResolveRefs(YamlMappingNode root)
    {
        var objects = WalkContract(root, "#").ToArray();
        var schemaLocations = objects.Where(entry => entry.Component == "schemas").Select(entry => entry.Location).ToHashSet(StringComparer.Ordinal);
        foreach ((string location, YamlNode objectNode, string? component) in objects)
        {
            if (objectNode is not YamlMappingNode mapping || FindChild(mapping, "$ref") is not { } node)
            {
                continue;
            }

            string reference = node.ShouldBeOfType<YamlScalarNode>(location).Value ?? string.Empty;
            string context = $"{location}/$ref points to {reference}";
            reference.StartsWith("#/", StringComparison.Ordinal).ShouldBeTrue(context);
            string[] segments = reference[2..].Split('/');
            if (component == "schemas")
            {
                (reference.StartsWith("#/components/schemas/", StringComparison.Ordinal) && schemaLocations.Contains(reference))
                    .ShouldBeTrue($"{context} instead of a schema in #/components/schemas.");
            }
            else if (component is not null)
            {
                (segments.Length == 3 && segments[0] == "components" && segments[1] == component)
                    .ShouldBeTrue($"{context} instead of a target in #/components/{component}.");
            }

            YamlNode? target = ResolvePointer(root, segments);
            target.ShouldNotBeNull(context);
            if (component == "parameters")
            {
                (target is YamlMappingNode parameter
                    && (GetOptionalScalar(parameter, "$ref") is not null
                        || (!string.IsNullOrWhiteSpace(GetOptionalScalar(parameter, "name"))
                            && GetOptionalScalar(parameter, "in") is "path" or "query" or "header" or "cookie")))
                    .ShouldBeTrue($"{context} and must resolve to a Parameter Object.");
            }
        }
    }

    private static IEnumerable<(string Location, YamlNode Node, string? Component)> WalkContract(
        YamlNode node,
        string location,
        string? component = null)
    {
        if (node is not YamlMappingNode mapping)
        {
            if (component == "schemas" && node is YamlScalarNode { Value: "true" or "false", Style: YamlDotNet.Core.ScalarStyle.Plain })
            {
                yield return (location, node, component);
            }

            yield break;
        }

        yield return (location, mapping, component);
        foreach ((YamlNode key, YamlNode child) in mapping.Children)
        {
            string name = key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
            string childLocation = $"{location}/{EscapePointerSegment(name)}";

            if (name == "example" || (component == "examples" && name == "value"))
            {
                continue;
            }

            bool schema = component == "schemas";
            string? entryComponent = (location, schema, name) switch
            {
                ("#/components", _, "schemas" or "parameters" or "requestBodies" or "responses" or "headers" or "examples") => name,
                (_, true, "properties" or "patternProperties" or "$defs" or "dependentSchemas" or "allOf" or "anyOf" or "oneOf" or "prefixItems") => "schemas",
                (_, false, "parameters" or "examples") => name,
                _ => null,
            };
            if (entryComponent is not null)
            {
                foreach (var entry in WalkEntries(child, childLocation, entryComponent))
                {
                    yield return entry;
                }
            }
            else if (!schema || name is "items" or "contains" or "additionalProperties" or "unevaluatedProperties"
                or "unevaluatedItems" or "propertyNames" or "not" or "if" or "then" or "else")
            {
                string? childComponent = schema || name == "schema" ? "schemas" : null;
                foreach (var entry in WalkContract(child, childLocation, childComponent))
                {
                    yield return entry;
                }
            }
        }
    }

    private static IEnumerable<(string Location, YamlNode Node, string? Component)> WalkEntries(
        YamlNode node,
        string location,
        string component)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach ((YamlNode key, YamlNode value) in mapping.Children)
            {
                string name = key.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
                foreach (var entry in WalkContract(value, $"{location}/{EscapePointerSegment(name)}", component))
                {
                    yield return entry;
                }
            }
        }
        else if (node is YamlSequenceNode sequence)
        {
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                foreach (var entry in WalkContract(sequence.Children[index], $"{location}/{index}", component))
                {
                    yield return entry;
                }
            }
        }
    }

    private static string EscapePointerSegment(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static YamlNode? FindChild(YamlMappingNode mapping, string name)
        => mapping.Children.TryGetValue(new YamlScalarNode(name), out YamlNode? child) ? child : null;

    private static YamlNode? ResolvePointer(YamlNode root, IReadOnlyList<string> segments)
    {
        YamlNode current = root;

        foreach (string segment in segments)
        {
            string key = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current is YamlMappingNode mapping && FindChild(mapping, key) is { } child)
            {
                current = child;
            }
            else if (current is YamlSequenceNode sequence && int.TryParse(key, out int index)
                && index >= 0 && index < sequence.Children.Count)
            {
                current = sequence.Children[index];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static YamlMappingNode LoadYamlMapping()
        => ParseYamlMapping(ReadArtifact(OpenApiResource));

    private static YamlMappingNode ParseYamlMapping(string content)
    {
        using StringReader reader = new(content);
        YamlStream yaml = new();
        yaml.Load(reader);

        return yaml.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string ReadArtifact(string resourceName)
    {
        using Stream stream = typeof(WorkspaceLockContractGroupTests).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Workspace-lock contract test input '{resourceName}' was not embedded.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    private static string GetScalar(YamlMappingNode mapping, string key)
    {
        mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue(key);

        return value.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
    }

    private static string? GetOptionalScalar(YamlMappingNode mapping, string key)
    {
        return mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static string SerializeYaml(YamlNode node)
    {
        YamlStream stream = new(new YamlDocument(node));
        using StringWriter writer = new();
        stream.Save(writer, false);

        return writer.ToString();
    }
}
