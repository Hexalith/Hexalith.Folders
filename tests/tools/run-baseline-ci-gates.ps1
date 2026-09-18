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
        # Story 8.5 AC4 (realizing the 7.18 AC6 "no fail-open selection" principle): the obsolete exclusion that hid
        # the two provider-boundary guards (OctokitReferencesStayInsideGitHubProviderBoundary,
        # ProviderAbstractionsShouldNotReferenceOutOfScopeRuntimeOrAdapterDependencies) is removed. Both are green at
        # HEAD (re-scoped by 174d634: Octokit is allow-listed composition-root DI per architecture A-6; the
        # Abstractions folder has no Dapr per S-5), so the baseline lane now runs the full Folders.Tests project and
        # CI proves the guards rather than masking them.
        runner_arguments = @()
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        # Deployment governance stays in this hermetic lane; deeper contract/parity classes run in focused gates.
        # Each class is invoked independently so a renamed or removed selector cannot be hidden by another class.
        runner_classes = @(
            'Hexalith.Folders.Contracts.Tests.ContractsSmokeTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.BaselineCiWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.RetentionAndTenantDeletionConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.AccessibilityCiWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.E2eCiWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.CapacityCalibrationConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.CapacitySmokeCiWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ContractParityCiWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.ScheduledDriftAndPolicyWorkflowConformanceTests',
            'Hexalith.Folders.Contracts.Tests.Deployment.SecurityRedactionCiWorkflowConformanceTests'
        )
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
        # Out-of-process regeneration is covered by the focused contract/parity gate.
        runner_arguments = @(
            '-method-', 'Hexalith.Folders.Client.Tests.ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration',
            '-method-', 'Hexalith.Folders.Client.Tests.ClientGenerationTests.HelperGenerationTargetRegeneratesWhenContractSpineChanges'
        )
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj'
        runner_arguments = @()
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj'
        runner_arguments = @()
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj'
        # Story 8.5 AC2 (realizing the 7.18 AC6 "no fail-open selection" principle): the obsolete exclusion that hid
        # the four governance/scaffold tests (FixtureContractTests.DeferredArtifactAreasCarryMachineCheckableOwnershipNotes,
        # ExitCriteriaDecisionArtifactTests.ExitCriteriaDecisionArtifactsExistWithRequiredDecisionShape, and the two
        # ScaffoldContractTests.{SolutionContainsOnlyCanonicalBuildableProjects,ProjectReferencesFollowAllowedDependencyDirection})
        # is removed. All four are green at HEAD (fixed by 174d634 scaffold split + 103fa18 governance docs), so the
        # baseline lane now runs the full Testing.Tests project (60/60) and CI proves them rather than masking them.
        runner_arguments = @()
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj'
        runner_arguments = @()
    },
    [ordered]@{
        project_path = 'tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj'
        # Story 8.5 AC6 triage: the former TenantSubscriptionEndpointShould exclusion is RE-INCLUDED. The three
        # endpoint tests are green and hermetic — they start an in-process slim WebApplication bound to
        # 127.0.0.1:0 with an in-memory projection store and need NO Dapr sidecar (the earlier "needs a sidecar"
        # speculation is disproven: full Workers.Tests is 19/19 in ~300ms with no external dependency). Workers.Tests
        # runs in no other focused gate, so leaving them masked would hide green tests (the 7.18 AC6 anti-pattern).
        runner_arguments = @()
    },
    # Hermetic SDK lifecycle example tests (RecordingHandler, no AppHost/Dapr/network). Running them here
    # in PR CI satisfies the "examples ... validated by CI" clause for the consumer SDK quickstart/reference.
    [ordered]@{
        project_path = 'samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj'
        runner_arguments = @()
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
        solution = 'Hexalith.Folders.CI.slnx'
        categories = @('dependency-mode', 'restore', 'build', 'format', 'lint', 'unit-tests', 'package-mode-restore', 'package-mode-build', 'package-mode-test')
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

function Invoke-BaselineSelectedClass {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$TestAssembly,
        [Parameter(Mandatory = $true)][string]$TestClass
    )

    $resultName = ($TestClass -replace '[^A-Za-z0-9_.-]', '_') + '.trx'
    $arguments = @(
        $TestAssembly,
        '-noLogo',
        '-noColor',
        '-class',
        $TestClass,
        '-result-trx',
        (Join-Path $reportDirectory $resultName)
    )
    Write-Host "BASELINE-CI category=unit-tests project=$ProjectPath selector=$TestClass"
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -eq 0 -and -not (($output -join [Environment]::NewLine) -match 'Total:\s+[1-9]\d*')) {
        Write-Host "BASELINE-CI category=unit-tests project=$ProjectPath status=failed reason=test-selection-drift selector=$TestClass observed=0"
        $exitCode = 1
    }

    $status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    $script:results += [ordered]@{
        category = 'unit-tests'
        project_path = $ProjectPath
        selector = $TestClass
        status = $status
        exit_code = $exitCode
    }
    Write-BaselineCiReport -Status $status -Results $script:results
    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

function Assert-DependencyMode {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [AllowEmptyCollection()][string[]]$AdditionalArguments = @(),
        [Parameter(Mandatory = $true)][hashtable]$ExpectedProperties
    )

    $projectPath = 'tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj'
    $propertyNames = @(
        'Configuration',
        'UseHexalithProjectReferences',
        'UseNuGetDeps',
        'HexalithEventStoreFromSource',
        'HexalithTenantsFromSource',
        'HexalithMemoriesFromSource',
        'HexalithFrontComposerFromSource',
        'HexalithFrontComposerTestingFromSource'
    )
    $arguments = @('msbuild', $projectPath, "-getProperty:$($propertyNames -join ',')") + $AdditionalArguments

    Write-Host "BASELINE-CI category=dependency-mode project=$projectPath evaluation=$Label"
    $rawOutput = (& dotnet @arguments | Out-String)
    $exitCode = $LASTEXITCODE
    $status = 'failed'
    if ($exitCode -eq 0) {
        try {
            $evaluation = $rawOutput | ConvertFrom-Json
            foreach ($propertyName in $ExpectedProperties.Keys) {
                $actual = [string]$evaluation.Properties.$propertyName
                $expected = [string]$ExpectedProperties[$propertyName]
                if ($actual -cne $expected) {
                    throw "Dependency mode evaluation '$Label' expected $propertyName='$expected' but found '$actual'."
                }
            }

            $status = 'passed'
        }
        catch {
            Write-Host "BASELINE-CI dependency-mode failure: $($_.Exception.Message)" -ForegroundColor Red
            $exitCode = 1
        }
    }

    $script:results += [ordered]@{
        category = 'dependency-mode'
        project_path = "$projectPath [$Label]"
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

    $sourceModeProperties = @{
        Configuration = 'Debug'
        UseHexalithProjectReferences = 'true'
        UseNuGetDeps = 'false'
        HexalithEventStoreFromSource = 'true'
        HexalithTenantsFromSource = 'true'
        HexalithMemoriesFromSource = 'true'
        HexalithFrontComposerFromSource = 'true'
        HexalithFrontComposerTestingFromSource = 'true'
    }
    Assert-DependencyMode -Label 'default' -AdditionalArguments @('-p:CI=false') -ExpectedProperties $sourceModeProperties
    Assert-DependencyMode -Label 'debug' -AdditionalArguments @('-p:CI=false', '-p:Configuration=Debug') -ExpectedProperties $sourceModeProperties
    $packageModeProperties = @{
        Configuration = 'Release'
        UseHexalithProjectReferences = 'false'
        UseNuGetDeps = 'true'
        HexalithEventStoreFromSource = ''
        HexalithTenantsFromSource = ''
        HexalithMemoriesFromSource = ''
        HexalithFrontComposerFromSource = ''
        HexalithFrontComposerTestingFromSource = ''
    }
    Assert-DependencyMode -Label 'release-package' -AdditionalArguments @('-p:Configuration=Release', '-p:UseNuGetDeps=true') -ExpectedProperties $packageModeProperties
    $ciPackageModeProperties = $packageModeProperties.Clone()
    $ciPackageModeProperties.Configuration = 'Debug'
    Assert-DependencyMode -Label 'ci-package' -AdditionalArguments @('-p:CI=true') -ExpectedProperties $ciPackageModeProperties

    if (-not $SkipRestoreBuild) {
        Invoke-BaselineCommand -Category 'restore' -Arguments @('restore', 'Hexalith.Folders.CI.slnx', '-p:Configuration=Release', '-p:UseNuGetDeps=true', '-m:1')
        Invoke-BaselineCommand -Category 'build' -Arguments @('build', 'Hexalith.Folders.CI.slnx', '--configuration', 'Release', '-p:UseNuGetDeps=true', '--no-restore', '-warnaserror', '-m:1')
    }

    # Scope format/lint to this repository's own source (src/tests/samples). The host
    # build requires sibling submodule working trees to be present, but those submodules
    # are independent repositories with their own formatting standards (e.g. CRLF
    # line-endings) and must not be evaluated by this repository's baseline gate.
    Invoke-BaselineCommand -Category 'format' -Arguments @('format', 'whitespace', 'Hexalith.Folders.CI.slnx', '--verify-no-changes', '--no-restore', '--include', './src/', './tests/', './samples/')
    Invoke-BaselineCommand -Category 'lint' -Arguments @('format', 'analyzers', 'Hexalith.Folders.CI.slnx', '--verify-no-changes', '--no-restore', '--severity', 'warn', '--include', './src/', './tests/', './samples/')

    foreach ($testProject in $unitTestProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($testProject.project_path)
        if ($testProject.Contains('runner_classes')) {
            $projectDirectory = Split-Path -Parent $testProject.project_path
            $testAssembly = Join-Path $projectDirectory "bin/Release/net10.0/$projectName.dll"
            foreach ($testClass in $testProject.runner_classes) {
                Invoke-BaselineSelectedClass -ProjectPath $testProject.project_path -TestAssembly $testAssembly -TestClass $testClass
            }
            continue
        }

        if ($testProject.runner_arguments.Count -gt 0) {
            $projectDirectory = Split-Path -Parent $testProject.project_path
            $testAssembly = Join-Path $projectDirectory "bin/Release/net10.0/$projectName.dll"
            $testArguments = @($testAssembly, '-noLogo', '-noColor') + @($testProject.runner_arguments) + @('-result-trx', (Join-Path $reportDirectory "$projectName.trx"))
        }
        else {
            $testArguments = @('test', $testProject.project_path, '--configuration', 'Release', '--no-restore', '--no-build', '--report-xunit-trx', '--report-xunit-trx-filename', "$projectName.trx")
        }

        Invoke-BaselineCommand -Category 'unit-tests' -ProjectPath $testProject.project_path -Arguments $testArguments
    }

    $packageModeProject = 'tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj'
    Invoke-BaselineCommand -Category 'package-mode-restore' -ProjectPath $packageModeProject -Arguments @('restore', $packageModeProject, '-p:Configuration=Release', '-p:UseNuGetDeps=true', '--force', '-m:1')
    Invoke-BaselineCommand -Category 'package-mode-build' -ProjectPath $packageModeProject -Arguments @('build', $packageModeProject, '-c', 'Release', '-p:UseNuGetDeps=true', '--no-restore', '-m:1')
    Invoke-BaselineCommand -Category 'package-mode-test' -ProjectPath $packageModeProject -Arguments @('test', $packageModeProject, '-c', 'Release', '-p:UseNuGetDeps=true', '--no-restore', '--no-build')

    Write-BaselineCiReport -Status 'passed' -Results $results
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
