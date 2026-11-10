using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
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
        string[] eligible = inventory.Where(row => !McpCliContractTestSupport.ExcludedIds.Contains(row[1]))
            .Select(row => row[3]).Order(StringComparer.Ordinal).ToArray();
        eligible.ShouldBe(McpCliContractTestSupport.DecoratedTypes().Select(type => type.FullName!).Order(StringComparer.Ordinal));
        eligible.ShouldBe(McpCliContractTestSupport.Operations.Select(row => row.Type.FullName!).Order(StringComparer.Ordinal));
        string decision = File.ReadAllText(Path.Combine(McpCliContractTestSupport.RepositoryRoot,
            "docs/contract/mcpcli-folders-operation-declarations.md"));
        string[] rows = decision.Split('\n').Where(line => Regex.IsMatch(line, @"^\| [CQ][0-9]{2} \|")).ToArray();
        rows.Length.ShouldBe(48);
        foreach (string[] row in inventory)
        {
            string declaration = rows.Single(line => line.StartsWith("| " + row[1] + " |", StringComparison.Ordinal));
            declaration.ShouldContain(McpCliContractTestSupport.ExcludedIds.Contains(row[1]) ? "excluded / defer" : "eligible");
            declaration.ShouldContain(row[3]);
            if (row[1].StartsWith('Q'))
            {
                string[] columns = declaration.Split('|', StringSplitOptions.TrimEntries);
                columns[8].Trim('`').ShouldBe(row[4], row[1] + " fixed freshness");
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
