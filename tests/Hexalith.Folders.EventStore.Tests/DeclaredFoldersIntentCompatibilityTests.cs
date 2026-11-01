using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Pins the pre-migration adapter output and real-authority resolution against all eighteen original policies.</summary>
public sealed class DeclaredFoldersIntentCompatibilityTests
{
    [Fact]
    public void DeclarationsCoverEveryExistingCommandConstantAndActualPayloadDeserializer()
    {
        var constants = typeof(Hexalith.Folders.Server.FoldersServerModule).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("CommandType", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!).Order(StringComparer.Ordinal).ToArray();
        var declarations = Hexalith.Folders.Routing.FoldersCommandCatalog.Commands;
        declarations.Select(declaration => declaration.Route.MessageType).Order(StringComparer.Ordinal).ShouldBe(constants);
        string? root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "Hexalith.Folders.slnx"))) root = Directory.GetParent(root)?.FullName;
        root.ShouldNotBeNull();
        string processor = File.ReadAllText(Path.Combine(root, "src", "Hexalith.Folders.Server", "FolderDomainProcessor.cs"));
        string native = File.ReadAllText(Path.Combine(root, "src", "Hexalith.Folders.Server", "FolderDomainProcessor.Native.cs"));
        var payloads = System.Text.RegularExpressions.Regex.Matches(processor + native,
                @"JsonSerializer\.Deserialize<(?<payload>[A-Za-z0-9.]+)>\(\s*(?:envelope|command)\.Payload")
            .Select(match => match.Groups["payload"].Value.Split('.').Last()).Distinct().Order(StringComparer.Ordinal).ToArray();
        declarations.Select(declaration => declaration.Route.Execution!.HandlerType.Split('+', '.').Last()).Distinct().Order(StringComparer.Ordinal)
            .ShouldBe(payloads);
        declarations.Count(declaration => declaration.Route.Execution!.HandlerType.EndsWith("+FolderAccessMutationPayload", StringComparison.Ordinal)).ShouldBe(2);
        declarations.Count(declaration => declaration.Route.Execution!.HandlerType.EndsWith("+OrganizationAclMutationPayload", StringComparison.Ordinal)).ShouldBe(3);
    }

    [Fact]
    public async Task DeclaredAdaptersPreserveAllLegacyCanonicalBytesAndScopes()
    {
        using var catalog = await AdmissionCatalogFixture.CreateAsync();
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-canonical-intents.json")));
        ServiceCollection services = new(); services.AddFoldersIdempotencyIntentAdapters();
        using var provider = services.BuildServiceProvider();
        var adapters = provider.GetServices<IIdempotencyIntentAdapter>().ToDictionary(adapter => adapter.CommandType);
        var records = fixtures.RootElement.EnumerateArray().ToArray();
        records.Length.ShouldBe(36);
        records.Select(record => record.GetProperty("CommandType").GetString()).Distinct().Count().ShouldBe(18);
        foreach (var record in records)
        {
            string commandType = record.GetProperty("CommandType").GetString()!;
            string aggregate = record.GetProperty("AggregateId").GetString()!;
            byte[] payload = Encoding.UTF8.GetBytes(record.GetProperty("payload").GetString()!);
            var command = new IdempotencyIntentCommand(commandType, "tenant-a", "folders", aggregate, payload,
                new Dictionary<string, string> { ["taskId"] = "task-a" });
            var adapter = adapters[commandType].ShouldBeAssignableTo<IDeclaredIdempotencyIntentAdapter>();
            var intent = adapter.CreateIntent(command);
            intent.SemanticPayload.ShouldBe(Convert.FromBase64String(record.GetProperty("semanticPayload").GetString()!));
            intent.CanonicalTarget.ShouldBe(record.GetProperty("CanonicalTarget").GetString());
            intent.PolicyVersion.ShouldBe(record.GetProperty("PolicyVersion").GetString());
            intent.DelegatedTaskScope.ShouldBe(record.GetProperty("DelegatedTaskScope").GetString());
            intent.CredentialScope.ShouldBe(record.GetProperty("CredentialScope").GetString());
            intent.SemanticOptions.ShouldBe(JsonSerializer.Deserialize<Dictionary<string, string>>(record.GetProperty("SemanticOptions")));
            var fields = adapter.Declaration.DescriptorSchema.Fields;
            using var semantic = JsonDocument.Parse(intent.SemanticPayload);
            var payloadFields = fields.Where(field => field.Source == "payload").ToDictionary(field => field.Name);
            payloadFields.Keys.Order(StringComparer.Ordinal)
                .ShouldBe(semantic.RootElement.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            foreach (JsonProperty field in semantic.RootElement.EnumerateObject())
            {
                payloadFields[field.Name].Type.ShouldBe("string");
                field.Value.ValueKind.ShouldBeOneOf(JsonValueKind.String, JsonValueKind.Null);
                if (field.Value.ValueKind == JsonValueKind.Null) payloadFields[field.Name].Nullable.ShouldBeTrue();
            }
            fields.Single(field => field.Source == "target").Name.ShouldBe("target");
            fields.Where(field => field.Source == "semantic-option").Select(field => field.Name).Order(StringComparer.Ordinal)
                .ShouldBe(intent.SemanticOptions!.Keys.Order(StringComparer.Ordinal));
            fields.Any(field => field.Source == "delegated-task-scope").ShouldBe(intent.DelegatedTaskScope is not null);
            fields.Any(field => field.Source == "credential-scope").ShouldBe(intent.CredentialScope is not null);
            var resolved = catalog.Registry.Resolve(new SubmitCommand("id-a", "tenant-a", "folders", aggregate, commandType, payload,
                "correlation-a", "user-a", new Dictionary<string, string>(command.Extensions!), IdempotencyKey: "key-a"));
            resolved.AdapterId.ShouldBe(record.GetProperty("AdapterId").GetString());
            resolved.OperationId.ShouldBe(record.GetProperty("OperationId").GetString());
            resolved.DescriptorVersion.ShouldBe(record.GetProperty("DescriptorVersion").GetInt32());
            resolved.RetentionTier.ToString().ShouldBe(record.GetProperty("retentionTier").GetString());
            resolved.CanonicalIntent.ShouldBe(Convert.FromBase64String(record.GetProperty("canonical").GetString()!));
            Convert.ToHexStringLower(SHA256.HashData(resolved.CanonicalIntent)).ShouldBe(record.GetProperty("sha256").GetString());
        }
    }
}
