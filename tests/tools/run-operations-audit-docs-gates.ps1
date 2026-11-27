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
    Write-Error 'OPERATIONS-AUDIT-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the operations and audit docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/operations-audit-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests'
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

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-OperationsAuditDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'operations-audit-docs-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode $testExitCode
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
        Write-OperationsAuditDocsReport -Status 'failed' -ExitCode 1
        Write-Error "OPERATIONS-AUDIT-DOCS-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-OperationsAuditDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
