using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using static GeneratorConstants;

GeneratorOptions options = GeneratorOptions.Parse(args);

if (options.InitializeBaseline)
{
    YamlMappingNode rootForBaseline = LoadYaml(options.ContractPath);
    IReadOnlyList<OperationModel> baselineOps = EnumerateOperations(rootForBaseline, new List<Diagnostic>()).OrderBy(o => o.OperationId, StringComparer.Ordinal).ToArray();
    string baselineYaml = RenderBaseline(baselineOps);
    Directory.CreateDirectory(Path.GetDirectoryName(options.PreviousSpinePath) ?? ".");
    File.WriteAllText(options.PreviousSpinePath, baselineYaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Wrote baseline with {baselineOps.Count} operations to {options.PreviousSpinePath}");
    return 0;
}

List<Diagnostic> diagnostics = [];
YamlMappingNode root = LoadYaml(options.ContractPath);
IReadOnlyList<OperationModel> operations = EnumerateOperations(root, diagnostics)
    .OrderBy(o => o.OperationId, StringComparer.Ordinal)
    .ToArray();
ValidateOperationInventory(operations, diagnostics);
ValidatePreviousSpine(options.PreviousSpinePath, operations, diagnostics, options.AllowEmptyBaseline, options.RepositoryRoot);

string output = RenderOracle(operations, diagnostics, options);
Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath) ?? ".");
File.WriteAllText(options.OutputPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
return 0;

static IReadOnlyList<OperationModel> EnumerateOperations(YamlMappingNode root, List<Diagnostic> diagnostics)
{
    HashSet<string> pathItemReservedKeys = new(StringComparer.Ordinal)
    {
        "parameters", "summary", "description", "servers", "$ref",
    };
    HashSet<string> recognizedMethods = new(StringComparer.Ordinal)
    {
        "get", "post", "put", "patch", "delete",
    };
    HashSet<string> nonInventoryMethods = new(StringComparer.Ordinal)
    {
        "head", "options", "trace",
    };

    List<OperationModel> operations = [];
    Dictionary<string, OperationModel> routeIdentities = new(StringComparer.Ordinal);

    foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in RequiredMapping(root, "paths").Children)
    {
        if (pathEntry.Key is not YamlScalarNode pathKeyScalar)
        {
            throw new InvalidOperationException("prerequisite_drift: non-scalar key under OpenAPI paths.");
        }

        string? rawPath = pathKeyScalar.Value;
        if (rawPath is null || string.IsNullOrWhiteSpace(rawPath) || rawPath == "~")
        {
            throw new InvalidOperationException("prerequisite_drift: empty or null path key in OpenAPI paths.");
        }

        string path = rawPath;
        YamlMappingNode pathItem = pathEntry.Value.AsMapping($"path item '{path}'");
        IReadOnlyList<ParameterModel> pathParameters = EnumerateParameters(pathItem).ToArray();

        foreach (KeyValuePair<YamlNode, YamlNode> methodEntry in pathItem.Children)
        {
            string methodKey = methodEntry.Key.AsScalar("method").Value ?? string.Empty;
            string method = methodKey.ToLowerInvariant();

            // OpenAPI 3.x permits arbitrary vendor extensions (`x-*`) at the path-item level. Skip
            // them as metadata-only inputs rather than treating them as malformed HTTP methods.
            if (method.StartsWith("x-", StringComparison.Ordinal))
            {
                continue;
            }

            if (pathItemReservedKeys.Contains(method))
            {
                if (method == "$ref")
                {
                    throw new InvalidOperationException($"prerequisite_drift: path '{path}' uses path-item $ref which is not supported by this generator.");
                }
                continue;
            }

            if (nonInventoryMethods.Contains(method))
            {
                diagnostics.Add(new Diagnostic(
                    Level: "warning",
                    Category: "non_inventory_method",
                    OperationId: null,
                    SourcePointer: $"paths.{path}.{method}",
                    Message: $"HTTP method '{method}' is not part of the parity inventory and was skipped."));
                continue;
            }

            if (!recognizedMethods.Contains(method))
            {
                throw new InvalidOperationException($"prerequisite_drift: unsupported HTTP method '{methodKey}' at path '{path}'.");
            }

            YamlMappingNode operation = methodEntry.Value.AsMapping($"operation {method} {path}");
            string operationId = RequiredScalar(operation, "operationId");
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
            ValidateOperationIdShape(operationId);

            IReadOnlyList<ParameterModel> parameters = [.. pathParameters, .. EnumerateParameters(operation)];
            string[] tags = ReadStringSequence(operation, "tags").ToArray();
            string[] idempotencyFields = ReadStringSequence(operation, "x-hexalith-idempotency-equivalence").ToArray();
            string[] errorCategories = ReadStringSequence(operation, "x-hexalith-canonical-error-categories")
                .Select(NormalizeErrorCategory)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] auditKeys = ReadAuditMetadataKeys(operation)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string? readConsistency = ReadConsistencyClass(operation);
            bool hasIdempotencyKey = HasIdempotencyKey(parameters, operation);
            string correlationHeader = ReadNestedScalar(operation, "x-hexalith-correlation", "correlationHeader") ?? "X-Correlation-Id";
            AuthorizationMetadata? authorization = ReadAuthorization(operation);
            // Treat `x-hexalith-parity-dimensions: null` (or an explicit empty mapping) as undeclared so
            // the reference_pending diagnostic still fires; only a populated extension counts.
            bool hasParityDimensions = HasNonEmptyExtension(operation, "x-hexalith-parity-dimensions");

            OperationModel model = new(
                Method: method,
                Path: NormalizePath(path),
                OperationId: operationId,
                Tags: tags,
                Parameters: parameters,
                HasIdempotencyKey: hasIdempotencyKey,
                IdempotencyFields: idempotencyFields,
                ReadConsistencyClass: readConsistency,
                ErrorCategories: errorCategories,
                AuditMetadataKeys: auditKeys,
                CorrelationHeader: correlationHeader,
                Authorization: authorization,
                HasParityDimensions: hasParityDimensions);

            string routeKey = method + " " + NormalizePath(path);
            if (routeIdentities.TryGetValue(routeKey, out OperationModel? existing))
            {
                throw new InvalidOperationException(
                    $"prerequisite_drift: route '{routeKey}' bound to both operationId '{existing.OperationId}' and '{operationId}'.");
            }
            routeIdentities[routeKey] = model;
            operations.Add(model);
        }
    }

    return operations;
}

static void ValidateOperationInventory(IReadOnlyList<OperationModel> operations, List<Diagnostic> diagnostics)
{
    foreach (IGrouping<string, OperationModel> duplicate in operations.GroupBy(o => o.OperationId, StringComparer.Ordinal).Where(g => g.Count() > 1))
    {
        throw new InvalidOperationException($"prerequisite_drift: duplicate operationId '{duplicate.Key}'.");
    }

    foreach (OperationModel operation in operations)
    {
        if (operation.ErrorCategories.Count == 0)
        {
            throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} lacks x-hexalith-canonical-error-categories.");
        }

        if (operation.AuditMetadataKeys.Count == 0)
        {
            throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} lacks x-hexalith-audit-metadata-keys.");
        }

        foreach (string auditKey in operation.AuditMetadataKeys)
        {
            if (!AuditKeyPattern.IsMatch(auditKey))
            {
                throw new InvalidOperationException(
                    $"prerequisite_drift: operation {operation.OperationId} audit metadata key '{auditKey}' violates pattern ^[a-z][a-z0-9_]*$.");
            }
        }

        if (!operation.HasParityDimensions)
        {
            diagnostics.Add(new Diagnostic(
                Level: "reference_pending",
                Category: "missing_parity_dimensions",
                OperationId: operation.OperationId,
                SourcePointer: $"paths.{operation.Path}.{operation.Method}",
                Message: "x-hexalith-parity-dimensions not declared on operation; reference-pending until canonical metadata is supplied."));
        }

        if (operation.Authorization is null)
        {
            diagnostics.Add(new Diagnostic(
                Level: "reference_pending",
                Category: "missing_authorization_metadata",
                OperationId: operation.OperationId,
                SourcePointer: $"paths.{operation.Path}.{operation.Method}",
                Message: "x-hexalith-authorization not declared on operation; auth_outcome_class falls back to error-category inference."));
        }

        if (operation.IsMutatingCommand)
        {
            if (!operation.HasIdempotencyKey || operation.IdempotencyFields.Count == 0)
            {
                throw new InvalidOperationException($"prerequisite_drift: mutating operation {operation.OperationId} lacks required idempotency metadata.");
            }

            if (operation.IdempotencyFields.Distinct(StringComparer.Ordinal).Count() != operation.IdempotencyFields.Count)
            {
                throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} declares duplicate idempotency fields.");
            }

            if (!operation.IdempotencyFields.SequenceEqual(operation.IdempotencyFields.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"prerequisite_drift: operation {operation.OperationId} idempotency fields are not ordinal-sorted.");
            }
        }
        else
        {
            if (operation.HasIdempotencyKey || operation.IdempotencyFields.Count > 0)
            {
                throw new InvalidOperationException($"prerequisite_drift: non-mutating operation {operation.OperationId} accepts idempotency metadata.");
            }

            if (string.IsNullOrWhiteSpace(operation.ReadConsistencyClass))
            {
                throw new InvalidOperationException($"prerequisite_drift: non-mutating operation {operation.OperationId} lacks x-hexalith-read-consistency.");
            }
        }

        foreach (string category in operation.ErrorCategories)
        {
            if (!OutcomeMappings.TryGetValue(category, out _))
            {
                throw new InvalidOperationException(
                    $"prerequisite_drift: operation {operation.OperationId} declares canonical category '{category}' with no behavioral parity mapping. Update Adapter Outcome Parity rule table before emission.");
            }
        }
    }
}

static void ValidatePreviousSpine(string previousSpinePath, IReadOnlyList<OperationModel> currentOperations, List<Diagnostic> diagnostics, bool allowEmptyBaseline, string repositoryRoot)
{
    if (!File.Exists(previousSpinePath))
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml is missing.");
    }

    YamlMappingNode previous;
    try
    {
        previous = LoadYaml(previousSpinePath);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"prerequisite_drift: previous-spine.yaml is unparseable: {ex.Message}");
    }

    if (!previous.Children.TryGetValue(new YamlScalarNode("operations"), out YamlNode? operationsNode))
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml lacks operations.");
    }

    if (operationsNode is YamlScalarNode { Value: null or "" })
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml operations is null.");
    }

    YamlSequenceNode operationsSeq = operationsNode as YamlSequenceNode
        ?? throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml operations must be a sequence.");

    if (operationsSeq.Children.Count == 0)
    {
        if (!allowEmptyBaseline)
        {
            throw new InvalidOperationException(
                "prerequisite_drift: previous-spine.yaml has empty operations and --allow-empty-baseline was not passed. Run with --initialize-baseline to capture the current spine.");
        }

        diagnostics.Add(new Diagnostic(
            Level: "warning",
            Category: "empty_previous_spine_baseline",
            OperationId: null,
            SourcePointer: "tests/fixtures/previous-spine.yaml",
            Message: "Previous spine baseline is empty; symmetric drift detection is disabled until a captured baseline is committed."));
        return;
    }

    HashSet<string> previousOperationIds = new(StringComparer.Ordinal);
    foreach (YamlNode previousNode in operationsSeq)
    {
        if (previousNode is YamlMappingNode previousMapping
            && previousMapping.Children.TryGetValue(new YamlScalarNode("operation_id"), out YamlNode? idNode)
            && idNode is YamlScalarNode idScalar
            && !string.IsNullOrEmpty(idScalar.Value))
        {
            previousOperationIds.Add(idScalar.Value!);
        }
    }

    HashSet<string> currentIdentities = currentOperations.Select(o => o.Identity).ToHashSet(StringComparer.Ordinal);
    Dictionary<string, OperationModel> currentByRoute = currentOperations.ToDictionary(o => o.Method + " " + o.Path, o => o, StringComparer.Ordinal);
    Dictionary<string, OperationModel> currentByOperationId = currentOperations.ToDictionary(o => o.OperationId, o => o, StringComparer.Ordinal);

    foreach (YamlNode node in operationsSeq)
    {
        if (node is not YamlMappingNode operation)
        {
            throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml operation entry must be a mapping.");
        }

        string operationId = ReadFlexibleScalar(operation, "operation_id", "operationId");
        string method = ReadFlexibleScalar(operation, "method", "http_method").ToLowerInvariant();
        string path = NormalizePath(ReadFlexibleScalar(operation, "path", "normalized_path"));
        string identity = method + " " + path + " " + operationId;

        if (currentIdentities.Contains(identity))
        {
            continue;
        }

        if (HasApprovedDeprecation(operation, repositoryRoot))
        {
            continue;
        }

        bool routeMatches = currentByRoute.TryGetValue(method + " " + path, out OperationModel? renamed);
        bool operationIdMatches = currentByOperationId.TryGetValue(operationId, out OperationModel? moved);

        if (routeMatches && operationIdMatches)
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: previous operation '{operationId}' both renamed (route '{method} {path}' now binds '{renamed!.OperationId}') and moved (id '{operationId}' now at '{moved!.Method} {moved.Path}') without approved deprecation.");
        }

        if (routeMatches)
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: previous operationId '{operationId}' renamed to '{renamed!.OperationId}' at route '{method} {path}' without approved deprecation.");
        }

        if (operationIdMatches)
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: previous operation '{operationId}' moved from '{method} {path}' to '{moved!.Method} {moved.Path}' without approved deprecation.");
        }

        throw new InvalidOperationException(
            $"prerequisite_drift: previous operation removed without approved deprecation '{identity}'.");
    }

    // Additive-drift check. Only enforced when the baseline explicitly declares an
    // `approved_additions:` key — its presence is the opt-in marker that the baseline is treated as
    // a complete inventory. Synthetic test baselines that omit the key are not subject to this gate
    // (they typically list a single fixture operation rather than the full Contract Spine).
    if (!previous.Children.ContainsKey(new YamlScalarNode("approved_additions")))
    {
        return;
    }

    HashSet<string> approvedAdditions = ReadApprovedAdditions(previous);
    foreach (OperationModel current in currentOperations)
    {
        if (previousOperationIds.Contains(current.OperationId) || approvedAdditions.Contains(current.OperationId))
        {
            continue;
        }

        throw new InvalidOperationException(
            $"prerequisite_drift: current operationId '{current.OperationId}' is not in the previous-spine baseline and is not listed under approved_additions. "
            + "Either add the operationId to `approved_additions:` in tests/fixtures/previous-spine.yaml or rerun with --initialize-baseline after an intentional sweep.");
    }
}

static HashSet<string> ReadApprovedAdditions(YamlMappingNode previous)
{
    HashSet<string> approved = new(StringComparer.Ordinal);
    if (!previous.Children.TryGetValue(new YamlScalarNode("approved_additions"), out YamlNode? node))
    {
        return approved;
    }

    if (node is YamlScalarNode { Value: null or "" })
    {
        return approved;
    }

    if (node is not YamlSequenceNode sequence)
    {
        throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml approved_additions must be a sequence of operationIds.");
    }

    foreach (YamlNode entry in sequence)
    {
        if (entry is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
        {
            throw new InvalidOperationException("prerequisite_drift: previous-spine.yaml approved_additions entries must be non-empty scalar operationIds.");
        }

        approved.Add(scalar.Value!);
    }

    return approved;
}

static string RenderOracle(IReadOnlyList<OperationModel> operations, IReadOnlyList<Diagnostic> diagnostics, GeneratorOptions options)
{
    StringBuilder builder = new();
    builder.Append("# generated_by: tests/tools/parity-oracle-generator\n");
    builder.Append("# contract_spine_sha256: ").Append(Sha256OfFile(options.ContractPath)).Append('\n');
    builder.Append("# parity_schema_sha256: ").Append(Sha256OfFile(options.SchemaPath)).Append('\n');
    builder.Append("# source_authority: openapi-operation-metadata + idempotency-and-parity-rules + architecture-adapter-parity-contract\n");
    builder.Append("# diagnostics_count: ").Append(diagnostics.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');

    // Sort diagnostics deterministically. The final tiebreakers on SourcePointer + Message guarantee
    // byte-stable ordering even when two diagnostics share the (Level, Category, OperationId) triplet
    // — for example, two reference_pending entries with OperationId=null produced by enumeration of
    // a YAML mapping whose key order is not guaranteed.
    foreach (Diagnostic diagnostic in diagnostics
                 .OrderBy(d => d.Level, StringComparer.Ordinal)
                 .ThenBy(d => d.Category, StringComparer.Ordinal)
                 .ThenBy(d => d.OperationId ?? string.Empty, StringComparer.Ordinal)
                 .ThenBy(d => d.SourcePointer ?? string.Empty, StringComparer.Ordinal)
                 .ThenBy(d => d.Message, StringComparer.Ordinal))
    {
        builder.Append("# diagnostic: level=").Append(diagnostic.Level)
               .Append(" category=").Append(diagnostic.Category)
               .Append(" operation=").Append(diagnostic.OperationId ?? "-")
               .Append(" source=").Append(diagnostic.SourcePointer ?? "-")
               .Append(" message=").Append(SafeCommentText(diagnostic.Message))
               .Append('\n');
    }

    foreach (OperationModel operation in operations)
    {
        RenderRow(builder, operation);
    }

    return builder.ToString();
}

static void RenderRow(StringBuilder builder, OperationModel operation)
{
    string family = operation.OperationFamily;
    builder.Append("- operation_id: ").Append(Quote(operation.OperationId)).Append('\n');
    builder.Append("  operation_family: ").Append(Quote(family)).Append('\n');
    builder.Append("  read_consistency_class: ").Append(Quote(operation.IsMutatingCommand ? "not_applicable" : operation.ReadConsistencyClass!)).Append('\n');
    builder.Append("  transport_parity:\n");
    builder.Append("    auth_outcome_class: ").Append(Quote(AuthOutcomeClass(operation))).Append('\n');
    builder.Append("    error_code_set:\n");
    foreach (string category in operation.ErrorCategories)
    {
        builder.Append("      - ").Append(Quote(category)).Append('\n');
    }

    builder.Append("    idempotency_key_rule: ").Append(Quote(IdempotencyRule(operation))).Append('\n');
    builder.Append("    audit_metadata_keys:\n");
    foreach (string key in operation.AuditMetadataKeys)
    {
        builder.Append("      - ").Append(Quote(key)).Append('\n');
    }

    builder.Append("    correlation_field_path: ").Append(Quote("headers." + operation.CorrelationHeader)).Append('\n');
    builder.Append("    terminal_states:\n");
    foreach (string state in TerminalStates(operation).OrderBy(s => s, StringComparer.Ordinal))
    {
        builder.Append("      - ").Append(Quote(state)).Append('\n');
    }

    OutcomeMapping successOutcome = OutcomeMappings["success"];
    builder.Append("  behavioral_parity:\n");
    builder.Append("    pre_sdk_error_class: ").Append(Quote(successOutcome.PreSdkErrorClass)).Append('\n');
    builder.Append("    idempotency_key_sourcing: ").Append(Quote(IdempotencyKeySourcing(operation))).Append('\n');
    builder.Append("    correlation_id_sourcing: ").Append(Quote("caller_provided")).Append('\n');
    builder.Append("    task_id_sourcing: ").Append(Quote(TaskIdSourcing(operation))).Append('\n');
    builder.Append("    credential_sourcing: ").Append(Quote("sdk_configuration")).Append('\n');
    builder.Append("    cli_exit_code: ").Append(successOutcome.CliExitCode.ToString(CultureInfo.InvariantCulture)).Append('\n');
    builder.Append("    mcp_failure_kind: ").Append(Quote(successOutcome.McpFailureKind)).Append('\n');

    builder.Append("  outcome_mapping:\n");
    foreach (string category in operation.ErrorCategories)
    {
        OutcomeMapping mapping = OutcomeMappings[category];
        builder.Append("    - canonical_error_category: ").Append(Quote(category)).Append('\n');
        builder.Append("      cli_exit_code: ").Append(mapping.CliExitCode.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("      mcp_failure_kind: ").Append(Quote(mapping.McpFailureKind)).Append('\n');
        builder.Append("      pre_sdk_error_class: ").Append(Quote(mapping.PreSdkErrorClass)).Append('\n');
    }

    builder.Append("  adapter_expectations:\n");
    foreach (string adapter in AdapterExpectations(operation).OrderBy(a => a, StringComparer.Ordinal))
    {
        builder.Append("    - ").Append(Quote(adapter)).Append('\n');
    }

    builder.Append("  ownership:\n");
    builder.Append("    owner_workstream: ").Append(Quote("C13 parity oracle generation")).Append('\n');
    builder.Append("    future_test_use: ").Append(Quote("SDK, REST, CLI, and MCP parity tests consume this generated row.")).Append('\n');
    builder.Append("    known_omissions:\n");
    builder.Append("      - ").Append(Quote("Runtime adapter behavior is implemented by later Epic 5 stories.")).Append('\n');
    builder.Append("    mutation_rules:\n");
    builder.Append("      - ").Append(Quote("Regenerate from the Contract Spine; do not hand-edit operation rows.")).Append('\n');
    builder.Append("    non_policy_placeholder: true\n");
    builder.Append("    synthetic_data_only: true\n");
}

static string RenderBaseline(IReadOnlyList<OperationModel> operations)
{
    StringBuilder builder = new();
    builder.Append("version: captured-baseline\n");
    builder.Append("source_marker: captured-from-openapi\n");
    builder.Append("intent: First captured Contract Spine baseline for symmetric drift detection.\n");
    builder.Append("ownership:\n");
    builder.Append("  owner_workstream: Phase 1 Contract Spine and drift-detection stories\n");
    builder.Append("  future_test_use: Symmetric drift detection input for parity-oracle generation.\n");
    builder.Append("  known_omissions:\n");
    builder.Append("    - No request/response schema fingerprints\n");
    builder.Append("    - No status-code surface\n");
    builder.Append("  mutation_rules:\n");
    builder.Append("    - Replace via the generator's --initialize-baseline command after intentional contract changes.\n");
    builder.Append("    - Add explicit deprecation entries for intentionally removed or renamed operations.\n");
    builder.Append("    - Add new operationIds to `approved_additions:` to acknowledge a sanctioned addition.\n");
    builder.Append("  non_policy_placeholder: true\n");
    builder.Append("  synthetic_data_only: true\n");
    builder.Append("approved_additions: []\n");
    builder.Append("operations:\n");
    foreach (OperationModel operation in operations.OrderBy(o => o.OperationId, StringComparer.Ordinal))
    {
        builder.Append("  - operation_id: ").Append(Quote(operation.OperationId)).Append('\n');
        builder.Append("    method: ").Append(Quote(operation.Method)).Append('\n');
        builder.Append("    path: ").Append(Quote(operation.Path)).Append('\n');
    }

    return builder.ToString();
}

static string AuthOutcomeClass(OperationModel operation)
{
    // Audit-access-denied is its own bucket distinct from generic ACL denial.
    if (operation.ErrorCategories.Contains("audit_access_denied", StringComparer.Ordinal))
    {
        return "audit_access_denied";
    }

    // Folder ACL and tenant access denials are the canonical "authorized but denied" outcomes for
    // operations that reach the tenant + folder authorization layers. They MUST be checked before
    // credential-missing so an operation declaring both does not collapse to the wrong bucket.
    if (operation.ErrorCategories.Contains("folder_acl_denied", StringComparer.Ordinal))
    {
        return "folder_acl_denied";
    }

    if (operation.ErrorCategories.Contains("tenant_access_denied", StringComparer.Ordinal) ||
        operation.ErrorCategories.Contains("cross_tenant_access_denied", StringComparer.Ordinal))
    {
        return "tenant_access_denied";
    }

    // Credential-missing only when no authorization-layer denial category is declared. Note that
    // 'authentication_failure' (which most operations declare) is intentionally NOT routed here:
    // it represents a credential validity issue at the authentication layer, not the absence of a
    // credential reference, so collapsing it would mask folder/tenant authorization signal.
    if (operation.ErrorCategories.Contains("credential_missing", StringComparer.Ordinal) ||
        operation.ErrorCategories.Contains("credential_reference_invalid", StringComparer.Ordinal))
    {
        return "credential_missing";
    }

    return operation.ErrorCategories.Contains("not_found", StringComparer.Ordinal)
        ? "safe_not_found"
        : "tenant_authorized";
}

static string IdempotencyRule(OperationModel operation)
{
    if (!operation.IsMutatingCommand)
    {
        return "not_accepted_for_non_mutating_operation";
    }

    return operation.IdempotencyFields.Contains("operation_id", StringComparer.Ordinal)
        ? "required_with_operation_id"
        : "required_for_mutating_command";
}

static string IdempotencyKeySourcing(OperationModel operation) =>
    operation.IsMutatingCommand ? "caller_provided" : "not_accepted";

static string TaskIdSourcing(OperationModel operation)
{
    bool taskScoped =
        operation.Parameters.Any(p => p.Field == "task_id") ||
        operation.IdempotencyFields.Contains("task_id", StringComparer.Ordinal);
    return taskScoped ? "caller_provided" : "not_task_scoped";
}

static string[] TerminalStates(OperationModel operation)
{
    if (operation.IsMutatingCommand)
    {
        return ["accepted"];
    }

    return operation.OperationFamily switch
    {
        "context_query" => ["context_returned"],
        "audit" => ["audit_returned"],
        "operations_console_projection" => ["projection_returned"],
        _ => ["projected"],
    };
}

static string[] AdapterExpectations(OperationModel operation)
{
    List<string> adapters = ["rest", "sdk", "cli", "mcp"];
    if (operation.OperationFamily == "operations_console_projection")
    {
        adapters.Add("ui");
    }

    return adapters.ToArray();
}

static IEnumerable<ParameterModel> EnumerateParameters(YamlMappingNode mapping)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? parametersNode))
    {
        yield break;
    }

    foreach (YamlNode node in parametersNode.AsSequence("parameters"))
    {
        YamlMappingNode parameter = node.AsMapping("parameter");
        string name;
        if (parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? referenceNode))
        {
            string referencePath = referenceNode.AsScalar("$ref").Value ?? string.Empty;
            string referenceFragment = referencePath.Split('/').Last();
            name = StripParameterReferenceSuffix(referenceFragment);
        }
        else
        {
            name = RequiredScalar(parameter, "name");
        }

        yield return new ParameterModel(NormalizeName(name), name);
    }
}

static string StripParameterReferenceSuffix(string referenceFragment)
{
    // Strip only suffixes that match the canonical component-name convention
    // <Field><PascalCaseSuffix> where the prefix begins with an uppercase letter and the suffix
    // is a recognized parameter-location word. This protects legitimate parameter names that
    // happen to end in 'Header', 'Path', etc. (e.g. AuthorizationHeader → would be wrongly
    // stripped to Authorization).
    string[] suffixes = ["Header", "Parameter", "Query", "Path"];
    foreach (string suffix in suffixes)
    {
        if (!referenceFragment.EndsWith(suffix, StringComparison.Ordinal))
        {
            continue;
        }

        int prefixLength = referenceFragment.Length - suffix.Length;
        if (prefixLength <= 0)
        {
            continue;
        }

        // Only strip when the prefix is multi-character AND ends with a lowercase letter or digit
        // followed by the suffix's leading uppercase letter — i.e. the canonical
        // 'idempotencyKeyHeader' / 'TaskIdHeader' shape. A reference fragment that is itself just
        // 'AuthorizationHeader' (single concept) has only uppercase letters separated by the suffix
        // boundary and is left intact.
        char prevChar = referenceFragment[prefixLength - 1];
        if (char.IsLower(prevChar) || char.IsDigit(prevChar))
        {
            return referenceFragment[..prefixLength];
        }
    }

    return referenceFragment;
}

static bool HasIdempotencyKey(IReadOnlyList<ParameterModel> parameters, YamlMappingNode operation) =>
    parameters.Any(p => p.Field == "idempotency_key") ||
    HasNonEmptyExtension(operation, "x-hexalith-idempotency-key");

static bool HasNonEmptyExtension(YamlMappingNode operation, string key)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        return false;
    }

    return node switch
    {
        YamlScalarNode scalar => !string.IsNullOrEmpty(scalar.Value) && scalar.Value != "~",
        YamlMappingNode mapping => mapping.Children.Count > 0,
        YamlSequenceNode sequence => sequence.Children.Count > 0,
        _ => true,
    };
}

static string? ReadConsistencyClass(YamlMappingNode operation)
{
    string? raw = ReadNestedScalar(operation, "x-hexalith-read-consistency", "class");
    if (raw is null)
    {
        return null;
    }

    string normalized = raw.Replace("_", "-", StringComparison.Ordinal);
    if (normalized == "not-applicable")
    {
        normalized = "not_applicable";
    }
    return normalized;
}

static AuthorizationMetadata? ReadAuthorization(YamlMappingNode operation)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode("x-hexalith-authorization"), out YamlNode? node))
    {
        return null;
    }

    YamlMappingNode mapping = node.AsMapping("x-hexalith-authorization");
    string requirement = mapping.Children.TryGetValue(new YamlScalarNode("requirement"), out YamlNode? req)
        ? req.AsScalar("requirement").Value ?? string.Empty
        : string.Empty;
    string tenantAuthority = mapping.Children.TryGetValue(new YamlScalarNode("tenantAuthority"), out YamlNode? auth)
        ? auth.AsScalar("tenantAuthority").Value ?? string.Empty
        : string.Empty;
    string safeDenial = mapping.Children.TryGetValue(new YamlScalarNode("safeDenial"), out YamlNode? denial)
        ? denial.AsScalar("safeDenial").Value ?? string.Empty
        : string.Empty;

    return new AuthorizationMetadata(requirement, tenantAuthority, safeDenial);
}

static IEnumerable<string> ReadAuditMetadataKeys(YamlMappingNode operation)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode("x-hexalith-audit-metadata-keys"), out YamlNode? node))
    {
        yield break;
    }

    foreach (YamlNode item in node.AsSequence("x-hexalith-audit-metadata-keys"))
    {
        yield return RequiredScalar(item.AsMapping("audit metadata key"), "name");
    }
}

static IReadOnlyList<string> ReadStringSequence(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        return [];
    }

    return node.AsSequence(key).Children.Select(c => c.AsScalar(key).Value ?? string.Empty).ToArray();
}

static string? ReadNestedScalar(YamlMappingNode mapping, string parent, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(parent), out YamlNode? node))
    {
        return null;
    }

    YamlMappingNode parentMapping = node.AsMapping(parent);
    return parentMapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value)
        ? value.AsScalar(key).Value
        : null;
}

static string ReadFlexibleScalar(YamlMappingNode mapping, params string[] keys)
{
    foreach (string key in keys)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
        {
            return value.AsScalar(key).Value ?? string.Empty;
        }
    }

    throw new InvalidOperationException($"prerequisite_drift: previous-spine entry missing required scalar '{string.Join("|", keys)}'.");
}

static bool HasApprovedDeprecation(YamlMappingNode operation, string repositoryRoot)
{
    if (!operation.Children.TryGetValue(new YamlScalarNode("deprecation"), out YamlNode? deprecationNode))
    {
        return false;
    }

    if (!deprecationNode.AsMapping("deprecation").Children.TryGetValue(new YamlScalarNode("approved"), out YamlNode? approvedNode))
    {
        return false;
    }

    string raw = approvedNode.AsScalar("approved").Value ?? string.Empty;
    if (!YamlBooleanTrueLiterals.Contains(raw))
    {
        return false;
    }

    _ = ReadDeprecationEvidenceScalar(deprecationNode.AsMapping("deprecation"), "rationale");
    _ = ReadDeprecationEvidenceScalar(deprecationNode.AsMapping("deprecation"), "approval_reference");
    _ = ReadDeprecationEvidenceScalar(deprecationNode.AsMapping("deprecation"), "effective_date");
    string approvalSource = ReadDeprecationEvidenceScalar(deprecationNode.AsMapping("deprecation"), "approval_source");
    ValidateRepositoryRelativeApprovalSource(repositoryRoot, approvalSource);
    return true;
}

static string ReadDeprecationEvidenceScalar(YamlMappingNode deprecation, string key)
{
    if (!deprecation.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value))
    {
        throw new InvalidOperationException($"previous-spine-drift: approved deprecation is missing metadata field '{key}'.");
    }

    string scalar = value.AsScalar(key).Value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(scalar))
    {
        throw new InvalidOperationException($"previous-spine-drift: approved deprecation metadata field '{key}' must be a non-empty scalar.");
    }

    if (scalar.Contains('\r') || scalar.Contains('\n'))
    {
        throw new InvalidOperationException($"previous-spine-drift: approved deprecation metadata field '{key}' must be a single-line scalar.");
    }

    return scalar;
}

static void ValidateRepositoryRelativeApprovalSource(string repositoryRoot, string approvalSource)
{
    if (Path.IsPathFullyQualified(approvalSource) || approvalSource.Contains("://", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("previous-spine-drift: approved deprecation approval_source must be a repository-relative path.");
    }

    string normalizedRoot = Path.GetFullPath(repositoryRoot);
    string fullSource = Path.GetFullPath(Path.Combine(normalizedRoot, approvalSource));
    string rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
        ? normalizedRoot
        : normalizedRoot + Path.DirectorySeparatorChar;
    if (!fullSource.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("previous-spine-drift: approved deprecation approval_source escapes the repository root.");
    }

    if (!File.Exists(fullSource))
    {
        throw new InvalidOperationException($"previous-spine-drift: approved deprecation approval_source does not exist: {approvalSource}");
    }
}

static string NormalizeErrorCategory(string category) => category switch
{
    // Removed silent rewrite for provider_outcome_unknown → unknown_provider_outcome (Story 1.13 P-26).
    // Generator now requires the OpenAPI Contract Spine to use the canonical 'unknown_provider_outcome' name.
    _ => category,
};

static string NormalizePath(string path)
{
    string trimmed = path.Trim();
    if (trimmed.Length > 1 && trimmed.EndsWith('/'))
    {
        trimmed = trimmed[..^1];
    }
    return trimmed;
}

static string NormalizeName(string value)
{
    StringBuilder builder = new(value.Length);
    for (int i = 0; i < value.Length; i++)
    {
        char c = value[i];
        if (c is '-' or '_')
        {
            if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }
        else if (char.IsLetterOrDigit(c))
        {
            if (char.IsUpper(c) && i > 0 && builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }
    }

    return builder.ToString() switch
    {
        "x_hexalith_task_id" => "task_id",
        string normalized => normalized,
    };
}

static void ValidateOperationIdShape(string operationId)
{
    if (!OperationIdPattern.IsMatch(operationId))
    {
        throw new InvalidOperationException(
            $"prerequisite_drift: operationId '{operationId}' violates pattern ^[A-Z][A-Za-z0-9]*$.");
    }
}

static string Quote(string value)
{
    for (int i = 0; i < value.Length; i++)
    {
        char c = value[i];
        if (char.IsControl(c) && c != '\t')
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: value contains control character (codepoint {(int)c:X4}); cannot emit safely.");
        }

        // U+2028 (LINE SEPARATOR) and U+2029 (PARAGRAPH SEPARATOR) are NOT classified as control
        // characters by char.IsControl but ARE line terminators in some JSON/YAML consumers. Reject
        // them up front so byte-stable output cannot smuggle line breaks into a single-line scalar.
        if (c == '\u2028' || c == '\u2029')
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: value contains line/paragraph separator (codepoint {(int)c:X4}); cannot emit safely.");
        }

        if (char.IsHighSurrogate(c))
        {
            if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
            {
                throw new InvalidOperationException(
                    $"prerequisite_drift: value contains lone high surrogate (codepoint {(int)c:X4}); cannot emit safely.");
            }

            // Skip the well-formed low surrogate; the next loop iteration will land on the char
            // following the pair.
            i++;
            continue;
        }

        if (char.IsLowSurrogate(c))
        {
            throw new InvalidOperationException(
                $"prerequisite_drift: value contains lone low surrogate (codepoint {(int)c:X4}); cannot emit safely.");
        }
    }

    return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}

static string SafeCommentText(string value) =>
    value
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('#', ' ');

static YamlMappingNode LoadYaml(string path)
{
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"prerequisite_drift: required input file is missing: {path}");
    }

    // Read bytes once, then strip an optional UTF-8 BOM before handing the text to YamlDotNet.
    // YamlDotNet does not normalize BOM and a BOM-prefixed root node sometimes loads as an
    // unexpected scalar — failing closed early is cleaner than producing a malformed tree.
    byte[] bytes = File.ReadAllBytes(path);
    int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
    string text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

    YamlStream yaml = new();
    using (StringReader reader = new(text))
    {
        yaml.Load(reader);
    }

    if (yaml.Documents.Count == 0)
    {
        throw new InvalidOperationException($"prerequisite_drift: YAML input '{path}' contains no documents.");
    }

    if (yaml.Documents.Count > 1)
    {
        throw new InvalidOperationException(
            $"prerequisite_drift: YAML input '{path}' contains multiple documents; generator requires a single document per file.");
    }

    return yaml.Documents[0].RootNode.AsMapping("root");
}

static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing mapping '{key}'.");
    }

    return node.AsMapping(key);
}

static string RequiredScalar(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing scalar '{key}'.");
    }

    return node.AsScalar(key).Value ?? string.Empty;
}

static string Sha256(string value) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

// Read the file's UTF-8 bytes (BOM-stripped) and normalize line endings to LF before hashing.
// Using one read avoids TOCTOU between LoadYaml() and the provenance hash, and BOM stripping keeps
// the hash stable across machines whose editors insert/preserve BOM differently.
static string Sha256OfFile(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    int offset = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
    string normalized = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset).NormalizeLineEndings();
    return Sha256(normalized);
}

#pragma warning disable SA1402 // multiple types in file (single-file tool)
internal sealed record GeneratorOptions(
    string RepositoryRoot,
    string ContractPath,
    string SchemaPath,
    string PreviousSpinePath,
    string OutputPath,
    bool InitializeBaseline,
    bool AllowEmptyBaseline)
{
    public static GeneratorOptions Parse(string[] args)
    {
        Dictionary<string, string> parsed = new(StringComparer.Ordinal);
        HashSet<string> flags = new(StringComparer.Ordinal);

        int i = 0;
        while (i < args.Length)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected positional argument '{token}' at position {i}; arguments must be --key value pairs or --flag.");
            }

            if (token is "--help" or "--initialize-baseline" or "--allow-empty-baseline")
            {
                if (!flags.Add(token))
                {
                    throw new ArgumentException($"Flag '{token}' supplied more than once.");
                }
                i += 1;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for argument '{token}'.");
            }

            string value = args[i + 1];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for argument '{token}'; got another flag '{value}'.");
            }

            if (parsed.ContainsKey(token))
            {
                throw new ArgumentException($"Argument '{token}' supplied more than once.");
            }

            parsed[token] = value;
            i += 2;
        }

        if (flags.Contains("--help"))
        {
            Console.WriteLine("Usage: parity-oracle-generator [--repository-root <path>] [--contract <path>] [--schema <path>] [--previous-spine <path>] [--output <path>] [--initialize-baseline] [--allow-empty-baseline]");
            Environment.Exit(0);
        }

        // Avoid eager LocateRepositoryRoot() evaluation when the caller supplied --repository-root.
        // Dictionary.GetValueOrDefault would evaluate the default expression even on hit.
        string repositoryRoot = Path.GetFullPath(parsed.TryGetValue("--repository-root", out string? rootArg) ? rootArg : LocateRepositoryRoot());
        return new GeneratorOptions(
            RepositoryRoot: repositoryRoot,
            ContractPath: Path.GetFullPath(parsed.GetValueOrDefault("--contract", Path.Combine(repositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml"))),
            SchemaPath: Path.GetFullPath(parsed.GetValueOrDefault("--schema", Path.Combine(repositoryRoot, "tests", "fixtures", "parity-contract.schema.json"))),
            PreviousSpinePath: Path.GetFullPath(parsed.GetValueOrDefault("--previous-spine", Path.Combine(repositoryRoot, "tests", "fixtures", "previous-spine.yaml"))),
            OutputPath: Path.GetFullPath(parsed.GetValueOrDefault("--output", Path.Combine(repositoryRoot, "tests", "fixtures", "parity-contract.yaml"))),
            InitializeBaseline: flags.Contains("--initialize-baseline"),
            AllowEmptyBaseline: flags.Contains("--allow-empty-baseline"));
    }

    private static string LocateRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException(
            "prerequisite_drift: could not locate repository root (Hexalith.Folders.slnx not found). Pass --repository-root explicitly.");
    }
}

internal sealed record OperationModel(
    string Method,
    string Path,
    string OperationId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ParameterModel> Parameters,
    bool HasIdempotencyKey,
    IReadOnlyList<string> IdempotencyFields,
    string? ReadConsistencyClass,
    IReadOnlyList<string> ErrorCategories,
    IReadOnlyList<string> AuditMetadataKeys,
    string CorrelationHeader,
    AuthorizationMetadata? Authorization,
    bool HasParityDimensions)
{
    public string Identity => Method + " " + Path + " " + OperationId;

    public bool IsMutatingCommand => IdempotencyFields.Count > 0;

    public string OperationFamily
    {
        get
        {
            if (IsMutatingCommand)
            {
                return "mutating_command";
            }

            if (Tags.Contains("context-queries", StringComparer.Ordinal))
            {
                return "context_query";
            }

            if (Tags.Contains("audit", StringComparer.Ordinal))
            {
                return "audit";
            }

            if (Tags.Contains("ops-console", StringComparer.Ordinal))
            {
                return "operations_console_projection";
            }

            return "query_status";
        }
    }
}

internal sealed record ParameterModel(string Field, string Name);
internal sealed record AuthorizationMetadata(string Requirement, string TenantAuthority, string SafeDenial);
internal sealed record Diagnostic(string Level, string Category, string? OperationId, string? SourcePointer, string Message);
internal sealed record OutcomeMapping(int CliExitCode, string McpFailureKind, string PreSdkErrorClass);

internal static class TextExtensions
{
    public static string NormalizeLineEndings(this string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}

internal static class YamlExtensions
{
    public static YamlMappingNode AsMapping(this YamlNode node, string name) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{name} must be a mapping.");

    public static YamlSequenceNode AsSequence(this YamlNode node, string name) =>
        node as YamlSequenceNode ?? throw new InvalidOperationException($"{name} must be a sequence.");

    public static YamlScalarNode AsScalar(this YamlNode node, string name) =>
        node as YamlScalarNode ?? throw new InvalidOperationException($"{name} must be a scalar.");
}

internal static class GeneratorConstants
{
    public static readonly Regex OperationIdPattern = new("^[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);
    public static readonly Regex AuditKeyPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    // YAML 1.2 recognizes ONLY the literal scalars true/false (case-insensitive). The legacy YAML 1.1
    // aliases (yes/no/on/off/y/n/1/0) are explicitly excluded because previous-spine deprecation entries
    // are a fail-closed drift guard; a stray `approved: 1` (typically a misalignment with a numeric
    // workspace identifier) must NOT silently bless an operation removal. 'yes' is grandfathered for
    // backwards compatibility with already-committed baseline fixtures and the focused test that asserts
    // it; new authoring should use the YAML 1.2 'true' literal.
    public static readonly HashSet<string> YamlBooleanTrueLiterals = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "yes",
    };

    // Adapter Outcome Parity table from docs/contract/idempotency-and-parity-rules.md (Story 1.5).
    // Extended with bounded inference for canonical_error categories beyond the 15 representative
    // outcomes so generation does not fail closed on every operation. Updates require schema-bounded
    // enum changes and a focused-test addition.
    public static readonly IReadOnlyDictionary<string, OutcomeMapping> OutcomeMappings =
        new Dictionary<string, OutcomeMapping>(StringComparer.Ordinal)
        {
            ["success"] = new(0, "none", "none"),
            ["authentication_failure"] = new(65, "authentication_failure", "credential_missing"),
            ["client_configuration_error"] = new(64, "usage_error", "client_configuration_error"),
            ["credential_missing"] = new(65, "credential_missing", "credential_missing"),
            ["credential_reference_missing"] = new(65, "credential_missing", "credential_missing"),
            ["credential_reference_invalid"] = new(65, "credential_reference_invalid", "credential_missing"),
            ["tenant_access_denied"] = new(66, "tenant_access_denied", "none"),
            ["cross_tenant_access_denied"] = new(66, "cross_tenant_access_denied", "none"),
            ["folder_acl_denied"] = new(66, "folder_acl_denied", "none"),
            ["audit_access_denied"] = new(66, "audit_access_denied", "none"),
            ["validation_error"] = new(69, "validation_error", "none"),
            ["workspace_locked"] = new(67, "workspace_locked", "none"),
            ["lock_conflict"] = new(67, "lock_conflict", "none"),
            ["lock_expired"] = new(67, "lock_expired", "none"),
            ["lock_not_owned"] = new(67, "lock_not_owned", "none"),
            ["stale_workspace"] = new(67, "stale_workspace", "none"),
            ["authorization_revocation_detected"] = new(73, "authorization_revocation_detected", "none"),
            ["workspace_not_ready"] = new(72, "workspace_not_ready", "none"),
            ["workspace_preparation_failed"] = new(72, "workspace_preparation_failed", "none"),
            ["dirty_workspace"] = new(72, "dirty_workspace", "none"),
            ["commit_failed"] = new(70, "commit_failed", "none"),
            ["file_operation_failed"] = new(70, "file_operation_failed", "none"),
            ["path_validation_failed"] = new(69, "path_validation_failed", "none"),
            ["idempotency_conflict"] = new(68, "idempotency_conflict", "none"),
            ["input_limit_exceeded"] = new(69, "input_limit_exceeded", "none"),
            ["response_limit_exceeded"] = new(69, "response_limit_exceeded", "none"),
            ["query_timeout"] = new(1, "query_timeout", "none"),
            ["provider_readiness_failed"] = new(70, "provider_readiness_failed", "none"),
            ["provider_failure_known"] = new(70, "provider_failure_known", "none"),
            ["unknown_provider_outcome"] = new(71, "unknown_provider_outcome", "none"),
            ["provider_permission_insufficient"] = new(70, "provider_permission_insufficient", "none"),
            ["provider_unavailable"] = new(70, "provider_unavailable", "none"),
            ["provider_rate_limited"] = new(70, "provider_rate_limited", "none"),
            ["unsupported_provider_capability"] = new(70, "unsupported_provider_capability", "none"),
            ["repository_binding_unavailable"] = new(70, "repository_binding_unavailable", "none"),
            ["branch_ref_policy_invalid"] = new(69, "branch_ref_policy_invalid", "none"),
            ["repository_conflict"] = new(70, "repository_conflict", "none"),
            ["duplicate_binding"] = new(70, "duplicate_binding", "none"),
            ["reconciliation_required"] = new(72, "reconciliation_required", "none"),
            ["not_found"] = new(73, "not_found", "none"),
            ["state_transition_invalid"] = new(74, "state_transition_invalid", "none"),
            ["read_model_unavailable"] = new(72, "read_model_unavailable", "none"),
            ["projection_stale"] = new(72, "projection_stale", "none"),
            ["projection_unavailable"] = new(72, "projection_unavailable", "none"),
            ["range_unsatisfiable"] = new(69, "range_unsatisfiable", "none"),
            ["failed_operation"] = new(70, "failed_operation", "none"),
            ["redacted"] = new(75, "redacted", "none"),
            ["internal_error"] = new(1, "internal_error", "none"),
        };
}
#pragma warning restore SA1402
