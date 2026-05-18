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
$pushed = $false
try {
    Push-Location $repositoryRoot
    $pushed = $true

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
    else {
        $testAssembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin') -Recurse -Filter 'Hexalith.Folders.Contracts.Tests.dll' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
            Select-Object -First 1

        if ($null -eq $testAssembly) {
            Write-Error 'SAFETY-PREREQUISITE-DRIFT: safety test assembly is missing. Run the safety gate without -SkipRestoreBuild, or run the shared restore/build lane before using -SkipRestoreBuild.'
            exit 1
        }
    }

    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
