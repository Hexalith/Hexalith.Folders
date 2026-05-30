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
            'tests/fixtures/idempotency-encoding-corpus.json',
            'tests/fixtures/idempotency-encoding-corpus-consumption.yaml',
            'tests/fixtures/pattern-example-manifest.yaml',
            'tests/fixtures/cache-key-exceptions.yaml',
            'tests/fixtures/parity-contract.yaml',
            'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml'
        )
        report_path = '_bmad-output/gates/governance-completeness/latest.json'
        diagnostic_policy = 'metadata-only'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-GovernanceTests {
    # Keep a native non-zero exit from dotnet test as a returnable code (do not let it throw
    # under Stop) so the xUnit v3 in-process fallback below is reliably reached.
    $PSNativeCommandUseErrorActionPreference = $false
    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests | Out-Host
    if ($LASTEXITCODE -eq 0) {
        return 0
    }

    # Match the extensionless ELF runner on Linux and the .exe runner on Windows; the regex
    # excludes .dll/.pdb/.json artifacts that a bare -Filter pattern would otherwise miss/include.
    $testExecutable = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^Hexalith\.Folders\.Contracts\.Tests(\.exe)?$' -and $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $testExecutable) {
        return $LASTEXITCODE
    }

    & $testExecutable.FullName -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests | Out-Host
    return $LASTEXITCODE
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-GovernanceReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj --no-restore
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
