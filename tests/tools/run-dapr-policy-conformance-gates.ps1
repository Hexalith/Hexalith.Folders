#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'DAPR-POLICY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the Dapr policy conformance gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/dapr-policy-conformance'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.ProductionAccessControlPolicyShouldBeDenyByDefaultAndMatchFixtureProvenance',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.ProductionDaprSystemConfigurationShouldEnableMtlsWithoutSecretMaterial',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.PolicyConformanceFixtureShouldCoverAllowedAndDeniedTriples',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.LocalDevelopmentAccessControlShouldRemainPermissiveAndMarkedLocalOnly',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.ProductionSidecarBindingsShouldAttachEveryAppToItsAccessControlConfiguration',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.ProductionPubSubComponentShouldConstrainTenantEventTopicScopes',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.ProductionSecretStoreArtifactsShouldBeReferenceOnlyAndDenyByDefault',
    'Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests.WorkflowAndScriptShouldWireOfflineDaprPolicyConformanceGate'
)

function Write-DaprPolicyConformanceReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'dapr-policy-conformance'
        status = $Status
        exit_code = $ExitCode
        canonical_inputs = @(
            'deploy/dapr/production/accesscontrol.yaml',
            'deploy/dapr/production/daprsystem.yaml',
            'deploy/dapr/production/pubsub.yaml',
            'deploy/dapr/production/sidecar-config-bindings.yaml',
            'tests/fixtures/dapr-policy-conformance.yaml'
        )
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        report_path = '_bmad-output/gates/dapr-policy-conformance/latest.json'
        diagnostic_policy = 'metadata-only'
        live_dapr_kind_gate = 'reference_pending_story_7_8'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-DaprPolicyConformanceReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'dapr-policy-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    $passedMethods = @()
    try {
        [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
        $passedIds = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -ceq 'Passed' } |
            ForEach-Object { $_.testId })
        $passedMethods = @($trx.TestRun.TestDefinitions.UnitTest | Where-Object { $_.id -cin $passedIds } |
            ForEach-Object { "$($_.TestMethod.className).$($_.TestMethod.name)" })
    }
    catch {
        # Report missing or malformed results through the bounded gate diagnostic.
        $passedMethods = @()
    }

    $missingMethods = @($runnerMethods | Where-Object { $_ -cnotin $passedMethods })
    if ($missingMethods.Count -gt 0) {
        Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode 1
        Write-Error "DAPR-POLICY-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-DaprPolicyConformanceReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
