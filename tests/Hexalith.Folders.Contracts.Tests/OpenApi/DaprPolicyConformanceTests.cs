using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Folders.Aspire;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance;

public sealed class DaprPolicyConformanceTests
{
    private const string ExpectedDenyOutcome = "403";
    private const string ExpectedTrustDomain = "hexalith-production";
    private const string ExpectedNamespace = "hexalith-production";
    private const string PolicyPath = "deploy/dapr/production/accesscontrol.yaml";
    private const string MtlsPath = "deploy/dapr/production/daprsystem.yaml";
    private const string FixturePath = "tests/fixtures/dapr-policy-conformance.yaml";
    private static readonly string[] StableAppIds =
    [
        FoldersAspireModule.EventStoreAppId,
        FoldersAspireModule.TenantsAppId,
        FoldersAspireModule.FoldersAppId,
        FoldersAspireModule.FoldersWorkersAppId,
        FoldersAspireModule.FoldersUiAppId,
    ];

    [Fact]
    public void ProductionAccessControlPolicyShouldBeDenyByDefaultAndMatchFixtureProvenance()
    {
        ProductionPolicy policy = LoadProductionPolicy();
        ConformanceFixture fixture = LoadFixture();

        policy.Targets.Select(static t => t.TargetAppId).Order(StringComparer.Ordinal).ShouldBe(StableAppIds.Order(StringComparer.Ordinal));
        policy.Targets.ShouldAllBe(static t => string.Equals(t.DefaultAction, "deny", StringComparison.Ordinal));
        policy.Targets.ShouldAllBe(static t => string.Equals(t.TrustDomain, ExpectedTrustDomain, StringComparison.Ordinal));
        policy.Targets.ShouldAllBe(static t => string.Equals(t.Namespace, ExpectedNamespace, StringComparison.Ordinal));

        foreach (TargetPolicy target in policy.Targets)
        {
            target.CallerPolicies.ShouldAllBe(static p => string.Equals(p.DefaultAction, "deny", StringComparison.Ordinal));
            target.CallerPolicies.ShouldAllBe(static p => string.Equals(p.TrustDomain, ExpectedTrustDomain, StringComparison.Ordinal));
            target.CallerPolicies.ShouldAllBe(static p => string.Equals(p.Namespace, ExpectedNamespace, StringComparison.Ordinal));
            target.CallerPolicies.Select(static p => p.SourceAppId).ShouldAllBe(appId => StableAppIds.Contains(appId, StringComparer.Ordinal));
            target.CallerPolicies.SelectMany(static p => p.Operations).ShouldAllBe(static o => string.Equals(o.Action, "allow", StringComparison.Ordinal));
            target.CallerPolicies.SelectMany(static p => p.Operations).ShouldAllBe(static o => !o.Name.Contains('*', StringComparison.Ordinal));
            target.CallerPolicies.SelectMany(static p => p.Operations).SelectMany(static o => o.HttpVerbs).ShouldAllBe(static verb => !string.Equals(verb, "*", StringComparison.Ordinal));
        }

        string semanticHash = ComputeSemanticHash(policy);
        fixture.PolicyProvenance.Path.ShouldBe(PolicyPath);
        fixture.PolicyProvenance.SemanticSha256.ShouldBe(
            semanticHash,
            $"Update {FixturePath} policyProvenance.semanticSha256 and conformance cases when production Dapr policy semantics change. New normalized hash: {semanticHash}");

        HashSet<AllowRule> policyRules = [.. policy.AllowRules()];
        HashSet<AllowRule> fixtureRules = [.. fixture.TargetPolicies.SelectMany(static t => t.AllowRules)];
        fixtureRules.ShouldBe(policyRules);
    }

    [Fact]
    public void ProductionDaprSystemConfigurationShouldEnableMtlsWithoutSecretMaterial()
    {
        YamlMappingNode document = LoadSingleYamlDocument(MtlsPath);

        document.GetScalar("kind").ShouldBe("Configuration");
        YamlMappingNode metadata = document.GetMapping("metadata");
        metadata.GetScalar("namespace").ShouldBe(ExpectedNamespace);

        YamlMappingNode mtls = document.GetMapping("spec").GetMapping("mtls");
        mtls.GetBool("enabled").ShouldBeTrue();
        mtls.GetScalar("workloadCertTTL").ShouldNotBeNullOrWhiteSpace();
        mtls.GetScalar("allowedClockSkew").ShouldNotBeNullOrWhiteSpace();

        string yaml = File.ReadAllText(RepositoryPath(MtlsPath), Encoding.UTF8);
        yaml.ShouldNotContain("BEGIN CERTIFICATE", Case.Insensitive);
        yaml.ShouldNotContain("token", Case.Insensitive);
        yaml.ShouldNotContain("password", Case.Insensitive);
        yaml.ShouldNotContain("private key", Case.Insensitive);
    }

    [Fact]
    public void PolicyConformanceFixtureShouldCoverAllowedAndDeniedTriples()
    {
        ProductionPolicy policy = LoadProductionPolicy();
        ConformanceFixture fixture = LoadFixture();

        string[] requiredNegativeCategories =
        [
            "unknown-source-app",
            "known-unauthorized-source-app",
            "wrong-target-app",
            "wrong-operation",
            "wrong-http-verb",
            "wrong-namespace",
            "wrong-trust-domain",
        ];

        fixture.Cases.Select(static c => c.NegativeCategory).Where(static c => c is not null).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ShouldBe(requiredNegativeCategories.Order(StringComparer.Ordinal));

        foreach (AllowRule rule in policy.AllowRules())
        {
            ConformanceCase[] matchingCases = [.. fixture.Cases.Where(c => string.Equals(c.RuleId, rule.Id, StringComparison.Ordinal))];
            matchingCases.Count(static c => string.Equals(c.ExpectedOutcome, "allow", StringComparison.Ordinal)).ShouldBe(1);
            matchingCases.Count(static c => string.Equals(c.ExpectedOutcome, ExpectedDenyOutcome, StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(2);
            matchingCases
                .Where(static c => string.Equals(c.ExpectedOutcome, ExpectedDenyOutcome, StringComparison.Ordinal))
                .Select(static c => c.NegativeCategory)
                .ShouldBe(requiredNegativeCategories, ignoreOrder: true);
        }

        fixture.Cases.Select(static c => c.Id).ShouldBeUnique();
        fixture.Cases.Select(static c => c.ExpectedOutcome).ShouldAllBe(static outcome =>
            string.Equals(outcome, "allow", StringComparison.Ordinal) ||
            string.Equals(outcome, ExpectedDenyOutcome, StringComparison.Ordinal));

        foreach (ConformanceCase conformanceCase in fixture.Cases)
        {
            string actual = Evaluate(policy, conformanceCase);
            actual.ShouldBe(
                conformanceCase.ExpectedOutcome,
                $"Dapr conformance case '{conformanceCase.Id}' expected metadata-only outcome '{conformanceCase.ExpectedOutcome}'.");
        }
    }

    [Fact]
    public void LocalDevelopmentAccessControlShouldRemainPermissiveAndMarkedLocalOnly()
    {
        string path = "src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml";
        string yaml = File.ReadAllText(RepositoryPath(path), Encoding.UTF8);
        YamlMappingNode document = LoadSingleYamlDocument(path);

        yaml.ShouldContain("Local development only", Case.Insensitive);
        document.GetMapping("spec").GetMapping("accessControl").GetScalar("defaultAction").ShouldBe("allow");
    }

    private static ProductionPolicy LoadProductionPolicy()
    {
        string path = RepositoryPath(PolicyPath);
        File.Exists(path).ShouldBeTrue($"{PolicyPath} must exist as the repository-local production Dapr policy artifact.");

        using StreamReader reader = File.OpenText(path);
        YamlStream stream = new();
        stream.Load(reader);

        TargetPolicy[] targets = [.. stream.Documents
            .Select(static d => (YamlMappingNode)d.RootNode)
            .Where(static d => string.Equals(d.GetScalar("kind"), "Configuration", StringComparison.Ordinal))
            .Where(static d => d.GetMapping("spec").TryGetMapping("accessControl", out _))
            .Select(ParseTargetPolicy)];

        return new ProductionPolicy(targets);
    }

    private static TargetPolicy ParseTargetPolicy(YamlMappingNode document)
    {
        YamlMappingNode metadata = document.GetMapping("metadata");
        YamlMappingNode annotations = metadata.GetMapping("annotations");
        string targetAppId = annotations.GetScalar("hexalith.io/target-app-id");
        YamlMappingNode accessControl = document.GetMapping("spec").GetMapping("accessControl");
        YamlSequenceNode policies = accessControl.GetSequence("policies");
        CallerPolicy[] callerPolicies = [.. policies.Children.Cast<YamlMappingNode>().Select(ParseCallerPolicy)];

        return new TargetPolicy(
            targetAppId,
            metadata.GetScalar("name"),
            metadata.GetScalar("namespace"),
            accessControl.GetScalar("trustDomain"),
            accessControl.GetScalar("defaultAction"),
            callerPolicies);
    }

    private static CallerPolicy ParseCallerPolicy(YamlMappingNode policy)
    {
        YamlSequenceNode operations = policy.GetSequence("operations");
        Operation[] parsedOperations = [.. operations.Children.Cast<YamlMappingNode>().Select(ParseOperation)];
        return new CallerPolicy(
            policy.GetScalar("appId"),
            policy.GetScalar("namespace"),
            policy.GetScalar("trustDomain"),
            policy.GetScalar("defaultAction"),
            parsedOperations);
    }

    private static Operation ParseOperation(YamlMappingNode operation)
    {
        string[] httpVerbs = [.. operation.GetSequence("httpVerb").Children.Cast<YamlScalarNode>().Select(static n => n.Value ?? string.Empty)];
        return new Operation(operation.GetScalar("name"), httpVerbs, operation.GetScalar("action"));
    }

    private static ConformanceFixture LoadFixture()
    {
        string path = RepositoryPath(FixturePath);
        File.Exists(path).ShouldBeTrue($"{FixturePath} must exist as the machine-readable Dapr policy conformance fixture.");
        YamlMappingNode document = LoadSingleYamlDocument(FixturePath);

        FixturePolicyProvenance provenance = new(
            document.GetMapping("policyProvenance").GetScalar("path"),
            document.GetMapping("policyProvenance").GetScalar("semanticSha256"));

        FixtureTargetPolicy[] targetPolicies = [.. document.GetSequence("targetPolicies").Children
            .Cast<YamlMappingNode>()
            .Select(ParseFixtureTargetPolicy)];
        ConformanceCase[] cases = [.. document.GetSequence("cases").Children
            .Cast<YamlMappingNode>()
            .Select(ParseConformanceCase)];

        return new ConformanceFixture(provenance, targetPolicies, cases);
    }

    private static FixtureTargetPolicy ParseFixtureTargetPolicy(YamlMappingNode target)
    {
        AllowRule[] allowRules = [.. target.GetSequence("allowRules").Children
            .Cast<YamlMappingNode>()
            .Select(rule => new AllowRule(
                rule.GetScalar("id"),
                target.GetScalar("targetAppId"),
                rule.GetScalar("sourceAppId"),
                rule.GetScalar("operation"),
                rule.GetScalar("httpVerb"),
                rule.GetScalar("namespace"),
                rule.GetScalar("trustDomain")))];

        return new FixtureTargetPolicy(target.GetScalar("targetAppId"), allowRules);
    }

    private static ConformanceCase ParseConformanceCase(YamlMappingNode node)
        => new(
            node.GetScalar("id"),
            node.GetScalar("ruleId"),
            node.GetScalar("targetAppId"),
            node.GetScalar("sourceAppId"),
            node.GetScalar("operation"),
            node.GetScalar("httpVerb"),
            node.GetScalar("namespace"),
            node.GetScalar("trustDomain"),
            node.TryGetScalar("negativeCategory", out string? negativeCategory) ? negativeCategory : null,
            node.GetScalar("expectedOutcome"));

    private static string Evaluate(ProductionPolicy policy, ConformanceCase conformanceCase)
    {
        TargetPolicy? target = policy.Targets.SingleOrDefault(t => string.Equals(t.TargetAppId, conformanceCase.TargetAppId, StringComparison.Ordinal));
        if (target is null)
        {
            return ExpectedDenyOutcome;
        }

        CallerPolicy? caller = target.CallerPolicies.SingleOrDefault(p =>
            string.Equals(p.SourceAppId, conformanceCase.SourceAppId, StringComparison.Ordinal) &&
            string.Equals(p.Namespace, conformanceCase.Namespace, StringComparison.Ordinal) &&
            string.Equals(p.TrustDomain, conformanceCase.TrustDomain, StringComparison.Ordinal));
        if (caller is null)
        {
            return ExpectedDenyOutcome;
        }

        bool operationAllowed = caller.Operations.Any(o =>
            string.Equals(o.Name, conformanceCase.Operation, StringComparison.Ordinal) &&
            o.HttpVerbs.Contains(conformanceCase.HttpVerb, StringComparer.Ordinal) &&
            string.Equals(o.Action, "allow", StringComparison.Ordinal));

        return operationAllowed ? "allow" : ExpectedDenyOutcome;
    }

    private static string ComputeSemanticHash(ProductionPolicy policy)
    {
        var semanticPolicy = policy.Targets
            .OrderBy(static t => t.TargetAppId, StringComparer.Ordinal)
            .Select(static t => new
            {
                t.TargetAppId,
                t.Namespace,
                t.TrustDomain,
                t.DefaultAction,
                Policies = t.CallerPolicies
                    .OrderBy(static p => p.SourceAppId, StringComparer.Ordinal)
                    .Select(static p => new
                    {
                        p.SourceAppId,
                        p.Namespace,
                        p.TrustDomain,
                        p.DefaultAction,
                        Operations = p.Operations
                            .OrderBy(static o => o.Name, StringComparer.Ordinal)
                            .ThenBy(static o => string.Join(",", o.HttpVerbs.Order(StringComparer.Ordinal)), StringComparer.Ordinal)
                            .Select(static o => new
                            {
                                o.Name,
                                HttpVerbs = o.HttpVerbs.Order(StringComparer.Ordinal).ToArray(),
                                o.Action,
                            })
                            .ToArray(),
                    })
                    .ToArray(),
            })
            .ToArray();

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(semanticPolicy);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static string RepositoryPath(string relativePath)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory, "Hexalith.Folders.slnx")))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private sealed record ProductionPolicy(TargetPolicy[] Targets)
    {
        public IEnumerable<AllowRule> AllowRules()
            => Targets.SelectMany(static t => t.CallerPolicies.SelectMany(p => p.Operations.SelectMany(o => o.HttpVerbs.Select(verb => new AllowRule(
                $"{t.TargetAppId}.{p.SourceAppId}.{o.Name.TrimStart('/').Replace("/", "-", StringComparison.Ordinal)}.{verb.ToLowerInvariant()}",
                t.TargetAppId,
                p.SourceAppId,
                o.Name,
                verb,
                p.Namespace,
                p.TrustDomain)))));
    }

    private sealed record TargetPolicy(
        string TargetAppId,
        string ConfigName,
        string Namespace,
        string TrustDomain,
        string DefaultAction,
        CallerPolicy[] CallerPolicies);

    private sealed record CallerPolicy(
        string SourceAppId,
        string Namespace,
        string TrustDomain,
        string DefaultAction,
        Operation[] Operations);

    private sealed record Operation(string Name, string[] HttpVerbs, string Action);

    private sealed record AllowRule(
        string Id,
        string TargetAppId,
        string SourceAppId,
        string Operation,
        string HttpVerb,
        string Namespace,
        string TrustDomain);

    private sealed record FixturePolicyProvenance(string Path, string SemanticSha256);

    private sealed record FixtureTargetPolicy(string TargetAppId, AllowRule[] AllowRules);

    private sealed record ConformanceFixture(
        FixturePolicyProvenance PolicyProvenance,
        FixtureTargetPolicy[] TargetPolicies,
        ConformanceCase[] Cases);

    private sealed record ConformanceCase(
        string Id,
        string RuleId,
        string TargetAppId,
        string SourceAppId,
        string Operation,
        string HttpVerb,
        string Namespace,
        string TrustDomain,
        string? NegativeCategory,
        string ExpectedOutcome);
}

internal static class YamlNodeExtensions
{
    public static string GetScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        YamlScalarNode scalar = value.ShouldBeOfType<YamlScalarNode>();
        return scalar.Value.ShouldNotBeNull();
    }

    public static bool TryGetScalar(this YamlMappingNode node, string key, out string? value)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? yamlNode) && yamlNode is YamlScalarNode scalar)
        {
            value = scalar.Value;
            return true;
        }

        value = null;
        return false;
    }

    public static YamlMappingNode GetMapping(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    public static bool TryGetMapping(this YamlMappingNode node, string key, out YamlMappingNode? mapping)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlMappingNode mappingNode)
        {
            mapping = mappingNode;
            return true;
        }

        mapping = null;
        return false;
    }

    public static YamlSequenceNode GetSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }

    public static bool GetBool(this YamlMappingNode node, string key)
    {
        string value = node.GetScalar(key);
        return bool.Parse(value);
    }
}
