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

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    [ordered]@{
        gate = 'governance-completeness'
        status = 'discovered'
        canonical_inputs = @(
            'docs/exit-criteria/c0-c13-governance-evidence.yaml',
            'tests/fixtures/idempotency-encoding-corpus.json',
            'tests/fixtures/idempotency-encoding-corpus-consumption.yaml',
            'tests/fixtures/pattern-example-manifest.yaml',
            'tests/fixtures/cache-key-exceptions.yaml',
            'tests/fixtures/parity-contract.yaml',
            'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml'
        )
        report_path = '_bmad-output/gates/governance-completeness/latest.json'
        diagnostic_policy = 'metadata-only'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    dotnet build tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
