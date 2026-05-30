#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild,

    [ValidateSet('pinned-snapshots', 'latest-supported')]
    [string]$ProviderProfile = 'pinned-snapshots'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'NIGHTLY-DRIFT-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the nightly drift gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$gateRelativePath = '_bmad-output/gates/nightly-drift'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$sanitizedReportRelativePath = '_bmad-output/gates/nightly-drift/sanitized-forgejo-drift.json'
$sanitizedReportPath = Join-Path $repositoryRoot $sanitizedReportRelativePath
$manifestPath = 'tests/contracts/forgejo/supported-versions.json'
$classificationFixturePath = 'tests/tools/forgejo-drift/classification-fixtures.json'
$sanitizedReportScriptPath = 'tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1'
$testProjectPath = 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
$trxName = 'nightly-drift-forgejo.trx'
$trxPath = Join-Path $reportDirectory $trxName
$pushed = $false
$results = @()
$usedXunitFallback = $false
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

$categories = @(
    'forgejo-manifest-integrity',
    'forgejo-snapshot-coverage',
    'forgejo-drift-classification',
    'forgejo-sanitized-report',
    'live-provider-drift'
)

$requiredSnapshotPaths = @(
    '/version',
    '/user',
    '/orgs/{org}/repos',
    '/repos/{owner}/{repo}',
    '/repos/{owner}/{repo}/branches',
    '/repos/{owner}/{repo}/branches/{branch}',
    '/repos/{owner}/{repo}/contents/{filepath}',
    '/repos/{owner}/{repo}/git/commits/{sha}',
    '/repos/{owner}/{repo}/statuses/{sha}'
)

$requiredInputs = @(
    $manifestPath,
    $classificationFixturePath,
    $sanitizedReportScriptPath
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

function Write-NightlyDriftReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results,
        [AllowNull()]$Manifest,
        [AllowNull()]$SanitizedReport
    )

    $versions = @()
    if ($null -ne $Manifest) {
        $versions = @($Manifest.entries | ForEach-Object {
            [ordered]@{
                provider_version = $_.version
                version_family = $_.versionFamily
                support_class = $_.supportClass
                snapshot_path = $_.snapshotPath
                compatibility_posture = $_.expectedApiCompatibilityPosture
            }
        })
    }

    $payload = [ordered]@{
        gate = 'nightly-drift'
        status = $Status
        report_path = '_bmad-output/gates/nightly-drift/latest.json'
        diagnostic_policy = 'metadata-only'
        trigger_policy = 'schedule_utc_or_manual_dispatch_default_branch'
        provider = 'forgejo'
        provider_profile = $ProviderProfile
        categories = $categories
        manifest_path = $manifestPath
        classification_fixture_path = $classificationFixturePath
        sanitized_report_path = $sanitizedReportRelativePath
        test_project = $testProjectPath
        expected_test_count = 7
        live_provider_drift = [ordered]@{
            status = 'reference_pending_story_7_8'
            owner = 'folders-provider-maintainers'
            command_shape = 'pwsh ./tests/tools/run-nightly-drift-gates.ps1 -ProviderProfile pinned-snapshots'
            evidence_path = '_bmad-output/gates/nightly-drift/latest.json'
            follow_up_boundary = 'replace reference_pending only when a synthetic credential-free live provider lane can classify live schema drift without retaining raw upstream responses'
        }
        provider_versions = $versions
        sanitized_report_schema = if ($null -ne $SanitizedReport) { $SanitizedReport.schemaVersion } else { $null }
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
        results = $Results
    }

    $payload | ConvertTo-Json -Depth 10 | Set-Content -Path $latestReportPath -Encoding utf8NoBOM
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    Add-Result -Category $Category -Status 'failed' -Severity 'failure' -ExitCode 1
    Write-NightlyDriftReport -Status 'failed' -Results $script:results -Manifest $null -SanitizedReport $null
    Write-Error "NIGHTLY-DRIFT-FAILED: category=$Category reason=$Reason"
    exit 1
}

function Assert-RequiredInput {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not (Test-Path (Join-Path $repositoryRoot $RelativePath))) {
        Fail-Gate -Category 'forgejo-manifest-integrity' -Reason "missing-input path=$RelativePath"
    }
}

function Get-ManifestIntegrityHash {
    param(
        [Parameter(Mandatory = $true)]$Entry
    )

    $payload = @(
        $Entry.version,
        $Entry.versionFamily,
        $Entry.supportClass,
        $Entry.sourceUrl,
        $Entry.snapshotPath,
        $Entry.expectedApiCompatibilityPosture,
        $Entry.owner,
        $Entry.reviewer,
        $Entry.datedSource
    ) -join '|'

    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'sha256:' + [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Assert-ManifestIntegrity {
    param(
        [Parameter(Mandatory = $true)]$Manifest
    )

    if ($Manifest.schemaVersion -ne 'forgejo-supported-versions-v1') {
        Fail-Gate -Category 'forgejo-manifest-integrity' -Reason 'manifest-schema-version-drift'
    }

    $versions = @($Manifest.entries | ForEach-Object { $_.version })
    if (@($versions | Select-Object -Unique).Count -ne $versions.Count) {
        Fail-Gate -Category 'forgejo-manifest-integrity' -Reason 'duplicate-version'
    }

    foreach ($entry in $Manifest.entries) {
        foreach ($field in @('version', 'versionFamily', 'supportClass', 'sourceUrl', 'snapshotPath', 'expectedApiCompatibilityPosture', 'owner', 'reviewer', 'datedSource', 'integrityHash')) {
            if ([string]::IsNullOrWhiteSpace([string]$entry.$field)) {
                Fail-Gate -Category 'forgejo-manifest-integrity' -Reason "missing-manifest-field field=$field"
            }
        }

        if ($entry.integrityHash -ne (Get-ManifestIntegrityHash -Entry $entry)) {
            Fail-Gate -Category 'forgejo-manifest-integrity' -Reason "stale-integrity-hash version=$($entry.version)"
        }

        if (@('supported', 'additive-compatible') -notcontains $entry.expectedApiCompatibilityPosture) {
            Fail-Gate -Category 'forgejo-drift-classification' -Reason "blocking-drift-classification version=$($entry.version) classification=$($entry.expectedApiCompatibilityPosture)"
        }
    }
}

function Assert-SnapshotCoverage {
    param(
        [Parameter(Mandatory = $true)]$Manifest
    )

    foreach ($entry in $Manifest.entries) {
        $snapshotPath = Join-Path $repositoryRoot $entry.snapshotPath
        if (-not (Test-Path $snapshotPath)) {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "missing-snapshot version=$($entry.version)"
        }

        $snapshot = Get-Content -Raw -Path $snapshotPath | ConvertFrom-Json
        if ($snapshot.swagger -ne '2.0') {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "snapshot-schema-drift version=$($entry.version)"
        }

        foreach ($path in $requiredSnapshotPaths) {
            if (-not ($snapshot.paths.PSObject.Properties.Name -contains $path)) {
                Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "missing-provider-operation-path version=$($entry.version) path=$path"
            }
        }
    }
}

function Assert-DriftClassificationFixtures {
    param(
        [Parameter(Mandatory = $true)]$Fixtures
    )

    if ($Fixtures.schemaVersion -ne 'forgejo-drift-classification-fixtures-v1') {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason 'classification-fixture-schema-drift'
    }

    if ($Fixtures.redactionPolicy -ne 'metadata-only') {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason 'classification-fixture-redaction-policy-drift'
    }

    $fixtureNames = @($Fixtures.fixtures | ForEach-Object { $_.changeKind })
    foreach ($changeKind in @('additive-field', 'removed-field', 'type-change', 'enum-new-string-value', 'unknown-operation')) {
        if ($fixtureNames -notcontains $changeKind) {
            Fail-Gate -Category 'forgejo-drift-classification' -Reason "missing-change-kind-fixture kind=$changeKind"
        }
    }

    foreach ($fixture in $Fixtures.fixtures) {
        if ($fixture.expectedClassification -eq 'unknown-unclassified' -and $fixture.severity -ne 'failure') {
            Fail-Gate -Category 'forgejo-drift-classification' -Reason 'unknown-unclassified-not-failure'
        }

        if ($fixture.expectedClassification -eq 'breaking-incompatible' -and $fixture.severity -ne 'failure') {
            Fail-Gate -Category 'forgejo-drift-classification' -Reason 'breaking-incompatible-not-failure'
        }
    }
}

function Assert-NoForbiddenDiagnostics {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Category
    )

    foreach ($forbidden in @('access_token=', 'token=', 'ghp_', '-----BEGIN', 'user:', '@forgejo', 'customer', 'private-instance', 'owner-secret', 'repo-secret', 'diff --git', 'provider payload', 'raw schema diff')) {
        if ($Text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Fail-Gate -Category $Category -Reason 'unsafe-diagnostic-material'
        }
    }
}

function Assert-SanitizedReport {
    param(
        [Parameter(Mandatory = $true)]$Report
    )

    if ($Report.schemaVersion -ne 'forgejo-drift-report-v1') {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'sanitized-report-schema-drift'
    }

    if ($Report.artifactRetention.status -ne 'sanitized-metadata-only') {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'sanitized-report-retention-policy-drift'
    }

    if ($Report.artifactRetention.rawSchemaDiffsRetained -ne $false) {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'raw-schema-diff-retention'
    }

    if ($Report.redactionScan.status -ne 'passed') {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'redaction-scan-failed'
    }

    Assert-NoForbiddenDiagnostics -Text ($Report | ConvertTo-Json -Depth 12) -Category 'forgejo-sanitized-report'
}

function Assert-TestAssembly {
    $assembly = Get-ChildItem -Path (Join-Path $repositoryRoot 'tests') -Recurse -Filter 'Hexalith.Folders.Tests.dll' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]net\d+\.\d+(?:-[\w]+)?[\\/]' } |
        Select-Object -First 1

    if ($null -eq $assembly) {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason 'missing-test-assembly'
    }
}

function Get-ExecutedTestCount {
    param(
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Output
    )

    $joined = ($Output -join [Environment]::NewLine)
    $total = 0
    foreach ($match in [regex]::Matches($joined, 'Total:\s+(\d+)')) {
        $total += [int]$match.Groups[1].Value
    }

    return $total
}

function Invoke-XunitInProcessFallback {
    $script:usedXunitFallback = $true
    Write-Host 'NIGHTLY-DRIFT category=forgejo-drift-classification vstest-socket-denied=true fallback=xunit-in-process'
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests'
    if (-not (Test-Path $runnerPath)) {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason 'xunit-in-process-runner-missing'
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class 'Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests' 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    if ((Get-ExecutedTestCount -Output $runnerOutput) -ne 7) {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason 'zero-or-partial-test-selection expected=7'
    }

    if ($runnerExitCode -ne 0) {
        Add-Result -Category 'forgejo-drift-classification' -Status 'failed' -Severity 'failure' -ExitCode $runnerExitCode
        Write-NightlyDriftReport -Status 'failed' -Results $script:results -Manifest $null -SanitizedReport $null
        exit $runnerExitCode
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        if ($Category -eq 'forgejo-drift-classification' -and (($output -join [Environment]::NewLine) -match 'System\.Net\.Sockets\.SocketException.*Permission denied')) {
            Invoke-XunitInProcessFallback
            return
        }

        Add-Result -Category $Category -Status 'failed' -Severity 'failure' -ExitCode $exitCode
        Write-NightlyDriftReport -Status 'failed' -Results $script:results -Manifest $null -SanitizedReport $null
        Write-Error "NIGHTLY-DRIFT-FAILED: category=$Category exit_code=$exitCode output=$($output -join ' ')"
        exit $exitCode
    }
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Write-NightlyDriftReport -Status 'discovered' -Results $results -Manifest $null -SanitizedReport $null

    foreach ($input in $requiredInputs) {
        Assert-RequiredInput -RelativePath $input
    }

    if (-not $SkipRestoreBuild) {
        Invoke-DotNet -Category 'forgejo-manifest-integrity' -Arguments @('restore', 'Hexalith.Folders.slnx', '-m:1', '-p:NuGetAudit=false')
        Invoke-DotNet -Category 'forgejo-manifest-integrity' -Arguments @('build', 'Hexalith.Folders.slnx', '--no-restore', '-m:1')
    }

    Assert-TestAssembly

    $manifest = Get-Content -Raw -Path (Join-Path $repositoryRoot $manifestPath) | ConvertFrom-Json
    $fixtures = Get-Content -Raw -Path (Join-Path $repositoryRoot $classificationFixturePath) | ConvertFrom-Json

    Assert-ManifestIntegrity -Manifest $manifest
    Add-Result -Category 'forgejo-manifest-integrity' -Status 'passed' -Severity 'none' -ExitCode 0

    Assert-SnapshotCoverage -Manifest $manifest
    Add-Result -Category 'forgejo-snapshot-coverage' -Status 'passed' -Severity 'none' -ExitCode 0

    Assert-DriftClassificationFixtures -Fixtures $fixtures

    if (Test-Path $trxPath) {
        Remove-Item $trxPath -Force
    }

    Invoke-DotNet -Category 'forgejo-drift-classification' -Arguments @(
        'test', $testProjectPath,
        '--no-build',
        '--filter', 'FullyQualifiedName~Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests',
        '--results-directory', $reportDirectory,
        '--logger', "trx;LogFileName=$trxName"
    )

    [int]$executedTests = 0
    if (Test-Path $trxPath) {
        [xml]$trx = Get-Content -Raw -Path $trxPath
        $executedTests = [int]$trx.TestRun.ResultSummary.Counters.total
    }

    if (-not $usedXunitFallback -and (Test-Path $trxPath) -and $executedTests -ne 7) {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason "zero-or-partial-test-selection expected=7 actual=$executedTests"
    }

    Add-Result -Category 'forgejo-drift-classification' -Status 'passed' -Severity 'none' -ExitCode 0

    & (Join-Path $repositoryRoot $sanitizedReportScriptPath) -RepositoryRoot $repositoryRoot -OutputPath $sanitizedReportPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sanitizedReportPath)) {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'missing-sanitized-report'
    }

    $sanitizedReport = Get-Content -Raw -Path $sanitizedReportPath | ConvertFrom-Json
    Assert-SanitizedReport -Report $sanitizedReport
    Add-Result -Category 'forgejo-sanitized-report' -Status 'passed' -Severity 'none' -ExitCode 0

    Add-Result -Category 'live-provider-drift' -Status 'reference_pending_story_7_8' -Severity 'warning' -ExitCode 0
    Write-NightlyDriftReport -Status 'passed' -Results $results -Manifest $manifest -SanitizedReport $sanitizedReport
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
