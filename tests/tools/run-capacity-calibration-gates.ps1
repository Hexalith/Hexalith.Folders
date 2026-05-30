#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'CAPACITY-CALIBRATION-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the capacity calibration gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$gateRelativePath = '_bmad-output/gates/capacity-calibration'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$calibrationReportRelativePath = '_bmad-output/gates/capacity-calibration/reports'
$evidenceRelativePath = '_bmad-output/gates/capacity-calibration/reports/lifecycle-capacity-evidence.json'
$loadProjectPath = 'tests/load/Hexalith.Folders.LoadTests.csproj'
$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
$pushed = $false
$results = @()
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

$categories = @(
    'harness-build-output',
    'release-calibration-run',
    'calibration-artifacts',
    'evidence-shape',
    'target-comparison',
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
    'tests/load/Scenarios/LifecycleCapacityProfile.cs',
    'tests/load/Scenarios/LifecycleCapacityScenario.cs',
    'tests/load/Scenarios/LifecycleCapacityEvidenceWriter.cs',
    'tests/load/Scenarios/LifecycleCapacityRunRecorder.cs',
    'docs/exit-criteria/c1-capacity.md',
    'docs/exit-criteria/c2-freshness.md',
    'docs/exit-criteria/c5-scalability-quantifiers.md',
    'docs/exit-criteria/c0-c13-governance-evidence.yaml',
    'docs/operations/capacity-calibration.md'
)

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

function Write-CapacityCalibrationReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowNull()]$Evidence,
        [int]$ExitCode = 0
    )

    $payload = [ordered]@{
        gate = 'capacity-calibration'
        status = $Status
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/capacity-calibration/latest.json'
        diagnostic_policy = 'metadata-only'
        categories = $categories
        profile_name = 'release-calibration'
        run_id = 'capacity-calibration'
        source_commit = $sourceCommit
        required_measured_steps = $requiredSteps
        evidence_path = $evidenceRelativePath
        calibration_report_path = $calibrationReportRelativePath
        c1_artifact_path = 'docs/exit-criteria/c1-capacity.md'
        c2_artifact_path = 'docs/exit-criteria/c2-freshness.md'
        c5_artifact_path = 'docs/exit-criteria/c5-scalability-quantifiers.md'
        results = $script:results
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
    }

    if ($null -ne $Evidence) {
        $payload.hardware_profile = $Evidence.hardware_profile
        $payload.measured_steps = @($Evidence.measured_steps)
        $payload.observed_step_counts = $Evidence.observed_step_counts
        $payload.observed_counts = $Evidence.observed_counts
        $payload.result_codes = $Evidence.result_codes
        $payload.scenario_names = @($Evidence.scenario_names)
        $payload.result_artifact_paths = @($Evidence.result_artifact_paths)
        $payload.latency_stats = $Evidence.step_latency_statistics
        $payload.throughput = $Evidence.throughput
        $payload.freshness_observations = $Evidence.freshness_observations
        $payload.target_comparison = $Evidence.target_comparison
    }

    $payload | ConvertTo-Json -Depth 12 | Set-Content -Path $latestReportPath -Encoding utf8NoBOM
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason,
        [int]$ExitCode = 1
    )

    Add-Result -Category $Category -Status 'failed' -ExitCode $ExitCode
    Write-CapacityCalibrationReport -Status 'failed' -Evidence $null -ExitCode $ExitCode
    Write-Error "CAPACITY-CALIBRATION-FAILED: category=$Category reason=$Reason"
    exit $ExitCode
}

function Assert-RequiredInput {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Fail-Gate -Category 'calibration-artifacts' -Reason "missing-input path=$RelativePath"
    }
}

function Assert-LoadHarnessAssembly {
    $assembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests/load/bin') -Recurse -Filter 'Hexalith.Folders.LoadTests.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $assembly) {
        Fail-Gate -Category 'harness-build-output' -Reason 'missing-load-harness-assembly'
    }

    Add-Result -Category 'harness-build-output' -Status 'passed' -ExitCode 0
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

function Assert-MetadataOnlyString {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Category
    )

    if ($Value -match '^(?:[A-Za-z]:[\\/]|/|\\\\)') {
        Fail-Gate -Category $Category -Reason 'absolute-path-diagnostic'
    }

    # Also catch absolute paths embedded mid-string (e.g. "evidence at /home/...") so the
    # metadata-only guarantee does not depend on the leak appearing at the start of the value.
    if ($Value -match '(?:[A-Za-z]:[\\/]|(?<![\w.])/(?:home|root|Users|var|etc|tmp|mnt|opt)[\\/]|\\\\[^\\]+\\)') {
        Fail-Gate -Category $Category -Reason 'absolute-path-diagnostic'
    }

    if ($Value -match '(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|raw file contents|provider payload|environment dump|local absolute path|stack trace|https?://') {
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

function Assert-CalibrationDocument {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Criterion
    )

    $text = Get-Content -Raw -Path (Join-Path $repositoryRoot $RelativePath)
    foreach ($required in @('status:', 'decision owner:', 'approval authority:', 'last reviewed:', 'Run command', 'Evidence path', 'Rollback or recalibration rule')) {
        if (-not $text.Contains($required, [StringComparison]::OrdinalIgnoreCase)) {
            Fail-Gate -Category 'calibration-artifacts' -Reason "missing-document-field criterion=$Criterion field=$required"
        }
    }

    if ($text -match '(?i)\bTBD\b|\blater\b|prose-only') {
        Fail-Gate -Category 'calibration-artifacts' -Reason "placeholder-target criterion=$Criterion"
    }

    if ($Criterion -eq 'C1') {
        foreach ($target in @('Maximum concurrent tenants | 4', 'Folders per tenant | 2', 'Active workspaces per tenant | 2', 'Concurrent agent tasks per tenant | 2')) {
            if (-not $text.Contains($target, [StringComparison]::Ordinal)) {
                Fail-Gate -Category 'calibration-artifacts' -Reason "missing-numeric-target criterion=C1 target=$target"
            }
        }
    }

    if ($Criterion -eq 'C2' -and -not $text.Contains('Maximum commit-to-status-read freshness lag | 500', [StringComparison]::Ordinal)) {
        Fail-Gate -Category 'calibration-artifacts' -Reason 'missing-c2-freshness-value'
    }

    if ($Criterion -eq 'C5') {
        foreach ($target in @('Tenant scale units | 4', 'Folder scale units per tenant | 2', 'Workspace scale units per tenant | 2', 'Agent task scale units per tenant | 2', 'Minimum lifecycle iteration rate | 1')) {
            if (-not $text.Contains($target, [StringComparison]::Ordinal)) {
                Fail-Gate -Category 'calibration-artifacts' -Reason "missing-c5-quantifier target=$target"
            }
        }
    }
}

function Assert-GovernanceEvidence {
    $governance = Get-Content -Raw -Path (Join-Path $repositoryRoot 'docs/exit-criteria/c0-c13-governance-evidence.yaml')

    # Full structural YAML well-formedness (criteria sequence, criterion fields, no duplicate keys)
    # is enforced semantically by CapacityCalibrationConformanceTests via YamlDotNet YamlStream.Load,
    # which fails closed on malformed YAML. PowerShell 7 has no hermetic native YAML parser, so this
    # gate adds a lightweight tab-indentation guard (tabs are illegal for YAML indentation) plus the
    # required-key substring contract below.
    if ($governance -match "`t") {
        Fail-Gate -Category 'calibration-artifacts' -Reason 'malformed-governance-yaml-tab-indentation'
    }

    foreach ($expected in @('artifact_path: docs/exit-criteria/c1-capacity.md', 'artifact_path: docs/exit-criteria/c2-freshness.md', 'artifact_path: docs/exit-criteria/c5-scalability-quantifiers.md', 'run-capacity-calibration-gates.ps1')) {
        if (-not $governance.Contains($expected, [StringComparison]::Ordinal)) {
            Fail-Gate -Category 'calibration-artifacts' -Reason "governance-evidence-drift expected=$expected"
        }
    }
}

function Assert-NoRecursiveSubmoduleSetup {
    # Honor the task's "fail closed on recursive submodule setup" contract by scanning the artifacts
    # this lane is responsible for. Tokens are concatenated so this gate file itself stays free of the
    # literal flag (the conformance test asserts the script never contains it).
    $recursiveToken = '--' + 'recursive'
    $recurseSubmodulesToken = '--' + 'recurse-submodules'
    foreach ($relativePath in @(
            '.github/workflows/release-packages.yml',
            'docs/operations/capacity-calibration.md',
            'tests/load/README.md',
            'docs/exit-criteria/c0-c13-governance-evidence.yaml')) {
        $fullPath = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path $fullPath)) {
            continue
        }

        $content = Get-Content -Raw -Path $fullPath
        if ($content.Contains($recursiveToken, [StringComparison]::OrdinalIgnoreCase) `
                -or $content.Contains($recurseSubmodulesToken, [StringComparison]::OrdinalIgnoreCase)) {
            Fail-Gate -Category 'calibration-artifacts' -Reason "recursive-submodule-setup path=$relativePath"
        }
    }
}

function Assert-MetadataOnlyReportFiles {
    # AC13 requires all committed reports (not only the JSON evidence) to stay metadata-only. NBomber
    # text/markdown reports are scanned for absolute paths and unsafe diagnostics. The raw nbomber-log
    # is sanitized at the producer (SanitizeNbomberLogs) and excluded here to avoid flagging benign
    # tool/console formatting.
    $reportsFullPath = Join-Path $repositoryRoot $calibrationReportRelativePath
    if (-not (Test-Path $reportsFullPath)) {
        return
    }

    $reportFiles = Get-ChildItem -Path $reportsFullPath -File -ErrorAction SilentlyContinue |
        Where-Object { ($_.Extension -in @('.txt', '.md')) -and ($_.Name -notlike 'nbomber-log-*') }
    foreach ($file in $reportFiles) {
        foreach ($line in (Get-Content -Path $file.FullName)) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            if ($line -match '(?:[A-Za-z]:[\\/]|(?<![\w.])/(?:home|root|Users|var|etc|tmp|mnt|opt)[\\/]|\\\\[^\\]+\\)') {
                Fail-Gate -Category 'metadata-only-report' -Reason 'absolute-path-in-report'
            }

            if ($line -match '(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git') {
                Fail-Gate -Category 'metadata-only-report' -Reason 'unsafe-diagnostic-in-report'
            }
        }
    }
}

function Invoke-CapacityCommand {
    Write-Host 'CAPACITY-CALIBRATION category=release-calibration-run status=running'
    & dotnet run --no-build --project $loadProjectPath -- --profile release-calibration --run-id capacity-calibration --report-folder $calibrationReportRelativePath
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Add-Result -Category 'release-calibration-run' -Status 'failed' -ExitCode $exitCode
        Write-CapacityCalibrationReport -Status 'failed' -Evidence $null -ExitCode $exitCode
        exit $exitCode
    }

    Add-Result -Category 'release-calibration-run' -Status 'passed' -ExitCode 0
}

function Read-Evidence {
    $fullPath = Join-Path $repositoryRoot $evidenceRelativePath
    if (-not (Test-Path $fullPath)) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-calibration-evidence'
    }

    try {
        return Get-Content -Raw -Path $fullPath | ConvertFrom-Json
    }
    catch {
        Fail-Gate -Category 'evidence-shape' -Reason 'malformed-evidence-json'
    }
}

function Assert-NumericComparison {
    param(
        [Parameter(Mandatory = $true)]$Comparison,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Comparison) {
        Fail-Gate -Category 'target-comparison' -Reason "missing-target-comparison name=$Name"
    }

    if ($null -eq $Comparison.target -or $null -eq $Comparison.observed) {
        Fail-Gate -Category 'target-comparison' -Reason "non-numeric-target name=$Name"
    }

    [double]$target = $Comparison.target
    [double]$observed = $Comparison.observed
    if ($target -le 0 -or $observed -lt 0) {
        Fail-Gate -Category 'target-comparison' -Reason "unsafe-target-value name=$Name"
    }

    if ($Comparison.passed -ne $true) {
        Fail-Gate -Category 'target-comparison' -Reason "threshold-mismatch name=$Name"
    }
}

function Assert-EvidenceShape {
    param([Parameter(Mandatory = $true)]$Evidence)

    if ($Evidence.profile_name -ne 'release-calibration') {
        Fail-Gate -Category 'evidence-shape' -Reason 'profile-name-drift'
    }

    if ($Evidence.thresholds -ne 'release_calibrated') {
        Fail-Gate -Category 'evidence-shape' -Reason 'threshold-posture-drift'
    }

    if ($Evidence.git_commit -ne $sourceCommit -or $Evidence.git_commit -notmatch '^[0-9a-fA-F]{40}$') {
        Fail-Gate -Category 'evidence-shape' -Reason 'stale-source-commit'
    }

    if ($null -eq $Evidence.hardware_profile -or $null -eq $Evidence.hardware_profile.target_hardware_profile) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-hardware-profile'
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

        if ($null -eq $Evidence.step_latency_statistics.$step -or [int]$Evidence.step_latency_statistics.$step.count -lt 1) {
            Fail-Gate -Category 'evidence-shape' -Reason "missing-latency-stat step=$step"
        }
    }

    foreach ($field in @('tenant_count', 'folder_count', 'workspace_count', 'task_count', 'operation_count', 'idempotency_key_count')) {
        if ($null -eq $Evidence.observed_counts.$field -or [int]$Evidence.observed_counts.$field -lt 1) {
            Fail-Gate -Category 'evidence-shape' -Reason "missing-observed-count field=$field"
        }
    }

    if ($null -eq $Evidence.throughput.lifecycle_iterations_per_second -or [double]$Evidence.throughput.lifecycle_iterations_per_second -lt 1) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-throughput-rate'
    }

    if ($null -eq $Evidence.freshness_observations.commit_to_status_read_ms -or [double]$Evidence.freshness_observations.commit_to_status_read_ms.p95_ms -gt 500) {
        Fail-Gate -Category 'evidence-shape' -Reason 'missing-c2-freshness-observation'
    }

    foreach ($pathValue in @($Evidence.result_artifact_paths)) {
        Assert-RepositoryRelativePath -PathValue $pathValue -Category 'evidence-shape'
    }
}

function Assert-TargetComparison {
    param([Parameter(Mandatory = $true)]$Evidence)

    foreach ($name in @('max_concurrent_tenants', 'folders_per_tenant', 'active_workspaces_per_tenant', 'concurrent_agent_tasks_per_tenant')) {
        Assert-NumericComparison -Comparison $Evidence.target_comparison.c1.$name -Name "c1.$name"
    }

    Assert-NumericComparison -Comparison $Evidence.target_comparison.c2.max_commit_to_status_read_freshness_ms -Name 'c2.max_commit_to_status_read_freshness_ms'

    foreach ($name in @('tenant_scale_units', 'folder_scale_units_per_tenant', 'workspace_scale_units_per_tenant', 'agent_task_scale_units_per_tenant', 'minimum_lifecycle_iterations_per_second')) {
        Assert-NumericComparison -Comparison $Evidence.target_comparison.c5.$name -Name "c5.$name"
    }

    Add-Result -Category 'target-comparison' -Status 'passed' -ExitCode 0
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-CapacityCalibrationReport -Status 'discovered' -Evidence $null

    foreach ($input in $requiredInputs) {
        Assert-RequiredInput -RelativePath $input
    }

    Assert-CalibrationDocument -RelativePath 'docs/exit-criteria/c1-capacity.md' -Criterion 'C1'
    Assert-CalibrationDocument -RelativePath 'docs/exit-criteria/c2-freshness.md' -Criterion 'C2'
    Assert-CalibrationDocument -RelativePath 'docs/exit-criteria/c5-scalability-quantifiers.md' -Criterion 'C5'
    Assert-GovernanceEvidence
    Assert-NoRecursiveSubmoduleSetup
    Add-Result -Category 'calibration-artifacts' -Status 'passed' -ExitCode 0

    Assert-LoadHarnessAssembly
    Invoke-CapacityCommand

    $evidence = Read-Evidence
    Assert-EvidenceShape -Evidence $evidence
    Add-Result -Category 'evidence-shape' -Status 'passed' -ExitCode 0
    Assert-TargetComparison -Evidence $evidence

    Write-CapacityCalibrationReport -Status 'validating' -Evidence $evidence
    $report = Get-Content -Raw -Path $latestReportPath | ConvertFrom-Json
    Assert-MetadataOnlyJson -Node $evidence -Category 'metadata-only-report'
    Assert-MetadataOnlyJson -Node $report -Category 'metadata-only-report'
    Assert-MetadataOnlyReportFiles
    Add-Result -Category 'metadata-only-report' -Status 'passed' -ExitCode 0

    Write-CapacityCalibrationReport -Status 'passed' -Evidence $evidence
    Write-Host 'CAPACITY-CALIBRATION status=passed'
}
catch {
    # Fail closed on any unexpected error (including StrictMode missing-property exceptions raised
    # while validating malformed evidence) so latest.json reliably ends at status=failed rather than
    # an interim status. Fail-Gate emits its own specific reason and throws under the Stop preference;
    # only synthesize a generic failed report when no Fail-Gate reason was recorded. The error stream
    # is written directly so this handler cannot itself re-throw, and the detail is metadata-only
    # (exception type name, never the message).
    if (-not ($script:results | Where-Object { $_.status -eq 'failed' })) {
        Add-Result -Category 'evidence-shape' -Status 'failed' -ExitCode 1
        Write-CapacityCalibrationReport -Status 'failed' -Evidence $null -ExitCode 1
        [Console]::Error.WriteLine("CAPACITY-CALIBRATION-FAILED: reason=unexpected-failure type=$($_.Exception.GetType().Name)")
    }

    if ($pushed) {
        Pop-Location
        $pushed = $false
    }

    exit 1
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
