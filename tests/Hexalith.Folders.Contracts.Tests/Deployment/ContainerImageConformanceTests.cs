using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Hexalith.Folders.Aspire;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ContainerImageConformanceTests
{
    private const string DirectoryBuildTargetsPath = "Directory.Build.targets";
    private const string ServiceImagesPath = "deploy/containers/production/service-images.yaml";
    private const string GateScriptPath = "tests/tools/run-container-image-gates.ps1";
    private const string OperatorDocPath = "docs/operations/container-images-and-dapr-app-ids.md";

    private static readonly ServiceContainerContract[] ServiceContracts =
    [
        new(
            "server",
            "src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj",
            "hexalith-folders-server",
            FoldersAspireModule.FoldersAppId,
            "hexalith-folders-production-accesscontrol-folders"),
        new(
            "workers",
            "src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj",
            "hexalith-folders-workers",
            FoldersAspireModule.FoldersWorkersAppId,
            "hexalith-folders-production-accesscontrol-folders-workers"),
        new(
            "ui",
            "src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj",
            "hexalith-folders-ui",
            FoldersAspireModule.FoldersUiAppId,
            "hexalith-folders-production-accesscontrol-folders-ui"),
    ];

    [Fact]
    public void ServiceProjectsShouldDeclareStableSdkContainerMetadata()
    {
        foreach (ServiceContainerContract contract in ServiceContracts)
        {
            XDocument project = XDocument.Load(RepositoryPath(contract.ProjectPath));

            GetProperty(project, "IsPublishable").ShouldBe("true");
            GetProperty(project, "EnableContainer").ShouldBe("true");
            GetProperty(project, "ContainerRepository").ShouldBe(contract.Repository);
            GetProperty(project, "HexalithContainerServiceName").ShouldBe(contract.ServiceName);
            ReadText(contract.ProjectPath).ShouldNotContain("Microsoft.NET.Build.Containers", Case.Insensitive);
        }

        string targets = ReadText(DirectoryBuildTargetsPath);
        targets.ShouldContain("<ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0-alpine</ContainerBaseImage>");
        targets.ShouldContain("<ContainerUser>app</ContainerUser>");
        targets.ShouldContain("<ContainerPort Include=\"8080\" Type=\"tcp\" />");

        foreach (string label in RequiredContainerLabels())
        {
            targets.ShouldContain($"<ContainerLabel Include=\"{label}\"", Case.Sensitive);
        }

        targets.ShouldNotContain("Microsoft.NET.Build.Containers", Case.Insensitive);
    }

    [Fact]
    public void ProductionServiceImageBindingsShouldMapRepositoriesToStableDaprAppIds()
    {
        YamlMappingNode document = LoadSingleYamlDocument(ServiceImagesPath);
        document.GetScalar("kind").ShouldBe("ServiceImageBindings");

        YamlSequenceNode services = document
            .GetMapping("spec")
            .GetSequence("services");
        ServiceImageBinding[] bindings = [.. services.Children.Cast<YamlMappingNode>().Select(ParseServiceImageBinding)];

        bindings.Select(static b => b.ServiceName).Order(StringComparer.Ordinal)
            .ShouldBe(ServiceContracts.Select(static c => c.ServiceName).Order(StringComparer.Ordinal));

        foreach (ServiceContainerContract contract in ServiceContracts)
        {
            ServiceImageBinding binding = bindings.Single(b => string.Equals(b.ServiceName, contract.ServiceName, StringComparison.Ordinal));
            binding.ImageRepository.ShouldBe(contract.Repository);
            binding.DaprAppId.ShouldBe(contract.DaprAppId);
            binding.DaprConfig.ShouldBe(contract.DaprConfig);
            binding.ProjectPath.ShouldBe(contract.ProjectPath);
            binding.Registry.ShouldBe("deployment-owned");
            binding.Namespace.ShouldBe("hexalith-production");
        }
    }

    [Fact]
    public void ContainerImageGateShouldPublishArchivesAndEmitMetadataOnlyReport()
    {
        string script = ReadText(GateScriptPath);

        script.ShouldContain("#Requires -Version 7");
        script.ShouldContain("/t:PublishContainer");
        script.ShouldContain("ContainerArchiveOutputPath");
        script.ShouldContain("_bmad-output/gates/container-images/latest.json");
        script.ShouldContain("hexalith-folders-server");
        script.ShouldContain("hexalith-folders-workers");
        script.ShouldContain("hexalith-folders-ui");
        script.ShouldContain("$LASTEXITCODE");
        script.ShouldNotContain("ContainerRegistry", Case.Insensitive);
        script.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
    }

    [Fact]
    public void ContainerAndDeploymentArtifactsShouldNotContainSecretShapedMaterial()
    {
        foreach (string path in SanitizedArtifactPaths())
        {
            string text = ReadText(path);
            foreach (Regex pattern in ForbiddenSecretPatterns())
            {
                pattern.IsMatch(text).ShouldBeFalse($"{path} contains forbidden secret-shaped material matching {pattern}.");
            }
        }
    }

    [Fact]
    public void NewContainerArtifactsShouldNotIntroduceRecursiveSubmoduleSetup()
    {
        Regex recursiveSubmodule = RecursiveSubmodulePattern();
        foreach (string path in SanitizedArtifactPaths())
        {
            recursiveSubmodule.IsMatch(ReadText(path)).ShouldBeFalse($"{path} must not initialize nested submodules recursively.");
        }
    }

    private static string[] RequiredContainerLabels()
        =>
        [
            "org.opencontainers.image.source",
            "org.opencontainers.image.revision",
            "org.opencontainers.image.version",
            "org.opencontainers.image.title",
            "org.opencontainers.image.vendor",
            "org.opencontainers.image.licenses",
            "io.hexalith.project",
            "io.hexalith.service",
        ];

    private static string[] SanitizedArtifactPaths()
        =>
        [
            DirectoryBuildTargetsPath,
            ServiceImagesPath,
            GateScriptPath,
            OperatorDocPath,
            "src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj",
            "src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj",
            "src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj",
            "deploy/dapr/production/sidecar-config-bindings.yaml",
        ];

    private static string GetProperty(XDocument document, string name)
    {
        XElement? element = document.Descendants(name).SingleOrDefault();
        element.ShouldNotBeNull($"Project must contain exactly one {name} property.");
        return element.Value;
    }

    private static ServiceImageBinding ParseServiceImageBinding(YamlMappingNode node)
        => new(
            node.GetScalar("service"),
            node.GetScalar("projectPath"),
            node.GetScalar("imageRepository"),
            node.GetScalar("registry"),
            node.GetScalar("namespace"),
            node.GetScalar("daprAppId"),
            node.GetScalar("daprConfig"));

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static string ReadText(string relativePath)
        => File.ReadAllText(RepositoryPath(relativePath), Encoding.UTF8);

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

    private static Regex[] ForbiddenSecretPatterns()
        =>
        [
            GitHubClassicTokenPattern(),
            GitHubFineGrainedTokenPattern(),
            ClientSecretPattern(),
            PrivateKeyNamePattern(),
            PrivateKeyBlockPattern(),
            PasswordAssignmentPattern(),
            TokenAssignmentPattern(),
            ProductionUrlPattern(),
        ];

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubClassicTokenPattern();

    [GeneratedRegex(@"github_pat_[A-Za-z0-9_]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubFineGrainedTokenPattern();

    [GeneratedRegex(@"\bclient_secret\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClientSecretPattern();

    [GeneratedRegex(@"\bprivate_key\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyNamePattern();

    [GeneratedRegex(@"BEGIN [A-Z ]*PRIVATE KEY", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyBlockPattern();

    [GeneratedRegex(@"\bpassword\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordAssignmentPattern();

    [GeneratedRegex(@"\btoken\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenAssignmentPattern();

    [GeneratedRegex(@"https?://[^\s'""<>]*(?:prod|production)[^\s'""<>]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductionUrlPattern();

    private static Regex RecursiveSubmodulePattern()
        => new(
            string.Concat(@"git\s+submodule\s+update\s+--init\s+", "--", "recursive|", "--", "recursive"),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record ServiceContainerContract(
        string ServiceName,
        string ProjectPath,
        string Repository,
        string DaprAppId,
        string DaprConfig);

    private sealed record ServiceImageBinding(
        string ServiceName,
        string ProjectPath,
        string ImageRepository,
        string Registry,
        string Namespace,
        string DaprAppId,
        string DaprConfig);
}

internal static class ContainerImageYamlNodeExtensions
{
    public static string GetScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        YamlScalarNode scalar = value.ShouldBeOfType<YamlScalarNode>();
        return scalar.Value.ShouldNotBeNull();
    }

    public static YamlMappingNode GetMapping(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    public static YamlSequenceNode GetSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }
}
