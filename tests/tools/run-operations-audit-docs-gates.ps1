#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'OPERATIONS-AUDIT-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the operations and audit docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/operations-audit-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.RequiredOperationsAuditDocsExist',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.AuditDocOperationInventoryEqualsSpineAuditFamily',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.AuditDocFieldCatalogEqualsAuditRecordAndTimelineDtos',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.AuditDocOperationKindAndResultTaxonomyEqualsObservationEnums',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.ConsoleDocDispositionVocabularyAndTechnicalStatesEqualMapper',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.RedactionDocVocabulariesEqualWireAndPresentationEnums',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.AlertingDocSignalsEqualObservabilityManifest',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.RecoveryDocRetentionDispositionsEqualRetentionSources',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.ConsoleDocPinsRoutesJourneysTrustQuestionsAndPerceivedWait',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.IncidentDocPinsLastResortReadGuardrailsAndReferencePending',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.RecoveryDocPinsAuthoritativeRecordsAndNoAutomation',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.AllPublishedOperationsAuditDocsStayMetadataOnly',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.OperationsAuditDocsGateScriptFailsClosedAndEmitsBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.ContractSpineWorkflowAndBaselineCiWireOperationsAuditDocsGate',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.OperationsAuditDocsLatestReportStaysMetadataOnlyWhenPresent',
    'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests.NegativeControlsRejectVacuousAndUnsafeOperationsAuditDocsEvidence'
)

function Write-OperationsAuditDocsReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'operations-audit-docs'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/operations-audit-docs/latest.json'
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        canonical_inputs = @(
            'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml',
            'src/Hexalith.Folders.Contracts/Projections/Audit/AuditRecord.cs',
            'src/Hexalith.Folders.Contracts/Projections/Audit/OperationTimelineEntry.cs',
            'src/Hexalith.Folders/Observability/FolderAuditOperationKind.cs',
            'src/Hexalith.Folders/Observability/FolderAuditResult.cs',
            'src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs',
            'src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs',
            'src/Hexalith.Folders.UI/Services/FieldDisclosure.cs',
            'deploy/observability/production/observability.yaml',
            'docs/runbooks/tenant-deletion.md',
            'tests/fixtures/audit-leakage-corpus.json'
        )
        surfaces = @(
            [ordered]@{ surface = 'console-workflows'; doc = 'docs/operations/operations-console.md'; assertion = 'routes-journeys-disposition-and-technical-states-equal-mapper' },
            [ordered]@{ surface = 'audit-fields'; doc = 'docs/operations/audit-and-redaction.md'; assertion = 'audit-operations-fields-and-kind-result-taxonomy-equal-source' },
            [ordered]@{ surface = 'redaction'; doc = 'docs/operations/audit-and-redaction.md'; assertion = 'wire-and-presentation-vocabularies-equal-redactionvisibility-and-fielddisclosure' },
            [ordered]@{ surface = 'incident-mode'; doc = 'docs/operations/incident-alerting-and-recovery.md'; assertion = 'last-resort-read-guardrails-and-projection-independent-read-reference-pending' },
            [ordered]@{ surface = 'alerting-handoff'; doc = 'docs/operations/incident-alerting-and-recovery.md'; assertion = 'alert-signals-and-severities-equal-observability-manifest' },
            [ordered]@{ surface = 'backup-recovery'; doc = 'docs/operations/incident-alerting-and-recovery.md'; assertion = 'authoritative-records-rebuildable-projections-and-no-automation' }
        )
        # Live alert backends, dashboards, paging, and backup/restore tooling are operations-runbook-owned and
        # stay OUTSIDE this repository (Story 7.17). This gate only enforces metadata-only documentation intent.
        reference_pending = [ordered]@{
            status = 'reference_pending_external_tooling'
            severity = 'warning'
            owner = 'Operations Runbook (Story 7.17)'
            command_shape = 'pwsh ./tests/tools/run-operations-audit-docs-gates.ps1'
            evidence_path = '_bmad-output/gates/operations-audit-docs/latest.json'
            follow_up_boundary = 'live alerting, dashboards, paging, and backup/restore tooling stay external; metadata-only intent only; no live endpoints, tokens, or rendered content in CI'
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
    Write-Host 'OPERATIONS-AUDIT-DOCS category=static-operations-audit-references vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode 1
        Write-Error 'OPERATIONS-AUDIT-DOCS-GATE-VACUOUS: xUnit in-process runner missing for OperationsAuditDocsConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode 1
        Write-Error "OPERATIONS-AUDIT-DOCS-GATE-VACUOUS: expected $($runnerMethods.Count) operations-audit-docs conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-OperationsAuditDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'operations-audit-docs-conformance.trx'
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
            Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
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
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode 1
        Write-Error "OPERATIONS-AUDIT-DOCS-GATE-VACUOUS: expected at least $($runnerMethods.Count) operations-audit-docs conformance facts but $executedTests executed. The --filter no longer matches the OperationsAuditDocsConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-OperationsAuditDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
