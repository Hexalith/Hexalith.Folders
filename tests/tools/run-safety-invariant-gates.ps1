param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'SAFETY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the safety invariant gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/safety-invariants'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false

function Write-SafetyReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'safety-invariants'
        status = $Status
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/safety-invariants/latest.json'
        diagnostic_policy = 'metadata-only'
        validation_class = 'Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-SafetyTests {
    $testAssembly = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll'
    if (-not (Test-Path $testAssembly)) {
        Write-Host 'SAFETY-PREREQUISITE-DRIFT: safety test assembly is missing. Build the Release test project before running this gate.'
        return 1
    }

    $PSNativeCommandUseErrorActionPreference = $false
    $output = & dotnet $testAssembly -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -eq 0 -and -not (($output -join [Environment]::NewLine) -match 'Total:\s+[1-9]\d*')) {
        Write-Host 'SAFETY-PREREQUISITE-DRIFT: safety test selection executed zero tests.'
        return 1
    }

    return $exitCode
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-SafetyReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true
        if ($LASTEXITCODE -ne 0) {
            Write-SafetyReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror
        if ($LASTEXITCODE -ne 0) {
            Write-SafetyReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testAssembly = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll'

        if (-not (Test-Path $testAssembly)) {
            Write-Error 'SAFETY-PREREQUISITE-DRIFT: safety test assembly is missing. Run the safety gate without -SkipRestoreBuild, or run the shared restore/build lane before using -SkipRestoreBuild.'
            Write-SafetyReport -Status 'failed' -ExitCode 1
            exit 1
        }
    }

    $testExitCode = Invoke-SafetyTests
    if ($testExitCode -ne 0) {
        Write-SafetyReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    Write-SafetyReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
