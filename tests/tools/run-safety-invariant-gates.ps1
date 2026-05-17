param(
    [switch]$NoRestore
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

    $restoreArgs = @()
    if ($NoRestore) {
        $restoreArgs += '--no-restore'
    }

    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj @restoreArgs --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
