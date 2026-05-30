#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'CAPACITY-SMOKE-CI-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the capacity smoke CI gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$gateRelativePath = '_bmad-output/gates/capacity-smoke-ci'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$selfCheckRelativePath = '_bmad-output/gates/capacity-smoke-ci/self-check'
$smokeReportRelativePath = '_bmad-output/gates/capacity-smoke-ci/reports'
$evidenceRelativePath = '_bmad-output/gates/capacity-smoke-ci/reports/lifecycle-capacity-evidence.json'
$loadProjectPath = 'tests/load/Hexalith.Folders.LoadTests.csproj'
$loadReadmePath = 'tests/load/README.md'
$pushed = $false
$results = @()
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

$categories = @(
    'harness-self-check',
    'quick-lifecycle-smoke',
    'evidence-shape',
    'non-production-thresholds',
    'metadata-only-report'
)

$requiredSteps = @(
    'prepare_workspace',
    'acquire_workspace_lock',
    'mutate_workspace_file',
    'commit_workspace',
    'read_workspace_status'
)

$requiredInputs = @(
    $loadProjectPath,
    $loadReadmePath,
    'tests/load/Scenarios/LifecycleCapacityScenario.cs',
    'tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs',
    'tests/load/Scenarios/LifecycleCapacityRunRecorder.cs'
)

function Write-CapacitySmokeReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results,
        [AllowNull()]$Evidence
    )

    $payload = [ordered]@{
        gate = 'capacity-smoke-ci'
        status = $Status
        report_path = '_bmad-output/gates/capacity-smoke-ci/latest.json'
        diagnostic_policy = 'metadata-only'
        categories = $categories
        profile_name = 'quick'
        run_id = 'capacity-smoke-ci'
        threshold_posture = 'reference_pending'
        required_measured_steps = $requiredSteps
        evidence_path = $evidenceRelativePath
        self_check_report_path = $selfCheckRelativePath
        smoke_report_path = $smokeReportRelativePath
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
        results = $Results
    }

    if ($null -ne $Evidence) {
        $payload.measured_steps = @($Evidence.measured_steps)
        $payload.observed_step_counts = $Evidence.observed_step_counts
        $payload.observed_counts = $Evidence.observed_counts
        $payload.result_codes = $Evidence.result_codes
        $payload.scenario_names = @($Evidence.scenario_names)
        $payload.result_artifact_paths = @($Evidence.result_artifact_paths)
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -Path $latestReportPath -Encoding utf8NoBOM
}

function Add-Result {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][int]$ExitCode
    )

    $script:results += [ordered]@{
        category = $Category
        status = $Status
        exit_code = $ExitCode
    }
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    Add-Result -Category $Category -Status 'failed' -ExitCode 1
    Write-CapacitySmokeReport -Status 'failed' -Results $script:results -Evidence $null
    Write-Error "CAPACITY-SMOKE-CI-FAILED: category=$Category reason=$Reason"
    exit 1
}

function Assert-RequiredInput {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Fail-Gate -Category 'harness-self-check' -Reason "missing-input path=$RelativePath"
    }
}

function Invoke-CapacityCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "CAPACITY-SMOKE-CI category=$Category status=running"
    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Add-Result -Category $Category -Status 'failed' -ExitCode $exitCode
        Write-CapacitySmokeReport -Status 'failed' -Results $script:results -Evidence $null
        Write-Error "CAPACITY-SMOKE-CI-FAILED: category=$Category exit_code=$exitCode"
        exit $exitCode
    }

    Add-Result -Category $Category -Status 'passed' -ExitCode 0
}

function Assert-LoadHarnessAssembly {
    $assembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/load/bin') -Recurse -Filter 'Hexalith.Folders.LoadTests.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $assembly) {
        Fail-Gate -Category 'harness-self-check' -Reason 'missing-load-harness-assembly'
    }
}

function Assert-MetadataOnlyString {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Category
    )

    if ($Value -match '^(?:[A-Za-z]:[\\/]|/|\\\\)') {
        Fail-Gate -Category $Category -Reason 'absolute-path-diagnostic'
    }

    if ($Value -match '(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|diff --git|raw file contents|provider payload|environment dump|local absolute path|stack trace|cache-key-value|https?://') {
        Fail-Gate -Category $Category -Reason 'unsafe-diagnostic-field'
    }
}

function Assert-MetadataOnlyJson {
    param(
        [AllowNull()]$Node,
        [Parameter(Mandatory = $true)][string]$Category
    )

    if ($null -eq $Node) {
        return
    }

    if ($Node -is [string]) {
        Assert-MetadataOnlyString -Value $Node -Category $Category
        return
    }

    if ($Node -is [ValueType]) {
        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [System.Collections.IDictionary]) {
        foreach ($item in $Node) {
            Assert-MetadataOnlyJson -Node $item -Category $Category
        }

        return
    }

    foreach ($property in $Node.PSObject.Properties) {
        Assert-MetadataOnlyJson -Node $property.Value -Category $Category
    }
}

function Assert-RepositoryRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$Category
    )

    if ([System.IO.Path]::IsPathFullyQualified($PathValue)) {
        Fail-Gate -Category $Category -Reason 'absolute-artifact-path'
    }

    if ($PathValue.Contains('..', [StringComparison]::Ordinal)) {
        Fail-Gate -Category $Category -Reason 'parent-directory-artifact-path'
    }
}

function Read-Evidence {
    $fullPath = Join-Path $repositoryRoot $evidenceRelativePath
    if (-not (Test-Path $fullPath)) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-evidence-sidecar'
    }

    try {
        return Get-Content -Raw -Path $fullPath | ConvertFrom-Json
    }
    catch {
        Fail-Gate -Category 'evidence-shape' -Reason 'malformed-evidence-json'
    }
}

function Assert-EvidenceShape {
    param(
        [Parameter(Mandatory = $true)]$Evidence
    )

    if ($Evidence.profile_name -ne 'quick') {
        Fail-Gate -Category 'evidence-shape' -Reason 'profile-name-drift'
    }

    if (@($Evidence.scenario_names) -notcontains 'folder_workspace_full_lifecycle') {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-lifecycle-scenario'
    }

    foreach ($step in $requiredSteps) {
        if (@($Evidence.measured_steps) -notcontains $step) {
            Fail-Gate -Category 'evidence-shape' -Reason "missing-measured-step step=$step"
        }

        $count = [int]$Evidence.observed_step_counts.$step
        if ($count -lt 1) {
            Fail-Gate -Category 'evidence-shape' -Reason "zero-or-partial-step-execution step=$step"
        }
    }

    foreach ($field in @('tenant_count', 'folder_count', 'workspace_count', 'task_count', 'operation_count', 'idempotency_key_count')) {
        if ($null -eq $Evidence.observed_counts.$field -or [int]$Evidence.observed_counts.$field -lt 1) {
            Fail-Gate -Category 'evidence-shape' -Reason "missing-observed-count field=$field"
        }
    }

    if ($null -eq $Evidence.result_codes.Accepted -or [int]$Evidence.result_codes.Accepted -lt 4) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-lifecycle-accepted-result-codes'
    }

    if ($null -eq $Evidence.result_codes.Allowed -or [int]$Evidence.result_codes.Allowed -lt 1) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-status-allowed-result-code'
    }

    foreach ($pathValue in @($Evidence.result_artifact_paths)) {
        Assert-RepositoryRelativePath -PathValue $pathValue -Category 'evidence-shape'
    }
}

function Assert-NonProductionThresholds {
    param(
        [Parameter(Mandatory = $true)]$Evidence
    )

    if ($Evidence.thresholds -ne 'reference_pending') {
        Fail-Gate -Category 'non-production-thresholds' -Reason 'threshold-posture-drift'
    }

    $json = $Evidence | ConvertTo-Json -Depth 12
    foreach ($forbidden in @('p95', 'throughput', 'concurrent tenant', 'c1 target', 'c2 target', 'c5 target', 'target hardware')) {
        if ($json.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            Fail-Gate -Category 'non-production-thresholds' -Reason 'release-threshold-claim'
        }
    }

    if ($elapsed.Elapsed.TotalMinutes -gt 5) {
        Fail-Gate -Category 'non-production-thresholds' -Reason 'smoke-duration-sanity-limit'
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-CapacitySmokeReport -Status 'discovered' -Results $results -Evidence $null

    foreach ($input in $requiredInputs) {
        Assert-RequiredInput -RelativePath $input
    }

    Assert-LoadHarnessAssembly

    Invoke-CapacityCommand -Category 'harness-self-check' -Arguments @(
        'run', '--no-build', '--project', $loadProjectPath, '--',
        '--self-check',
        '--profile', 'quick',
        '--report-folder', $selfCheckRelativePath
    )

    Invoke-CapacityCommand -Category 'quick-lifecycle-smoke' -Arguments @(
        'run', '--no-build', '--project', $loadProjectPath, '--',
        '--profile', 'quick',
        '--run-id', 'capacity-smoke-ci',
        '--report-folder', $smokeReportRelativePath
    )

    $evidence = Read-Evidence
    Assert-EvidenceShape -Evidence $evidence
    Add-Result -Category 'evidence-shape' -Status 'passed' -ExitCode 0
    Write-CapacitySmokeReport -Status 'evidence-validated' -Results $results -Evidence $evidence

    Assert-NonProductionThresholds -Evidence $evidence
    Add-Result -Category 'non-production-thresholds' -Status 'passed' -ExitCode 0

    Assert-MetadataOnlyJson -Node $evidence -Category 'metadata-only-report'
    Write-CapacitySmokeReport -Status 'passed' -Results $results -Evidence $evidence
    $report = Get-Content -Raw -Path $latestReportPath | ConvertFrom-Json
    Assert-MetadataOnlyJson -Node $report -Category 'metadata-only-report'
    Add-Result -Category 'metadata-only-report' -Status 'passed' -ExitCode 0
    Write-CapacitySmokeReport -Status 'passed' -Results $results -Evidence $evidence
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
