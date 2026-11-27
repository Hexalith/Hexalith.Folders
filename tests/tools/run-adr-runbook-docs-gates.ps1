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
    Write-Error 'ADR-RUNBOOK-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the ADR/runbook docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/adr-runbook-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests'
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

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-AdrRunbookDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'adr-runbook-docs-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode $testExitCode
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
        Write-AdrRunbookDocsReport -Status 'failed' -ExitCode 1
        Write-Error "ADR-RUNBOOK-DOCS-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-AdrRunbookDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
