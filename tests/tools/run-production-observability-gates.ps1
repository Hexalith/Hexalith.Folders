#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests'
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
        health_probes = @('/health/live', '/health/ready')
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

function Get-ExecutedTestCount {
    param(
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Output
    )

    $joined = ($Output -join [Environment]::NewLine)
    $total = 0
    foreach ($match in [regex]::Matches($joined, 'Total:\s+(\d+)')) {
        $total += [int]$match.Groups[1].Value
    }

    return $total
}

function Invoke-XunitInProcessFallback {
    $script:usedXunitFallback = $true
    Write-Host 'PRODUCTION-OBSERVABILITY category=static-observability-shape vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode 1
        Write-Error 'PRODUCTION-OBSERVABILITY-GATE-VACUOUS: xUnit in-process runner missing for ProductionObservabilityConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode 1
        Write-Error "PRODUCTION-OBSERVABILITY-GATE-VACUOUS: expected $($runnerMethods.Count) observability conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ProductionObservabilityReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-ProductionObservabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-ProductionObservabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'production-observability-conformance.trx'
    $trxPath = Join-Path $reportDirectory $trxName
    if (Test-Path $trxPath) {
        Remove-Item $trxPath -Force
    }

    $testOutput = dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter $testFilter --results-directory $reportDirectory --logger "trx;LogFileName=$trxName" 2>&1
    if ($LASTEXITCODE -ne 0) {
        if (($testOutput -join [Environment]::NewLine) -match 'System\.Net\.Sockets\.SocketException.*Permission denied') {
            Invoke-XunitInProcessFallback
        }
        else {
            Write-ProductionObservabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testOutput | ForEach-Object { Write-Host $_ }
    }

    # Fail closed if the namespace/type filter ever drifts and silently matches zero conformance facts
    # (VSTest exits 0 on an empty filter), which would otherwise let this load-bearing gate pass vacuously.
    [int]$executedTests = 0
    if (-not $usedXunitFallback -and (Test-Path $trxPath)) {
        [xml]$trx = Get-Content -Raw -Path $trxPath
        $executedTests = [int]$trx.TestRun.ResultSummary.Counters.total
    }

    if (-not $usedXunitFallback -and $executedTests -lt $runnerMethods.Count) {
        Write-ProductionObservabilityReport -Status 'failed' -ExitCode 1
        Write-Error "PRODUCTION-OBSERVABILITY-GATE-VACUOUS: expected at least $($runnerMethods.Count) observability conformance facts but $executedTests executed. The --filter no longer matches the ProductionObservabilityConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-ProductionObservabilityReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
