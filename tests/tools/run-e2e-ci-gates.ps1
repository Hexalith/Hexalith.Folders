#Requires -Version 7

# Story 8.5 (AC5, Option A) — focused end-to-end CI gate. Provisions Playwright Chromium and runs the FULL
# Hexalith.Folders.UI.E2E.Tests lane (all 63 tests: the Accessibility, Responsive, Smoke, and StateLabels
# namespaces) over the read-only operations console. Story 8.4's run-accessibility-ci-gates.ps1 covers only the
# Accessibility subset; this gate closes the genuine environmental gap by exercising the 40 non-accessibility
# E2E tests in a Chromium-provisioned CI job so a full-solution test run no longer env-fails on UI.E2E. Mirrors
# the focused-gate contract of run-accessibility-ci-gates.ps1: hermetic, metadata-only, no artifact upload /
# publish / secrets / network beyond localhost / recursive submodule init. Writes a metadata-only report to
# _bmad-output/gates/e2e/latest.json (status lifecycle: discovered -> passed | failed).
param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild,
    [switch]$SkipBrowserInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'E2E-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the E2E gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/e2e'
$reportPath = Join-Path $reportDirectory 'latest.json'
$e2eProject = 'tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj'
$pushed = $false

function Write-E2eReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'e2e'
        status = $Status
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/e2e/latest.json'
        diagnostic_policy = 'metadata-only'
        validation_class = 'Hexalith.Folders.UI.E2E.Tests'
        scope = 'full-ui-e2e-lane'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-E2eTests {
    # Keep a native non-zero exit from dotnet test as a returnable code (do not let it throw under Stop) so the
    # xUnit v3 in-process fallback below is reliably reached.
    $PSNativeCommandUseErrorActionPreference = $false
    dotnet test $e2eProject --no-build | Out-Host
    if ($LASTEXITCODE -eq 0) {
        return 0
    }

    # Match the extensionless ELF runner on Linux and the .exe runner on Windows; the regex excludes
    # .dll/.pdb/.json artifacts that a bare -Filter pattern would otherwise miss/include.
    $testExecutable = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.UI.E2E.Tests/bin') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^Hexalith\.Folders\.UI\.E2E\.Tests(\.exe)?$' -and $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $testExecutable) {
        return $LASTEXITCODE
    }

    & $testExecutable.FullName -noLogo -noColor | Out-Host
    return $LASTEXITCODE
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-E2eReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-E2eReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-E2eReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testAssembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.UI.E2E.Tests/bin') -Recurse -Filter 'Hexalith.Folders.UI.E2E.Tests.dll' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
            Select-Object -First 1

        if ($null -eq $testAssembly) {
            Write-Error 'E2E-PREREQUISITE-DRIFT: UI E2E test assembly is missing. Run the E2E gate without -SkipRestoreBuild, or run the shared restore/build lane before using -SkipRestoreBuild.'
            Write-E2eReport -Status 'failed' -ExitCode 1
            exit 1
        }
    }

    # Provision the headless Chromium browser the lane needs. The E2E project is already built above (or by the
    # shared lane under -SkipRestoreBuild), so forward -SkipBuild to install-playwright.ps1 (its own switch is
    # -SkipBuild, not -SkipBrowserInstall). In CI this is provisioned by a separate step and the gate is invoked
    # with -SkipBrowserInstall.
    if (-not $SkipBrowserInstall) {
        & (Join-Path $repositoryRoot 'tests/install-playwright.ps1') -SkipBuild
        if ($LASTEXITCODE -ne 0) {
            Write-E2eReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $testExitCode = Invoke-E2eTests
    if ($testExitCode -ne 0) {
        Write-E2eReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    Write-E2eReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
