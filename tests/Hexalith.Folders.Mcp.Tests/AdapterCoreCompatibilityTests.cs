using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Configuration;
using Hexalith.Folders.Mcp.Credentials;
using Hexalith.Folders.Mcp.Errors;
using Hexalith.Folders.Mcp.Infrastructure;
using Hexalith.Folders.Mcp.Tooling;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>Pins the adapter mechanics before their shared-source extraction.</summary>
public sealed class AdapterCoreCompatibilityTests
{
    [Fact]
    public void MetadataSerializationPreservesNestedFilteringAndFormatting()
    {
        object value = new { DisplayName = "visible", Missing = (string?)null, Child = new { CONTENTBYTES = "secret", InlineContent = "secret", StreamDescriptor = "secret", Count = 2 } };

        MetadataOnlyJson.Serialize(value).ShouldBe("{\n  \"displayName\": \"visible\",\n  \"child\": {\n    \"count\": 2\n  }\n}".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    public void OptionalBodyOmissionAndJsonNullConstructAnEmptyBody(string? json)
        => RequestBody.Read<Dictionary<string, string>>(json).ShouldBeEmpty();

    [Fact]
    public void BodyParsingPreservesValuesAndSafeFailureText()
    {
        RequestBody.Read<Dictionary<string, string>>("  {\"key\":\" value \"}  ")["key"].ShouldBe(" value ");
        Should.Throw<McpUsageException>(() => RequestBody.Read<Dictionary<string, string>>("{ secret"))
            .Message.ShouldBe("The request body is not valid JSON for this operation.");
        Should.Throw<McpUsageException>(() => RequestBody.ReadRequired<Dictionary<string, string>>(" "))
            .Message.ShouldBe("The request body is required for this operation.");
    }

    [Fact]
    public void CredentialEnvironmentWinsWithoutReadingLowerSourcesAndTrims()
    {
        McpCredentialResolver resolver = new(
            new FoldersMcpAuthOptions { Token = "inline", TokenFile = "/unused-token-file" },
            name => name == "HEXALITH_TOKEN" ? "  env-token  " : throw new System.InvalidOperationException("Lower source read"),
            _ => throw new System.InvalidOperationException("Lower source read"));

        resolver.ResolveToken().ShouldBe("env-token");
    }

    [Fact]
    public async Task MutationSourcingPreservesCallerValuesAndProjectsSuccess()
    {
        ToolPipeline pipeline = TestSupport.Pipeline(Substitute.For<IClient>());

        string json = await pipeline.ExecuteMutationAsync(" key ", " task ", " caller-correlation ", (_, source, cancellation) =>
        {
            source.CorrelationId.ShouldBe(" caller-correlation ");
            source.TaskId.ShouldBe(" task ");
            source.IdempotencyKey.ShouldBe(" key ");
            cancellation.ShouldBe(TestContext.Current.CancellationToken);
            return Task.FromResult<object?>(new { Count = 1 });
        }, TestContext.Current.CancellationToken);

        json.ShouldBe("{\n  \"correlationId\": \" caller-correlation \",\n  \"result\": {\n    \"count\": 1\n  }\n}".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalProjectionVectorsRemainStable()
    {
        FailureKindProjection.Project(CanonicalErrorCategory.Idempotency_conflict).ShouldBe("idempotency_conflict");
        FailureKindProjection.Project(CanonicalErrorCategory.File_policy_unavailable).ShouldBe("file_policy_unavailable");
        FailureKindProjection.Project(CanonicalErrorCategory.Client_configuration_error).ShouldBe("client_configuration_error");
    }
}
