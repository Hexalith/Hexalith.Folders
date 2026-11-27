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
    Write-Error 'PRODUCTION-OBSERVABILITY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the production observability gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/production-observability'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.ProductionObservabilityManifestShouldDeclareSanitizedExporterHealthAndSignalIntent',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.ServiceDefaultsShouldExportThreeSignalFamiliesAndSplitLivenessReadiness',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.OperationalSignalInstrumentsShouldBeBoundedAndCentralizedOnTheSingleMeter',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.DeadLetterTopicsShouldBeDeclaredInProductionPubSub',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.ProductionObservabilityGateScriptShouldFailClosedAndEmitBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.ContractSpineWorkflowAndBaselineCiShouldWireObservabilityGate',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.GovernanceEvidenceAndC2DocsShouldRecordStory712Ownership',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.OperationsDocShouldDocumentValidationAndMetadataOnlyPolicy',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.ProductionObservabilityLatestReportShouldStayMetadataOnlyWhenPresent',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests.NegativeControlsRejectVacuousAndUnsafeObservabilityEvidence'
)

function Write-ProductionObservabilityReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'production-observability'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/production-observability/latest.json'
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        canonical_inputs = @(
            'deploy/observability/production/observability.yaml',
            'deploy/dapr/production/pubsub.yaml',
            'src/Hexalith.Folders.ServiceDefaults/Extensions.cs',
            'src/Hexalith.Folders.ServiceDefaults/MonitoredSnapshotReadinessCheck.cs',
            'src/Hexalith.Folders/Observability/FolderTelemetryNames.cs',
            'src/Hexalith.Folders/Observability/FolderTelemetryEmitter.cs',
            'docs/exit-criteria/c2-freshness.md',
            'docs/exit-criteria/c0-c13-governance-evidence.yaml',
            'docs/operations/production-observability.md'
        )
        signal_categories = @(
            [ordered]@{ signal = 'projection_lag'; severity = 'warning'; threshold_source = 'docs/exit-criteria/c2-freshness.md'; owning_component = 'folders-server' },
            [ordered]@{ signal = 'dead_letter_depth'; severity = 'warning'; threshold_source = 'architecture-i7'; owning_component = 'folders-workers' },
            [ordered]@{ signal = 'provider_failure'; severity = 'error'; threshold_source = 'architecture-i7'; owning_component = 'folders-workers' },
            [ordered]@{ signal = 'stale_lock'; severity = 'warning'; threshold_source = 'architecture-process-patterns'; owning_component = 'folders-server' },
            [ordered]@{ signal = 'cleanup_failure'; severity = 'error'; threshold_source = 'prd-cleanup-observability'; owning_component = 'folders-workers' }
        )
        exporter_signals = @('traces', 'metrics', 'logs')
        health_probes = @(
            [ordered]@{ owning_component = 'folders-server'; liveness = '/alive'; readiness = '/ready' },
            [ordered]@{ owning_component = 'folders-workers'; liveness = '/alive'; readiness = '/ready' }
        )
        # Live exporter/alert firing against a real backend lives outside this repo per the ops runbook.
        live_exporter_alert_smoke = [ordered]@{
            status = 'reference_pending_story_7_12'
            severity = 'warning'
            owner = 'Release Readiness'
            command_shape = 'pwsh ./tests/tools/run-production-observability-gates.ps1'
            evidence_path = '_bmad-output/gates/production-observability/latest.json'
            follow_up_boundary = 'metadata-only intent only; no live endpoints, tokens, or backend assertions in CI'
        }
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ProductionObservabilityReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-ProductionObservabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'production-observability-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode $testExitCode
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
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode 1
        Write-Error "PRODUCTION-OBSERVABILITY-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-ProductionObservabilityReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
