#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'ADR-RUNBOOK-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the ADR/runbook docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/adr-runbook-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrRunbookManifestIsNonVacuousAndFilesExist',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.NewAdrsHaveRequiredSectionsAcceptedStatusNoPlaceholderAndRealDecisionIds',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrTemplateAndExistingAdrArePreserved',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.RunbooksHaveExpectedSectionContractsAndGapContent',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrAndRunbookIndexesMatchDirectoryInventories',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrRunbookDocsStayMetadataOnly',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrRunbookGateScriptFailsClosedAndEmitsBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.ContractSpineWorkflowAndBaselineCiWireAdrRunbookGateOnlyInAllowedLanes',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.AdrRunbookLatestReportStaysMetadataOnlyAndMatchesInventory',
    'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests.NegativeControlsRejectAdrRunbookDocDriftAndUnsafeEvidence'
)

# <!-- adr-runbook-source-of-truth -->
# ADR|contract|0002-contract-spine-single-source-of-truth.md|C0,A-1,A-2,A-3,C13
# ADR|provider|0003-provider-abstraction-and-capability-model.md|A-6,A-7,C12
# ADR|idempotency|0004-per-command-canonical-idempotency.md|A-9,D-7
# ADR|security|0005-layered-authorization-and-oidc.md|S-4,S-2,S-6,C9
# ADR|observability|0006-observability-and-operational-signals.md|I-6,I-7,C2
# ADR|deployment|0007-container-deployment-with-dapr.md|I-2,I-3,I-4,I-1
# RUNBOOK|tenant deletion|tenant-deletion.md|preserved
# RUNBOOK|retention|retention.md|new
# RUNBOOK|alerts|alerts.md|new
# RUNBOOK|rollback|rollback.md|new
# RUNBOOK|provider drift|provider-drift.md|new
# RUNBOOK|reconciliation|reconciliation.md|new
# RUNBOOK|incident-mode operations|incident-mode.md|new
# <!-- /adr-runbook-source-of-truth -->

$adrInventory = @(
    [ordered]@{ area = 'contract'; file = '0002-contract-spine-single-source-of-truth.md'; decision_ids = @('C0', 'A-1', 'A-2', 'A-3', 'C13') },
    [ordered]@{ area = 'provider'; file = '0003-provider-abstraction-and-capability-model.md'; decision_ids = @('A-6', 'A-7', 'C12') },
    [ordered]@{ area = 'idempotency'; file = '0004-per-command-canonical-idempotency.md'; decision_ids = @('A-9', 'D-7') },
    [ordered]@{ area = 'security'; file = '0005-layered-authorization-and-oidc.md'; decision_ids = @('S-4', 'S-2', 'S-6', 'C9') },
    [ordered]@{ area = 'observability'; file = '0006-observability-and-operational-signals.md'; decision_ids = @('I-6', 'I-7', 'C2') },
    [ordered]@{ area = 'deployment'; file = '0007-container-deployment-with-dapr.md'; decision_ids = @('I-2', 'I-3', 'I-4', 'I-1') }
)
$runbookInventory = @(
    [ordered]@{ topic = 'tenant deletion'; file = 'tenant-deletion.md'; preserved = $true },
    [ordered]@{ topic = 'retention'; file = 'retention.md'; preserved = $false },
    [ordered]@{ topic = 'alerts'; file = 'alerts.md'; preserved = $false },
    [ordered]@{ topic = 'rollback'; file = 'rollback.md'; preserved = $false },
    [ordered]@{ topic = 'provider drift'; file = 'provider-drift.md'; preserved = $false },
    [ordered]@{ topic = 'reconciliation'; file = 'reconciliation.md'; preserved = $false },
    [ordered]@{ topic = 'incident-mode operations'; file = 'incident-mode.md'; preserved = $false }
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

function Write-AdrRunbookDocsReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'adr-runbook-docs'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/adr-runbook-docs/latest.json'
        source_commit = Get-SourceCommit
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        adr_total = 6
        runbook_total = 7
        canonical_inputs = @(
            'docs/adrs/index.md',
            'docs/runbooks/index.md',
            'docs/adrs/0000-template.md',
            'docs/adrs/0001-folder-domain-processor-persistence.md',
            '_bmad-output/planning-artifacts/architecture.md',
            'tests/tools/run-adr-runbook-docs-gates.ps1',
            '.github/workflows/contract-spine.yml',
            'tests/tools/run-baseline-ci-gates.ps1'
        )
        surfaces = @(
            'adr-set',
            'adr-template-preserved',
            'runbook-set',
            'adr-index',
            'runbook-index',
            'architecture-decision-citations',
            'metadata-only',
            'ci-wiring'
        )
        adr_inventory = $adrInventory
        runbook_inventory = $runbookInventory
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
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
    Write-Host 'ADR-RUNBOOK-DOCS category=static-adr-runbook-docs vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode 1
        Write-Error 'ADR-RUNBOOK-DOCS-GATE-VACUOUS: xUnit in-process runner missing for AdrRunbookDocsConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode 1
        Write-Error "ADR-RUNBOOK-DOCS-GATE-VACUOUS: expected $($runnerMethods.Count) ADR/runbook conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-AdrRunbookDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'adr-runbook-docs-conformance.trx'
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
            $testOutput | ForEach-Object { Write-Host $_ }
            Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testOutput | ForEach-Object { Write-Host $_ }
    }

    [int]$executedTests = 0
    if (-not $usedXunitFallback -and (Test-Path $trxPath)) {
        [xml]$trx = Get-Content -Raw -Path $trxPath
        $executedTests = [int]$trx.TestRun.ResultSummary.Counters.total
    }

    if (-not $usedXunitFallback -and $executedTests -lt $runnerMethods.Count) {
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode 1
        Write-Error "ADR-RUNBOOK-DOCS-GATE-VACUOUS: expected at least $($runnerMethods.Count) ADR/runbook conformance facts but $executedTests executed. The --filter no longer matches the AdrRunbookDocsConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-AdrRunbookDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
