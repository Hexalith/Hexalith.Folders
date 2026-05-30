#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'CONTRACT-PARITY-CI-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the contract parity CI gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/contract-parity-ci'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$results = @()

$testGates = @(
    [ordered]@{
        category = 'server-vs-spine'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests.ServerVsSpine|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineFoundationTests'
        runner_classes = @('Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests', 'Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineFoundationTests')
        artifact_paths = @('src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml', 'src/Hexalith.Folders.Server')
    },
    [ordered]@{
        category = 'previous-spine'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests.PreviousSpineBaselinePinsCurrentOperationInventory|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.PreviousSpineBaselineCoversEveryCurrentOperation|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedForRemovedPreviousSpineOperationWithoutDeprecation|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedForEmptyBaselineWithoutOverride|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorAcceptsApprovedDeprecationWithYamlBooleanLiteral|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedForApprovedDeprecationWithoutEvidence|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedForDanglingDeprecationApprovalSource'
        runner_classes = @('Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests', 'Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests')
        artifact_paths = @('tests/fixtures/previous-spine.yaml', 'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml')
    },
    [ordered]@{
        category = 'generated-client'
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.NswagConfigurationUsesContractSpineAsOnlyInput|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.GeneratedOutputCarriesStableSafeProvenance|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.StaleGeneratedOutputDetectionUsesAllContentHashes|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.HelperGeneratorProjectIsBuildTimeInput'
        runner_classes = @('Hexalith.Folders.Client.Tests.ClientGenerationTests')
        artifact_paths = @('src/Hexalith.Folders.Client/nswag.json', 'src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs')
    },
    [ordered]@{
        category = 'idempotency-helpers'
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.MutatingRequestsExposeIdempotencyHelpersAndQueriesDoNot|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.BranchRefPolicyHelperUsesDeclaredSpineFields|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.FileMutationHelperSeparatesAddChangeAndRemove|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.CreateFolderHelperUsesDeclaredLexicographicFields|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.CreateRepositoryBackedFolderHelperIncludesExistingFolderIdentity|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.NullAndOmittedValuesProduceDifferentCanonicalHashes|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.FileMutationHelpersIgnoreRawContentAndLocalPaths|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.CanonicalHasherFailsClosedForMalformedMetadata|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.IdempotencyEncodingCorpusIsConsumedForParserPolicyCases|FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests.CanonicalHasherPinsPrimitiveEdgeFormatting'
        runner_classes = @('Hexalith.Folders.Client.Tests.ClientGenerationTests')
        artifact_paths = @('src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs', 'tests/fixtures/idempotency-encoding-corpus.json')
    },
    [ordered]@{
        category = 'parity-oracle-schema'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratedParityRowsValidateAgainstSeedSchemaEnumsAndRequiredColumns|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratedParityRowsClassifyMutatingAndNonMutatingIdempotencyRules|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratedOutcomeMappingPopulatesEveryDeclaredErrorCategory|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.ParitySchemaCanonicalEnumDoesNotDuplicateProviderOutcomeUnknown|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.ParitySchemaOutcomeMappingShapeIsBounded'
        runner_classes = @('Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'tests/fixtures/parity-contract.schema.json')
    },
    [ordered]@{
        category = 'parity-oracle-determinism'
        project_path = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratedParityOracleContainsEveryCurrentOperationExactlyOnce|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorOutputIsByteStableAndMetadataOnly|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedWhenMutatingIdempotencyMetadataIsMissing|FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests.GeneratorFailsClosedForDuplicateIdempotencyFields'
        runner_classes = @('Hexalith.Folders.Contracts.Tests.OpenApi.ParityOracleGeneratorTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'tests/tools/parity-oracle-generator')
    },
    [ordered]@{
        category = 'sdk-transport-parity'
        project_path = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Client.Tests.TransportParityConformanceTests|FullyQualifiedName~Hexalith.Folders.Client.Tests.ArchiveFolderClientConformanceTests|FullyQualifiedName~Hexalith.Folders.Client.Tests.LifecycleStatusClientConformanceTests'
        runner_classes = @('Hexalith.Folders.Client.Tests.TransportParityConformanceTests', 'Hexalith.Folders.Client.Tests.ArchiveFolderClientConformanceTests', 'Hexalith.Folders.Client.Tests.LifecycleStatusClientConformanceTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs')
    },
    [ordered]@{
        category = 'rest-sdk-golden-parity'
        project_path = 'tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.IntegrationTests.EndToEnd.GoldenLifecycleParityTests'
        runner_classes = @('Hexalith.Folders.IntegrationTests.EndToEnd.GoldenLifecycleParityTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'tests/shared/Parity/GoldenLifecycle.cs')
    },
    [ordered]@{
        category = 'cli-behavioral-parity'
        project_path = 'tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Cli.Tests.ParityOracleConformanceTests|FullyQualifiedName~Hexalith.Folders.Cli.Tests.BehavioralParityTests'
        runner_classes = @('Hexalith.Folders.Cli.Tests.ParityOracleConformanceTests', 'Hexalith.Folders.Cli.Tests.BehavioralParityTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'src/Hexalith.Folders.Cli')
    },
    [ordered]@{
        category = 'mcp-behavioral-parity'
        project_path = 'tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.Mcp.Tests.ParityOracleConformanceTests|FullyQualifiedName~Hexalith.Folders.Mcp.Tests.PreSdkFailureTests|FullyQualifiedName~Hexalith.Folders.Mcp.Tests.PostSdkMappingTests|FullyQualifiedName~Hexalith.Folders.Mcp.Tests.SourcingTests|FullyQualifiedName~Hexalith.Folders.Mcp.Tests.FailureKindProjectionTests'
        runner_classes = @('Hexalith.Folders.Mcp.Tests.ParityOracleConformanceTests', 'Hexalith.Folders.Mcp.Tests.PreSdkFailureTests', 'Hexalith.Folders.Mcp.Tests.PostSdkMappingTests', 'Hexalith.Folders.Mcp.Tests.SourcingTests', 'Hexalith.Folders.Mcp.Tests.FailureKindProjectionTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'src/Hexalith.Folders.Mcp')
    },
    [ordered]@{
        category = 'mixed-surface-handoff'
        project_path = 'tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj'
        filter = 'FullyQualifiedName~Hexalith.Folders.IntegrationTests.AdapterParity.CrossAdapterBehavioralParityTests|FullyQualifiedName~Hexalith.Folders.IntegrationTests.MixedSurfaceHandoff.MixedSurfaceHandoffTests'
        runner_classes = @('Hexalith.Folders.IntegrationTests.AdapterParity.CrossAdapterBehavioralParityTests', 'Hexalith.Folders.IntegrationTests.MixedSurfaceHandoff.MixedSurfaceHandoffTests')
        artifact_paths = @('tests/fixtures/parity-contract.yaml', 'tests/shared/Parity/MixedSurfaceScenario.cs')
    }
)

function Write-ContractParityReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results
    )

    [ordered]@{
        gate = 'contract-parity-ci'
        status = $Status
        report_path = '_bmad-output/gates/contract-parity-ci/latest.json'
        diagnostic_policy = 'metadata-only'
        categories = $testGates.category
        test_gates = $testGates
        results = $Results
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-ContractParityGate {
    param(
        [Parameter(Mandatory = $true)]$Gate
    )

    Write-Host "CONTRACT-PARITY-CI category=$($Gate.category) project=$($Gate.project_path)"
    $arguments = @('test', $gate.project_path, '--no-restore', '--no-build', '--filter', $gate.filter)
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0 -and (($output -join [Environment]::NewLine) -match 'System\.Net\.Sockets\.SocketException.*Permission denied')) {
        $exitCode = Invoke-XunitInProcessFallback -Gate $Gate
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
    Write-ContractParityReport -Status $status -Results $script:results

    if ($exitCode -ne 0) {
        exit $exitCode
    }
}

function Invoke-XunitInProcessFallback {
    param(
        [Parameter(Mandatory = $true)]$Gate
    )

    Write-Host "CONTRACT-PARITY-CI category=$($Gate.category) vstest-socket-denied=true fallback=xunit-in-process"
    $projectDirectory = Split-Path -Parent (Join-Path $repositoryRoot $Gate.project_path)
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($Gate.project_path)
    $runnerPath = Join-Path $projectDirectory "bin/Debug/net10.0/$projectName"
    if (-not (Test-Path $runnerPath)) {
        Write-Error "CONTRACT-PARITY-CI-PREREQUISITE-DRIFT: xUnit in-process runner not found at repository-relative project output for $($Gate.project_path). Build the solution before running this gate."
        return 1
    }

    $runnerArguments = @('-noLogo', '-noColor')
    foreach ($class in $Gate.runner_classes) {
        $runnerArguments += @('-class', $class)
    }

    & $runnerPath @runnerArguments 2>&1 | ForEach-Object { Write-Host $_ }
    return $LASTEXITCODE
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ContractParityReport -Status 'discovered' -Results $results

    foreach ($gate in $testGates) {
        Invoke-ContractParityGate -Gate $gate
    }

    Write-ContractParityReport -Status 'passed' -Results $results
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
