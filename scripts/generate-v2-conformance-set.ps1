[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BaselineCommit = '3f1056d998ac4688f36eb869c516812c1a4ddb71',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$generatorPath = Join-Path $PSScriptRoot 'generate-pd10-v2-conformance-set.py'
$generatorArguments = @(
    $generatorPath,
    '--repository-root',
    $RepositoryRoot,
    '--baseline-commit',
    $BaselineCommit
)
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $generatorArguments += @('--output', $OutputPath)
}

& python3 @generatorArguments
if ($LASTEXITCODE -ne 0) {
    throw "PD10 v2 conformance-set generation failed with exit code $LASTEXITCODE."
}

Write-Output 'Wrote the deterministic PD10 v2 conformance-set manifest.'
