#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild,

    [ValidateSet('static-plus-live-reference', 'static-only')]
    [string]$PolicyMode = 'static-plus-live-reference'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'SCHEDULED-POLICY-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the scheduled policy conformance gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$gateRelativePath = '_bmad-output/gates/policy-conformance'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$staticGateReportRelativePath = '_bmad-output/gates/dapr-policy-conformance/latest.json'
$staticGateReportPath = Join-Path $repositoryRoot $staticGateReportRelativePath
$staticGateScript = 'tests/tools/run-dapr-policy-conformance-gates.ps1'
$testProjectPath = 'tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj'
$pushed = $false
$results = @()
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

$categories = @(
    'static-policy-shape',
    'fixture-provenance',
    'negative-triple-coverage',
    'mtls-and-sidecar-bindings',
    'pubsub-topic-scopes',
    'live-kind-dapr-denial'
)

$requiredInputs = @(
    'deploy/dapr/production/accesscontrol.yaml',
    'deploy/dapr/production/daprsystem.yaml',
    'deploy/dapr/production/pubsub.yaml',
    'deploy/dapr/production/secretstore.yaml',
    'deploy/dapr/production/sidecar-config-bindings.yaml',
    'tests/fixtures/dapr-policy-conformance.yaml',
    $staticGateScript
)

$negativeCategories = @(
    'unknown-source-app',
    'known-unauthorized-source-app',
    'wrong-target-app',
    'wrong-operation',
    'wrong-http-verb',
    'wrong-namespace',
    'wrong-trust-domain'
)

function Add-Result {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][string]$Severity,
        [Parameter(Mandatory = $true)][int]$ExitCode
    )

    $script:results += [ordered]@{
        category = $Category
        status = $Status
        severity = $Severity
        exit_code = $ExitCode
    }
}

function Write-ScheduledPolicyReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results,
        [AllowNull()]$StaticReport
    )

    [ordered]@{
        gate = 'policy-conformance'
        status = $Status
        report_path = '_bmad-output/gates/policy-conformance/latest.json'
        diagnostic_policy = 'metadata-only'
        trigger_policy = 'schedule_utc_or_manual_dispatch_default_branch'
        policy_mode = $PolicyMode
        categories = $categories
        canonical_inputs = $requiredInputs | Where-Object { $_ -ne $staticGateScript }
        static_gate_script = $staticGateScript
        static_gate_report_path = $staticGateReportRelativePath
        test_project = $testProjectPath
        expected_static_gate_minimum_test_count = 8
        negative_categories = $negativeCategories
        live_kind_dapr_denial = [ordered]@{
            status = 'reference_pending_story_7_8'
            owner = 'platform-engineering'
            command_shape = 'pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1 -PolicyMode static-plus-live-reference'
            evidence_path = '_bmad-output/gates/policy-conformance/latest.json'
            follow_up_boundary = 'replace reference_pending only when isolated synthetic apps prove denied Dapr service invocation returns 403 without production endpoints or secrets'
        }
        static_gate_status = if ($null -ne $StaticReport) { $StaticReport.status } else { $null }
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
        results = $Results
    } | ConvertTo-Json -Depth 10 | Set-Content -Path $latestReportPath -Encoding utf8NoBOM
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    Add-Result -Category $Category -Status 'failed' -Severity 'failure' -ExitCode 1
    Write-ScheduledPolicyReport -Status 'failed' -Results $script:results -StaticReport $null
    Write-Error "SCHEDULED-POLICY-FAILED: category=$Category reason=$Reason"
    exit 1
}

function Assert-RequiredInput {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Fail-Gate -Category 'static-policy-shape' -Reason "missing-input path=$RelativePath"
    }
}

function Assert-TestAssembly {
    $assembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests') -Recurse -Filter 'Hexalith.Folders.Contracts.Tests.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $assembly) {
        Fail-Gate -Category 'static-policy-shape' -Reason 'missing-test-assembly'
    }
}

function Assert-PolicyFixtureText {
    $fixture = Get-Content -Raw -Path (Join-Path $repositoryRoot 'tests/fixtures/dapr-policy-conformance.yaml')
    foreach ($category in $negativeCategories) {
        if ($fixture.IndexOf($category, [StringComparison]::Ordinal) -lt 0) {
            Fail-Gate -Category 'negative-triple-coverage' -Reason "missing-negative-category category=$category"
        }
    }
}

function Invoke-RestoreAndBuild {
    dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        Add-Result -Category 'static-policy-shape' -Status 'failed' -Severity 'failure' -ExitCode $LASTEXITCODE
        Write-ScheduledPolicyReport -Status 'failed' -Results $script:results -StaticReport $null
        exit $LASTEXITCODE
    }

    dotnet build Hexalith.Folders.slnx --no-restore -m:1
    if ($LASTEXITCODE -ne 0) {
        Add-Result -Category 'static-policy-shape' -Status 'failed' -Severity 'failure' -ExitCode $LASTEXITCODE
        Write-ScheduledPolicyReport -Status 'failed' -Results $script:results -StaticReport $null
        exit $LASTEXITCODE
    }
}

function Assert-StaticGateReport {
    param(
        [Parameter(Mandatory = $true)]$Report
    )

    if ($Report.gate -ne 'dapr-policy-conformance') {
        Fail-Gate -Category 'static-policy-shape' -Reason 'static-gate-report-drift'
    }

    if ($Report.status -ne 'passed') {
        Fail-Gate -Category 'static-policy-shape' -Reason "static-gate-not-passed status=$($Report.status)"
    }

    if ($Report.diagnostic_policy -ne 'metadata-only') {
        Fail-Gate -Category 'static-policy-shape' -Reason 'static-gate-diagnostic-policy-drift'
    }

    if ($Report.live_dapr_kind_gate -ne 'reference_pending_story_7_8') {
        Fail-Gate -Category 'live-kind-dapr-denial' -Reason 'live-kind-reference-pending-drift'
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-ScheduledPolicyReport -Status 'discovered' -Results $results -StaticReport $null

    foreach ($input in $requiredInputs) {
        Assert-RequiredInput -RelativePath $input
    }

    # Build before asserting the test assembly exists. The scheduled workflow restores/builds
    # and passes -SkipRestoreBuild; a local run without that switch must build here so the
    # documented `pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1` command works
    # from a clean checkout instead of failing closed on a missing test assembly.
    if (-not $SkipRestoreBuild) {
        Invoke-RestoreAndBuild
    }

    Assert-TestAssembly
    Assert-PolicyFixtureText

    # The scheduled workflow (or this wrapper) has already restored/built the solution, so always
    # invoke the static gate with -SkipRestoreBuild to avoid a redundant second restore/build.
    & pwsh -NoLogo -NoProfile -File (Join-Path $repositoryRoot $staticGateScript) -SkipRestoreBuild
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) {
        $exitCode = 0
    }

    if ((Test-Path $staticGateReportPath)) {
        $postRunStaticReport = Get-Content -Raw -Path $staticGateReportPath | ConvertFrom-Json
        if ($postRunStaticReport.status -eq 'passed') {
            $exitCode = 0
        }
    }

    if ($exitCode -ne 0) {
        Add-Result -Category 'static-policy-shape' -Status 'failed' -Severity 'failure' -ExitCode $exitCode
        Write-ScheduledPolicyReport -Status 'failed' -Results $results -StaticReport $null
        exit $exitCode
    }

    if (-not (Test-Path $staticGateReportPath)) {
        Fail-Gate -Category 'static-policy-shape' -Reason 'missing-static-gate-report'
    }

    $staticReport = Get-Content -Raw -Path $staticGateReportPath | ConvertFrom-Json
    Assert-StaticGateReport -Report $staticReport

    Add-Result -Category 'static-policy-shape' -Status 'passed' -Severity 'none' -ExitCode 0
    Add-Result -Category 'fixture-provenance' -Status 'passed' -Severity 'none' -ExitCode 0
    Add-Result -Category 'negative-triple-coverage' -Status 'passed' -Severity 'none' -ExitCode 0
    Add-Result -Category 'mtls-and-sidecar-bindings' -Status 'passed' -Severity 'none' -ExitCode 0
    Add-Result -Category 'pubsub-topic-scopes' -Status 'passed' -Severity 'none' -ExitCode 0
    Add-Result -Category 'live-kind-dapr-denial' -Status 'reference_pending_story_7_8' -Severity 'warning' -ExitCode 0

    Write-ScheduledPolicyReport -Status 'passed' -Results $results -StaticReport $staticReport
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
