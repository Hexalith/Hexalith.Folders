#Requires -Version 7

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ReleaseTag = '',
    [string]$SourceRevisionId = '',
    [ValidateSet('DryRun', 'Publish')][string]$Mode = 'DryRun',
    [string]$FeedSource = '',
    [string]$ApiKeyEnvironmentVariable = 'NUGET_API_KEY',
    [switch]$SkipRestoreBuild,
    [switch]$SkipPack
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptRoot '../..')).ProviderPath
$manifestRelativePath = 'tools/release-packages.json'
$manifestPath = Join-Path $repositoryRoot $manifestRelativePath
$policyRelativePath = 'deploy/nuget/release-packages.yaml'
$policyPath = Join-Path $repositoryRoot $policyRelativePath
$packagesRelativePath = 'nupkgs'
$packagesDirectory = Join-Path $repositoryRoot $packagesRelativePath
$reportRelativePath = '_bmad-output/gates/release-packages/latest.json'
$reportPath = Join-Path $repositoryRoot $reportRelativePath
$categories = @(
    'version-policy',
    'source-revision-policy',
    'manifest-package-set',
    'restore-build',
    'package-build',
    'package-metadata',
    'symbol-packages',
    'archive-safety',
    'dependency-closure',
    'consumer-validation',
    'metadata-only-report',
    'publish'
)
$results = @()
$packageReports = @()
$manifestPackages = @()
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

function Write-Report {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    $reportDirectory = Split-Path -Parent $reportPath
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    [ordered]@{
        gate = 'release-packages'
        status = $Status
        mode = $Mode
        exit_code = $ExitCode
        report_path = $reportRelativePath
        diagnostic_policy = 'metadata-only'
        categories = $categories
        package_version = $Version
        release_tag = $ReleaseTag
        source_revision_id = $SourceRevisionId
        package_manifest_path = $manifestRelativePath
        package_policy_path = $policyRelativePath
        package_output_path = $packagesRelativePath
        pushed_package_ids = @($manifestPackages | ForEach-Object { $_.id })
        package_reports = $packageReports
        results = $results
        elapsed_ms = [int64]$elapsed.ElapsedMilliseconds
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Fail-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Reason,
        [int]$ExitCode = 1
    )

    Add-Result -Category $Category -Status 'failed' -ExitCode $ExitCode
    Write-Report -Status 'failed' -ExitCode $ExitCode
    Write-Error "RELEASE-PACKAGES-FAILED: category=$Category reason=$Reason"
    exit $ExitCode
}

function Invoke-CommandChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "RELEASE-PACKAGES category=$Category status=running"
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        Fail-Gate -Category $Category -Reason "command-failed exit_code=$LASTEXITCODE" -ExitCode $LASTEXITCODE
    }
}

function Assert-VersionAndSource {
    $semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
    if ($Version -notmatch $semVerPattern) {
        Fail-Gate -Category 'version-policy' -Reason 'invalid-semver'
    }
    if ($ReleaseTag.Length -gt 0 -and $ReleaseTag -cne "v$Version") {
        Fail-Gate -Category 'version-policy' -Reason 'release-tag-version-mismatch'
    }
    Add-Result -Category 'version-policy' -Status 'passed' -ExitCode 0

    if ([string]::IsNullOrWhiteSpace($SourceRevisionId)) {
        $script:SourceRevisionId = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    }
    if ($SourceRevisionId -cnotmatch '^[0-9a-f]{40}$') {
        Fail-Gate -Category 'source-revision-policy' -Reason 'source-revision-id-must-be-lowercase-full-sha'
    }
    Add-Result -Category 'source-revision-policy' -Status 'passed' -ExitCode 0
}

function Read-AndValidateManifest {
    if (-not (Test-Path $manifestPath) -or -not (Test-Path $policyPath)) {
        Fail-Gate -Category 'manifest-package-set' -Reason 'missing-release-package-contract'
    }
    try {
        $manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
    }
    catch {
        Fail-Gate -Category 'manifest-package-set' -Reason 'malformed-json-manifest'
    }
    $packages = @($manifest.packages)
    if ($packages.Count -ne 5) {
        Fail-Gate -Category 'manifest-package-set' -Reason 'expected-exactly-five-packages'
    }
    $ids = @($packages | ForEach-Object { [string]$_.id })
    $projects = @($packages | ForEach-Object { [string]$_.project })
    if (@($ids | Sort-Object -Unique).Count -ne 5 -or @($projects | Sort-Object -Unique).Count -ne 5) {
        Fail-Gate -Category 'manifest-package-set' -Reason 'duplicate-package-id-or-project'
    }
    foreach ($package in $packages) {
        if ($package.id -notmatch '^Hexalith\.Folders(?:\.[A-Za-z0-9._-]+)?$') {
            Fail-Gate -Category 'manifest-package-set' -Reason 'package-id-outside-folders-scope'
        }
        $projectPath = Join-Path $repositoryRoot $package.project
        if (-not (Test-Path $projectPath) -or $package.project -notmatch '^src/.+\.csproj$') {
            Fail-Gate -Category 'manifest-package-set' -Reason "invalid-package-project package_id=$($package.id)"
        }
    }
    $policy = Get-Content -Raw -Path $policyPath
    foreach ($required in @(
        'inventoryPath: tools/release-packages.json',
        'expectedPackageCount: 5',
        'feed: https://api.nuget.org/v3/index.json',
        'duplicatePolicy: fail'
    )) {
        if (-not $policy.Contains($required, [StringComparison]::Ordinal)) {
            Fail-Gate -Category 'manifest-package-set' -Reason 'release-policy-drift'
        }
    }
    $script:manifestPackages = $packages
    Add-Result -Category 'manifest-package-set' -Status 'passed' -ExitCode 0
}

function Invoke-RestoreBuild {
    if ($SkipRestoreBuild) {
        Add-Result -Category 'restore-build' -Status 'skipped-same-run-prerequisite' -ExitCode 0
        return
    }
    Invoke-CommandChecked -Category 'restore-build' -Executable 'dotnet' -Arguments @(
        'restore',
        'Hexalith.Folders.CI.slnx',
        '-p:Configuration=Release',
        '-p:UseNuGetDeps=true',
        '-m:1'
    )
    Invoke-CommandChecked -Category 'restore-build' -Executable 'dotnet' -Arguments @(
        'build',
        'Hexalith.Folders.CI.slnx',
        '--configuration',
        'Release',
        '-p:UseNuGetDeps=true',
        '--no-restore',
        '-warnaserror',
        '-m:1'
    )
    Add-Result -Category 'restore-build' -Status 'passed' -ExitCode 0
}

function Invoke-PackAndValidate {
    if (-not $SkipPack) {
        Invoke-CommandChecked -Category 'package-build' -Executable 'python3' -Arguments @(
            'scripts/pack-release-packages.py',
            $packagesRelativePath,
            $Version,
            '--source-revision',
            $SourceRevisionId
        )
        Add-Result -Category 'package-build' -Status 'passed' -ExitCode 0
    }
    else {
        Add-Result -Category 'package-build' -Status 'skipped-prepared-artifacts' -ExitCode 0
    }

    Invoke-CommandChecked -Category 'package-metadata' -Executable 'python3' -Arguments @(
        'scripts/validate-nuget-packages.py',
        $packagesRelativePath,
        '--version',
        $Version,
        '--source-revision',
        $SourceRevisionId
    )
    foreach ($category in @('package-metadata', 'symbol-packages', 'archive-safety', 'dependency-closure')) {
        Add-Result -Category $category -Status 'passed' -ExitCode 0
    }

    Invoke-CommandChecked -Category 'consumer-validation' -Executable 'python3' -Arguments @(
        'scripts/validate-consumer-package-references.py',
        $packagesRelativePath
    )
    Add-Result -Category 'consumer-validation' -Status 'passed' -ExitCode 0

    $script:packageReports = @($manifestPackages | ForEach-Object {
        [ordered]@{
            package_id = $_.id
            project_path = $_.project
            package_path = "$packagesRelativePath/$($_.id).$Version.nupkg"
            symbol_package_path = "$packagesRelativePath/$($_.id).$Version.snupkg"
            version = $Version
            source_revision_id = $SourceRevisionId
        }
    })
}

function Assert-MetadataOnlyReport {
    Write-Report -Status 'validating'
    $report = Get-Content -Raw -Path $reportPath
    if ($report.Contains($repositoryRoot, [StringComparison]::Ordinal) -or
        $report -match '(?i)authorization:|bearer\s+|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|provider payload') {
        Fail-Gate -Category 'metadata-only-report' -Reason 'unsafe-report-content'
    }
    Add-Result -Category 'metadata-only-report' -Status 'passed' -ExitCode 0
}

function Invoke-Publish {
    if ($Mode -ne 'Publish') {
        Add-Result -Category 'publish' -Status 'skipped-dry-run' -ExitCode 0
        return
    }
    if ($env:GITHUB_ACTIONS -cne 'true' -or $ReleaseTag.Length -eq 0) {
        Fail-Gate -Category 'publish' -Reason 'publish-requires-guarded-github-release-context'
    }
    if ($FeedSource -cne 'https://api.nuget.org/v3/index.json') {
        Fail-Gate -Category 'publish' -Reason 'publish-feed-must-be-nuget-org'
    }
    $apiKey = [Environment]::GetEnvironmentVariable($ApiKeyEnvironmentVariable)
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Fail-Gate -Category 'publish' -Reason 'missing-api-key-environment-variable'
    }
    foreach ($package in $manifestPackages) {
        $archive = Join-Path $packagesDirectory "$($package.id).$Version.nupkg"
        Invoke-CommandChecked -Category 'publish' -Executable 'dotnet' -Arguments @(
            'nuget',
            'push',
            $archive,
            '--source',
            $FeedSource,
            '--api-key',
            $apiKey
        )
    }
    Add-Result -Category 'publish' -Status 'passed' -ExitCode 0
}

$pushedLocation = $false
try {
    Push-Location $repositoryRoot
    $pushedLocation = $true
    Assert-VersionAndSource
    Read-AndValidateManifest
    Invoke-RestoreBuild
    Invoke-PackAndValidate
    Assert-MetadataOnlyReport
    Invoke-Publish
    Write-Report -Status 'passed'
    Write-Host 'RELEASE-PACKAGES status=passed'
}
finally {
    if ($pushedLocation) {
        Pop-Location
    }
}
