#Requires -Version 7
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Hexalith.Folders.Contracts.Tests', 'Hexalith.Folders.Client.Tests', 'Hexalith.Folders.Tests', 'Hexalith.Folders.Server.Tests', 'Hexalith.Folders.AppHost.Tests', 'Hexalith.Folders.UI.E2E.Tests')]
    [string]$ProjectName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assembly = Join-Path $repositoryRoot "tests/$ProjectName/bin/Release/net11.0/$ProjectName.dll"
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw "Shared Release build output is missing for $ProjectName."
}

# No class or method selector: ownership delegates the complete project once.
$output = & dotnet $assembly -noLogo -noColor -explicit on 2>&1
$exitCode = $LASTEXITCODE
$output | ForEach-Object { Write-Host $_ }
if ($exitCode -ne 0) {
    exit $exitCode
}

$summaryPattern = '(?m)^\s*' + [regex]::Escape($ProjectName) + '\s+Total:\s*(?<total>\d+),\s*Errors:\s*(?<errors>\d+),\s*Failed:\s*(?<failed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Not Run:\s*(?<notRun>\d+)(?:,\s*Time:\s*\d+(?:\.\d+)?s)?\s*$'
$summaries = [regex]::Matches(($output -join [Environment]::NewLine), $summaryPattern)
if ($summaries.Count -ne 1 -or [int]$summaries[0].Groups['total'].Value -lt 1) {
    throw "$ProjectName did not produce one nonempty complete-project test summary."
}
foreach ($counter in @('errors', 'failed', 'skipped', 'notRun')) {
    if ([int]$summaries[0].Groups[$counter].Value -ne 0) {
        throw "$ProjectName reported unsuccessful tests: $counter."
    }
}
