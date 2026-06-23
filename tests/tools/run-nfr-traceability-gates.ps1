#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'NFR-TRACEABILITY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the NFR traceability gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/nfr-traceability'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityDocExists',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityDocNamesItsSourceAuthorities',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.PrdAndEpicsNfrInventoriesAlignOneForOne',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.TraceabilityTableHasSeventyRowsMatchingPrdHashes',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.TraceabilityTableRowsCarryCategoryStatusAndConcreteEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.ReferencePendingRowsAreOwnedAndSurfaceKnownGaps',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NineCategoryRollupCoversAllSeventyNfrs',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.BddRequiredEvidenceClassesArePresent',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityDocStaysMetadataOnlyWithOperatorBoilerplate',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.GovernanceEvidenceReferencePendingCriteriaStaySurfaced',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityGateScriptFailsClosedAndEmitsBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.ContractSpineWorkflowAndBaselineCiWireNfrTraceabilityGate',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.ReleasePackageWiringRequiresNfrTraceabilityEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityGateRunsOnlyInReleasePrerequisiteJob',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NfrTraceabilityLatestReportStaysMetadataOnlyAndMatchesDoc',
    'Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests.NegativeControlsRejectVacuousAndUnsafeNfrTraceabilityEvidence'
)

function Get-SourceCommit {
    $commit = ''
    try {
        $commit = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
        if ($null -ne $commit) {
            $commit = $commit.Trim()
        }
    }
    catch {
        $commit = ''
    }

    if ([string]::IsNullOrWhiteSpace($commit)) {
        return 'NO_VCS'
    }

    return $commit
}

function Write-NfrTraceabilityReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    # Owned, release-blocking reference-pending gaps. Every entry MUST carry an owner so the release
    # gate can reject an unowned gap. These mirror the reference-pending rows in
    # docs/exit-criteria/nfr-traceability.md and the conformance test asserts the two stay in sync.
    $releaseBlockingGaps = @(
        [ordered]@{ nfr = 'NFR18'; criterion = 'C7'; owner = 'Architecture'; consuming_story = '4-3'; gap = 'lock-revalidation-budget-and-mid-task-revocation-evidence' },
        [ordered]@{ nfr = 'NFR26'; criterion = 'C4'; owner = 'PM'; consuming_story = '4-8'; gap = 'pm-approval-of-context-query-input-bounds' },
        [ordered]@{ nfr = 'NFR28'; criterion = 'C4'; owner = 'PM'; consuming_story = '4-8'; gap = 'pm-approval-of-large-file-and-payload-limits' },
        [ordered]@{ nfr = 'NFR44'; criterion = 'C12'; owner = 'Provider Readiness'; consuming_story = '7-8'; gap = 'live-provider-drift-requires-credentials-absent-in-ci' },
        [ordered]@{ nfr = 'NFR54'; criterion = ''; owner = 'Operations Runbook (Story 7.17)'; consuming_story = '7-17'; gap = 'live-alert-delivery-tooling-deferred' },
        [ordered]@{ nfr = 'NFR55'; criterion = ''; owner = 'Operations Runbook (Story 7.17)'; consuming_story = '7-17'; gap = 'backup-restore-tooling-and-recovery-drill-evidence-deferred' },
        [ordered]@{ nfr = 'NFR57'; criterion = 'C3'; owner = 'Legal + PM'; consuming_story = '7-11'; gap = 'legal-pm-approval-of-retention-durations' }
        # NFR62/63/65/66 (operations-console accessibility) were release-blocking manual gaps; Story 8.4 wired the
        # automated axe / WCAG 2.2 AA CI gate (accessibility-gates), so they move out of release-blocking gaps —
        # NFR63/65/66 to covered, NFR62 to release-validation (manual screen-reader review remains). Removing them
        # here keeps the report's release_blocking_gaps in sync with the docs/exit-criteria/nfr-traceability.md
        # reference-pending rows, which the conformance test cross-checks.
    )

    [ordered]@{
        gate = 'nfr-traceability'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/nfr-traceability/latest.json'
        source_commit = Get-SourceCommit
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        nfr_total = 70
        category_total = 9
        canonical_inputs = @(
            '_bmad-output/planning-artifacts/prd.md',
            '_bmad-output/planning-artifacts/epics.md',
            '_bmad-output/planning-artifacts/architecture.md',
            'docs/exit-criteria/c0-c13-governance-evidence.yaml',
            'docs/exit-criteria/nfr-traceability.md',
            'tests/tools/run-release-package-gates.ps1',
            '.github/workflows/release-packages.yml'
        )
        surfaces = @(
            'prd-nfr-inventory',
            'epics-nfr-inventory',
            'traceability-table',
            'category-rollup',
            'bdd-evidence-rollup',
            'release-evidence',
            'reference-pending-gaps',
            'ci-wiring',
            'release-wiring',
            'metadata-only'
        )
        release_blocking_gaps = $releaseBlockingGaps
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
    Write-Host 'NFR-TRACEABILITY category=static-nfr-traceability vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-NfrTraceabilityReport -Status 'failed' -ExitCode 1
        Write-Error 'NFR-TRACEABILITY-GATE-VACUOUS: xUnit in-process runner missing for NfrTraceabilityConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-NfrTraceabilityReport -Status 'failed' -ExitCode 1
        Write-Error "NFR-TRACEABILITY-GATE-VACUOUS: expected $($runnerMethods.Count) NFR traceability conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-NfrTraceabilityReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-NfrTraceabilityReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-NfrTraceabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-NfrTraceabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'nfr-traceability-conformance.trx'
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
            Write-NfrTraceabilityReport -Status 'failed' -ExitCode $LASTEXITCODE
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
        Write-NfrTraceabilityReport -Status 'failed' -ExitCode 1
        Write-Error "NFR-TRACEABILITY-GATE-VACUOUS: expected at least $($runnerMethods.Count) NFR traceability conformance facts but $executedTests executed. The --filter no longer matches the NfrTraceabilityConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-NfrTraceabilityReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
