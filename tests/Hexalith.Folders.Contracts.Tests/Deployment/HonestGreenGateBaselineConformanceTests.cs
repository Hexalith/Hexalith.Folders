using System.Reflection;
using System.Text;

using Shouldly;

using Xunit;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests.Deployment;

/// <summary>
/// Epic 8 action-item closure / Epic 11 honest-green gate baseline guard (delivered via bmad-correct-course
/// <c>sprint-change-proposal-2026-07-07-honest-green-gate-baseline.md</c>). Consolidated, belt-and-suspenders
/// conformance test that fails if a CI change (Stories 11.3/11.7/11.11/11.13) drops either UI-facing gate job,
/// narrows the full UI E2E lane to a subset, reintroduces a forbidden hermetic-lane substring (AD7), or deletes
/// one of the CI-workflow conformance classes that pin those invariants. It complements — never replaces — the
/// per-gate <see cref="E2eCiWorkflowConformanceTests"/> and <see cref="AccessibilityCiWorkflowConformanceTests"/>:
/// even if one of those is individually weakened, the base invariants are re-checked here.
/// </summary>
public sealed class HonestGreenGateBaselineConformanceTests
{
    private const string WorkflowPath = ".github/workflows/ci.yml";
    private const string E2eGateScriptPath = "tests/tools/run-e2e-ci-gates.ps1";
    private const string AccessibilityGateScriptPath = "tests/tools/run-accessibility-ci-gates.ps1";

    // The two UI-facing gate jobs that constitute the honest-green baseline: the full UI E2E lane and the
    // axe / WCAG 2.2 AA accessibility lane. Both must stay present and blocking in ci.yml.
    private static readonly string[] _blockingUiGateJobs =
    [
        "accessibility-gates",
        "e2e-gates",
    ];

    // AD7: substrings that would turn a hermetic localhost gate into a non-hermetic / secret-bearing / publishing
    // lane. None may appear anywhere in ci.yml. `--recursive` is guarded separately (obfuscated at the call site).
    private static readonly string[] _forbiddenCiSubstrings =
    [
        "upload-artifact",
        "secrets.",
        "services:",
        "dotnet publish",
        "docker",
        "playwright install",
    ];

    [Fact]
    public void CiWorkflowKeepsBothUiGateJobsPresentAndBlocking()
    {
        YamlMappingNode workflow = LoadSingleYamlDocument(WorkflowPath);
        YamlMappingNode jobs = GetMapping(workflow, "jobs");
        string workflowText = ReadText(WorkflowPath);

        foreach (string job in _blockingUiGateJobs)
        {
            jobs.Children.ContainsKey(new YamlScalarNode(job))
                .ShouldBeTrue($"ci.yml must keep the '{job}' gate job — the honest-green baseline requires it present.");

            YamlMappingNode jobNode = GetMapping(jobs, job);

            // Blocking = the job fails the workflow run. A gate job must never be soft-failed via continue-on-error: true.
            if (jobNode.Children.TryGetValue(new YamlScalarNode("continue-on-error"), out YamlNode? continueOnError))
            {
                continueOnError.ToString().ShouldNotBe("true", $"The '{job}' gate job must stay blocking (continue-on-error must not be true).");
            }
        }

        // Presence must mean "runs its gate", not an empty stub: both focused gate scripts are invoked in ci.yml.
        workflowText.ShouldContain("./tests/tools/run-e2e-ci-gates.ps1 -SkipBrowserInstall", Case.Sensitive);
        workflowText.ShouldContain("./tests/tools/run-accessibility-ci-gates.ps1 -SkipBrowserInstall", Case.Sensitive);
    }

    [Fact]
    public void FullUiE2eLaneIsNeverNarrowedToASubset()
    {
        // The no-filter invariant: the E2E gate runs the FULL Hexalith.Folders.UI.E2E.Tests project (all 63 cases),
        // never a namespace / FQN subset. The accessibility gate legitimately scopes to its own namespace, so this
        // narrowing check applies only to the full-lane script.
        string script = ReadText(E2eGateScriptPath);

        script.ShouldNotContain("--filter", Case.Sensitive);
        script.ShouldNotContain("-namespace", Case.Sensitive);
        script.ShouldNotContain("FullyQualifiedName", Case.Sensitive);

        script.ShouldContain("full-ui-e2e-lane", Case.Sensitive);
        script.ShouldContain("Hexalith.Folders.UI.E2E.Tests", Case.Sensitive);
    }

    [Fact]
    public void CiWorkflowAndGateScriptsStayFreeOfForbiddenHermeticLaneSubstrings()
    {
        foreach (string relativePath in new[] { WorkflowPath, E2eGateScriptPath, AccessibilityGateScriptPath })
        {
            string content = ReadText(relativePath);
            foreach (string forbidden in _forbiddenCiSubstrings)
            {
                content.ShouldNotContain(forbidden, Case.Insensitive);
            }

            content.ShouldNotContain(string.Concat("--", "recursive"), Case.Insensitive);
        }
    }

    [Fact]
    public void CiWorkflowConformanceClassesRemainPresentAndUnweakened()
    {
        // Compile-time deletion guard: removing any of these classes breaks this file's build. The runtime assertions
        // below additionally catch a class being emptied of its defining [Fact] methods.
        (Type Type, string[] RequiredFactMethods)[] pinnedConformanceClasses =
        [
            (typeof(E2eCiWorkflowConformanceTests),
                ["E2eGateScriptProvisionsBrowserRunsFullLaneAndFailsClosed", "E2eWorkflowAvoidsForbiddenInfrastructureSubstrings"]),
            (typeof(AccessibilityCiWorkflowConformanceTests),
                ["AccessibilityGateScriptProvisionsBrowserRunsScopedTestsAndFailsClosed", "AccessibilityWorkflowAvoidsForbiddenInfrastructureSubstrings"]),
            (typeof(BaselineCiWorkflowConformanceTests), []),
            (typeof(ContractParityCiWorkflowConformanceTests), []),
            (typeof(SecurityRedactionCiWorkflowConformanceTests), []),
        ];

        foreach ((Type type, string[] requiredFactMethods) in pinnedConformanceClasses)
        {
            type.Namespace.ShouldBe("Hexalith.Folders.Contracts.Tests.Deployment");
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0)
                .ShouldBeTrue($"{type.Name} must retain at least one [Fact] conformance assertion.");

            foreach (string requiredMethod in requiredFactMethods)
            {
                type.GetMethod(requiredMethod, BindingFlags.Public | BindingFlags.Instance)
                    .ShouldNotBeNull($"{type.Name} must keep its '{requiredMethod}' invariant assertion.");
            }
        }
    }

    private static YamlMappingNode LoadSingleYamlDocument(string relativePath)
    {
        using StreamReader reader = File.OpenText(RepositoryPath(relativePath));
        YamlStream stream = new();
        stream.Load(reader);
        stream.Documents.Count.ShouldBe(1);
        return stream.Documents[0].RootNode.ShouldBeOfType<YamlMappingNode>();
    }

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value).ShouldBeTrue($"Missing YAML mapping key '{key}'.");
        return value.ShouldBeOfType<YamlMappingNode>();
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
}
