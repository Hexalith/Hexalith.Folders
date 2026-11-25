using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;
using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

public sealed partial class ReleasePackageConformanceTests
{
    private const string ManifestPath = "tools/release-packages.json";
    private const string PolicyPath = "deploy/nuget/release-packages.yaml";

    private static readonly string[] _expectedPackages =
    [
        "Hexalith.Folders.Contracts",
        "Hexalith.Folders.EventStoreExtensions",
        "Hexalith.Folders",
        "Hexalith.Folders.Client",
        "Hexalith.Folders.Aspire",
        "Hexalith.Folders.Testing",
    ];

    [Fact]
    public void JsonManifestShouldBeTheSingleExactReleasePackageInventory()
    {
        using JsonDocument document = JsonDocument.Parse(ReadText(ManifestPath));
        JsonElement[] packages = document.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        packages.Select(static package => package.GetProperty("id").GetString()).ShouldBe(_expectedPackages);
        packages.Select(static package => package.GetProperty("project").GetString()).Distinct(StringComparer.Ordinal).Count().ShouldBe(_expectedPackages.Length);

        foreach (JsonElement package in packages)
        {
            string projectPath = package.GetProperty("project").GetString().ShouldNotBeNull();
            XDocument project = XDocument.Load(RepositoryPath(projectPath));
            GetProperty(project, "IsPackable").ShouldBe("true");
        }

        YamlMappingNode policy = LoadSingleYamlDocument(PolicyPath);
        policy.GetReleaseScalar("kind").ShouldBe("ReleasePackagePolicy");
        policy.GetReleaseScalar("inventoryPath").ShouldBe(ManifestPath);
        policy.GetReleaseScalar("expectedPackageCount").ShouldBe(_expectedPackages.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        policy.GetReleaseScalar("feed").ShouldBe("https://api.nuget.org/v3/index.json");
        policy.GetReleaseScalar("symbolsRequired").ShouldBe("true");
        policy.GetReleaseScalar("duplicatePolicy").ShouldBe("fail");
        policy.Children.ContainsKey(new YamlScalarNode("releaseSet")).ShouldBeFalse(
            "The deployment policy must point to the JSON inventory rather than duplicate its package list.");
        policy.GetReleaseSequence("excludedPackableProjects").Children.Select(static value => value.ToString())
            .ShouldBe([
                "src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj",
                "src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj",
            ]);
    }

    [Fact]
    public void FoldersAspireShouldUseAspire17DaprIntegration()
    {
        string packagesPath = RepositoryPath("references/Hexalith.Builds/Props/Directory.Packages.props");
        if (!File.Exists(packagesPath))
        {
            packagesPath = RepositoryPath("../Hexalith.Builds/Props/Directory.Packages.props");
        }
        XDocument packages = XDocument.Load(packagesPath);
        XElement catalogDefaultDaprVersion = packages.Descendants("HexalithAspireHostingDaprVersion")
            .Single(element => element.Attribute("Condition") is null);
        XElement daprIntegration = packages.Descendants("PackageVersion")
            .Single(element => string.Equals(
                (string?)element.Attribute("Include"),
                "CommunityToolkit.Aspire.Hosting.Dapr",
                StringComparison.Ordinal));

        ((string?)daprIntegration.Attribute("Version")).ShouldBe("$(HexalithAspireHostingDaprVersion)");

        XDocument localPackages = XDocument.Load(RepositoryPath("Directory.Packages.props"));
        XElement foldersDaprVersion = localPackages.Descendants("HexalithAspireHostingDaprVersion")
            .Single(element => element.Attribute("Condition") is null);
        XElement foldersDaprOverride = localPackages.Descendants("PackageVersion")
            .Single(element => string.Equals(
                (string?)element.Attribute("Update"),
                "CommunityToolkit.Aspire.Hosting.Dapr",
                StringComparison.Ordinal));
        // The catalog has a project-specific Folders.Aspire fallback. The module's
        // later PackageVersion update selects the supported Aspire 17 integration.
        foldersDaprVersion.Value.ShouldBe(catalogDefaultDaprVersion.Value);
        foldersDaprVersion.Value.ShouldMatch(@"^17\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$");
        ((string?)foldersDaprOverride.Attribute("Version")).ShouldBe("$(HexalithAspireHostingDaprVersion)");

        XDocument foldersAspire = XDocument.Load(RepositoryPath("src/Hexalith.Folders.Aspire/Hexalith.Folders.Aspire.csproj"));
        foldersAspire.Descendants("PackageReference")
            .Single(element => (string?)element.Attribute("Include") == "CommunityToolkit.Aspire.Hosting.Dapr")
            .Attribute("VersionOverride").ShouldBeNull();
        GetProperty(foldersAspire, "NoWarn").ShouldContain("NU5104");
    }

    [Fact]
    public void MtpAndSupplyChainConfigurationShouldRemainEnabled()
    {
        using JsonDocument globalJson = JsonDocument.Parse(ReadText("global.json"));
        globalJson.RootElement.GetProperty("test").GetProperty("runner").GetString()
            .ShouldBe("Microsoft.Testing.Platform");
        ReadText("Directory.Build.targets")
            .ShouldContain("Microsoft.Testing.Extensions.CodeCoverage", Case.Sensitive);

    }

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
            if (File.Exists(candidate) || Directory.Exists(candidate))
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

    private static string GetProperty(XDocument document, string name)
    {
        XElement? element = document.Descendants(name).SingleOrDefault();
        element.ShouldNotBeNull($"Project must contain exactly one {name} property.");
        return element.Value;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.String);
        return property.GetString().ShouldNotBeNull();
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out JsonElement property).ShouldBeTrue($"Missing JSON property '{propertyName}'.");
        property.ValueKind.ShouldBe(JsonValueKind.Array);
        return property.EnumerateArray().Select(static item => item.GetString().ShouldNotBeNull()).ToArray();
    }

    private static void AssertMetadataOnlyJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    AssertMetadataOnlyJson(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertMetadataOnlyJson(item);
                }
                break;
            case JsonValueKind.String:
                string value = element.GetString().ShouldNotBeNull();
                RootedPathPattern().IsMatch(value).ShouldBeFalse();
                ForbiddenReportDiagnosticPattern().IsMatch(value).ShouldBeFalse();
                break;
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|/|\\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPathPattern();

    [GeneratedRegex(@"secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenReportDiagnosticPattern();

    [GeneratedRegex(@"ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|\bclient_secret\b|\bprivate_key\b|BEGIN [A-Z ]*PRIVATE KEY|\bpassword\s*=|\btoken\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenCredentialPattern();
}

internal static class ReleasePackageYamlNodeExtensions
{
    public static string GetReleaseScalar(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML scalar key '{key}'.");
        return value.ShouldBeOfType<YamlScalarNode>().Value.ShouldNotBeNull();
    }

    public static YamlMappingNode GetReleaseMapping(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
    }

    public static YamlSequenceNode GetReleaseSequence(this YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML sequence key '{key}'.");
        return value.ShouldBeOfType<YamlSequenceNode>();
    }
}
