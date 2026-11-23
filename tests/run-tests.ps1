param(
    [ValidateSet("All", "Coverage", "Integration", "Testing", "UiE2E")]
    [string] $Mode = "All"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$testOptions = @()

switch ($Mode) {
    { $_ -in "All", "Coverage" } {
        $solution = [xml](Get-Content -LiteralPath (Join-Path $repoRoot "Hexalith.Folders.slnx") -Raw)
        $testEntries = $solution.SelectNodes("//Project[starts-with(@Path, 'tests/') or starts-with(@Path, 'samples/')]")
        $projects = foreach ($entry in $testEntries) {
            $path = Join-Path $repoRoot ($entry.GetAttribute("Path"))
            $project = [xml](Get-Content -LiteralPath $path -Raw)
            if ($null -ne $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='xunit.v3']")) {
                $path
            }
        }

        if (-not $projects) {
            throw "No xUnit test projects were found in Hexalith.Folders.slnx."
        }

        if ($Mode -eq "Coverage") {
            $testOptions = @(
                "--coverage", "--coverage-output-format", "cobertura", "--coverage-output", "coverage.cobertura.xml",
                "--results-directory", (Join-Path $repoRoot "TestResults"), "--results-directory-layout", "per-module"
            )
        }
    }
    "Integration" {
        $projects = Join-Path $repoRoot "tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj"
    }
    "Testing" {
        $projects = Join-Path $repoRoot "tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj"
    }
    "UiE2E" {
        $projects = Join-Path $repoRoot "tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj"
    }
}

$exitCode = 0
Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        & dotnet test --project $project @testOptions
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
}

exit $exitCode
