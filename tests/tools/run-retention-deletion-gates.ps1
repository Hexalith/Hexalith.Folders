#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$gateRelativePath = '_bmad-output/gates/retention-deletion'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$rawCommit = & git -C $repositoryRoot rev-parse HEAD 2>$null
$gitExitCode = $LASTEXITCODE
if ($gitExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($rawCommit)) {
    # Guard before .Trim(): when git is absent, the tree is not a repo, or HEAD is unborn the
    # command yields $null, and calling .Trim() on it would throw under Set-StrictMode -Latest
    # before the try block, making this NO_VCS fallback unreachable dead code.
    $sourceCommit = 'NO_VCS'
}
else {
    $sourceCommit = ([string]$rawCommit).Trim()
}

$requiredClasses = @(
    'Audit metadata',
    'Workspace status',
    'Provider correlation IDs',
    'Read-model views',
    'Temporary working files',
    'Cleanup records'
)

$artifactPaths = @(
    'docs/exit-criteria/c3-retention.md',
    'docs/operations/retention-and-tenant-deletion.md',
    'docs/runbooks/tenant-deletion.md',
    'docs/exit-criteria/c0-c13-governance-evidence.yaml',
    '.github/workflows/release-packages.yml',
    'tests/tools/run-release-package-gates.ps1',
    'deploy/nuget/release-packages.yaml'
)

$categories = @(
    'policy-source',
    'tenant-deletion-matrix',
    'governance-evidence',
    'release-readiness',
    'source-commit',
    'metadata-only-report'
)

$results = @()
$policyRows = @()
$tenantDeletionRows = @()
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

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

function Write-RetentionDeletionReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][string]$PolicyStatus,
        [int]$ExitCode = 0
    )

    $payload = [ordered]@{
        gate = 'retention-deletion'
        status = $Status
        policy_status = $PolicyStatus
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/retention-deletion/latest.json'
        diagnostic_policy = 'metadata-only'
        source_commit = $sourceCommit
        required_data_classes = $requiredClasses
        tenant_deletion_matrix_rows = $script:tenantDeletionRows
        artifact_paths = $artifactPaths
        validation_categories = $categories
        result_summaries = $script:results
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -Path $latestReportPath -Encoding utf8NoBOM
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason,
        [int]$ExitCode = 1
    )

    Add-Result -Category $Category -Status 'failed' -ExitCode $ExitCode
    Write-RetentionDeletionReport -Status 'failed' -PolicyStatus 'invalid' -ExitCode $ExitCode
    # Emit the bounded, metadata-only diagnostic on a non-throwing channel so the explicit exit
    # below is actually reached. Write-Error would throw under $ErrorActionPreference='Stop',
    # divert into the catch block, and leave `exit $ExitCode` as dead code.
    [Console]::Error.WriteLine("RETENTION-DELETION-FAILED: category=$Category reason=$Reason")
    exit $ExitCode
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

    if ($Value -match '(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|raw file contents|provider payload|environment dump|stack trace|https?://') {
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

function Split-MarkdownRow {
    param([Parameter(Mandatory = $true)][string]$Line)

    return @($Line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
}

function Read-MarkdownTable {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$HeaderSentinel
    )

    $lines = Get-Content -Path (Join-Path $repositoryRoot $RelativePath)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith('|', [StringComparison]::Ordinal) -and $lines[$i].Contains($HeaderSentinel, [StringComparison]::Ordinal)) {
            $headers = Split-MarkdownRow -Line $lines[$i]
            $rows = @()
            for ($j = $i + 2; $j -lt $lines.Count; $j++) {
                if (-not $lines[$j].StartsWith('|', [StringComparison]::Ordinal)) {
                    break
                }

                $cells = Split-MarkdownRow -Line $lines[$j]
                if ($cells.Count -ne $headers.Count) {
                    Fail-Gate -Category 'policy-source' -Reason "malformed-markdown-table path=$RelativePath"
                }

                $row = [ordered]@{}
                for ($cellIndex = 0; $cellIndex -lt $headers.Count; $cellIndex++) {
                    $row[$headers[$cellIndex]] = $cells[$cellIndex]
                }

                $rows += $row
            }

            return $rows
        }
    }

    Fail-Gate -Category 'policy-source' -Reason "missing-markdown-table path=$RelativePath"
}

function Assert-ArtifactExists {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    Assert-RepositoryRelativePath -PathValue $RelativePath -Category 'policy-source'
    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Fail-Gate -Category 'policy-source' -Reason "missing-artifact path=$RelativePath"
    }
}

function Assert-NoRecursiveSubmoduleSetup {
    $recursiveToken = '--' + 'recursive'
    $recursiveCommand = 'git submodule update --init ' + $recursiveToken
    foreach ($relativePath in @('.github/workflows/release-packages.yml', 'tests/tools', 'docs', 'deploy', 'src')) {
        $fullPath = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path $fullPath)) {
            continue
        }

        $files = if ((Get-Item $fullPath).PSIsContainer) {
            Get-ChildItem -Path $fullPath -Recurse -File -ErrorAction SilentlyContinue
        }
        else {
            @(Get-Item $fullPath)
        }

        foreach ($file in $files) {
            if ($file.FullName -match '[\\/]bin[\\/]|[\\/]obj[\\/]') {
                continue
            }

            $content = Get-Content -Raw -Path $file.FullName
            if ($null -eq $content) {
                continue
            }

            if ($content.Contains($recursiveCommand, [StringComparison]::OrdinalIgnoreCase)) {
                Fail-Gate -Category 'metadata-only-report' -Reason 'recursive-submodule-setup'
            }
        }
    }
}

function Assert-C3Policy {
    $c3 = Get-Content -Raw -Path (Join-Path $repositoryRoot 'docs/exit-criteria/c3-retention.md')
    if (-not $c3.Contains('policy status: approved', [StringComparison]::Ordinal)) {
        Fail-Gate -Category 'policy-source' -Reason 'missing-policy-status'
    }

    # PM approved 2026-06-22 (Jerome); Legal approved 2026-06-24 (Jérôme Piquot, Louveciennes), so the
    # release posture is cleared for live publishing. The posture must record the approved-for-release state.
    if (-not $c3.Contains('release posture: approved_for_live_release', [StringComparison]::Ordinal)) {
        Fail-Gate -Category 'policy-source' -Reason 'missing-approved-release-posture'
    }

    $script:policyRows = Read-MarkdownTable -RelativePath 'docs/exit-criteria/c3-retention.md' -HeaderSentinel 'Retention class identifier'
    foreach ($required in $requiredClasses) {
        $row = @($script:policyRows | Where-Object { $_.'Data class' -eq $required }) | Select-Object -First 1
        if ($null -eq $row) {
            Fail-Gate -Category 'policy-source' -Reason "missing-c3-class class=$required"
        }

        foreach ($field in @('Retention duration', 'Cleanup trigger', 'Disposal behavior', 'Tenant-deletion disposition', 'Tenant-isolation implication', 'Observability evidence', 'Owner', 'Authority', 'Approval state', 'Review date')) {
            if ([string]::IsNullOrWhiteSpace($row[$field])) {
                Fail-Gate -Category 'policy-source' -Reason "missing-c3-field class=$required field=$field"
            }
        }

        if (@('deleted', 'tombstoned', 'retained', 'anonymized') -notcontains $row.'Tenant-deletion disposition') {
            Fail-Gate -Category 'policy-source' -Reason "invalid-tenant-deletion-disposition class=$required"
        }

        if (-not $row.'Approval state'.Contains('Legal approved', [StringComparison]::OrdinalIgnoreCase)) {
            Fail-Gate -Category 'policy-source' -Reason "unexpected-c3-approval-state class=$required"
        }
    }

    Add-Result -Category 'policy-source' -Status 'passed' -ExitCode 0
}

function Assert-TenantDeletionDocs {
    $operations = Get-Content -Raw -Path (Join-Path $repositoryRoot 'docs/operations/retention-and-tenant-deletion.md')
    foreach ($expected in @(
            'pwsh ./tests/tools/run-retention-deletion-gates.ps1',
            '_bmad-output/gates/retention-deletion/latest.json',
            'pending approval blocks live release',
            'metadata-only',
            'git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants')) {
        if (-not $operations.Contains($expected, [StringComparison]::Ordinal)) {
            Fail-Gate -Category 'tenant-deletion-matrix' -Reason "missing-operations-doc-text expected=$expected"
        }
    }

    $runbook = Get-Content -Raw -Path (Join-Path $repositoryRoot 'docs/runbooks/tenant-deletion.md')
    foreach ($disposition in @('deleted', 'tombstoned', 'retained', 'anonymized')) {
        if (-not $runbook.Contains(" $disposition |", [StringComparison]::Ordinal)) {
            Fail-Gate -Category 'tenant-deletion-matrix' -Reason "missing-runbook-disposition disposition=$disposition"
        }
    }

    $rows = Read-MarkdownTable -RelativePath 'docs/runbooks/tenant-deletion.md' -HeaderSentinel 'Disposition'
    foreach ($required in $requiredClasses) {
        $row = @($rows | Where-Object { $_.'Data class' -eq $required }) | Select-Object -First 1
        if ($null -eq $row) {
            Fail-Gate -Category 'tenant-deletion-matrix' -Reason "missing-runbook-class class=$required"
        }

        $script:tenantDeletionRows += [ordered]@{
            data_class = $row.'Data class'
            disposition = $row.Disposition
            automation = $row.'Manual or automated step'
            retention_behavior = $row.'Retention behavior'
        }
    }

    Add-Result -Category 'tenant-deletion-matrix' -Status 'passed' -ExitCode 0
}

function Assert-GovernanceEvidence {
    $governance = Get-Content -Raw -Path (Join-Path $repositoryRoot 'docs/exit-criteria/c0-c13-governance-evidence.yaml')
    foreach ($expected in @(
            'criterion_id: C3',
            'status: approved',
            'artifact_path: docs/exit-criteria/c3-retention.md',
            'verification_command: .\tests\tools\run-retention-deletion-gates.ps1')) {
        if (-not $governance.Contains($expected, [StringComparison]::Ordinal)) {
            Fail-Gate -Category 'governance-evidence' -Reason "governance-evidence-drift expected=$expected"
        }
    }

    Add-Result -Category 'governance-evidence' -Status 'passed' -ExitCode 0
}

function Assert-ReleaseReadiness {
    $workflow = Get-Content -Raw -Path (Join-Path $repositoryRoot '.github/workflows/release-packages.yml')
    $packageGate = Get-Content -Raw -Path (Join-Path $repositoryRoot 'tests/tools/run-release-package-gates.ps1')
    $manifest = Get-Content -Raw -Path (Join-Path $repositoryRoot 'deploy/nuget/release-packages.yaml')

    foreach ($expected in @(
            './tests/tools/run-retention-deletion-gates.ps1',
            '_bmad-output/gates/retention-deletion/latest.json',
            'c3-retention-approval-blocks-live-publish')) {
        if (-not ($workflow.Contains($expected, [StringComparison]::Ordinal) `
                -or $packageGate.Contains($expected, [StringComparison]::Ordinal) `
                -or $manifest.Contains($expected, [StringComparison]::Ordinal))) {
            Fail-Gate -Category 'release-readiness' -Reason "release-readiness-drift expected=$expected"
        }
    }

    Add-Result -Category 'release-readiness' -Status 'passed' -ExitCode 0
}

try {
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

    foreach ($artifact in $artifactPaths) {
        Assert-ArtifactExists -RelativePath $artifact
    }

    if ($sourceCommit -notmatch '^(?:NO_VCS|[0-9a-fA-F]{40})$') {
        Fail-Gate -Category 'source-commit' -Reason 'invalid-source-commit'
    }

    Add-Result -Category 'source-commit' -Status 'passed' -ExitCode 0
    Assert-C3Policy
    Assert-TenantDeletionDocs
    Assert-GovernanceEvidence
    Assert-ReleaseReadiness
    Assert-NoRecursiveSubmoduleSetup

    Write-RetentionDeletionReport -Status 'passed' -PolicyStatus 'approved' -ExitCode 0
    $report = Get-Content -Raw -Path $latestReportPath | ConvertFrom-Json
    Assert-MetadataOnlyJson -Node $report -Category 'metadata-only-report'
    Add-Result -Category 'metadata-only-report' -Status 'passed' -ExitCode 0
    Write-RetentionDeletionReport -Status 'passed' -PolicyStatus 'approved' -ExitCode 0
    Write-Host 'RETENTION-DELETION status=passed policy_status=approved'
}
catch {
    if (-not ($script:results | Where-Object { $_.status -eq 'failed' })) {
        Add-Result -Category 'policy-source' -Status 'failed' -ExitCode 1
        Write-RetentionDeletionReport -Status 'failed' -PolicyStatus 'invalid' -ExitCode 1
        [Console]::Error.WriteLine("RETENTION-DELETION-FAILED: reason=unexpected-failure type=$($_.Exception.GetType().Name)")
    }

    exit 1
}
