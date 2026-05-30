#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'SECURITY-REDACTION-CI-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the security redaction CI gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/security-redaction-ci'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$results = @()

$testGates = @(
    [ordered]@{
        category = 'sentinel-corpus'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.TelemetrySurfaceVocabularyIsExplicitAndInventoryAddressable|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.ChannelInventoryResolvesCoveredSourcesAndBoundsMissingChannels'
        runner_methods = @(
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.TelemetrySurfaceVocabularyIsExplicitAndInventoryAddressable',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SentinelCorpusAvoidsRealDataAndKeepsNegativeControlsQuarantined',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.ChannelInventoryResolvesCoveredSourcesAndBoundsMissingChannels'
        )
        artifact_paths = @('tests/fixtures/audit-leakage-corpus.json', 'tests/fixtures/safety-channel-inventory.json', 'tests/fixtures/quarantine/safety-negative-controls.json')
    },
    [ordered]@{
        category = 'redaction-channel-scan'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SafetyScansDetectQuarantinedControlsWithoutScanningQuarantineAsNormalArtifacts|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.OpenApiExamplesAndContextQueriesRemainMetadataOnly|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.StoryFourteenTelemetryChannelsAreCoveredByRuntimeArtifacts'
        runner_methods = @(
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SafetyScansDetectQuarantinedControlsWithoutScanningQuarantineAsNormalArtifacts',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.OpenApiExamplesAndContextQueriesRemainMetadataOnly',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.SafeDenialAndDiagnosticStatesDoNotRevealResourceExistence',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.StoryElevenDiagnosticChannelsAreReevaluatedAgainstCurrentArtifacts',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.StoryFourteenTelemetryChannelsAreCoveredByRuntimeArtifacts'
        )
        artifact_paths = @('src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml', 'tests/fixtures/safety-channel-inventory.json', 'src/Hexalith.Folders.Client/Generated')
    },
    [ordered]@{
        category = 'forbidden-field-diagnostics'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.MissingChannelDiagnosticsAreEmittedAsBoundedRuntimeEvidence|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.WorkflowAndDocumentationExposeSameOfflineSafetyGate|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.AuditOpsConsoleContractGroupTests.AuditOpsConsoleSchemas_EnforceBehavioralInvariantsFromFollowUpReview'
        runner_methods = @(
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.MissingChannelDiagnosticsAreEmittedAsBoundedRuntimeEvidence',
            'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests.WorkflowAndDocumentationExposeSameOfflineSafetyGate',
            'Hexalith.Folders.Contracts.Tests.OpenApi.AuditOpsConsoleContractGroupTests.AuditOpsConsoleSchemas_EnforceBehavioralInvariantsFromFollowUpReview'
        )
        artifact_paths = @('src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml', 'docs/contract/safety-invariant-ci-gates.md', 'tests/tools/run-safety-invariant-gates.ps1')
    },
    [ordered]@{
        category = 'tenant-cache-key-lint'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyExceptionApprovalStateFailsClosedForExpiredOrUnknownStatus|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyLintNegativeControlsClassifyTenantScopeAndExceptionsWithoutEchoingKeyValues'
        runner_methods = @(
            'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope',
            'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyExceptionApprovalStateFailsClosedForExpiredOrUnknownStatus',
            'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests.CacheKeyLintNegativeControlsClassifyTenantScopeAndExceptionsWithoutEchoingKeyValues'
        )
        artifact_paths = @('tests/fixtures/cache-key-exceptions.yaml', 'tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs')
    }
)

$requiredInputs = @(
    'tests/fixtures/audit-leakage-corpus.json',
    'tests/fixtures/safety-channel-inventory.json',
    'tests/fixtures/quarantine/safety-negative-controls.json',
    'tests/fixtures/cache-key-exceptions.yaml',
    'docs/contract/safety-invariant-ci-gates.md',
    'docs/contract/governance-and-completeness-ci-gates.md',
    'tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs',
    'tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs',
    'tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs'
)

function Write-SecurityRedactionReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results
    )

    [ordered]@{
        gate = 'security-redaction-ci'
        status = $Status
        report_path = '_bmad-output/gates/security-redaction-ci/latest.json'
        diagnostic_policy = 'metadata-only'
        categories = $testGates.category
        test_gates = $testGates
        results = $Results
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Assert-RequiredInput {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Write-SecurityRedactionReport -Status 'failed' -Results $script:results
        Write-Error "SECURITY-REDACTION-CI-PREREQUISITE-DRIFT: missing repository artifact path=$RelativePath"
        exit 1
    }
}

function Assert-TestAssembly {
    $testAssembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin') -Recurse -Filter 'Hexalith.Folders.Contracts.Tests.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $testAssembly) {
        Write-SecurityRedactionReport -Status 'failed' -Results $script:results
        Write-Error 'SECURITY-REDACTION-CI-PREREQUISITE-DRIFT: test assembly missing for project path=tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        exit 1
    }
}

function Invoke-SecurityRedactionGate {
    param(
        [Parameter(Mandatory = $true)]$Gate
    )

    Write-Host "SECURITY-REDACTION-CI category=$($Gate.category) project=$($Gate.project_path)"
    $arguments = @('test', $gate.project_path, '--no-restore', '--no-build', '--filter', $gate.filter)
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -and (($output -join [Environment]::NewLine) -match 'System\.Net\.Sockets\.SocketException.*Permission denied')) {
        $exitCode = Invoke-XunitInProcessFallback -Gate $Gate
    }
    else {
        # Always surface the runner output. The source-of-truth tests emit metadata-only
        # diagnostics in their assertion messages, so echoing them keeps CI failures actionable
        # (emitting channel / rule / sample ID per AC 7) without leaking raw payloads.
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -eq 0) {
            # `dotnet test --filter` returns exit 0 even when the filter matches zero (or fewer
            # than expected) tests, so a renamed/typo'd method name would pass this PR-blocking
            # gate vacuously. Fail closed unless every expected test in the category actually ran.
            $expectedCount = @($Gate.runner_methods).Count
            $observedCount = Get-ExecutedTestCount -Output $output
            if ($observedCount -ne $expectedCount) {
                Write-Host "SECURITY-REDACTION-CI category=$($Gate.category) status=failed reason=test-selection-drift expected=$expectedCount observed=$observedCount"
                $exitCode = 1
            }
        }
        else {
            Write-Host "SECURITY-REDACTION-CI category=$($Gate.category) status=failed project=$($Gate.project_path)"
        }
    }

    $status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    $script:results += [ordered]@{
        category = $Gate.category
        project_path = $Gate.project_path
        filter = $Gate.filter
        artifact_paths = $Gate.artifact_paths
        status = $status
        exit_code = $exitCode
    }
    Write-SecurityRedactionReport -Status $status -Results $script:results

    if ($exitCode -ne 0) {
        Write-Error "SECURITY-REDACTION-CI-FAILED: category=$($Gate.category) project=$($Gate.project_path)"
        exit $exitCode
    }
}

function Get-ExecutedTestCount {
    param(
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Output
    )

    # Both `dotnet test` and the xUnit v3 self-executable summarize selection as `Total: N`.
    # Sum every occurrence so multi-target output is counted correctly; absent any summary
    # line (e.g. "No test matches the given testcase filter") the count is zero.
    $joined = ($Output -join [Environment]::NewLine)
    $total = 0
    foreach ($match in [regex]::Matches($joined, 'Total:\s+(\d+)')) {
        $total += [int]$match.Groups[1].Value
    }

    return $total
}

function Invoke-XunitInProcessFallback {
    param(
        [Parameter(Mandatory = $true)]$Gate
    )

    Write-Host "SECURITY-REDACTION-CI category=$($Gate.category) vstest-socket-denied=true fallback=xunit-in-process"
    $projectDirectory = Split-Path -Parent (Join-Path $repositoryRoot $Gate.project_path)
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($Gate.project_path)
    $runnerPath = Join-Path $projectDirectory "bin/Debug/net10.0/$projectName"
    if (-not (Test-Path $runnerPath)) {
        Write-Error "SECURITY-REDACTION-CI-PREREQUISITE-DRIFT: xUnit in-process runner missing for project path=$($Gate.project_path)"
        return 1
    }

    $runnerArguments = @('-noLogo', '-noColor')
    foreach ($method in $Gate.runner_methods) {
        $runnerArguments += @('-method', $method)
    }

    $runnerOutput = & $runnerPath @runnerArguments 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    # The xUnit v3 runner also exits 0 when zero (or fewer than expected) methods resolve, so
    # fail closed unless every expected test in the category actually ran.
    $expectedCount = @($Gate.runner_methods).Count
    $observedCount = Get-ExecutedTestCount -Output $runnerOutput
    if ($observedCount -ne $expectedCount) {
        Write-Error "SECURITY-REDACTION-CI-PREREQUISITE-DRIFT: test-selection drift for category=$($Gate.category) expected=$expectedCount observed=$observedCount"
        return 1
    }

    if ($runnerExitCode -ne 0) {
        Write-Host "SECURITY-REDACTION-CI category=$($Gate.category) status=failed fallback=xunit-in-process"
    }

    return $runnerExitCode
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-SecurityRedactionReport -Status 'discovered' -Results $results

    foreach ($input in $requiredInputs) {
        Assert-RequiredInput -RelativePath $input
    }

    Assert-TestAssembly

    foreach ($gate in $testGates) {
        Invoke-SecurityRedactionGate -Gate $gate
    }

    Write-SecurityRedactionReport -Status 'passed' -Results $results
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
