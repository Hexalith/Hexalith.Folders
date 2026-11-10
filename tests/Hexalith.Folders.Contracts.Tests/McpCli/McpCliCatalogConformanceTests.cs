using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Hexalith.McpCli.Abstractions;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.McpCli;

public sealed class McpCliCatalogConformanceTests
{
    [Fact]
    public void EligibleInventoryAndDecisionRecordEqualTheDecoratedSetExactly()
    {
        string[][] inventory = McpCliContractTestSupport.InventoryRows();
        inventory.Length.ShouldBe(48);
        Type[] decorated = McpCliContractTestSupport.DecoratedTypes();
        decorated.ShouldAllBe(type => type.IsPublic, "every visibility is inspected, matching CatalogBuilder.GetTypes()");
        string[] eligible = inventory.Where(row => !McpCliContractTestSupport.ExcludedIds.Contains(row[1]))
            .Select(row => row[3]).Order(StringComparer.Ordinal).ToArray();
        eligible.ShouldBe(decorated.Select(type => type.FullName!).Order(StringComparer.Ordinal));
        eligible.ShouldBe(McpCliContractTestSupport.Operations.Select(row => row.Type.FullName!).Order(StringComparer.Ordinal));
        string decision = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "docs/contract/mcpcli-folders-operation-declarations.md"));
        string[][] rows = decision.Split('\n').Where(line => Regex.IsMatch(line, @"^\| [CQ][0-9]{2} \|"))
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries)
                .Select(cell => cell.Replace("`", string.Empty, StringComparison.Ordinal)).ToArray()).ToArray();
        rows.Length.ShouldBe(48);
        JsonObject spine = McpCliContractTestSupport.LoadSpine();
        Match digest = Regex.Match(decision, @"oracle `contract_spine_sha256`\s*`(?<digest>[a-f0-9]{64})`");
        digest.Success.ShouldBeTrue("the handoff must identify its exact Contract Spine bytes");
        string spinePath = Path.Combine(McpCliContractTestSupport.RepositoryRoot, "src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml");
        digest.Groups["digest"].Value.ShouldBe(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(spinePath))).ToLowerInvariant());
        foreach (string[] row in inventory)
        {
            string[] columns = rows.Single(cells => cells[1] == row[1]);
            columns.Length.ShouldBe(10, row[1]);
            columns[3].ShouldBe(row[3], row[1] + " exact CLR type");
            if (McpCliContractTestSupport.ExcludedIds.Contains(row[1]))
            {
                columns[2].ShouldBe("excluded / defer: seed-backed operations_console_projection diagnostic", row[1]);
                columns[4].ShouldBe("— (undecorated)", row[1]);
                columns[5].ShouldBe(row[3].Split('.').Last(), row[1]);
                columns[6].ShouldBe("folderId (spine scope; undecorated)", row[1]);
                columns[7].ShouldBe("—", row[1]);
                JsonObject operation = McpCliContractTestSupport.FindOperation(spine, columns[5]);
                operation["parameters"]!.AsArray().OfType<JsonObject>()
                    .Select(parameter => McpCliContractTestSupport.Resolve(spine, parameter))
                    .ShouldContain(parameter => parameter["in"]!.GetValue<string>() == "path"
                        && parameter["name"]!.GetValue<string>() == "folderId" && parameter["required"]!.GetValue<bool>(), row[1]);
            }
            else
            {
                OperationDeclaration declaration = McpCliContractTestSupport.Operations.Single(item => item.Id == row[1]);
                columns[2].ShouldBe("eligible", row[1] + " exact owner consent");
                HexalithCommandAttribute? command = declaration.Type.GetCustomAttribute<HexalithCommandAttribute>();
                HexalithQueryAttribute? query = declaration.Type.GetCustomAttribute<HexalithQueryAttribute>();
                columns[4].ShouldBe(command?.WireType ?? query!.WireType, row[1] + " wire name");
                columns[5].ShouldBe(declaration.SpineOperation, row[1] + " spine mapping");
                columns[6].ShouldBe(command?.AggregateIdProperty ?? query?.AggregateIdProperty ?? "none", row[1] + " aggregate source");
                columns[7].ShouldBe(ContractSpineDriftGateTests.DecisionExemptionCodes(spine, declaration), row[1] + " exemptions");
                if (row[1] == "C10")
                {
                    spine["components"]!["schemas"]!.AsObject().ShouldContainKey(columns[5]);
                }
                else
                {
                    McpCliContractTestSupport.FindOperation(spine, columns[5]).ShouldNotBeNull(row[1]);
                }
            }

            if (row[1].StartsWith('Q'))
            {
                columns[8].ShouldBe(row[4], row[1] + " fixed freshness");
            }
            else
            {
                columns[8].ShouldBe("—", row[1]);
            }
        }
    }

    [Fact]
    public void ModuleUsesPublicTenantStringIdentifiersAndExplicitWireNames()
    {
        HexalithModuleAttribute module = McpCliContractTestSupport.ContractsAssembly.GetCustomAttribute<HexalithModuleAttribute>()!;
        module.Name.ShouldBe("folders");
        module.Description.ShouldNotBeNullOrWhiteSpace();
        module.IdentifierKind.ShouldBe(IdentifierKind.String);
        module.WireTypeConvention.ShouldBe(WireTypeConvention.Explicit);
        module.FixedTenant.ShouldBeNull();
        module.SerializerOptionsProvider.ShouldBeNull();
    }

    [Fact]
    public void EveryOperationPinsItsRoutingStringsAndHasAClosedSerializableShape()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        foreach (OperationDeclaration row in McpCliContractTestSupport.Operations)
        {
            Type type = row.Type;
            type.IsPublic.ShouldBeTrue(row.Id);
            type.IsSealed.ShouldBeTrue(row.Id);
            type.GetMethod("<Clone>$").ShouldNotBeNull(row.Id);
            names.Add(KebabCase.FromTypeName(type.Name)).ShouldBeTrue(row.Id);
            KebabCase.FromTypeName(type.Name).ShouldBe(row.Part, row.Id);
            HexalithCommandAttribute? command = type.GetCustomAttribute<HexalithCommandAttribute>();
            HexalithQueryAttribute? query = type.GetCustomAttribute<HexalithQueryAttribute>();
            (command is null ^ query is null).ShouldBeTrue(row.Id);
            (command?.Domain ?? query!.Domain).ShouldBe("folders", row.Id);
            (command?.WireType ?? query!.WireType).ShouldBe("Hexalith.Folders." + (command is null ? "Queries." : "Commands.") + type.Name, row.Id);
            (command?.AggregateIdProperty ?? query?.AggregateIdProperty).ShouldBe(row.AggregateIdProperty, row.Id);
            (command?.Description ?? query!.Description).ShouldNotBeNullOrWhiteSpace(row.Id);
            command?.Name.ShouldBeNull(row.Id);
            query?.Name.ShouldBeNull(row.Id);
            command?.PlatformScope.ShouldBeNull(row.Id);
            query?.PlatformScope.ShouldBeNull(row.Id);
            command?.TenantProperty.ShouldBeNull(row.Id);
            query?.TenantProperty.ShouldBeNull(row.Id);
            command?.ActorProperty.ShouldBeNull(row.Id);
            command?.CorrelationProperty.ShouldBeNull(row.Id);
            if (query is not null)
            {
                query.ProjectionType.ShouldBe(row.Part, row.Id);
                Regex.IsMatch(query.ProjectionType!, "^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$").ShouldBeTrue(row.Id);
                query.WireType!.ShouldNotContain(':');
                query.AggregateId.ShouldBeNull(row.Id);
                query.ProjectionActorType.ShouldBeNull(row.Id);
                type.GetProperty("IdempotencyKey").ShouldBeNull(row.Id);
            }
            else
            {
                command!.IdempotencyKeyProperty.ShouldBe("IdempotencyKey", row.Id);
                PropertyInfo key = type.GetProperty("IdempotencyKey")!;
                key.PropertyType.ShouldBe(typeof(string), row.Id);
                key.IsDefined(typeof(JsonRequiredAttribute)).ShouldBeTrue(row.Id);
                new NullabilityInfoContext().Create(key).ReadState.ShouldBe(NullabilityState.NotNull, row.Id);
            }

            if (row.AggregateIdProperty is not null)
            {
                PropertyInfo aggregate = type.GetProperty(row.AggregateIdProperty)!;
                aggregate.PropertyType.ShouldBe(typeof(string), row.Id);
                aggregate.IsDefined(typeof(HexalithIdentifierAttribute)).ShouldBeTrue(row.Id);
                options.GetTypeInfo(type).Properties.ShouldContain(property => property.Name == aggregate.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
            }

            Inspect(type, new HashSet<Type>());
        }
    }

    private static void Inspect(Type type, HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        type.ShouldNotBe(typeof(object));
        type.ShouldNotBe(typeof(JsonElement));
        type.ShouldNotBe(typeof(JsonDocument));
        type.IsDefined(typeof(JsonPolymorphicAttribute)).ShouldBeFalse(type.FullName);
        type.IsDefined(typeof(JsonDerivedTypeAttribute)).ShouldBeFalse(type.FullName);
        if (type.IsArray)
        {
            Inspect(type.GetElementType()!, visited);
            return;
        }

        if (type.IsEnum)
        {
            type.GetCustomAttribute<DescriptionAttribute>()!.Description.ShouldNotBeNullOrWhiteSpace(type.FullName);
            foreach (FieldInfo value in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string expected = value.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;
                JsonSerializer.SerializeToNode(value.GetValue(null), type)!.GetValue<string>().ShouldBe(expected, type.Name + "." + value.Name);
            }

            return;
        }

        if (type == typeof(string) || type.IsPrimitive || !visited.Add(type))
        {
            return;
        }

        type.GetCustomAttribute<DescriptionAttribute>()!.Description.ShouldNotBeNullOrWhiteSpace(type.FullName);
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            property.GetCustomAttribute<DescriptionAttribute>()!.Description.ShouldNotBeNullOrWhiteSpace(type.Name + "." + property.Name);
            property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name.ShouldNotBeNullOrWhiteSpace(type.Name + "." + property.Name);
            if (property.Name.EndsWith("Id", StringComparison.Ordinal) || property.Name.EndsWith("Ref", StringComparison.Ordinal))
            {
                property.IsDefined(typeof(HexalithIdentifierAttribute)).ShouldBeTrue(type.Name + "." + property.Name);
            }

            Inspect(property.PropertyType, visited);
        }
    }
}
