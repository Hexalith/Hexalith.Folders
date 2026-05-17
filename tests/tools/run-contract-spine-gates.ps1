param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptRoot '..\..')
Set-Location $repositoryRoot

$restoreArgs = @()
if ($NoRestore) {
    $restoreArgs += '--no-restore'
}

dotnet test 'tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj' @restoreArgs --filter 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi'
dotnet test 'tests\Hexalith.Folders.Client.Tests\Hexalith.Folders.Client.Tests.csproj' @restoreArgs --filter 'FullyQualifiedName~Hexalith.Folders.Client.Tests.ClientGenerationTests'
