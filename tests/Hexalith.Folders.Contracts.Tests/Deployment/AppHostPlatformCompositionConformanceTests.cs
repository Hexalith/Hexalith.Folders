using System.Text;
using System.Text.Json;

using Hexalith.Folders.Aspire;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Story 9.1 conformance: the Folders AppHost composes EventStore (gateway-only) + Tenants through the platform
/// Aspire helpers and sources the shared Dapr components from checked-in <c>DaprComponents</c> YAML instead of
/// creating them in code. These tests pin the net-new local-development component artifacts (AC3) and the
/// AppHost project/program shape that switched to the <c>.Aspire</c> helper references (AC2) — neither was
/// covered by the existing topology or production Dapr-policy suites.
/// </summary>
public sealed class AppHostPlatformCompositionConformanceTests
{
    private const string DaprComponentsDir = "src/Hexalith.Folders.AppHost/DaprComponents";
    private const string StateStorePath = $"{DaprComponentsDir}/statestore.yaml";
    private const string PubSubPath = $"{DaprComponentsDir}/pubsub.yaml";
    private const string ResiliencyPath = $"{DaprComponentsDir}/resiliency.yaml";
    private const string MemoriesSecretStorePath = $"{DaprComponentsDir}/secretstore.memories.yaml";
    private const string MemoriesLlmPath = $"{DaprComponentsDir}/llm.memories.yaml";
    private const string MemoriesSecretsJsonPath = $"{DaprComponentsDir}/secrets.json";
    private const string AppHostCsprojPath = "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj";
    private const string AppHostProgramPath = "src/Hexalith.Folders.AppHost/Program.cs";

    // AC3: the local-dev Dapr components must be scoped to exactly the Folders topology app-ids — no
    // eventstore-admin (gateway-only), no sample. Story 9.2 adds the memories search-index server, which
    // reuses the shared statestore/pubsub, so memories is now an expected scope (not forbidden).
    private static readonly string[] ExpectedFoldersScopes =
    [
        FoldersAspireModule.EventStoreAppId,
        FoldersAspireModule.TenantsAppId,
        FoldersAspireModule.FoldersAppId,
        FoldersAspireModule.FoldersWorkersAppId,
        FoldersAspireModule.FoldersUiAppId,
        FoldersAspireModule.MemoriesAppId,
    ];

    private static readonly string[] ForbiddenScopes = ["eventstore-admin", "eventstore-admin-ui", "sample"];

    [Fact]
    public void LocalStateStoreComponentShouldPreserveRedisActorSemanticsAndFoldersScopes()
    {
        YamlMappingNode component = LoadSingleYamlDocument(StateStorePath);

        component.GetScalar("kind").ShouldBe("Component");
        component.GetMapping("metadata").GetScalar("name").ShouldBe(FoldersAspireModule.StateStoreComponentName);

        YamlMappingNode spec = component.GetMapping("spec");
        spec.GetScalar("type").ShouldBe("state.redis");
        spec.GetScalar("version").ShouldBe("v1");

        // AC3: preserve today's in-code state-store semantics verbatim — actor state, the local Redis host, and
        // especially keyPrefix "none" (un-prefixed keys keep the established Folders state-key layout).
        IReadOnlyDictionary<string, string> metadata = ParseComponentMetadata(spec.GetSequence("metadata"));
        metadata["redisHost"].ShouldBe("localhost:6379");
        metadata["actorStateStore"].ShouldBe("true");
        metadata["keyPrefix"].ShouldBe("none");

        AssertFoldersScopes(component);
    }

    [Fact]
    public void LocalPubSubComponentShouldBeRedisAndScopedToFoldersTopology()
    {
        YamlMappingNode component = LoadSingleYamlDocument(PubSubPath);

        component.GetScalar("kind").ShouldBe("Component");
        component.GetMapping("metadata").GetScalar("name").ShouldBe(FoldersAspireModule.PubSubComponentName);

        YamlMappingNode spec = component.GetMapping("spec");
        spec.GetScalar("type").ShouldBe("pubsub.redis");
        spec.GetScalar("version").ShouldBe("v1");

        AssertFoldersScopes(component);
    }

    [Fact]
    public void LocalResiliencyComponentShouldTargetEventStoreAppAndSharedComponentsWithoutScopes()
    {
        YamlMappingNode component = LoadSingleYamlDocument(ResiliencyPath);

        component.GetScalar("kind").ShouldBe("Resiliency");
        component.GetMapping("metadata").GetScalar("name").ShouldBe("resiliency");

        // A Dapr Resiliency CRD is not scoped per app-id — there is intentionally no `scopes:` field; each
        // sidecar that loads it applies the matching targets.
        component.Children.ContainsKey(new YamlScalarNode("scopes"))
            .ShouldBeFalse("A Dapr Resiliency document must not carry a scopes field.");

        YamlMappingNode targets = component.GetMapping("spec").GetMapping("targets");

        MappingKeys(targets.GetMapping("apps"))
            .ShouldContain(FoldersAspireModule.EventStoreAppId);

        string[] componentTargets = MappingKeys(targets.GetMapping("components"));
        componentTargets.ShouldContain(FoldersAspireModule.StateStoreComponentName);
        componentTargets.ShouldContain(FoldersAspireModule.PubSubComponentName);
    }

    [Fact]
    public void AppHostProjectShouldReferencePlatformAspireHelpersAndNotRuntimeProjects()
    {
        string csproj = File.ReadAllText(RepositoryPath(AppHostCsprojPath), Encoding.UTF8);

        // AC2: the AppHost references the .Aspire hosting-extension libraries (IsAspireProjectResource="false").
        csproj.ShouldContain(@"Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj");
        csproj.ShouldContain(@"Hexalith.Tenants.Aspire\Hexalith.Tenants.Aspire.csproj");
        // Story 9.2 / AC2: the Memories search-index helper is referenced the same way.
        csproj.ShouldContain(@"Hexalith.Memories.Aspire\Hexalith.Memories.Aspire.csproj");

        // AC2: the two direct EventStore/Tenants runtime project references are removed (the helpers add the
        // runtime projects themselves via SuppressBuild project metadata).
        csproj.ShouldNotContain(@"Hexalith.EventStore\Hexalith.EventStore.csproj");
        csproj.ShouldNotContain(@"Hexalith.Tenants\Hexalith.Tenants.csproj");
        // Story 9.2 / AC2: no Folders-owned Memories runtime/container project — the cross-repo memories project
        // is added by the helper via SuppressBuild project metadata, not referenced here.
        csproj.ShouldNotContain(@"Hexalith.Memories.Server\Hexalith.Memories.Server.csproj");
        csproj.ShouldNotContain(@"Hexalith.Memories\Hexalith.Memories.csproj");
    }

    [Fact]
    public void AppHostProgramShouldNotUseGeneratedEventStoreOrTenantsProjectMetadata()
    {
        string program = File.ReadAllText(RepositoryPath(AppHostProgramPath), Encoding.UTF8);

        // AC2: the generated Projects.* metadata types for the runtime EventStore/Tenants projects are no longer
        // used; the platform helpers (AddHexalithEventStoreGatewayProject / AddHexalithTenantsServer) compose them.
        program.ShouldNotContain("Projects.Hexalith_EventStore");
        program.ShouldNotContain("Projects.Hexalith_Tenants");
    }

    [Fact]
    public void AppHostProgramShouldComposeMemoriesStandaloneWithoutDeferredRoutingOrGeneratedMetadata()
    {
        string program = File.ReadAllText(RepositoryPath(AppHostProgramPath), Encoding.UTF8);

        // Story 9.2 / AC1: the Memories search-index server IS composed via the platform helper, reusing the
        // shared EventStore state store + pub/sub component instances.
        program.ShouldContain("AddHexalithMemoriesSearchIndexServer");
        program.ShouldContain("eventStoreResources.StateStore");
        program.ShouldContain("eventStoreResources.PubSub");

        // AC2: the cross-repo memories runtime project is added by the helper via SuppressBuild project metadata,
        // so Program.cs uses no generated Projects.Hexalith_Memories* type.
        program.ShouldNotContain("Projects.Hexalith_Memories");

        // AC1: source->index routing env vars are deferred to Story 9.3 and must NOT be wired here. The canonical
        // Tenants AppHost sets EventStoreIntegration__Routing__* on its memories server; the Folders 9.2 AppHost
        // omits it (and the worker-side producer / folders->memories invoke authorization are Epic 10).
        program.ShouldNotContain("EventStoreIntegration__Routing");
    }

    [Fact]
    public void MemoriesSecretStoreComponentYamlShouldBeUnscopedLocalFileSecretStore()
    {
        YamlMappingNode component = LoadSingleYamlDocument(MemoriesSecretStorePath);

        component.GetScalar("kind").ShouldBe("Component");
        // Named "secretstore" (the name the Memories server resolves); the AppHost registers it under the unique
        // resource id "memories-secretstore" so it does not collide with the shared statestore/pubsub.
        component.GetMapping("metadata").GetScalar("name").ShouldBe("secretstore");

        YamlMappingNode spec = component.GetMapping("spec");
        spec.GetScalar("type").ShouldBe("secretstores.local.file");
        spec.GetScalar("version").ShouldBe("v1");

        // AC3 + Critical Note 7: the secret store reads the checked-in secrets.json so the local.file component
        // initializes at boot.
        IReadOnlyDictionary<string, string> metadata = ParseComponentMetadata(spec.GetSequence("metadata"));
        metadata["secretsFile"].ShouldBe("DaprComponents/secrets.json");

        // AC3: the memories-secretstore/memories-llm components stay unscoped (global), matching the Tenants
        // AppHost — a scopes: field would wrongly restrict a global component to an app-id allow-list.
        AssertUnscoped(component, MemoriesSecretStorePath);
    }

    [Fact]
    public void MemoriesLlmComponentYamlShouldBeUnscopedEchoConversationComponent()
    {
        YamlMappingNode component = LoadSingleYamlDocument(MemoriesLlmPath);

        component.GetScalar("kind").ShouldBe("Component");
        component.GetMapping("metadata").GetScalar("name").ShouldBe("llm");

        YamlMappingNode spec = component.GetMapping("spec");
        // Dev-only echo conversation component (no real LLM, no cost); production swaps spec.type for a real provider.
        spec.GetScalar("type").ShouldBe("conversation.echo");
        spec.GetScalar("version").ShouldBe("v1");

        AssertUnscoped(component, MemoriesLlmPath);
    }

    [Fact]
    public void MemoriesLocalSecretsFileShouldExistAsEmptyJsonObject()
    {
        // Critical Note 7: secretstore.memories.yaml references secretsFile "DaprComponents/secrets.json"; the
        // secretstores.local.file component fails to initialize at boot if it is missing. It is an empty object.
        string path = RepositoryPath(MemoriesSecretsJsonPath);
        File.Exists(path).ShouldBeTrue($"{MemoriesSecretsJsonPath} must exist for the local.file secret store to boot.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        document.RootElement.EnumerateObject().Any().ShouldBeFalse($"{MemoriesSecretsJsonPath} must be an empty object ({{}}).");
    }

    [Fact]
    public void NewMemoriesComponentArtifactsShouldUseLfLineEndings()
    {
        // AC3 + project-context LF rule: the new Dapr YAML/JSON artifacts use LF (the .editorconfig pins YAML /
        // container artifacts to LF). A stray CR would break the LF-normalized format gate and container tooling.
        foreach (string path in new[] { MemoriesSecretStorePath, MemoriesLlmPath, MemoriesSecretsJsonPath })
        {
            File.ReadAllText(RepositoryPath(path), Encoding.UTF8)
                .Contains('\r', StringComparison.Ordinal)
                .ShouldBeFalse($"{path} must use LF line endings (no CR).");
        }
    }

    private static void AssertUnscoped(YamlMappingNode component, string path)
        => component.Children.ContainsKey(new YamlScalarNode("scopes"))
            .ShouldBeFalse($"{path} must stay unscoped (global) — the memories secret-store/llm components carry no scopes: field.");

    private static void AssertFoldersScopes(YamlMappingNode component)
    {
        string[] scopes = ReadScopes(component);
        scopes.ShouldBe(ExpectedFoldersScopes, ignoreOrder: true);

        foreach (string forbidden in ForbiddenScopes)
        {
            scopes.ShouldNotContain(forbidden);
        }
    }

    private static string[] ReadScopes(YamlMappingNode component)
        => [.. component.GetSequence("scopes").Children.Cast<YamlScalarNode>().Select(static node => node.Value!)];

    private static string[] MappingKeys(YamlMappingNode node)
        => [.. node.Children.Keys.Cast<YamlScalarNode>().Select(static key => key.Value!)];

    private static IReadOnlyDictionary<string, string> ParseComponentMetadata(YamlSequenceNode metadata)
        => metadata.Children
            .Cast<YamlMappingNode>()
            .ToDictionary(
                static node => node.GetScalar("name"),
                static node => node.GetScalar("value"),
                StringComparer.Ordinal);

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1, $"{relativePath} must contain exactly one YAML document.");
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
}
