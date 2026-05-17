param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptRoot '..' '..')
Push-Location $repositoryRoot
try {
    $restoreArgs = @()
    if ($NoRestore) {
        $restoreArgs += '--no-restore'
    }

    $projects = @(
        @{
            Path   = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
            Filter = 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi'
        },
        @{
            Path   = 'tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj'
            Filter = 'FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests'
        }
    )

    $aggregateExitCode = 0
    foreach ($project in $projects) {
        dotnet test $project.Path @restoreArgs --filter $project.Filter
        if ($LASTEXITCODE -ne 0 -and $aggregateExitCode -eq 0) {
            $aggregateExitCode = $LASTEXITCODE
        }
    }

    if ($aggregateExitCode -ne 0) {
        exit $aggregateExitCode
    }
}
finally {
    Pop-Location
}
