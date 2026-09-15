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
$githubProfilePath = 'tests/contracts/github/pinned-profile.json'
$githubPackagePinPath = 'references/Hexalith.Builds/Props/Directory.Packages.props'
$githubTestClass = 'Hexalith.Folders.Tests.Providers.GitHub.GitHubDriftConformanceTests'
$githubTrxName = 'nightly-drift-github.trx'
$classificationFixturePath = 'tests/tools/forgejo-drift/classification-fixtures.json'
$sanitizedReportScriptPath = 'tests/tools/forgejo-drift/Write-SanitizedForgejoDriftReport.ps1'
$testProjectPath = 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
$trxName = 'nightly-drift-forgejo.trx'
$trxPath = Join-Path $reportDirectory $trxName
$githubTrxPath = Join-Path $reportDirectory $githubTrxName
$pushed = $false
$results = @()
$usedXunitFallback = $false
$elapsed = [System.Diagnostics.Stopwatch]::StartNew()

$categories = @(
    'forgejo-manifest-integrity',
    'forgejo-snapshot-coverage',
    'forgejo-drift-classification',
    'forgejo-sanitized-report',
    'github-pinned-profile-integrity',
    'github-failure-mode-coverage',
    'credentialed-live-provider-evidence'
)

$requiredSnapshotPaths = @(
    '/version',
    '/orgs/{org}/repos',
    '/repos/{owner}/{repo}',
    '/repos/{owner}/{repo}/branches/{branch}',
    '/repos/{owner}/{repo}/branch_protections/{name}'
    '/repos/{owner}/{repo}/contents'
    '/repos/{owner}/{repo}/contents/{filepath}'
    '/repos/{owner}/{repo}/git/refs/{ref}'
    '/repos/{owner}/{repo}/commits'
)

$requiredInputs = @(
    $manifestPath,
    $classificationFixturePath,
    $sanitizedReportScriptPath,
    $githubProfilePath
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

function Get-ProviderCategories {
    param(
        [Parameter(Mandatory = $true)][string]$Provider
    )

    # Derived from the declared category inventory by provider prefix, so a new provider category is covered
    # the moment it is declared instead of silently leaving the provider row reporting passed.
    return @($script:categories | Where-Object { $_ -like "$Provider-*" })
}

function Get-ProviderHermeticStatus {
    param(
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][array]$Results,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Categories
    )

    if ($Categories.Count -eq 0) {
        return 'not_started'
    }

    $observed = @($Results | Where-Object { $Categories -contains $_.category })
    if ($observed.Count -eq 0) {
        return 'not_started'
    }

    if (@($observed | Where-Object { $_.status -ne 'passed' }).Count -gt 0) {
        return 'failed'
    }

    if ($observed.Count -lt $Categories.Count) {
        return 'in_progress'
    }

    return 'passed'
}

function Assert-ProviderHermeticStatusDerivation {
    # The derivation is the mechanism behind the "no hardcoded provider status" guarantee, so it is exercised
    # against synthetic result sets on every run. Replacing the body with a constant fails here immediately.
    $synthetic = @('synthetic-a', 'synthetic-b')
    $passedResults = @(
        [ordered]@{ category = 'synthetic-a'; status = 'passed' },
        [ordered]@{ category = 'synthetic-b'; status = 'passed' }
    )
    $failedResults = @(
        [ordered]@{ category = 'synthetic-a'; status = 'passed' },
        [ordered]@{ category = 'synthetic-b'; status = 'failed' }
    )
    $partialResults = @([ordered]@{ category = 'synthetic-a'; status = 'passed' })
    $foreignResults = @([ordered]@{ category = 'unrelated-category'; status = 'passed' })

    $cases = @(
        @{ expected = 'passed'; results = $passedResults; categories = $synthetic },
        @{ expected = 'failed'; results = $failedResults; categories = $synthetic },
        @{ expected = 'in_progress'; results = $partialResults; categories = $synthetic },
        @{ expected = 'not_started'; results = $foreignResults; categories = $synthetic },
        @{ expected = 'not_started'; results = @(); categories = $synthetic },
        @{ expected = 'not_started'; results = $passedResults; categories = @() }
    )

    foreach ($case in $cases) {
        $actual = Get-ProviderHermeticStatus -Results $case.results -Categories $case.categories
        if ($actual -ne $case.expected) {
            Fail-Gate -Category 'github-pinned-profile-integrity' -Reason "hermetic-status-derivation-drift expected=$($case.expected) actual=$actual"
        }
    }

    foreach ($provider in @('forgejo', 'github')) {
        if (@(Get-ProviderCategories -Provider $provider).Count -eq 0) {
            Fail-Gate -Category 'github-pinned-profile-integrity' -Reason "provider-category-inventory-empty provider=$provider"
        }
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

    # Per-provider hermetic status is derived from the category results actually recorded in this run,
    # so no provider row can report a hardcoded placeholder.
    $ProviderHermeticStatus = [ordered]@{
        forgejo = Get-ProviderHermeticStatus -Results $Results -Categories (Get-ProviderCategories -Provider 'forgejo')
        github = Get-ProviderHermeticStatus -Results $Results -Categories (Get-ProviderCategories -Provider 'github')
    }

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
        providers = @('forgejo', 'github')
        provider_profile = $ProviderProfile
        categories = $categories
        manifest_path = $manifestPath
        github_profile_path = $githubProfilePath
        classification_fixture_path = $classificationFixturePath
        sanitized_report_path = $sanitizedReportRelativePath
        test_project = $testProjectPath
        expected_test_count = 10
        github_expected_test_count = 5
        provider_status = @(
            [ordered]@{
                provider = 'forgejo'
                hermetic_status = $ProviderHermeticStatus.forgejo
                evidence_kind = 'pinned-swagger-snapshot-manifest-and-classification-fixtures'
                credentialed_live_evidence = 'not_run'
            },
            [ordered]@{
                provider = 'github'
                hermetic_status = $ProviderHermeticStatus.github
                evidence_kind = 'pinned-profile-manifest-and-failure-mode-coverage-matrix'
                credentialed_live_evidence = 'not_run'
            }
        )
        credentialed_live_provider_evidence = [ordered]@{
            status = 'not_run'
            reason = 'no credentialed provider lane runs in scheduled CI; C12 is closed on hermetic plus scheduled containerized and fixture evidence only'
            owner = 'folders-provider-maintainers'
            forgejo_command_shape = 'pwsh ./tests/tools/run-forgejo-provider-evidence-gates.ps1'
            github_command_shape = 'none; the live GitHub mutation archive is waived, not executed'
            evidence_path = '_bmad-output/gates/nightly-drift/latest.json'
            residual_debt = 'credentialed live provider runs against both providers remain residual provider-ready debt recorded in the OQ4 catalog'
            closing_condition = 'retire not_run only when an operator-run credentialed lane for both providers archives metadata-only positive, denial and tenant-isolation evidence against approved isolated installations, and the OQ4 catalog is re-cut with a new version, digest and three fresh authority approvals'
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
        $Entry.datedSource,
        $Entry.sourceArtifactSha256,
        $Entry.snapshotSha256,
        $Entry.expectedOperationCount
    ) -join '|'

    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'sha256:' + [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Assert-ManifestIntegrity {
    param(
        [Parameter(Mandatory = $true)]$Manifest
    )

    if ($Manifest.schemaVersion -ne 'forgejo-supported-versions-v2') {
        Fail-Gate -Category 'forgejo-manifest-integrity' -Reason 'manifest-schema-version-drift'
    }

    $versions = @($Manifest.entries | ForEach-Object { $_.version })
    if (@($versions | Select-Object -Unique).Count -ne $versions.Count) {
        Fail-Gate -Category 'forgejo-manifest-integrity' -Reason 'duplicate-version'
    }

    foreach ($entry in $Manifest.entries) {
        foreach ($field in @('version', 'versionFamily', 'supportClass', 'sourceUrl', 'snapshotPath', 'expectedApiCompatibilityPosture', 'owner', 'reviewer', 'datedSource', 'sourceArtifactSha256', 'snapshotSha256', 'expectedOperationCount', 'integrityHash')) {
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

        $snapshotHash = 'sha256:' + (Get-FileHash -Algorithm SHA256 -Path $snapshotPath).Hash.ToLowerInvariant()
        if ($entry.snapshotSha256 -ne $snapshotHash) {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "snapshot-hash-drift version=$($entry.version)"
        }

        $snapshot = Get-Content -Raw -Path $snapshotPath | ConvertFrom-Json
        if ($snapshot.swagger -ne '2.0') {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "snapshot-schema-drift version=$($entry.version)"
        }

        if ($snapshot.'x-hexalith-review'.source -ne $entry.sourceUrl -or
            ('sha256:' + $snapshot.'x-hexalith-review'.sourceArtifactSha256) -ne $entry.sourceArtifactSha256) {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "snapshot-source-evidence-drift version=$($entry.version)"
        }

        foreach ($path in $requiredSnapshotPaths) {
            if (-not ($snapshot.paths.PSObject.Properties.Name -contains $path)) {
                Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "missing-provider-operation-path version=$($entry.version) path=$path"
            }
        }

        $operationCount = @($snapshot.paths.PSObject.Properties | ForEach-Object { $_.Value.PSObject.Properties }).Count
        if ($operationCount -ne [int]$entry.expectedOperationCount) {
            Fail-Gate -Category 'forgejo-snapshot-coverage' -Reason "used-operation-count-drift version=$($entry.version)"
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

function Assert-GitHubPinnedProfile {
    param(
        [Parameter(Mandatory = $true)]$PinnedProfile
    )

    if ($PinnedProfile.schemaVersion -ne 'github-pinned-profile-v1') {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'github-profile-schema-drift'
    }

    foreach ($field in @('provider', 'catalogPath', 'catalogVersion', 'packagePinPath', 'octokitPackageVersion', 'libGit2SharpPackageVersion', 'libGit2SharpPinPath', 'restApiVersion', 'productHeader', 'driftLane', 'provingAssembly')) {
        if ([string]::IsNullOrWhiteSpace([string]$PinnedProfile.$field)) {
            Fail-Gate -Category 'github-pinned-profile-integrity' -Reason "missing-github-profile-field field=$field"
        }
    }

    if ($PinnedProfile.networkCallsPermitted -ne $false) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'github-lane-network-call-permitted'
    }

    $profileText = Get-Content -Raw -Path (Join-Path $repositoryRoot $githubProfilePath)
    Assert-NoForbiddenDiagnostics -Text $profileText -Category 'github-pinned-profile-integrity'

    $catalogPath = Join-Path $repositoryRoot $PinnedProfile.catalogPath
    if (-not (Test-Path $catalogPath)) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'missing-catalog'
    }

    $catalog = Get-Content -Raw -Path $catalogPath
    $backtick = [char]0x60
    if (-not $catalog.Contains("- Catalog version: $backtick$($PinnedProfile.catalogVersion)$backtick")) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'catalog-version-drift'
    }

    if (-not $catalog.Contains("Octokit $backtick$($PinnedProfile.octokitPackageVersion)$backtick")) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'catalog-package-version-drift'
    }

    if (-not $catalog.Contains("X-GitHub-Api-Version: $($PinnedProfile.restApiVersion)")) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'catalog-api-version-drift'
    }

    if (-not $catalog.Contains("$backtick$($PinnedProfile.productHeader)$backtick")) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'catalog-product-header-drift'
    }

    $quote = [char]0x22
    $packagePin = Get-Content -Raw -Path (Join-Path $repositoryRoot $githubPackagePinPath)
    $expectedPin = "PackageVersion Include=$quote" + 'Octokit' + "$quote Version=$quote$($PinnedProfile.octokitPackageVersion)$quote"
    if (-not $packagePin.Contains($expectedPin)) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'package-pin-drift'
    }

    if (-not $catalog.Contains("LibGit2Sharp $backtick$($PinnedProfile.libGit2SharpPackageVersion)$backtick")) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'catalog-native-package-version-drift'
    }

    $nativePin = Get-Content -Raw -Path (Join-Path $repositoryRoot $PinnedProfile.libGit2SharpPinPath)
    $expectedNativePin = "PackageVersion Include=$quote" + 'LibGit2Sharp' + "$quote Version=$quote$($PinnedProfile.libGit2SharpPackageVersion)$quote"
    if (-not $nativePin.Contains($expectedNativePin)) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'native-package-pin-drift'
    }

    $categories = @($PinnedProfile.failureModeCoverage | ForEach-Object { $_.providerNeutralCategory })
    if ($categories.Count -eq 0) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'empty-failure-mode-coverage'
    }

    if (@($categories | Select-Object -Unique).Count -ne $categories.Count) {
        Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'duplicate-failure-mode-category'
    }

    foreach ($row in $PinnedProfile.failureModeCoverage) {
        foreach ($field in @('providerNeutralCategory', 'provingFixture', 'provingCondition')) {
            if ([string]::IsNullOrWhiteSpace([string]$row.$field)) {
                Fail-Gate -Category 'github-pinned-profile-integrity' -Reason "missing-failure-mode-field field=$field"
            }
        }

        if (-not $catalog.Contains("- $backtick$($row.providerNeutralCategory)$backtick")) {
            Fail-Gate -Category 'github-pinned-profile-integrity' -Reason 'orphaned-failure-mode-category'
        }
    }
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
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$ClassName,
        [Parameter(Mandatory = $true)][int]$ExpectedCount
    )

    $script:usedXunitFallback = $true
    Write-Host "NIGHTLY-DRIFT category=$Category vstest-socket-denied=true fallback=xunit-in-process"
    $runnerPath = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests'
    if (-not (Test-Path $runnerPath)) {
        Fail-Gate -Category $Category -Reason 'xunit-in-process-runner-missing'
    }

    $runnerOutput = & $runnerPath -noLogo -noColor -class $ClassName 2>&1
    $runnerExitCode = $LASTEXITCODE
    $runnerOutput | ForEach-Object { Write-Host $_ }

    if ((Get-ExecutedTestCount -Output $runnerOutput) -ne $ExpectedCount) {
        Fail-Gate -Category $Category -Reason "zero-or-partial-test-selection expected=$ExpectedCount"
    }

    if ($runnerExitCode -ne 0) {
        Add-Result -Category $Category -Status 'failed' -Severity 'failure' -ExitCode $runnerExitCode
        Write-NightlyDriftReport -Status 'failed' -Results $script:results -Manifest $null -SanitizedReport $null
        exit $runnerExitCode
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$FallbackClassName,
        [int]$FallbackExpectedCount = 0
    )

    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        if ($FallbackClassName -and (($output -join [Environment]::NewLine) -match 'System\.Net\.Sockets\.SocketException.*Permission denied|Testing with VSTest target is no longer supported')) {
            Invoke-XunitInProcessFallback -Category $Category -ClassName $FallbackClassName -ExpectedCount $FallbackExpectedCount
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

    Assert-ProviderHermeticStatusDerivation

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
    ) -FallbackClassName 'Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests' -FallbackExpectedCount 10

    [int]$executedTests = 0
    if (Test-Path $trxPath) {
        [xml]$trx = Get-Content -Raw -Path $trxPath
        $executedTests = [int]$trx.TestRun.ResultSummary.Counters.total
    }

    if (-not $usedXunitFallback -and (Test-Path $trxPath) -and $executedTests -ne 10) {
        Fail-Gate -Category 'forgejo-drift-classification' -Reason "zero-or-partial-test-selection expected=10 actual=$executedTests"
    }

    Add-Result -Category 'forgejo-drift-classification' -Status 'passed' -Severity 'none' -ExitCode 0

    & (Join-Path $repositoryRoot $sanitizedReportScriptPath) -RepositoryRoot $repositoryRoot -OutputPath $sanitizedReportPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sanitizedReportPath)) {
        Fail-Gate -Category 'forgejo-sanitized-report' -Reason 'missing-sanitized-report'
    }

    $sanitizedReport = Get-Content -Raw -Path $sanitizedReportPath | ConvertFrom-Json
    Assert-SanitizedReport -Report $sanitizedReport
    Add-Result -Category 'forgejo-sanitized-report' -Status 'passed' -Severity 'none' -ExitCode 0

    $githubProfile = Get-Content -Raw -Path (Join-Path $repositoryRoot $githubProfilePath) | ConvertFrom-Json
    Assert-GitHubPinnedProfile -PinnedProfile $githubProfile
    Add-Result -Category 'github-pinned-profile-integrity' -Status 'passed' -Severity 'none' -ExitCode 0

    if (Test-Path $githubTrxPath) {
        Remove-Item $githubTrxPath -Force
    }

    $script:usedXunitFallback = $false
    Invoke-DotNet -Category 'github-failure-mode-coverage' -Arguments @(
        'test', $testProjectPath,
        '--no-build',
        '--filter', "FullyQualifiedName~$githubTestClass",
        '--results-directory', $reportDirectory,
        '--logger', "trx;LogFileName=$githubTrxName"
    ) -FallbackClassName $githubTestClass -FallbackExpectedCount 5

    [int]$githubExecutedTests = 0
    if (Test-Path $githubTrxPath) {
        [xml]$githubTrx = Get-Content -Raw -Path $githubTrxPath
        $githubExecutedTests = [int]$githubTrx.TestRun.ResultSummary.Counters.total
    }

    if (-not $usedXunitFallback -and (Test-Path $githubTrxPath) -and $githubExecutedTests -ne 5) {
        Fail-Gate -Category 'github-failure-mode-coverage' -Reason "zero-or-partial-test-selection expected=5 actual=$githubExecutedTests"
    }

    Add-Result -Category 'github-failure-mode-coverage' -Status 'passed' -Severity 'none' -ExitCode 0

    # Credentialed live provider evidence is reported as explicitly not run, never as a hardcoded
    # placeholder status standing in for real hermetic drift coverage.
    Add-Result -Category 'credentialed-live-provider-evidence' -Status 'not_run' -Severity 'informational' -ExitCode 0
    Write-NightlyDriftReport -Status 'passed' -Results $results -Manifest $manifest -SanitizedReport $sanitizedReport
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
