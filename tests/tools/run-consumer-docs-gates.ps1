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
    Write-Error 'CONSUMER-DOCS-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the consumer docs gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/consumer-docs'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$trxDirectory = $null
$testProject = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$testClass = 'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.RequiredConsumerReferenceDocsExist',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ApiReferenceOperationAndTagInventoryEqualsSpine',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ApiReferenceDocumentsSecurityHeadersAndProblemDetailsFromSpine',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.CliReferenceExitCodeRowsEqualFoldersExitCodes',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.McpReferenceEnumeratesAllFortyNineToolsAndTwoResources',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.McpReferenceFailureKindCatalogEqualsOraclePlusPreSdkKinds',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.AllPublishedConsumerDocsStayMetadataOnly',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ConsumerDocsGateScriptFailsClosedAndEmitsBoundedEvidence',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ContractSpineWorkflowAndBaselineCiWireConsumerDocsGate',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ConsumerDocsLatestReportStaysMetadataOnlyWhenPresent',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.PatternExampleManifestRegistersCompilableGoldenLifecycleExample',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.SdkReferencesPinGoldenLifecycleAndIdempotencyHelperTrap',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.CliReferencePinsCommandGroupsCredentialPrecedenceAndMutationRules',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.McpReferencePinsToolGroupCountsTransportDiscoveryAndFailureDrift',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.AuthenticationReferencePinsS2ClaimProvenanceAndCredentialSources',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.LifecycleDiagramsRenderThreeMermaidFencesWithExpectedDiagramTypes',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramDispositionTableMatchesC6StateCatalog',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramEventLabelsEqualC6EventVocabulary',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.FileCommitFlowDiagramNodesAreCanonicalSpineOperations',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.AuthAclDecisionFlowEncodesFixedSixLayerDenyByDefaultOrder',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.NegativeControlsRejectVacuousAndUnsafeConsumerDocsEvidence'
)

function Write-ConsumerDocsReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'consumer-docs'
        status = $Status
        exit_code = $ExitCode
        diagnostic_policy = 'metadata-only'
        report_path = '_bmad-output/gates/consumer-docs/latest.json'
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        canonical_inputs = @(
            'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml',
            'tests/fixtures/parity-contract.yaml',
            'src/Hexalith.Folders.Cli/FoldersExitCodes.cs',
            'tests/fixtures/pattern-example-manifest.yaml'
        )
        surfaces = @(
            [ordered]@{ surface = 'openapi-reference'; doc = 'docs/sdk/api-reference.md'; assertion = 'operation-and-tag-inventory-equals-spine' },
            [ordered]@{ surface = 'sdk-quickstart'; doc = 'docs/sdk/quickstart.md'; assertion = 'metadata-only' },
            [ordered]@{ surface = 'cli-reference'; doc = 'docs/sdk/cli-reference.md'; assertion = 'exit-code-rows-equal-foldersexitcodes' },
            [ordered]@{ surface = 'mcp-reference'; doc = 'docs/sdk/mcp-reference.md'; assertion = '47-tools-2-resources-and-failure-kind-catalog' },
            [ordered]@{ surface = 'authentication'; doc = 'docs/sdk/authentication.md'; assertion = 'metadata-only-invalid-issuers-only' },
            [ordered]@{ surface = 'lifecycle-diagrams'; doc = 'docs/diagrams'; assertion = 'c6-traceable-metadata-only' },
            [ordered]@{ surface = 'examples-validation'; doc = 'tests/tools/pattern-examples'; assertion = 'compilable-example-and-hermetic-sample-tests' }
        )
        # Live doc-site rendering and external Redoc HTML generation live outside this hermetic PR gate.
        live_doc_render_smoke = [ordered]@{
            status = 'reference_pending_external_tooling'
            severity = 'warning'
            owner = 'Release Readiness'
            command_shape = 'pwsh ./tests/tools/run-consumer-docs-gates.ps1'
            evidence_path = '_bmad-output/gates/consumer-docs/latest.json'
            follow_up_boundary = 'metadata-only intent only; no live endpoints, tokens, or rendered HTML in CI'
        }
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ConsumerDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $dependencyArguments = if ($Configuration -eq 'Release') {
            @('-p:UseNuGetDeps=true')
        }
        else {
            @('-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false')
        }
        dotnet build $testProject --configuration $Configuration -m:1 @dependencyArguments
        if ($LASTEXITCODE -ne 0) {
            Write-ConsumerDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'consumer-docs-conformance.trx'
    $trxDirectory = [IO.Directory]::CreateTempSubdirectory('folders-conformance-').FullName
    $trxPath = Join-Path $trxDirectory $trxName

    dotnet test --project $testProject --configuration $Configuration --no-build `
        --filter-class $testClass --results-directory $trxDirectory `
        --report-xunit-trx --report-xunit-trx-filename $trxName 2>&1 | Out-Null
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "The conformance test runner exited with code $testExitCode."
        Write-ConsumerDocsReport -Status 'failed' -ExitCode $testExitCode
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
        Write-ConsumerDocsReport -Status 'failed' -ExitCode 1
        Write-Error "CONSUMER-DOCS-GATE-VACUOUS: $($missingMethods.Count) required test methods have no passing result. Check the class selector and TRX report."
        exit 1
    }

    Write-Host "Verified $($runnerMethods.Count) required test methods in $testClass."
    Write-ConsumerDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
    if ($null -ne $trxDirectory) {
        Remove-Item -LiteralPath $trxDirectory -Recurse -Force
    }
}
