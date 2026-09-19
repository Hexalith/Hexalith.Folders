using System.Text;
using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

string repositoryRoot = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
string sourcePath = Path.Combine(repositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
string outputPath = Path.Combine(repositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");
string matrixPath = Path.Combine(repositoryRoot, "docs", "contract", "authorization-matrix.md");

YamlMappingNode root = LoadMapping(sourcePath);
IReadOnlyDictionary<string, AuthorizationRow> rows = LoadAuthorizationRows(matrixPath);

SetScalar(RequiredMapping(root, "info"), "version", "v2");
SetScalar(RequiredMapping(root, "info"), "summary", "PD10 v2 authorization Contract Spine candidate for tenant-scoped repository-backed folder workflows.");
SetScalar(
    RequiredMapping(root, "info"),
    "description",
    "Generated PD10 v2 candidate. Every protected operation authenticates and establishes fresh authority before protected observation. This candidate is not production-routed until A6b, Section 9, and A8 are accepted.");

YamlSequenceNode servers = RequiredSequence(root, "servers");
SetScalar(AsMapping(servers.Children[0], "servers[0]"), "url", "/api/v2");

YamlMappingNode paths = RequiredMapping(root, "paths");
YamlMappingNode v2Paths = new();
HashSet<string> operationIds = new(StringComparer.Ordinal);
foreach (KeyValuePair<YamlNode, YamlNode> pathEntry in paths.Children)
{
    string sourceRoute = RequiredScalar(pathEntry.Key, "route");
    YamlMappingNode pathItem = AsMapping(pathEntry.Value, sourceRoute);
    YamlMappingNode[] operations = FindOperations(pathItem).ToArray();
    string[] pathOperationIds = operations.Select(GetOperationId).ToArray();
    string route = pathOperationIds.SingleOrDefault(operationId => operationId is "GetTaskStatus" or "GetReadinessDiagnostics" or "GetProjectionFreshness") switch
    {
        "GetTaskStatus" => "/api/v2/folders/{folderId}/tasks/{taskId}/status",
        "GetReadinessDiagnostics" => "/api/v2/folders/{folderId}/ops-console/readiness-diagnostics",
        "GetProjectionFreshness" => "/api/v2/folders/{folderId}/ops-console/projection-freshness",
        _ => sourceRoute.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal),
    };

    foreach (YamlMappingNode operation in operations)
    {
        string operationId = GetOperationId(operation);
        if (!rows.TryGetValue(operationId, out AuthorizationRow? row))
        {
            throw new InvalidOperationException($"Authorization matrix has no operation row for '{operationId}'.");
        }

        if (!operationIds.Add(operationId))
        {
            throw new InvalidOperationException($"Duplicate operationId '{operationId}'.");
        }

        MutateOperation(operation, operationId, row, route);
    }
    v2Paths.Add(route, pathItem);
}

if (operationIds.Count != 49 || rows.Count != 49 || !operationIds.SetEquals(rows.Keys))
{
    throw new InvalidOperationException($"PD10 requires exactly 49 identical operation identities; contract={operationIds.Count}, matrix={rows.Count}.");
}

root.Children[new YamlScalarNode("paths")] = v2Paths;
MutateComponents(RequiredMapping(root, "components"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using StreamWriter writer = new(outputPath, false, new UTF8Encoding(false)) { NewLine = "\n" };
new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);

static void MutateOperation(YamlMappingNode operation, string operationId, AuthorizationRow row, string route)
{
    YamlMappingNode responses = RequiredMapping(operation, "responses");
    responses.Children.Remove(new YamlScalarNode("403"));
    responses.Children[new YamlScalarNode("401")] = Ref("#/components/responses/AuthenticationFailure401");
    responses.Children[new YamlScalarNode("404")] = Ref("#/components/responses/SafeDenial404");
    responses.Children[new YamlScalarNode("503")] = Ref("#/components/responses/ProtectedOperationUnavailable503");

    YamlSequenceNode categories = RequiredSequence(operation, "x-hexalith-canonical-error-categories");
    string[] sourceCategories = categories.Children
        .Select(node => RequiredScalar(node, $"{operationId}.category"))
        .ToArray();
    IEnumerable<string> normalizedCategorySequence = sourceCategories
        .Where(category => category is not "not_found" and not "cross_tenant_access_denied" and not "audit_access_denied")
        .Append("authentication_failure")
        .Append("tenant_access_denied")
        .Append("read_model_unavailable");
    if (sourceCategories.Contains("idempotency_conflict", StringComparer.Ordinal))
    {
        normalizedCategorySequence = normalizedCategorySequence.Append("concurrency_conflict");
    }

    string[] normalizedCategories = normalizedCategorySequence
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
    operation.Children[new YamlScalarNode("x-hexalith-canonical-error-categories")] = Sequence(normalizedCategories);

    YamlMappingNode authorization = RequiredMapping(operation, "x-hexalith-authorization");
    SetScalar(authorization, "candidateVersion", "2.0.0");
    SetScalar(authorization, "operationFamily", row.Family);
    authorization.Children[new YamlScalarNode("requiredScopes")] = Sequence(row.RequiredScopes);
    authorization.Children[new YamlScalarNode("notApplicableScopes")] = Sequence(row.NotApplicableScopes);
    authorization.Children[new YamlScalarNode("evaluationOrder")] = Sequence(
    [
        "authentication",
        "authority_evidence_availability",
        "tenant_access",
        "principal_and_delegation_intersection",
        "folder_acl_allow",
        "family_grant",
        "resource_scope_binding",
        "freshness_revalidation",
        "observation",
    ]);
    SetScalar(authorization, "freshNegativeOutcome", "safe-denial-404");
    SetScalar(authorization, "unusableAuthorityOutcome", "authority-unavailable-503");
    SetScalar(authorization, "observationBoundary", "No lookup, count, filter, provider call, content read, audit access, task access, or search egress occurs before authorization succeeds.");

    if (operationId is "GetTaskStatus" or "GetReadinessDiagnostics" or "GetProjectionFreshness")
    {
        EnsureParameterReference(operation, "#/components/parameters/FolderId", insertFirst: true);
    }

    if (operationId == "GetEffectivePermissions")
    {
        SetScalar(authorization, "requirement", "fresh tenant authority and folder read grant");
    }
    else if (operationId == "ListFolderAclEntries")
    {
        SetScalar(authorization, "requirement", "fresh tenant authority and folder administer grant before ACL observation");
    }
    else if (operationId == "ValidateProviderReadiness")
    {
        SetScalar(authorization, "requirement", "fresh tenant authority and tenant folder-create permission before provider observation");
    }
    else if (operationId == "GetTaskStatus")
    {
        SetScalar(authorization, "requirement", "fresh tenant authority and bound-folder read grant before task lookup; task must bind to the route folder");
        SetScalar(authorization, "taskBinding", "task.folderId == route.folderId");
    }
    else if (operationId is "GetReadinessDiagnostics" or "GetProjectionFreshness")
    {
        SetScalar(authorization, "requirement", "fresh tenant authority, folder read grant, operator diagnostic scope, and audience partition before diagnostic lookup");
    }

    SetScalar(operation, "x-hexalith-candidate-route", route);
}

static void MutateComponents(YamlMappingNode components)
{
    YamlMappingNode responses = RequiredMapping(components, "responses");
    responses.Children.Remove(new YamlScalarNode("SafeAuthorizationDenial403"));
    responses.Children[new YamlScalarNode("AuthenticationFailure401")] = Response(
        "Canonical unauthenticated response evaluated before protected lookup.",
        "#/components/schemas/AuthenticationFailureProblem",
        "#/components/examples/SafeDenial401Unauthorized");
    responses.Children[new YamlScalarNode("SafeDenial404")] = Response(
        "Byte-equivalent non-enumerating response for every fresh negative authority fact.",
        "#/components/schemas/SafeDenialProblem",
        "#/components/examples/SafeDenial404NotFound");
    responses.Children[new YamlScalarNode("ProtectedOperationUnavailable503")] = Response(
        "Protected operation unavailability. Authority-evidence failure uses the exact non-disclosing authority-unavailable branch.",
        "#/components/schemas/AuthorityUnavailableProblem",
        "#/components/examples/AuthorityUnavailable");

    YamlMappingNode examples = RequiredMapping(components, "examples");
    examples.Children.Remove(new YamlScalarNode("SafeDenial403Forbidden"));
    examples.Children[new YamlScalarNode("AuthorityUnavailable")] = Example(
        503,
        "read_model_unavailable",
        "projection_unavailable",
        "Authorization evidence is temporarily unavailable.",
        true,
        "retry",
        "redacted");
    NormalizeProblemExamples(examples);

    YamlMappingNode schemas = RequiredMapping(components, "schemas");
    YamlMappingNode problem = RequiredMapping(schemas, "ProblemDetails");
    SetScalar(problem, "additionalProperties", "false");
    YamlMappingNode properties = RequiredMapping(problem, "properties");
    CloseDetailsSchema(RequiredMapping(properties, "details"), examples);

    schemas.Children[new YamlScalarNode("AuthenticationFailureProblem")] = ExactProblemSchema(
        401, "authentication_failure", "authentication_required", false, "check_credentials", "redacted");
    schemas.Children[new YamlScalarNode("SafeDenialProblem")] = ExactProblemSchema(
        404, "tenant_access_denied", "resource_unavailable", false, "no_action", "redacted");
    schemas.Children[new YamlScalarNode("AuthorityUnavailableProblem")] = ExactProblemSchema(
        503, "read_model_unavailable", "projection_unavailable", true, "retry", "redacted");

    ReplaceEnum(
        RequiredMapping(schemas, "CanonicalErrorCategory"),
        remove: ["cross_tenant_access_denied", "audit_access_denied", "not_found"],
        add: ["concurrency_conflict"]);
    ReplaceEnum(RequiredMapping(schemas, "CliExitCode"), remove: [], add: ["77"]);
    ReplaceEnum(
        RequiredMapping(schemas, "McpFailureKind"),
        remove: ["cross_tenant_access_denied", "audit_access_denied", "not_found"],
        add: ["concurrency_conflict"]);
}

static void CloseDetailsSchema(YamlMappingNode details, YamlMappingNode examples)
{
    SortedSet<string> keys = new(StringComparer.Ordinal) { "visibility" };
    CollectDetailsKeys(examples, keys);
    SetScalar(details, "additionalProperties", "false");
    details.Children[new YamlScalarNode("required")] = Sequence(["visibility"]);
    YamlMappingNode properties = new();
    foreach (string key in keys)
    {
        properties.Add(key, key == "visibility"
            ? EnumSchema("redacted", "metadata_only", "withheld", "unavailable", "absent")
            : new YamlMappingNode("description", "Bounded metadata-only detail field."));
    }

    details.Children[new YamlScalarNode("properties")] = properties;
}

static void CollectDetailsKeys(YamlNode node, ISet<string> keys)
{
    if (node is YamlMappingNode mapping)
    {
        foreach (KeyValuePair<YamlNode, YamlNode> child in mapping.Children)
        {
            string key = child.Key is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
            if (key == "details" && child.Value is YamlMappingNode details)
            {
                foreach (YamlNode detailKey in details.Children.Keys)
                {
                    keys.Add(RequiredScalar(detailKey, "details key"));
                }
            }
            CollectDetailsKeys(child.Value, keys);
        }
    }
    else if (node is YamlSequenceNode sequence)
    {
        foreach (YamlNode child in sequence.Children)
        {
            CollectDetailsKeys(child, keys);
        }
    }
}

static void NormalizeProblemExamples(YamlNode node)
{
    if (node is YamlMappingNode mapping)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode("category"), out YamlNode? categoryNode)
            && mapping.Children.TryGetValue(new YamlScalarNode("details"), out YamlNode? detailsNode)
            && detailsNode is YamlMappingNode details)
        {
            string category = RequiredScalar(categoryNode, "category");
            if (category is "cross_tenant_access_denied" or "audit_access_denied" or "not_found")
            {
                SetScalar(mapping, "status", "404");
                SetScalar(mapping, "category", "tenant_access_denied");
                SetScalar(mapping, "code", "resource_unavailable");
                SetScalar(mapping, "message", "The requested resource is unavailable.");
                SetScalar(mapping, "retryable", "false");
                SetScalar(mapping, "clientAction", "no_action");
                SetScalar(details, "visibility", "redacted");
            }

            mapping.Children.Remove(new YamlScalarNode("visibility"));
        }

        foreach (YamlNode child in mapping.Children.Values.ToArray())
        {
            NormalizeProblemExamples(child);
        }
    }
    else if (node is YamlSequenceNode sequence)
    {
        foreach (YamlNode child in sequence.Children)
        {
            NormalizeProblemExamples(child);
        }
    }
}

static YamlMappingNode ExactProblemSchema(int status, string category, string code, bool retryable, string clientAction, string visibility) =>
    new(
        "$ref", "#/components/schemas/ProblemDetails",
        "x-hexalith-exact-envelope", new YamlMappingNode(
            "status", ConstSchema(status.ToString()),
            "category", ConstSchema(category),
            "code", ConstSchema(code),
            "retryable", ConstSchema(retryable ? "true" : "false"),
            "clientAction", ConstSchema(clientAction),
            "details", new YamlMappingNode(
                "type", "object",
                "properties", new YamlMappingNode("visibility", ConstSchema(visibility)))));

static YamlMappingNode ConstSchema(string value) => new("const", value);

static YamlMappingNode EnumSchema(params string[] values) => new("type", "string", "enum", Sequence(values));

static YamlMappingNode Example(int status, string category, string code, string message, bool retryable, string clientAction, string visibility) =>
    new(
        "value", new YamlMappingNode(
            "type", "about:blank",
            "title", status == 401 ? "Authentication required" : status == 404 ? "Access unavailable" : "Authorization evidence unavailable",
            "status", status.ToString(),
            "category", category,
            "code", code,
            "message", message,
            "correlationId", "opaque_01HZY7Z6N7J4Q2X8Y9V0A1B2C3",
            "retryable", retryable ? "true" : "false",
            "clientAction", clientAction,
            "details", new YamlMappingNode("visibility", visibility)));

static YamlMappingNode Response(string description, string schemaRef, string exampleRef) =>
    new(
        "description", description,
        "content", new YamlMappingNode(
            "application/problem+json", new YamlMappingNode(
                "schema", Ref(schemaRef),
                "examples", new YamlMappingNode("canonical", Ref(exampleRef)))));

static void ReplaceEnum(YamlMappingNode schema, IReadOnlyCollection<string> remove, IReadOnlyCollection<string> add)
{
    YamlSequenceNode values = RequiredSequence(schema, "enum");
    string[] replaced = values.Children
        .Select(node => RequiredScalar(node, "enum value"))
        .Where(value => !remove.Contains(value, StringComparer.Ordinal))
        .Concat(add)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    schema.Children[new YamlScalarNode("enum")] = Sequence(replaced);
}

static void EnsureParameterReference(YamlMappingNode operation, string reference, bool insertFirst)
{
    YamlSequenceNode parameters;
    if (!operation.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? node))
    {
        parameters = new YamlSequenceNode();
        operation.Add("parameters", parameters);
    }
    else
    {
        parameters = AsSequence(node, "parameters");
    }

    if (parameters.Children.OfType<YamlMappingNode>().Any(parameter =>
        parameter.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? refNode)
        && RequiredScalar(refNode, "$ref") == reference))
    {
        return;
    }

    if (insertFirst)
    {
        parameters.Children.Insert(0, Ref(reference));
    }
    else
    {
        parameters.Add(Ref(reference));
    }
}

static IReadOnlyDictionary<string, AuthorizationRow> LoadAuthorizationRows(string path)
{
    Dictionary<string, AuthorizationRow> rows = new(StringComparer.Ordinal);
    Regex tokenPattern = new("`([^`]+)`", RegexOptions.CultureInvariant);
    foreach (string line in File.ReadLines(path))
    {
        if (!line.StartsWith("| `", StringComparison.Ordinal))
        {
            continue;
        }

        string[] cells = line.Split('|', StringSplitOptions.TrimEntries);
        if (cells.Length < 7 || !Regex.IsMatch(cells[2], "^(GET|POST|PUT|PATCH|DELETE)$", RegexOptions.CultureInvariant))
        {
            continue;
        }

        string operationId = Unquote(cells[1]);
        string family = Unquote(cells[4]);
        string[] required = tokenPattern.Matches(cells[5]).Select(match => match.Groups[1].Value).ToArray();
        string[] notApplicable = cells[6] == "none"
            ? []
            : tokenPattern.Matches(cells[6]).Select(match => match.Groups[1].Value).ToArray();
        rows.Add(operationId, new AuthorizationRow(family, required, notApplicable));
    }

    return rows;
}

static string Unquote(string value) => value.Trim().Trim('`');

static string GetOperationId(YamlMappingNode operation)
{
    return operation.Children.TryGetValue(new YamlScalarNode("operationId"), out YamlNode? node)
        ? RequiredScalar(node, "operationId")
        : throw new InvalidOperationException("Operation is missing operationId.");
}

static IEnumerable<YamlMappingNode> FindOperations(YamlMappingNode pathItem)
{
    foreach (string method in new[] { "get", "post", "put", "patch", "delete" })
    {
        if (pathItem.Children.TryGetValue(new YamlScalarNode(method), out YamlNode? node))
        {
            yield return AsMapping(node, method);
        }
    }
}

static YamlMappingNode Ref(string value) => new("$ref", value);

static YamlSequenceNode Sequence(IEnumerable<string> values) => new(values.Select(value => new YamlScalarNode(value)));

static void SetScalar(YamlMappingNode mapping, string key, string value) =>
    mapping.Children[new YamlScalarNode(key)] = new YamlScalarNode(value);

static YamlMappingNode RequiredMapping(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing mapping '{key}'.");
    }
    return AsMapping(node, key);
}

static YamlMappingNode AsMapping(YamlNode node, string name) =>
    node as YamlMappingNode ?? throw new InvalidOperationException($"'{name}' must be a mapping.");

static YamlSequenceNode RequiredSequence(YamlMappingNode mapping, string key)
{
    if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
    {
        throw new InvalidOperationException($"Missing sequence '{key}'.");
    }
    return AsSequence(node, key);
}

static YamlSequenceNode AsSequence(YamlNode node, string name) =>
    node as YamlSequenceNode ?? throw new InvalidOperationException($"'{name}' must be a sequence.");

static string RequiredScalar(YamlNode node, string name) =>
    node is YamlScalarNode scalar && scalar.Value is not null
        ? scalar.Value
        : throw new InvalidOperationException($"'{name}' must be a non-null scalar.");

static YamlMappingNode LoadMapping(string path)
{
    YamlStream yaml = new();
    using StreamReader reader = File.OpenText(path);
    yaml.Load(reader);
    return AsMapping(yaml.Documents.Single().RootNode, path);
}

internal sealed record AuthorizationRow(string Family, string[] RequiredScopes, string[] NotApplicableScopes);
