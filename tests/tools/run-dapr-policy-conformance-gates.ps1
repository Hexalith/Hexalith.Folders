#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'DAPR-POLICY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the Dapr policy conformance gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/dapr-policy-conformance'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false

function Write-DaprPolicyConformanceReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    [ordered]@{
        gate = 'dapr-policy-conformance'
        status = $Status
        exit_code = $ExitCode
        canonical_inputs = @(
            'deploy/dapr/production/accesscontrol.yaml',
            'deploy/dapr/production/daprsystem.yaml',
            'tests/fixtures/dapr-policy-conformance.yaml'
        )
        test_project = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
        report_path = '_bmad-output/gates/dapr-policy-conformance/latest.json'
        diagnostic_policy = 'metadata-only'
        live_dapr_kind_gate = 'reference_pending_story_7_8'
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-DaprPolicyConformanceReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.slnx --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter FullyQualifiedName~Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance
    if ($LASTEXITCODE -ne 0) {
        Write-DaprPolicyConformanceReport -Status 'failed' -ExitCode $LASTEXITCODE
        exit $LASTEXITCODE
    }

    Write-DaprPolicyConformanceReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
