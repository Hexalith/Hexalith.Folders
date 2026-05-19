param(
    [ValidateSet("All", "Coverage", "Integration", "Testing", "UiE2E")]
    [string] $Mode = "All"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repoRoot "Hexalith.Folders.slnx"

switch ($Mode) {
    "All" {
        & dotnet test $solution
    }
    "Coverage" {
        & dotnet test $solution '--collect:XPlat Code Coverage'
    }
    "Integration" {
        $project = Join-Path $repoRoot "tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj"
        & dotnet test $project
    }
    "Testing" {
        $project = Join-Path $repoRoot "tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj"
        & dotnet test $project
    }
    "UiE2E" {
        $project = Join-Path $repoRoot "tests\Hexalith.Folders.UI.E2E.Tests\Hexalith.Folders.UI.E2E.Tests.csproj"
        & dotnet test $project
    }
}

exit $LASTEXITCODE
