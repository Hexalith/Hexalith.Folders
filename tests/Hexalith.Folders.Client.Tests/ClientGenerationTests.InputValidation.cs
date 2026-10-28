using System.Xml.Linq;
using Hexalith.Folders.Client.Idempotency;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;
using YamlDotNet.RepresentationModel;
using static Hexalith.Folders.Client.Generation.Shared.YamlContractLoader;

namespace Hexalith.Folders.Client.Tests;

public sealed partial class ClientGenerationTests
{
    private static readonly Lazy<string> InputValidationGenerator = new(() =>
    {
        string project = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generation", "Hexalith.Folders.Client.Generation.csproj");
        ProcessResult build = RunProcess("dotnet", $"build \"{project}\" -c \"{BuildConfiguration}\" -m:1 -nr:false -p:NuGetAudit=false -v:q", RepositoryRoot, 120_000);
        build.ExitCode.ShouldBe(0, RedactDiagnosticOutput(build.Output, RepositoryRoot));
        return Path.Combine(Path.GetDirectoryName(project)!, "bin", BuildConfiguration, "net10.0", "Hexalith.Folders.Client.Generation.dll");
    });

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("relative/repository")]
    [InlineData("../repository")]
    public void GeneratorRejectsNonAbsoluteRepositoryRootBeforeReadingInputs(string repositoryRoot)
    {
        WithInputValidationDirectory(temp =>
        {
            ProcessResult result = RunInputValidationGenerator(temp, repositoryRoot, Path.Combine(temp, "missing-contract.yaml"));
            result.ExitCode.ShouldNotBe(0);
            RedactDiagnosticOutput(result.Output, temp).ShouldContain("--repository-root must be a fully qualified path.");
            AssertInputValidationOutputsUntouched(temp);
        });
    }

    [Theory]
    [InlineData("$ref", "")]
    [InlineData("$ref", " ")]
    [InlineData("$ref", "#/components/parameters/")]
    [InlineData("$ref", "#/components/parameters/ ")]
    [InlineData("$ref", "#/components/parameters/FolderId/extra")]
    [InlineData("$ref", "#/components/schemas/FolderId")]
    [InlineData("$ref", "external.yaml#/components/parameters/FolderId")]
    [InlineData("name", "")]
    [InlineData("name", " ")]
    [InlineData("description", "missing name and reference")]
    public void GeneratorRejectsMalformedParametersBeforePublishing(string key, string value)
    {
        WithInputValidationDirectory(temp =>
        {
            YamlMappingNode contract = LoadInputValidationContract();
            YamlMappingNode path = RequiredMapping(contract, "paths").Children.Values.Cast<YamlMappingNode>().First();
            path.Children[new YamlScalarNode("parameters")] = new YamlSequenceNode(new YamlMappingNode
            {
                { key, value },
            });
            string contractPath = SaveInputValidationContract(temp, contract);
            ProcessResult result = RunInputValidationGenerator(temp, RepositoryRoot, contractPath);
            result.ExitCode.ShouldNotBe(0);
            RedactDiagnosticOutput(result.Output, temp).ShouldContain("Parameter");
            AssertInputValidationOutputsUntouched(temp);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GeneratorRejectsReferencedParametersWithoutNames(string? name)
    {
        WithInputValidationDirectory(temp =>
        {
            YamlMappingNode contract = LoadInputValidationContract();
            YamlMappingNode parameter = RequiredMapping(RequiredMapping(RequiredMapping(contract, "components"), "parameters"), "FolderId");
            if (name is null)
            {
                parameter.Children.Remove(new YamlScalarNode("name"));
            }
            else
            {
                parameter.Children[new YamlScalarNode("name")] = new YamlScalarNode(name);
            }

            ProcessResult result = RunInputValidationGenerator(temp, RepositoryRoot, SaveInputValidationContract(temp, contract));
            result.ExitCode.ShouldNotBe(0);
            RedactDiagnosticOutput(result.Output, temp).ShouldContain("Parameter name must be non-empty.");
            AssertInputValidationOutputsUntouched(temp);
        });
    }

    [Fact]
    public void GeneratorRejectsMissingParameterReferenceTargets()
    {
        WithInputValidationDirectory(temp =>
        {
            YamlMappingNode contract = LoadInputValidationContract();
            RequiredMapping(RequiredMapping(contract, "components"), "parameters").Children.Remove(new YamlScalarNode("FolderId"));
            ProcessResult result = RunInputValidationGenerator(temp, RepositoryRoot, SaveInputValidationContract(temp, contract));
            result.ExitCode.ShouldNotBe(0);
            RedactDiagnosticOutput(result.Output, temp).ShouldContain("could not be resolved");
            AssertInputValidationOutputsUntouched(temp);
        });
    }

    [Theory]
    [InlineData("_")]
    [InlineData("---")]
    public void GeneratorPreservesLegalUnusedParametersWithoutHelperIdentifiers(string name)
    {
        WithInputValidationDirectory(temp =>
        {
            YamlMappingNode contract = LoadInputValidationContract();
            YamlMappingNode path = RequiredMapping(contract, "paths").Children.Values.Cast<YamlMappingNode>().First();
            path.Children[new YamlScalarNode("parameters")] = new YamlSequenceNode(new YamlMappingNode
            {
                { "name", name },
                { "in", "query" },
                { "schema", new YamlMappingNode { { "type", "string" } } },
            });
            ProcessResult result = RunInputValidationGenerator(temp, RepositoryRoot, SaveInputValidationContract(temp, contract));
            result.ExitCode.ShouldBe(0, RedactDiagnosticOutput(result.Output, temp));
            File.ReadAllText(Path.Combine(temp, "helpers.cs")).ShouldStartWith("// <auto-generated />");
        });
    }

    [Theory]
    [InlineData("field\0")]
    [InlineData("field\t")]
    [InlineData("field\r")]
    [InlineData("field\n")]
    [InlineData("field\u007f")]
    [InlineData("field\u0085")]
    [InlineData("field\uFEFF")]
    [InlineData("field\u2028")]
    [InlineData("field\u2029")]
    public void DirectCanonicalLineRejectsTheSameUnsafePathsAsCompute(string path)
    {
        AssertCanonicalPathRejected(path);
    }

    [Fact]
    public void DirectCanonicalLineRejectsMalformedUtf16Paths()
    {
        AssertCanonicalPathRejected("field" + (char)0xD800);
        AssertCanonicalPathRejected("field" + (char)0xDC00);
        AssertCanonicalPathRejected("field" + (char)0xD800 + "x");
    }

    [Fact]
    public void CanonicalLinePreservesEscapedValuesAndValidUnicodePaths()
    {
        string path = "field_" + char.ConvertFromUtf32(0x1F600);
        new IdempotencyField(path, true, "line\n\uFEFF").ToCanonicalLine()
            .ShouldBe("field=" + path + ";present=true;value=s:line\\n\\uFEFF");
        new IdempotencyField(path, false, "ignored").ToCanonicalLine()
            .ShouldBe("field=" + path + ";present=false;value=omitted");
        new IdempotencyField(path, true, null).ToCanonicalLine()
            .ShouldBe("field=" + path + ";present=true;value=null");
    }

    [Fact]
    public void DuplicateFieldDiagnosticPrecedesOrderDiagnosticWithoutChangingCaseSensitivity()
    {
        InvalidOperationException duplicate = Should.Throw<InvalidOperationException>(() => HexalithIdempotencyHasher.Compute("Probe",
        [
            new("z_field", true, 1),
            new("a_field", true, 2),
            new("z_field", true, 3),
        ]));
        duplicate.Message.ShouldBe("Duplicate idempotency field 'z_field'.");

        Should.NotThrow(() => HexalithIdempotencyHasher.Compute("Probe", [new("A", true, 1), new("a", true, 2)]));
        InvalidOperationException order = Should.Throw<InvalidOperationException>(() => HexalithIdempotencyHasher.Compute("Probe",
            [new("a", true, 1), new("A", true, 2)]));
        order.Message.ShouldContain("ordinal-ascending order");
    }

    [Fact]
    public void GeneratedNestedHelperHandlesNullAtEveryDepthAndRetainsPresenceSemantics()
    {
        WithInputValidationDirectory(temp =>
        {
            YamlMappingNode contract = LoadInputValidationContract();
            RequiredMapping(contract, "paths").Add("/nested-probe", new YamlMappingNode
            {
                { "post", new YamlMappingNode
                    {
                        { "operationId", "NestedProbe" },
                        { "requestBody", new YamlMappingNode
                            {
                                { "content", new YamlMappingNode
                                    {
                                        { "application/json", new YamlMappingNode
                                            {
                                                { "schema", new YamlMappingNode { { "$ref", "#/components/schemas/NestedProbeRequest" } } },
                                            }
                                        },
                                    }
                                },
                            }
                        },
                        { "responses", new YamlMappingNode { { "200", new YamlMappingNode { { "description", "Probe result" } } } } },
                        { "x-hexalith-idempotency-equivalence", new YamlSequenceNode("root.middle.leaf") },
                    }
                },
            });
            YamlMappingNode schemas = RequiredMapping(RequiredMapping(contract, "components"), "schemas");
            schemas.Add("NestedProbeRequest", new YamlMappingNode
            {
                { "type", "object" },
                { "properties", new YamlMappingNode
                    {
                        { "root", new YamlMappingNode { { "$ref", "#/components/schemas/NestedProbeRoot" } } },
                    }
                },
            });
            schemas.Add("NestedProbeRoot", new YamlMappingNode
            {
                { "type", "object" },
                { "properties", new YamlMappingNode
                    {
                        { "middle", new YamlMappingNode { { "$ref", "#/components/schemas/NestedProbeMiddle" } } },
                    }
                },
            });
            schemas.Add("NestedProbeMiddle", new YamlMappingNode
            {
                { "type", "object" },
                { "properties", new YamlMappingNode
                    {
                        { "leaf", new YamlMappingNode { { "type", "string" } } },
                    }
                },
            });
            string contractPath = SaveInputValidationContract(temp, contract);
            ProcessResult result = RunInputValidationGenerator(temp, RepositoryRoot, contractPath);
            result.ExitCode.ShouldBe(0, RedactDiagnosticOutput(result.Output, temp));

            string output = File.ReadAllText(Path.Combine(temp, "helpers.cs"));
            int start = output.IndexOf("public partial class NestedProbeRequest", StringComparison.Ordinal);
            start.ShouldBeGreaterThanOrEqualTo(0);
            int end = output.IndexOf("\n}\n", start, StringComparison.Ordinal) + 3;
            end.ShouldBeGreaterThan(start);
            File.WriteAllText(Path.Combine(temp, "NestedProbeRequest.g.cs"),
                "using Hexalith.Folders.Client.Idempotency;\nnamespace NestedProbe.Generated;\n" + output[start..end]);
            // Compile the emitted helper against DTOs generated from the same nested schemas.
            File.Delete(Path.Combine(temp, "helpers.cs"));
            File.Delete(Path.Combine(temp, "client.cs"));
            GenerateNestedProbeDtos(temp, contract, schemas);
            File.WriteAllText(Path.Combine(temp, "Program.cs"), """
                using Hexalith.Folders.Client.Idempotency;
                using NestedProbe.Generated;
                NestedProbeRequest request = new();
                Check(false, null);
                request.Root = new();
                Check(true, null);
                request.Root.Middle = new();
                Check(true, null);
                request.Root.Middle.Leaf = "synthetic-value";
                Check(true, "synthetic-value");
                void Check(bool present, object? value)
                {
                    string expected = HexalithIdempotencyHasher.Compute("NestedProbe", [new("root.middle.leaf", present, value)]);
                    if (request.ComputeIdempotencyHash() != expected) throw new InvalidOperationException("Nested hash mismatch.");
                }
                Console.WriteLine("nested-probe-passed");
                """);
            string probeProject = Path.Combine(temp, "probe.csproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0"), new XElement("OutputType", "Exe"),
                    new XElement("Nullable", "enable"), new XElement("ImplicitUsings", "enable")),
                new XElement("ItemGroup", new[] { typeof(HexalithIdempotencyHasher).Assembly, typeof(JToken).Assembly }
                    .Select(assembly => new XElement("Reference", new XAttribute("Include", assembly.GetName().Name!),
                        new XElement("HintPath", assembly.Location)))))).Save(probeProject);
            ProcessResult probe = RunProcess("dotnet", $"run --project \"{probeProject}\" --configuration \"{BuildConfiguration}\" -p:NuGetAudit=false", temp, 120_000);
            probe.ExitCode.ShouldBe(0, RedactDiagnosticOutput(probe.Output, temp));
            RedactDiagnosticOutput(probe.Output, temp).ShouldContain("nested-probe-passed");
        });
    }

    private static void GenerateNestedProbeDtos(string temp, YamlMappingNode contract, YamlMappingNode schemas)
    {
        YamlMappingNode probeSchemas = new();
        foreach (string name in new[] { "NestedProbeRequest", "NestedProbeRoot", "NestedProbeMiddle" })
        {
            probeSchemas.Add(name, RequiredMapping(schemas, name));
        }

        YamlMappingNode probeContract = new()
        {
            { "openapi", "3.1.0" },
            { "info", new YamlMappingNode { { "title", "Synthetic nested probe" }, { "version", "1.0.0" } } },
            { "paths", new YamlMappingNode { { "/nested-probe", RequiredMapping(RequiredMapping(contract, "paths"), "/nested-probe") } } },
            { "components", new YamlMappingNode { { "schemas", probeSchemas } } },
        };
        string probeContractPath = SaveInputValidationContract(temp, probeContract);
        JObject configuration = JObject.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "nswag.json")));
        configuration["documentGenerator"]!["fromDocument"]!["url"] = probeContractPath;
        JToken generator = configuration["codeGenerators"]!["openApiToCSharpClient"]!;
        generator["generateClientClasses"] = false;
        generator["generateClientInterfaces"] = false;
        generator["generateNullableReferenceTypes"] = true;
        generator["namespace"] = "NestedProbe.Generated";
        generator["output"] = Path.Combine(temp, "NestedProbeDtos.g.cs");
        string configurationPath = Path.Combine(temp, "nswag.json");
        File.WriteAllText(configurationPath, configuration.ToString());

        string clientProject = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Hexalith.Folders.Client.csproj");
        ProcessResult tool = RunProcess("dotnet", $"msbuild \"{clientProject}\" -getProperty:NSwagExe_Net100 -verbosity:quiet", RepositoryRoot, 120_000);
        tool.ExitCode.ShouldBe(0, RedactDiagnosticOutput(tool.Output, temp));
        string command = tool.Output.Trim();
        command.StartsWith("dotnet ", StringComparison.Ordinal).ShouldBeTrue(RedactDiagnosticOutput(command, temp));
        ProcessResult generation = RunProcess("dotnet", command["dotnet ".Length..] + $" run \"{configurationPath}\"", temp, 120_000);
        generation.ExitCode.ShouldBe(0, RedactDiagnosticOutput(generation.Output, temp));
    }

    private static void AssertCanonicalPathRejected(string path)
    {
        foreach (bool present in new[] { false, true })
        {
            IdempotencyField field = new(path, present, null);
            InvalidOperationException direct = Should.Throw<InvalidOperationException>(() => field.ToCanonicalLine());
            InvalidOperationException compute = Should.Throw<InvalidOperationException>(() => HexalithIdempotencyHasher.Compute("Probe", [field]));
            direct.Message.ShouldBe(compute.Message);
        }
    }

    private static YamlMappingNode LoadInputValidationContract() =>
        LoadYaml(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml"));

    private static string SaveInputValidationContract(string temp, YamlMappingNode contract)
    {
        string path = Path.Combine(temp, "contract.yaml");
        using StreamWriter writer = File.CreateText(path);
        new YamlStream(new YamlDocument(contract)).Save(writer, assignAnchors: false);
        return path;
    }

    private static ProcessResult RunInputValidationGenerator(string temp, string repositoryRoot, string contractPath)
    {
        string client = Path.Combine(temp, "client.cs");
        string output = Path.Combine(temp, "helpers.cs");
        File.Copy(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated", "HexalithFoldersClient.g.cs"), client);
        File.WriteAllText(output, "existing-helper-output");
        string configuration = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "nswag.json");
        return RunProcess("dotnet",
            $"\"{InputValidationGenerator.Value}\" --repository-root \"{repositoryRoot}\" --contract \"{contractPath}\" --configuration \"{configuration}\" --output \"{output}\" --client \"{client}\"",
            temp, 120_000);
    }

    private static void AssertInputValidationOutputsUntouched(string temp)
    {
        File.ReadAllText(Path.Combine(temp, "helpers.cs")).ShouldBe("existing-helper-output");
        File.ReadAllBytes(Path.Combine(temp, "client.cs")).ShouldBe(
            File.ReadAllBytes(Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Client", "Generated", "HexalithFoldersClient.g.cs")));
    }

    private static void WithInputValidationDirectory(Action<string> action)
    {
        string temp = Path.Combine(Path.GetTempPath(), "hexalith-sdk-inputs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            action(temp);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }
}
