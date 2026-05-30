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
    # Keep a native non-zero exit from dotnet test as a returnable code (do not let it throw
    # under Stop) so the xUnit v3 in-process fallback below is reliably reached.
    $PSNativeCommandUseErrorActionPreference = $false
    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests | Out-Host
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

    & $testExecutable.FullName -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests | Out-Host
    return $LASTEXITCODE
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-SafetyReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-SafetyReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-SafetyReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testAssembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin') -Recurse -Filter 'Hexalith.Folders.Contracts.Tests.dll' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
            Select-Object -First 1

        if ($null -eq $testAssembly) {
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
