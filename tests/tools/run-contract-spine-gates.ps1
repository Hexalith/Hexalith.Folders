param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptRoot '..' '..')
Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
        dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    $projects = @(
        @{
            Path = 'tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll'
            Selector = @('-namespace', 'Hexalith.Folders.Contracts.Tests.OpenApi')
        },
        @{
            Path = 'tests/Hexalith.Folders.Client.Tests/bin/Release/net10.0/Hexalith.Folders.Client.Tests.dll'
            Selector = @('-class', 'Hexalith.Folders.Client.Tests.ClientGenerationTests')
        }
    )

    $aggregateExitCode = 0
    foreach ($project in $projects) {
        if (-not (Test-Path $project.Path)) {
            Write-Host "CONTRACT-SPINE-PREREQUISITE-DRIFT: Release test assembly missing path=$($project.Path)"
            $aggregateExitCode = 1
            continue
        }
        $arguments = @($project.Path, '-noLogo', '-noColor') + @($project.Selector)
        $output = & dotnet @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -eq 0 -and -not (($output -join [Environment]::NewLine) -match 'Total:\s+[1-9]\d*')) {
            Write-Host "CONTRACT-SPINE-PREREQUISITE-DRIFT: selector executed zero tests path=$($project.Path)"
            $exitCode = 1
        }
        if ($exitCode -ne 0 -and $aggregateExitCode -eq 0) {
            $aggregateExitCode = $exitCode
        }
    }

    if ($aggregateExitCode -ne 0) {
        exit $aggregateExitCode
    }
}
finally {
    Pop-Location
}
