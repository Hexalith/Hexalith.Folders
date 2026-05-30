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
        filter = 'FullyQualifiedName!~Hexalith.Folders.Tests.Providers.GitHub.GitHubDependencyGuardTests.OctokitReferencesStayInsideGitHubProviderBoundary&FullyQualifiedName!~Hexalith.Folders.Tests.Providers.Abstractions.ProviderCapabilityBoundaryTests.ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies'
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.ContractsSmokeTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.BaselineCiWorkflowConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests'
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
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
        filter = 'FullyQualifiedName!~Hexalith.Folders.Testing.Tests.FixtureContractTests.DeferredArtifactAreasCarryMachineCheckableOwnershipNotes&FullyQualifiedName!~Hexalith.Folders.Testing.Tests.ExitCriteriaDecisionArtifactTests.ExitCriteriaDecisionArtifactsExistWithRequiredDecisionShape&FullyQualifiedName!~Hexalith.Folders.Testing.Tests.ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects&FullyQualifiedName!~Hexalith.Folders.Testing.Tests.ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection'
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj'
        filter = ''
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj'
        filter = 'FullyQualifiedName!~Hexalith.Folders.Workers.Tests.WorkersTenantEventTests.TenantSubscriptionEndpointShould'
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
