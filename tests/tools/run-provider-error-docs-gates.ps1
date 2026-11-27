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
    Write-Error 'PROVIDER-ERROR-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the provider and error docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/provider-error-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests'
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

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ProviderErrorDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'provider-error-docs-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode $testExitCode
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
        Write-ProviderErrorDocsReport -Status 'failed' -ExitCode 1
        Write-Error "PROVIDER-ERROR-DOCS-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-ProviderErrorDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
