#Requires -Version 7

# Story 8.4 — focused accessibility CI gate. Provisions Playwright Chromium and runs the axe-core /
# WCAG 2.2 AA scan plus the keyboard / visible-focus and zoom / no-clipping assertions over the read-only
# operations console (the Accessibility namespace of the UI E2E lane). Mirrors the focused-gate contract of
# run-safety-invariant-gates.ps1: hermetic, metadata-only, no artifact upload / publish / secrets / network
# beyond localhost / recursive submodule init. Writes a metadata-only report to
# _bmad-output/gates/accessibility/latest.json (status lifecycle: discovered -> passed | failed).
param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild,
    [switch]$SkipBrowserInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'ACCESSIBILITY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the accessibility gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/accessibility'
$reportPath = Join-Path $reportDirectory 'latest.json'
$e2eProject = 'tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj'
$accessibilityNamespace = 'Hexalith.Folders.UI.E2E.Tests.Accessibility'
$pushed = $false

function Write-AccessibilityReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'accessibility'
        status = $Status
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/accessibility/latest.json'
        diagnostic_policy = 'metadata-only'
        validation_class = 'Hexalith.Folders.UI.E2E.Tests.Accessibility'
        standard = 'WCAG-2.2-AA'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Invoke-AccessibilityTests {
    # Keep a native non-zero exit from dotnet test as a returnable code (do not let it throw under Stop) so the
    # xUnit v3 in-process fallback below is reliably reached.
    $PSNativeCommandUseErrorActionPreference = $false
    dotnet test $e2eProject --no-build --filter "FullyQualifiedName~$accessibilityNamespace" | Out-Host
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

    & $testExecutable.FullName -noLogo -noColor -namespace $accessibilityNamespace | Out-Host
    return $LASTEXITCODE
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-AccessibilityReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-AccessibilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-AccessibilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }
    else {
        $testAssembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/Hexalith.Folders.UI.E2E.Tests/bin') -Recurse -Filter 'Hexalith.Folders.UI.E2E.Tests.dll' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
            Select-Object -First 1

        if ($null -eq $testAssembly) {
            Write-Error 'ACCESSIBILITY-PREREQUISITE-DRIFT: UI E2E test assembly is missing. Run the accessibility gate without -SkipRestoreBuild, or run the shared restore/build lane before using -SkipRestoreBuild.'
            Write-AccessibilityReport -Status 'failed' -ExitCode 1
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
            Write-AccessibilityReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $testExitCode = Invoke-AccessibilityTests
    if ($testExitCode -ne 0) {
        Write-AccessibilityReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    Write-AccessibilityReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
