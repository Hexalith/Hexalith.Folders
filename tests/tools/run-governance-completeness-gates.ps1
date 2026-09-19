#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'GOVERNANCE-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the governance completeness gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/governance-completeness'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$governanceClasses = @(
    'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests',
    'Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests',
    'Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests'
)

function Write-GovernanceReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'governance-completeness'
        status = $Status
        exit_code = $ExitCode
        canonical_inputs = @(
            'docs/exit-criteria/c0-c13-governance-evidence.yaml',
            'docs/exit-criteria/c7-lock-authorization-timing.md',
            'docs/contract/file-context-contract-groups.md',
            'docs/contract/oq2-file-policy-evidence.yaml',
            'docs/contract/authorization-matrix.md',
            'docs/contract/oq3-authorization-evidence.yaml',
            'docs/contract/provider-compatibility-catalog.md',
            'docs/contract/oq4-provider-compatibility-evidence.yaml',
            'tests/fixtures/idempotency-encoding-corpus.json',
            'tests/fixtures/idempotency-encoding-corpus-consumption.yaml',
            'tests/fixtures/pattern-example-manifest.yaml',
            'tests/fixtures/cache-key-exceptions.yaml',
            'tests/fixtures/parity-contract.yaml',
            'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml',
            'src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml'
        )
        report_path = '_bmad-output/gates/governance-completeness/latest.json'
        diagnostic_policy = 'metadata-only'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-GovernanceTests {
    $testAssembly = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll'
    if (-not (Test-Path $testAssembly)) {
        Write-Host 'GOVERNANCE-PREREQUISITE-DRIFT: governance test assembly is missing. Build the Release test project before running this gate.'
        return 1
    }

    $PSNativeCommandUseErrorActionPreference = $false
    foreach ($testClass in $governanceClasses) {
        $output = & dotnet $testAssembly -noLogo -noColor -class $testClass 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            return $exitCode
        }
        if (-not (($output -join [Environment]::NewLine) -match 'Total:\s+[1-9]\d*')) {
            Write-Host "GOVERNANCE-PREREQUISITE-DRIFT: governance class selection executed zero tests class=$testClass"
            return 1
        }
    }

    return 0
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-GovernanceReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $testExitCode = Invoke-GovernanceTests
    if ($testExitCode -ne 0) {
        Write-GovernanceReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    Write-GovernanceReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
