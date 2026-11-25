namespace Hexalith.Folders.Server.Tests;

using System.Reflection;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.DomainService;
using Hexalith.Folders.EventStore;
using Hexalith.Folders.Server;

using Shouldly;

using Xunit;

/// <summary>Qualifies the actual owner factory against its complete command inventory and retained product outputs.</summary>
public sealed class FolderServerCatalogAdapterSourceTests
{
    /// <summary>The actual server image supplies every declared policy through the public SDK factory attribute.</summary>
    [Fact]
    public void ActualServerFactoryMatchesEveryOwnerCommandDeclaration()
    {
        RouteCatalogAdapterSourceAttribute source = typeof(FolderServerCatalogAdapterSource).Assembly
            .GetCustomAttributes<RouteCatalogAdapterSourceAttribute>().Single();
        source.SourceType.ShouldBe(typeof(FolderServerCatalogAdapterSource));
        source.SourceType.Assembly.ShouldBe(typeof(FolderServerCatalogAdapterSource).Assembly);
        typeof(FoldersCatalogAdapterSource).Assembly.GetName().Name.ShouldBe("Hexalith.Folders.EventStoreExtensions");
        var adapters = ((IReadOnlyList<IDeclaredIdempotencyIntentAdapter>)source.SourceType
            .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!).ToDictionary(adapter => adapter.CommandType);
        adapters.Values.Select(adapter => adapter.GetType().Assembly)
            .ShouldAllBe(assembly => assembly == typeof(FoldersCatalogAdapterSource).Assembly);
        adapters.Count.ShouldBe(18);
        adapters.Keys.Order(StringComparer.Ordinal).ShouldBe(FoldersRuntimeCatalogDeclarations.Commands
            .Select(command => command.Route.MessageType!).Order(StringComparer.Ordinal));
        foreach (var declaration in FoldersRuntimeCatalogDeclarations.Commands)
            JsonSerializer.Serialize(adapters[declaration.Route.MessageType!].Declaration, JsonSerializerOptions.Web)
                .ShouldBe(JsonSerializer.Serialize(declaration, JsonSerializerOptions.Web));
    }

    /// <summary>Every adapter in the real owner retains both original golden variants and their complete semantic scope.</summary>
    [Fact]
    public void ActualServerPoliciesRetainAllLegacyCanonicalIntentOutputs()
    {
        var adapters = FolderServerCatalogAdapterSource.Create().ToDictionary(adapter => adapter.CommandType);
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "CatalogPolicies", "legacy-canonical-intents.json")));
        JsonElement[] records = fixtures.RootElement.EnumerateArray().ToArray();
        records.Length.ShouldBe(36);
        records.Select(record => record.GetProperty("CommandType").GetString()).Distinct().Count().ShouldBe(18);
        foreach (JsonElement record in records)
        {
            string commandType = record.GetProperty("CommandType").GetString()!;
            IDeclaredIdempotencyIntentAdapter adapter = adapters[commandType];
            IdempotencyCanonicalIntent intent = adapter.CreateIntent(new(commandType, "tenant-a", "folders",
                record.GetProperty("AggregateId").GetString()!, Encoding.UTF8.GetBytes(record.GetProperty("payload").GetString()!),
                new Dictionary<string, string> { ["taskId"] = "task-a" }));
            adapter.AdapterId.ShouldBe(record.GetProperty("AdapterId").GetString());
            adapter.OperationId.ShouldBe(record.GetProperty("OperationId").GetString());
            adapter.DescriptorVersion.ShouldBe(record.GetProperty("DescriptorVersion").GetInt32());
            adapter.RetentionTier.ToString().ShouldBe(record.GetProperty("retentionTier").GetString());
            intent.SemanticPayload.ShouldBe(Convert.FromBase64String(record.GetProperty("semanticPayload").GetString()!));
            intent.CanonicalTarget.ShouldBe(record.GetProperty("CanonicalTarget").GetString());
            intent.PolicyVersion.ShouldBe(record.GetProperty("PolicyVersion").GetString());
            intent.SemanticOptions.ShouldBe(JsonSerializer.Deserialize<Dictionary<string, string>>(record.GetProperty("SemanticOptions")));
            intent.DelegatedTaskScope.ShouldBe(record.GetProperty("DelegatedTaskScope").GetString());
            intent.CredentialScope.ShouldBe(record.GetProperty("CredentialScope").GetString());
        }
    }
}
