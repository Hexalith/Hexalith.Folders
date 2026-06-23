using System.Text;

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
    private const string AppHostCsprojPath = "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj";
    private const string AppHostProgramPath = "src/Hexalith.Folders.AppHost/Program.cs";

    // AC3: the local-dev Dapr components must be scoped to exactly the Folders topology app-ids — no
    // eventstore-admin (gateway-only), no memories (deferred to 9.2), no sample.
    private static readonly string[] ExpectedFoldersScopes =
    [
        FoldersAspireModule.EventStoreAppId,
        FoldersAspireModule.TenantsAppId,
        FoldersAspireModule.FoldersAppId,
        FoldersAspireModule.FoldersWorkersAppId,
        FoldersAspireModule.FoldersUiAppId,
    ];

    private static readonly string[] ForbiddenScopes = ["eventstore-admin", "eventstore-admin-ui", "memories", "sample"];

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

        // AC2: the two direct EventStore/Tenants runtime project references are removed (the helpers add the
        // runtime projects themselves via SuppressBuild project metadata).
        csproj.ShouldNotContain(@"Hexalith.EventStore\Hexalith.EventStore.csproj");
        csproj.ShouldNotContain(@"Hexalith.Tenants\Hexalith.Tenants.csproj");
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
