#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'PROVIDER-ERROR-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the provider and error docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/provider-error-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.RequiredProviderErrorDocsExist',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocCapabilityOperationInventoryEqualsCatalog',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocCredentialModeInventoryEqualsEnum',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocReadinessResultCodeInventoryEqualsEnum',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocFailureCategoryInventoryEqualsEnumAndRetryability',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocForgejoSupportedVersionsEqualCatalogAndPinDrift',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocGitHubBehaviorAndCapabilityDifferencesArePinned',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderDocPinsKnownFailureHandlingAndNoSilentRetry',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocGeneratedCategoryInventoryEqualsClient',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocOracleCategoryInventoryEqualsParityContract',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocClientActionTokensEqualGeneratedEnum',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocCliExitCodesEqualFoldersExitCodes',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocMcpFailureKindProjectionRulesEqualSource',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocRetryAfterFieldsAreAdvisoryOnly',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.CanonicalErrorDocCrossLinksConsumerAndOpsDocsWithoutDuplication',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.AllPublishedProviderErrorDocsStayMetadataOnly',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderErrorDocsGateScriptFailsClosedAndEmitsBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ContractSpineWorkflowAndBaselineCiWireProviderErrorDocsGate',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.ProviderErrorDocsLatestReportStaysMetadataOnlyWhenPresent',
    'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests.NegativeControlsRejectVacuousAndUnsafeProviderErrorDocsEvidence'
)

function Write-ProviderErrorDocsReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'provider-error-docs'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/provider-error-docs/latest.json'
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        canonical_inputs = @(
            'src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs',
            'src/Hexalith.Folders/Providers/Abstractions/ProviderCredentialMode.cs',
            'src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategory.cs',
            'src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategoryExtensions.cs',
            'src/Hexalith.Folders/Queries/ProviderReadiness/ProviderReadinessResultCode.cs',
            'src/Hexalith.Folders/Providers/Forgejo/ForgejoSupportedVersionCatalog.cs',
            'src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs',
            'tests/fixtures/parity-contract.yaml',
            'src/Hexalith.Folders.Cli/FoldersExitCodes.cs',
            'src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs',
            'src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryAfter.cs',
            'src/Hexalith.Folders/Queries/Folders/WorkspaceStatusRetryEligibility.cs'
        )
        surfaces = @(
            [ordered]@{ surface = 'provider-capability'; doc = 'docs/operations/provider-integration-and-testing.md'; assertion = 'capability-operations-credential-modes-readiness-and-failure-categories-equal-source' },
            [ordered]@{ surface = 'forgejo-versions'; doc = 'docs/operations/provider-integration-and-testing.md'; assertion = 'supported-forgejo-versions-and-drift-classification-equal-catalog' },
            [ordered]@{ surface = 'provider-differences'; doc = 'docs/operations/provider-integration-and-testing.md'; assertion = 'github-vs-forgejo-differences-pinned-without-false-parity-and-no-silent-retry' },
            [ordered]@{ surface = 'canonical-categories'; doc = 'docs/operations/canonical-error-catalog.md'; assertion = 'generated-47-and-oracle-43-categories-equal-source' },
            [ordered]@{ surface = 'cross-surface-projection'; doc = 'docs/operations/canonical-error-catalog.md'; assertion = 'client-actions-cli-exit-codes-and-mcp-failure-kind-rules-equal-source' },
            [ordered]@{ surface = 'retry-after'; doc = 'docs/operations/canonical-error-catalog.md'; assertion = 'retry-after-fields-advisory-only-equal-source' }
        )
        # Provider drift runbooks, alert/rollback procedures, and live provider endpoints are
        # operations-runbook-owned and stay OUTSIDE this repository (Story 7.17). This gate only enforces
        # metadata-only documentation intent and source-pinned inventories.
        reference_pending = [ordered]@{
            status = 'reference_pending_external_tooling'
            severity = 'warning'
            owner = 'Operations Runbook (Story 7.17)'
            command_shape = 'pwsh ./tests/tools/run-provider-error-docs-gates.ps1'
            evidence_path = '_bmad-output/gates/provider-error-docs/latest.json'
            follow_up_boundary = 'provider drift runbooks, alert/rollback procedures, and live provider endpoints stay external; metadata-only intent only; no live endpoints, tokens, or raw provider payloads in CI'
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
    Write-Host 'PROVIDER-ERROR-DOCS category=static-provider-error-references vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode 1
        Write-Error 'PROVIDER-ERROR-DOCS-GATE-VACUOUS: xUnit in-process runner missing for ProviderErrorDocsConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode 1
        Write-Error "PROVIDER-ERROR-DOCS-GATE-VACUOUS: expected $($runnerMethods.Count) provider-error-docs conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ProviderErrorDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'provider-error-docs-conformance.trx'
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
            Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
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
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode 1
        Write-Error "PROVIDER-ERROR-DOCS-GATE-VACUOUS: expected at least $($runnerMethods.Count) provider-error-docs conformance facts but $executedTests executed. The --filter no longer matches the ProviderErrorDocsConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-ProviderErrorDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
