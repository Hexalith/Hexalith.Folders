#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
$usedXunitFallback = $false
$testFilter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests'
$runnerMethods = @(
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.RequiredConsumerReferenceDocsExist',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ApiReferenceOperationAndTagInventoryEqualsSpine',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.ApiReferenceDocumentsSecurityHeadersAndProblemDetailsFromSpine',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.CliReferenceExitCodeRowsEqualFoldersExitCodes',
    'Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests.McpReferenceEnumeratesAllFortySevenToolsAndTwoResources',
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
    Write-Host 'CONSUMER-DOCS category=static-consumer-references vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests'
    if (-not (Test-Path $runnerPath)) {
        Write-ConsumerDocsReport -Status 'failed' -ExitCode 1
        Write-Error 'CONSUMER-DOCS-GATE-VACUOUS: xUnit in-process runner missing for ConsumerDocsConformanceTests.'
        exit 1
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    [int]$executedTests = Get-ExecutedTestCount -Output $runnerOutput
    if ($executedTests -lt $runnerMethods.Count) {
        Write-ConsumerDocsReport -Status 'failed' -ExitCode 1
        Write-Error "CONSUMER-DOCS-GATE-VACUOUS: expected $($runnerMethods.Count) consumer-docs conformance facts but $executedTests executed."
        exit 1
    }

    if ($runnerExitCode -ne 0) {
        Write-ConsumerDocsReport -Status 'failed' -ExitCode $runnerExitCode
        exit $runnerExitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ConsumerDocsReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) {
            Write-ConsumerDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore -m:1
        if ($LASTEXITCODE -ne 0) {
            Write-ConsumerDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $trxName = 'consumer-docs-conformance.trx'
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
            Write-ConsumerDocsReport -Status 'failed' -ExitCode $LASTEXITCODE
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
        Write-ConsumerDocsReport -Status 'failed' -ExitCode 1
        Write-Error "CONSUMER-DOCS-GATE-VACUOUS: expected at least $($runnerMethods.Count) consumer-docs conformance facts but $executedTests executed. The --filter no longer matches the ConsumerDocsConformanceTests type; restore the filter or namespace."
        exit 1
    }

    Write-ConsumerDocsReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
