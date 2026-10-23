using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Commands;
using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Cli.Infrastructure;
using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Client.Generated;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

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
        => CommandOptions.ReadBody<Dictionary<string, string>>(json).ShouldBeEmpty();

    [Fact]
    public void BodyParsingPreservesValuesAndSafeFailureText()
    {
        CommandOptions.ReadBody<Dictionary<string, string>>("  {\"key\":\" value \"}  ")["key"].ShouldBe(" value ");
        Should.Throw<CliUsageException>(() => CommandOptions.ReadBody<Dictionary<string, string>>("{ secret"))
            .Message.ShouldBe("The --request body is not valid JSON for this operation.");
        Should.Throw<CliUsageException>(() => CommandOptions.ReadRequiredBody<Dictionary<string, string>>(" "))
            .Message.ShouldBe("The --request body is required for this operation.");
    }

    [Fact]
    public void CliBodyFileAndStdinSourcesRetainTheSameParsing()
    {
        string path = System.IO.Path.GetTempFileName();
        System.IO.TextReader previousInput = Console.In;
        try
        {
            const string body = "{\"key\":\" value \"}";
            System.IO.File.WriteAllText(path, body);
            CommandOptions.ReadBody<Dictionary<string, string>>("@" + path)["key"].ShouldBe(" value ");
            Console.SetIn(new System.IO.StringReader(body));
            CommandOptions.ReadBody<Dictionary<string, string>>("-")["key"].ShouldBe(" value ");
        }
        finally
        {
            Console.SetIn(previousInput);
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void CredentialEnvironmentWinsWithoutReadingLowerSourcesOrTrimming()
    {
        CredentialResolver resolver = new(
            name => name == "HEXALITH_TOKEN" ? "  env-token  " : throw new System.InvalidOperationException("Lower source read"),
            "/unused-credential-file");

        resolver.ResolveToken("flag-token").ShouldBe("  env-token  ");
    }

    [Fact]
    public async Task MutationSourcingPreservesCallerValuesAndProjectsSuccess()
    {
        TestCliConsole console = new();
        IClient client = Substitute.For<IClient>();
        CommandPipeline pipeline = new(new CliDependencies
        {
            Console = console,
            Credentials = new CredentialResolver(_ => null, "/unused-credential-file"),
            ClientFactory = (_, _) => client,
            IdempotencyKeyGenerator = () => throw new InvalidOperationException("Explicit key must win"),
        });
        GlobalOptions global = new("https://folders.invalid/", "token", " caller-correlation ", OutputMode.Json);

        int exit = await pipeline.ExecuteMutationAsync(global, " task ", " key ", true, (_, source, cancellation) =>
        {
            source.CorrelationId.ShouldBe(" caller-correlation ");
            source.TaskId.ShouldBe(" task ");
            source.IdempotencyKey.ShouldBe(" key ");
            cancellation.ShouldBe(TestContext.Current.CancellationToken);
            return Task.FromResult<object?>(new { Count = 1 });
        }, TestContext.Current.CancellationToken);

        exit.ShouldBe(0);
        console.StdOut.Trim().ShouldBe("{\n  \"count\": 1\n}".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        console.StdErr.ShouldContain("correlation-id:  caller-correlation ");
    }

    [Fact]
    public void CanonicalProjectionVectorsRemainStable()
    {
        ErrorProjection.Project(CanonicalErrorCategory.Idempotency_conflict).ShouldBe(68);
        ErrorProjection.Project(CanonicalErrorCategory.File_policy_unavailable).ShouldBe(72);
        ErrorProjection.Project(CanonicalErrorCategory.Client_configuration_error).ShouldBe(64);
    }
}
