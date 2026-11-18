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
    private const string ExpectedTrustDomainTemplate = "DAPR_TRUST_DOMAIN";
    private const string ExpectedNamespace = "hexalith-production";
    private const string ExpectedDaprSystemNamespace = "dapr-system";
    private const string ExpectedTenantEventsTopic = "tenants.events";
    private const string ExpectedMemoriesEventsTopic = "memories-events";
    private const string ExpectedFolderEventsTopic = "folders.events";
    private const string PolicyPath = "deploy/dapr/production/accesscontrol.yaml";
    private const string MtlsPath = "deploy/dapr/production/daprsystem.yaml";
    private const string PubSubPath = "deploy/dapr/production/pubsub.yaml";
    private const string SecretStorePath = "deploy/dapr/production/secretstore.yaml";
    private const string SidecarBindingsPath = "deploy/dapr/production/sidecar-config-bindings.yaml";
    private const string FixturePath = "tests/fixtures/dapr-policy-conformance.yaml";
    private static readonly string[] StableAppIds =
    [
        FoldersAspireModule.EventStoreAppId,
        FoldersAspireModule.TenantsAppId,
        FoldersAspireModule.FoldersAppId,
        FoldersAspireModule.FoldersWorkersAppId,
        FoldersAspireModule.FoldersUiAppId,
        FoldersAspireModule.MemoriesAppId,
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
            // Wildcards are categorically forbidden in production allow rules; there is intentionally no
            // exception/justification path. A future bounded-wildcard need requires a deliberate, reviewed
            // change to this test rather than a fixture-level opt-out.
            target.CallerPolicies.SelectMany(static p => p.Operations).ShouldAllBe(static o => !o.Name.Contains('*', StringComparison.Ordinal));
            target.CallerPolicies.SelectMany(static p => p.Operations).SelectMany(static o => o.HttpVerbs).ShouldAllBe(static verb => !string.Equals(verb, "*", StringComparison.Ordinal));

            // An allow operation with no httpVerb constraint applies to ALL verbs in Dapr (an effective wildcard
            // broader than the intended verb list); fail closed on an empty or blank verb list.
            target.CallerPolicies.SelectMany(static p => p.Operations)
                .ShouldAllBe(static o => o.HttpVerbs.Length > 0 && o.HttpVerbs.All(static v => !string.IsNullOrWhiteSpace(v)));

            // Fail closed on duplicate operations within a caller policy (same operation name + verb-set twice).
            foreach (CallerPolicy callerPolicy in target.CallerPolicies)
            {
                callerPolicy.Operations
                    .Select(static o => $"{o.Name}|{string.Join(',', o.HttpVerbs.Order(StringComparer.Ordinal))}")
                    .ShouldBeUnique();
            }

            // The trust-domain-template annotation marks the deployment-templating seam; assert the sentinel
            // placeholder so it cannot silently rot or ship as a real trust-domain value.
            target.TrustDomainTemplate.ShouldBe(ExpectedTrustDomainTemplate);
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
        metadata.GetScalar("name").ShouldBe("daprsystem");
        metadata.GetScalar("namespace").ShouldBe(ExpectedDaprSystemNamespace);

        YamlMappingNode mtls = document.GetMapping("spec").GetMapping("mtls");
        mtls.GetBool("enabled").ShouldBeTrue();
        mtls.GetScalar("workloadCertTTL").ShouldNotBeNullOrWhiteSpace();
        mtls.GetScalar("allowedClockSkew").ShouldNotBeNullOrWhiteSpace();

        // The mTLS evidence plus the access-control policy and the conformance fixture are all declared
        // sanitized, non-secret artifacts; scan every canonical input for secret-shaped material, not just one.
        AssertNoSecretMaterial(MtlsPath);
        AssertNoSecretMaterial(PolicyPath);
        AssertNoSecretMaterial(PubSubPath);
        AssertNoSecretMaterial(SecretStorePath);
        AssertNoSecretMaterial(SidecarBindingsPath);
        AssertNoSecretMaterial(FixturePath);
    }

    private static void AssertNoSecretMaterial(string relativePath)
    {
        string yaml = File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);
        yaml.ShouldNotContain("BEGIN CERTIFICATE", Case.Insensitive);
        if (relativePath == SidecarBindingsPath)
        {
            foreach (YamlMappingNode document in LoadYamlDocuments(relativePath))
            {
                document.GetScalar("kind").ShouldBe("Deployment");
                AssertReferenceOnlyBindings(document.GetMapping("spec").GetMapping("template"));
            }
        }
        else
        {
            yaml.ShouldNotContain("token", Case.Insensitive);
        }
        yaml.ShouldNotContain("password", Case.Insensitive);
        yaml.ShouldNotContain("private key", Case.Insensitive);
    }

    private static void AssertReferenceOnlyBindings(YamlMappingNode template)
    {
        string[] credentialWords = ["token", "password", "key", "credential", "secret"];
        foreach (var annotation in template.GetMapping("metadata").GetMapping("annotations").Children)
        {
            string name = ((YamlScalarNode)annotation.Key).Value!;
            if (!credentialWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))) continue;
            name.ShouldBe("dapr.io/app-token-secret");
            ((YamlScalarNode)annotation.Value).Value.ShouldBeOneOf("hexalith-folders-server-app-token",
                "hexalith-folders-workers-app-token", "hexalith-tenants-app-token");
        }
        if (!template.TryGetMapping("spec", out YamlMappingNode? pod)) return;
        foreach (YamlMappingNode container in pod!.GetSequence("containers").Children.Cast<YamlMappingNode>())
        {
            if (container.Children.TryGetValue(new YamlScalarNode("args"), out YamlNode? arguments))
            {
                foreach (YamlScalarNode argument in ((YamlSequenceNode)arguments).Children.Cast<YamlScalarNode>())
                    credentialWords.Any(word => (argument.Value ?? "").Contains(word, StringComparison.OrdinalIgnoreCase))
                        .ShouldBeFalse("Credential-bearing container arguments are forbidden.");
            }
            if (!container.Children.TryGetValue(new YamlScalarNode("env"), out YamlNode? envs)) continue;
            foreach (YamlMappingNode env in ((YamlSequenceNode)envs).Children.Cast<YamlMappingNode>())
            {
                string name = env.GetScalar("name");
                if (!credentialWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))) continue;
                if (name == "EventStore__IdempotencyAdmission__DigestKeySource")
                { env.GetScalar("value").ShouldBe("DaprSecret"); continue; }
                if (name == "EventStore__IdempotencyAdmission__DigestKeySecretStoreName")
                { env.GetScalar("value").ShouldBe("kubernetes"); continue; }
                if (name == "EventStore__IdempotencyAdmission__DigestKeySecretName")
                { env.GetScalar("value").ShouldBe("hexalith-eventstore-idempotency-keys"); continue; }
                env.Children.ContainsKey(new YamlScalarNode("value")).ShouldBeFalse();
                YamlMappingNode reference = env.GetMapping("valueFrom").GetMapping("secretKeyRef");
                reference.GetScalar("name").ShouldNotBeNullOrWhiteSpace();
                reference.GetScalar("key").ShouldNotBeNullOrWhiteSpace();
            }
        }
    }

    [Theory]
    [InlineData("env")]
    [InlineData("annotation")]
    [InlineData("args")]
    public void SecretScanRejectsLiteralSecretsAcrossPodBindings(string location)
    {
        string yaml = "metadata:\n  annotations: {}\nspec:\n  containers:\n    - name: workers\n      env: []\n";
        if (location == "env") yaml = yaml.Replace("env: []", "env:\n        - name: CLIENT_SECRET\n          value: sensitive-material");
        if (location == "annotation") yaml = yaml.Replace("annotations: {}", "annotations:\n    example/secret: sensitive-material");
        if (location == "args") yaml += "      args: [--secret, sensitive-material]\n";
        YamlStream stream = new();
        stream.Load(new StringReader(yaml));
        Should.Throw<ShouldAssertException>(() => AssertReferenceOnlyBindings((YamlMappingNode)stream.Documents[0].RootNode));
    }

    [Fact]
    public void ExternalProductionStateStoreContractRequiresSharedUnprefixedProjectionKeys()
    {
        var local = LoadSingleYamlDocument("src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml");
        local.GetMapping("metadata").GetScalar("name").ShouldBe("statestore");
        ParseComponentMetadata(local.GetMapping("spec").GetSequence("metadata"))["keyPrefix"].ShouldBe("none");
        string documentation = File.ReadAllText(RepositoryPath("docs/operations/container-images-and-dapr-app-ids.md"));
        documentation.ShouldContain("same physical backend for Workers and Server");
        documentation.ShouldContain("`keyPrefix: none`");
        documentation.ShouldContain("not a deployed backend result");
        documentation.ShouldContain("per component instance");
        documentation.ShouldContain("multiple replicas");
        documentation.ShouldContain("restart and reclaimed deliveries");
        string repair = File.ReadAllText(RepositoryPath("docs/operations/folder-permission-projection-recovery.md"));
        repair.ShouldContain("per component instance");
        repair.ShouldContain("guarantees global ordering");
        repair.ShouldContain("rejected sequence gap leaves the projection unchanged");
        repair.ShouldContain("retained order");
    }

    [Fact]
    public void ProductionWorkersDeliveryUsesSerializedScopedComponentRetriesAndDeadLetters()
    {
        YamlMappingNode component = LoadSingleYamlDocument("deploy/dapr/production/pubsub.workers.yaml");
        component.GetMapping("metadata").GetScalar("name").ShouldBe("folders-workers-pubsub");
        component.GetSequence("scopes").Children.Cast<YamlScalarNode>().Select(item => item.Value)
            .ShouldBe(["folders-workers"]);
        var metadata = ParseComponentMetadata(component.GetMapping("spec").GetSequence("metadata"));
        metadata["concurrency"].ShouldBe("1");
        ParseTopicScopes(metadata["subscriptionScopes"])["folders-workers"]
            .ShouldBe(["tenants.events", "folders.events"]);
        ParseTopicScopes(metadata["publishingScopes"])["folders-workers"]
            .ShouldBe(["deadletter.folders.events", "deadletter.tenants.events"]);
        YamlMappingNode resilience = LoadSingleYamlDocument("deploy/dapr/production/resiliency.yaml");
        resilience.GetSequence("scopes").Children.Cast<YamlScalarNode>().Select(item => item.Value)
            .ShouldBe(["folders-workers"]);
        var spec = resilience.GetMapping("spec");
        spec.GetMapping("policies").GetMapping("retries").GetMapping("permissionDelivery")
            .GetScalar("maxRetries").ShouldBe("10");
        spec.GetMapping("targets").GetMapping("components").GetMapping("folders-workers-pubsub")
            .GetMapping("inbound").GetScalar("retry").ShouldBe("permissionDelivery");
        YamlMappingNode workers = LoadYamlDocuments(SidecarBindingsPath).Single(item => item.GetMapping("metadata").GetScalar("name") == "hexalith-folders-workers");
        workers.GetMapping("spec").GetMapping("template").GetMapping("spec").GetSequence("containers")
            .Children.Cast<YamlMappingNode>().Single().GetSequence("env").Children.Cast<YamlMappingNode>()
            .Single(item => item.GetScalar("name") == "Folders__Workers__PubSubName")
            .GetScalar("value").ShouldBe("folders-workers-pubsub");
    }

    [Fact]
    public void ProductionWorkloadAndAdmissionCredentialsAreBoundByReferences()
    {
        var documents = LoadYamlDocuments(SidecarBindingsPath);
        var eventstore = documents.Single(item => item.GetMapping("metadata").GetScalar("name") == "hexalith-eventstore");
        var env = eventstore.GetMapping("spec").GetMapping("template").GetMapping("spec")
            .GetSequence("containers").Children.Cast<YamlMappingNode>().Single().GetSequence("env")
            .Children.Cast<YamlMappingNode>().ToDictionary(item => item.GetScalar("name"));
        foreach (string name in new[] { "ClientId", "ClientSecret" })
            env["Authentication__WorkloadIssuer__" + name].GetMapping("valueFrom").GetMapping("secretKeyRef")
                .GetScalar("name").ShouldBe("hexalith-eventstore-workload-client");
        env["EventStore__IdempotencyAdmission__Enabled"].GetScalar("value").ShouldBe("true");
        env["EventStore__IdempotencyAdmission__DigestKeySource"].GetScalar("value").ShouldBe("DaprSecret");
        foreach (string name in new[] { "ActiveDigestKeyVersion", "DigestKeySecretGeneration" })
            env["EventStore__IdempotencyAdmission__" + name].GetMapping("valueFrom").GetMapping("secretKeyRef")
                .GetScalar("name").ShouldBe("hexalith-eventstore-idempotency-keys");
        foreach (string app in new[] { "folders", "folders-workers", "tenants" })
        {
            var template = documents.Single(item => item.GetMapping("spec").GetMapping("template")
                .GetMapping("metadata").GetMapping("annotations").GetScalar("dapr.io/app-id") == app)
                .GetMapping("spec").GetMapping("template");
            string secret = template.GetMapping("metadata").GetMapping("annotations").GetScalar("dapr.io/app-token-secret");
            template.GetMapping("spec").GetSequence("containers").Children.Cast<YamlMappingNode>().Single()
                .GetSequence("env").Children.Cast<YamlMappingNode>().Single(item => item.GetScalar("name") == "APP_API_TOKEN")
                .GetMapping("valueFrom").GetMapping("secretKeyRef").GetScalar("name").ShouldBe(secret);
        }
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

    [Fact]
    public void ProductionSidecarBindingsShouldAttachEveryAppToItsAccessControlConfiguration()
    {
        YamlMappingNode[] documents = LoadYamlDocuments(SidecarBindingsPath);
        documents.Length.ShouldBe(StableAppIds.Length);

        SidecarBinding[] bindings = [.. documents.Select(ParseSidecarBinding)];
        bindings.Select(static b => b.AppId).Order(StringComparer.Ordinal).ShouldBe(StableAppIds.Order(StringComparer.Ordinal));

        foreach (SidecarBinding binding in bindings)
        {
            binding.Namespace.ShouldBe(ExpectedNamespace);
            binding.Enabled.ShouldBe("true");
            binding.ConfigName.ShouldBe(AccessControlConfigName(binding.AppId));
        }
    }

    [Fact]
    public void ProductionPubSubComponentShouldConstrainTenantEventTopicScopes()
    {
        YamlMappingNode component = LoadSingleYamlDocument(PubSubPath);

        component.GetScalar("kind").ShouldBe("Component");
        YamlMappingNode metadata = component.GetMapping("metadata");
        metadata.GetScalar("name").ShouldBe(FoldersAspireModule.PubSubComponentName);
        metadata.GetScalar("namespace").ShouldBe(ExpectedNamespace);

        YamlMappingNode spec = component.GetMapping("spec");
        spec.GetScalar("type").ShouldBe("pubsub.redis");
        spec.GetScalar("version").ShouldBe("v1");

        IReadOnlyDictionary<string, string> componentMetadata = ParseComponentMetadata(spec.GetSequence("metadata"));
        componentMetadata["protectedTopics"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ShouldBe([ExpectedTenantEventsTopic, ExpectedMemoriesEventsTopic, ExpectedFolderEventsTopic], ignoreOrder: true);

        IReadOnlyDictionary<string, string[]> publishingScopes = ParseTopicScopes(componentMetadata["publishingScopes"]);
        IReadOnlyDictionary<string, string[]> subscriptionScopes = ParseTopicScopes(componentMetadata["subscriptionScopes"]);

        publishingScopes.Keys.Order(StringComparer.Ordinal).ShouldBe(StableAppIds.Order(StringComparer.Ordinal));
        subscriptionScopes.Keys.Order(StringComparer.Ordinal).ShouldBe(StableAppIds.Order(StringComparer.Ordinal));

        publishingScopes[FoldersAspireModule.TenantsAppId].ShouldBeEmpty();
        subscriptionScopes[FoldersAspireModule.FoldersAppId].ShouldBe([ExpectedTenantEventsTopic]);
        subscriptionScopes[FoldersAspireModule.TenantsAppId].ShouldBe([ExpectedTenantEventsTopic]);

        // Story 10.3 (D1): the EventStore actor host publishes managed-tenant folder domain events to the single
        // folders.events topic; folders-workers subscribes both the Tenants events and folders.events. eventstore
        // is the only folders.events publisher and folders-workers the only folders.events subscriber.
        publishingScopes[FoldersAspireModule.EventStoreAppId]
            .ShouldBe([ExpectedTenantEventsTopic, ExpectedFolderEventsTopic], ignoreOrder: true);
        subscriptionScopes[FoldersAspireModule.FoldersWorkersAppId].ShouldBeEmpty();

        // Story 10.3 (AC9): the worker-side producer publishes SearchIndexEntryChanged to memories-events, so
        // folders-workers is scoped to publish it and memories is scoped to subscribe — the only memories-events
        // scopes. No other app may touch the topic.
        publishingScopes[FoldersAspireModule.FoldersWorkersAppId].ShouldBe([ExpectedMemoriesEventsTopic]);
        subscriptionScopes[FoldersAspireModule.MemoriesAppId].ShouldBe([ExpectedMemoriesEventsTopic]);

        // Only EventStore (Tenants and Folders events) and folders-workers (memories-events)
        // publish; every other app's publish scope stays empty.
        publishingScopes.Where(static scope =>
                !string.Equals(scope.Key, FoldersAspireModule.EventStoreAppId, StringComparison.Ordinal) &&
                !string.Equals(scope.Key, FoldersAspireModule.FoldersWorkersAppId, StringComparison.Ordinal))
            .SelectMany(static scope => scope.Value)
            .ShouldBeEmpty();
        subscriptionScopes.Where(static scope =>
                !string.Equals(scope.Key, FoldersAspireModule.FoldersAppId, StringComparison.Ordinal) &&
                !string.Equals(scope.Key, FoldersAspireModule.FoldersWorkersAppId, StringComparison.Ordinal) &&
                !string.Equals(scope.Key, FoldersAspireModule.MemoriesAppId, StringComparison.Ordinal) &&
                !string.Equals(scope.Key, FoldersAspireModule.TenantsAppId, StringComparison.Ordinal))
            .SelectMany(static scope => scope.Value)
            .ShouldBeEmpty();
    }

    [Fact]
    public void MemoriesShouldStayDenyByDefaultAndAllowOnlyTheFoldersSearchInvokeAndMemoriesEventsSubscribe()
    {
        // Story 10.3 (AC9): the worker-side producer publishes SearchIndexEntryChanged to memories-events via the
        // pubsub COMPONENT (no invoke). Story 10.5 adds the ONLY memories invoke allow-rule: the Folders Server query
        // facade reads the search index via folders -> memories GET /api/search. memories stays deny-by-default; the
        // only caller policy is folders for exactly that one read operation (folders-workers gets no invoke rule —
        // its egress is pub/sub only). This guard fails closed if any other memories invoke or publish scope appears.
        ProductionPolicy policy = LoadProductionPolicy();

        TargetPolicy memories = policy.Targets.Single(t =>
            string.Equals(t.TargetAppId, FoldersAspireModule.MemoriesAppId, StringComparison.Ordinal));
        memories.DefaultAction.ShouldBe("deny");

        CallerPolicy foldersCaller = memories.CallerPolicies.ShouldHaveSingleItem();
        foldersCaller.SourceAppId.ShouldBe(FoldersAspireModule.FoldersAppId);
        Operation searchOperation = foldersCaller.Operations.ShouldHaveSingleItem();
        searchOperation.Name.ShouldBe("/api/search");
        searchOperation.HttpVerbs.ShouldBe(["GET"]);
        searchOperation.Action.ShouldBe("allow");

        YamlMappingNode component = LoadSingleYamlDocument(PubSubPath);
        IReadOnlyDictionary<string, string> componentMetadata = ParseComponentMetadata(component.GetMapping("spec").GetSequence("metadata"));
        ParseTopicScopes(componentMetadata["publishingScopes"])[FoldersAspireModule.MemoriesAppId].ShouldBeEmpty();
        ParseTopicScopes(componentMetadata["subscriptionScopes"])[FoldersAspireModule.MemoriesAppId].ShouldBe([ExpectedMemoriesEventsTopic]);
        ParseTopicScopes(componentMetadata["publishingScopes"])[FoldersAspireModule.FoldersWorkersAppId].ShouldBe([ExpectedMemoriesEventsTopic]);
    }

    [Fact]
    public void ProductionSecretStoreArtifactsShouldBeReferenceOnlyAndDenyByDefault()
    {
        YamlMappingNode component = LoadSingleYamlDocument(SecretStorePath);
        component.GetScalar("kind").ShouldBe("Component");
        component.GetMapping("metadata").GetScalar("name").ShouldBe("folders-provider-credentials");
        component.GetMapping("metadata").GetScalar("namespace").ShouldBe(ExpectedNamespace);

        YamlMappingNode spec = component.GetMapping("spec");
        spec.GetScalar("type").ShouldStartWith("secretstores.");
        spec.GetScalar("version").ShouldBe("v1");

        YamlMappingNode[] configurations = LoadYamlDocuments(PolicyPath);
        SecretScope[] scopes = [.. configurations.SelectMany(ParseSecretScopes)];
        scopes.ShouldContain(static scope =>
            string.Equals(scope.TargetAppId, FoldersAspireModule.FoldersAppId, StringComparison.Ordinal) &&
            string.Equals(scope.StoreName, "folders-provider-credentials", StringComparison.Ordinal) &&
            string.Equals(scope.DefaultAccess, "deny", StringComparison.Ordinal));
        scopes.ShouldContain(static scope =>
            string.Equals(scope.TargetAppId, FoldersAspireModule.FoldersWorkersAppId, StringComparison.Ordinal) &&
            string.Equals(scope.StoreName, "folders-provider-credentials", StringComparison.Ordinal) &&
            string.Equals(scope.DefaultAccess, "deny", StringComparison.Ordinal));

        foreach (string appId in new[] { FoldersAspireModule.FoldersAppId, FoldersAspireModule.FoldersWorkersAppId })
        {
            scopes.Single(scope =>
                    string.Equals(scope.TargetAppId, appId, StringComparison.Ordinal) &&
                    string.Equals(scope.StoreName, "folders-provider-credentials", StringComparison.Ordinal))
                .AllowedSecrets
                .ShouldBe(
                    ["forgejo-user-delegated-ref-synthetic", "github-app-installation-ref-synthetic"],
                    ignoreOrder: true);
        }

        scopes.Where(static scope => string.Equals(scope.StoreName, "kubernetes", StringComparison.Ordinal))
            .ShouldAllBe(static scope => string.Equals(scope.DefaultAccess, "deny", StringComparison.Ordinal) && scope.AllowedSecrets.SequenceEqual(scope.TargetAppId == FoldersAspireModule.EventStoreAppId
                ? new[] { "hexalith-eventstore-idempotency-keys" } : Array.Empty<string>()));
    }

    [Fact]
    public void WorkflowAndScriptShouldWireOfflineDaprPolicyConformanceGate()
    {
        string workflow = File.ReadAllText(RepositoryPath(".github/workflows/contract-spine.yml"), Encoding.UTF8);
        string script = File.ReadAllText(RepositoryPath("tests/tools/run-dapr-policy-conformance-gates.ps1"), Encoding.UTF8);

        workflow.ShouldContain("./tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild");
        workflow.ShouldContain("submodules: false");
        workflow.ShouldContain("global-json-file: global.json");
        workflow.ShouldNotContain("git submodule update --init --recursive", Case.Insensitive);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj");
        script.ShouldContain("FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance");
        script.ShouldContain("deploy/dapr/production/pubsub.yaml");
        script.ShouldContain("deploy/dapr/production/sidecar-config-bindings.yaml");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldNotContain("--recursive", Case.Insensitive);
    }

    [Fact]
    public void PermissionRecoveryUsesAuthorizedApplicationConnectionAndKeepsDaprCallersDenied()
    {
        TargetPolicy workers = LoadProductionPolicy().Targets.Single(target => target.TargetAppId == "folders-workers");
        workers.DefaultAction.ShouldBe("deny");
        workers.CallerPolicies.ShouldBeEmpty();
        YamlMappingNode binding = LoadYamlDocuments(SidecarBindingsPath).Single(document =>
            document.GetMapping("spec").GetMapping("template").GetMapping("metadata")
                .GetMapping("annotations").GetScalar("dapr.io/app-id") == "folders-workers");
        YamlMappingNode template = binding.GetMapping("spec").GetMapping("template");
        string secret = template.GetMapping("metadata").GetMapping("annotations").GetScalar("dapr.io/app-token-secret");
        YamlMappingNode env = template.GetMapping("spec").GetSequence("containers").Children
            .Cast<YamlMappingNode>().Single().GetSequence("env").Children.Cast<YamlMappingNode>()
            .Single(item => item.GetScalar("name") == "APP_API_TOKEN");
        YamlMappingNode secretReference = env.GetMapping("valueFrom").GetMapping("secretKeyRef");
        secretReference.GetScalar("name").ShouldBe(secret);
        string runbook = File.ReadAllText(RepositoryPath("docs/operations/folder-permission-projection-recovery.md"));
        runbook.ShouldContain("`" + secret + "` secret's `" + secretReference.GetScalar("key") + "` key");
        runbook.ShouldContain("pods/portforward");
        runbook.ShouldContain("--address 127.0.0.1");
        runbook.ShouldContain("18080:8080");
        runbook.ShouldContain("`dapr-api-token` header");
        runbook.ShouldContain("Missing or wrong tokens must return 401");
        runbook.ShouldContain("never print it");
        runbook.ShouldContain("not a live Kubernetes repair exercise");
    }

    private static ProductionPolicy LoadProductionPolicy()
    {
        string path = RepositoryPath(PolicyPath);
        File.Exists(path).ShouldBeTrue($"{PolicyPath} must exist as the repository-local production Dapr policy artifact.");

        using StreamReader reader = File.OpenText(path);
        YamlStream stream = new();
        stream.Load(reader);

        YamlMappingNode[] documents = [.. stream.Documents.Select(static d => (YamlMappingNode)d.RootNode)];
        documents.ShouldNotBeEmpty();
        foreach (YamlMappingNode document in documents)
        {
            document.GetScalar("kind").ShouldBe("Configuration");
            document.GetMapping("spec").TryGetMapping("accessControl", out _)
                .ShouldBeTrue($"{PolicyPath} must contain only Dapr Configuration documents with spec.accessControl.");
        }

        TargetPolicy[] targets = [.. documents.Select(ParseTargetPolicy)];

        return new ProductionPolicy(targets);
    }

    private static string AccessControlConfigName(string appId)
        => $"hexalith-folders-production-accesscontrol-{appId}";

    private static SidecarBinding ParseSidecarBinding(YamlMappingNode document)
    {
        document.GetScalar("kind").ShouldBe("Deployment");
        string @namespace = document.GetMapping("metadata").GetScalar("namespace");
        YamlMappingNode annotations = document
            .GetMapping("spec")
            .GetMapping("template")
            .GetMapping("metadata")
            .GetMapping("annotations");

        return new SidecarBinding(
            annotations.GetScalar("dapr.io/app-id"),
            @namespace,
            annotations.GetScalar("dapr.io/enabled"),
            annotations.GetScalar("dapr.io/config"));
    }

    private static IReadOnlyDictionary<string, string> ParseComponentMetadata(YamlSequenceNode metadata)
        => metadata.Children
            .Cast<YamlMappingNode>()
            .ToDictionary(
                static node => node.GetScalar("name"),
                static node => node.GetScalar("value"),
                StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string[]> ParseTopicScopes(string scopes)
        => scopes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static entry =>
            {
                string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
                parts.Length.ShouldBe(2, $"Invalid Dapr pub/sub scope entry '{entry}'.");
                string[] topics = string.IsNullOrWhiteSpace(parts[1])
                    ? []
                    : [.. parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                return (AppId: parts[0], Topics: topics);
            })
            .ToDictionary(static scope => scope.AppId, static scope => scope.Topics, StringComparer.Ordinal);

    private static IEnumerable<SecretScope> ParseSecretScopes(YamlMappingNode document)
    {
        string targetAppId = document.GetMapping("metadata").GetMapping("annotations").GetScalar("hexalith.io/target-app-id");
        if (!document.GetMapping("spec").TryGetMapping("secrets", out YamlMappingNode? secrets) || secrets is null)
        {
            return [];
        }

        return secrets.GetSequence("scopes").Children
            .Cast<YamlMappingNode>()
            .Select(scope => new SecretScope(
                targetAppId,
                scope.GetScalar("storeName"),
                scope.GetScalar("defaultAccess"),
                [.. scope.GetSequence("allowedSecrets").Children.Cast<YamlScalarNode>().Select(static value => value.Value ?? string.Empty)]));
    }

    private static TargetPolicy ParseTargetPolicy(YamlMappingNode document)
    {
        YamlMappingNode metadata = document.GetMapping("metadata");
        YamlMappingNode annotations = metadata.GetMapping("annotations");
        string targetAppId = annotations.GetScalar("hexalith.io/target-app-id");
        string trustDomainTemplate = annotations.GetScalar("hexalith.io/trust-domain-template");
        YamlMappingNode accessControl = document.GetMapping("spec").GetMapping("accessControl");
        YamlSequenceNode policies = accessControl.GetSequence("policies");
        CallerPolicy[] callerPolicies = [.. policies.Children.Cast<YamlMappingNode>().Select(ParseCallerPolicy)];

        return new TargetPolicy(
            targetAppId,
            metadata.GetScalar("name"),
            trustDomainTemplate,
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
            // No caller policy matched: defer to the target's access-control defaultAction so the simulator
            // faithfully reflects a flipped defaultAction instead of assuming deny.
            return Outcome(target.DefaultAction);
        }

        bool operationAllowed = caller.Operations.Any(o =>
            string.Equals(o.Name, conformanceCase.Operation, StringComparison.Ordinal) &&
            o.HttpVerbs.Contains(conformanceCase.HttpVerb, StringComparer.Ordinal) &&
            string.Equals(o.Action, "allow", StringComparison.Ordinal));

        // An explicit allow operation wins; otherwise defer to the caller policy's defaultAction.
        return operationAllowed ? "allow" : Outcome(caller.DefaultAction);
    }

    private static string Outcome(string defaultAction)
        => string.Equals(defaultAction, "allow", StringComparison.Ordinal) ? "allow" : ExpectedDenyOutcome;

    private static string ComputeSemanticHash(ProductionPolicy policy)
    {
        // The semantic hash intentionally covers only access-control decision inputs (target/caller app IDs,
        // namespace, trust domain, defaultAction, and operations). Config metadata.name and the
        // hexalith.io/trust-domain-template annotation are deployment-templating provenance, not authorization
        // inputs, so they are excluded; the template annotation is asserted directly in Fact 1 instead.
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
        YamlMappingNode[] documents = LoadYamlDocuments(relativePath);
        documents.Length.ShouldBe(1);
        return documents[0];
    }

    private static YamlMappingNode[] LoadYamlDocuments(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        return [.. stream.Documents.Select(static d => (YamlMappingNode)d.RootNode)];
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
        string TrustDomainTemplate,
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

    private sealed record SidecarBinding(string AppId, string Namespace, string Enabled, string ConfigName);

    private sealed record SecretScope(string TargetAppId, string StoreName, string DefaultAccess, string[] AllowedSecrets);

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
