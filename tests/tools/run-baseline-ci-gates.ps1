#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'BASELINE-CI-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the baseline CI gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/baseline-ci'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$results = @()

$unitTestProjects = @(
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
        # Story 8.5 AC4 (realizing the 7.18 AC6 "no fail-open --filter" principle): the obsolete exclusion that hid
        # the two provider-boundary guards (OctokitReferencesStayInsideGitHubProviderBoundary,
        # ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies) is removed. Both are green at
        # HEAD (re-scoped by 174d634: Octokit is allow-listed composition-root DI per architecture A-6; the
        # Abstractions folder has no Dapr per S-5), so the baseline lane now runs the full Folders.Tests project and
        # CI proves the guards rather than masking them.
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        # Story 8.5 AC6 triage: this is a deliberate division-of-labor allow-list, not a fail-open mask. The baseline
        # lane runs only the hermetic deployment/conformance classes here; the contract-spine drift, parity-oracle,
        # safety-invariant, and Epic-1 negative-scope guard classes run in the focused contract-spine / safety /
        # contract-parity gates. The full Contracts.Tests project is 256/256 green and covered across those gates.
        # QA gap (qa-generate-e2e-tests, 2026-06-23): the two browserless Playwright-CI-lane conformance classes
        # (Accessibility/E2e CiWorkflowConformanceTests) are added here — they pin the accessibility-gates / e2e-gates
        # jobs + gate scripts + operator docs (pure file/YAML/JSON reads, no Chromium) and previously ran in NO CI
        # lane, which is itself the masked-test anti-pattern AC6 forbids. The baseline lane is their intended home.
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.ContractsSmokeTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.BaselineCiWorkflowConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.AccessibilityCiWorkflowConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.E2eCiWorkflowConformanceTests'
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
        # Story 8.5 AC6 triage: this exclusion is KEPT as genuinely environment-gated, not an obsolete mask. The two
        # codegen-regeneration tests spawn nested out-of-process toolchain work — GeneratedClientAndHelpersMatchIsolatedRegeneration
        # runs `dotnet restore` (network) + `dotnet msbuild /t:Generate...` in a temp tree, and
        # HelperGenerationTargetRegeneratesWhenContractSpineChanges runs `dotnet msbuild` helper regeneration — both of
        # which violate this lane's fast hermetic --no-restore/--no-build contract. The isolated-regeneration assertion
        # is exercised by the contract-and-parity gate (run-contract-parity-ci-gates.ps1, ClientGenerationTests).
        filter = 'FullyQualifiedName!~Hexalith.Folders.Client.Tests.ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration&FullyQualifiedName!~Hexalith.Folders.Client.Tests.ClientGenerationTests.HelperGenerationTargetRegeneratesWhenContractSpineChanges'
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj'
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj'
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj'
        # Story 8.5 AC2 (realizing the 7.18 AC6 "no fail-open --filter" principle): the obsolete exclusion that hid
        # the four governance/scaffold tests (FixtureContractTests.DeferredArtifactAreasCarryMachineCheckableOwnershipNotes,
        # ExitCriteriaDecisionArtifactTests.ExitCriteriaDecisionArtifactsExistWithRequiredDecisionShape, and the two
        # ScaffoldContractTests.{SolutionContainsOnlyCanonicalBuildableProjects,ProjectReferencesFollowAllowedDependencyDirection})
        # is removed. All four are green at HEAD (fixed by 174d634 scaffold split + 103fa18 governance docs), so the
        # baseline lane now runs the full Testing.Tests project (60/60) and CI proves them rather than masking them.
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj'
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj'
        # Story 8.5 AC6 triage: the former TenantSubscriptionEndpointShould exclusion is RE-INCLUDED. The three
        # endpoint tests are green and hermetic — they start an in-process slim WebApplication bound to
        # 127.0.0.1:0 with an in-memory projection store and need NO Dapr sidecar (the earlier "needs a sidecar"
        # speculation is disproven: full Workers.Tests is 19/19 in ~300ms with no external dependency). Workers.Tests
        # runs in no other focused gate, so leaving them masked would hide green tests (the 7.18 AC6 anti-pattern).
        filter = ''
    },
    # Hermetic SDK lifecycle example tests (RecordingHandler, no AppHost/Dapr/network). Running them here
    # in PR CI satisfies the "examples ... validated by CI" clause for the consumer SDK quickstart/reference.
    [ordered]@{
        project_path = 'samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj'
        filter = ''
    }
)

function Write-BaselineCiReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results
    )

    [ordered]@{
        gate = 'baseline-ci'
        status = $Status
        report_path = '_bmad-output/gates/baseline-ci/latest.json'
        diagnostic_policy = 'metadata-only'
        solution = 'Hexalith.Folders.slnx'
        categories = @('restore', 'build', 'format', 'lint', 'unit-tests')
        unit_test_projects = $unitTestProjects
        results = $Results
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-BaselineCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$ProjectPath = ''
    )

    Write-Host "BASELINE-CI category=$Category project=$ProjectPath"
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    $script:results += [ordered]@{
        category = $Category
        project_path = $ProjectPath
        status = $status
        exit_code = $exitCode
    }
    Write-BaselineCiReport -Status $status -Results $script:results

    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-BaselineCiReport -Status 'discovered' -Results $results

    if (-not $SkipRestoreBuild) {
        Invoke-BaselineCommand -Category 'restore' -Arguments @('restore', 'Hexalith.Folders.slnx', '-m:1', '-p:NuGetAudit=false')
        Invoke-BaselineCommand -Category 'build' -Arguments @('build', 'Hexalith.Folders.slnx', '--no-restore', '-m:1')
    }

    # Scope format/lint to this repository's own source (src/tests/samples). The host
    # build requires sibling submodule working trees to be present, but those submodules
    # are independent repositories with their own formatting standards (e.g. CRLF
    # line-endings) and must not be evaluated by this repository's baseline gate.
    Invoke-BaselineCommand -Category 'format' -Arguments @('format', 'whitespace', 'Hexalith.Folders.slnx', '--verify-no-changes', '--no-restore', '--include', './src/', './tests/', './samples/')
    Invoke-BaselineCommand -Category 'lint' -Arguments @('format', 'analyzers', 'Hexalith.Folders.slnx', '--verify-no-changes', '--no-restore', '--severity', 'warn', '--include', './src/', './tests/', './samples/')

    foreach ($testProject in $unitTestProjects) {
        $testArguments = @('test', $testProject.project_path, '--no-restore', '--no-build')
        if (-not [string]::IsNullOrWhiteSpace($testProject.filter)) {
            $testArguments += @('--filter', $testProject.filter)
        }

        Invoke-BaselineCommand -Category 'unit-tests' -ProjectPath $testProject.project_path -Arguments $testArguments
    }

    Write-BaselineCiReport -Status 'passed' -Results $results
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
