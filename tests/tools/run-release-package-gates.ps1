#Requires -Version 7

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ReleaseTag = '',
    [string]$SourceRevisionId = '',
    [ValidateSet('DryRun', 'Publish')][string]$Mode = 'DryRun',
    [string]$FeedSource = '',
    [string]$ApiKeyEnvironmentVariable = 'GITHUB_TOKEN',
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'RELEASE-PACKAGES-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the release package gate.'
    exit 1
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$manifestRelativePath = 'deploy/nuget/release-packages.yaml'
$manifestPath = Join-Path $repositoryRoot $manifestRelativePath
$gateRelativePath = '_bmad-output/gates/release-packages'
$reportDirectory = Join-Path $repositoryRoot $gateRelativePath
$packagesDirectory = Join-Path $reportDirectory 'packages'
$latestReportPath = Join-Path $reportDirectory 'latest.json'
$contractMetadataRelativePath = 'src/Hexalith.Folders.Contracts/FoldersContractMetadata.cs'
$openApiRelativePath = 'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml'
$expectedPushedPackages = @(
    'Hexalith.Folders.Contracts',
    'Hexalith.Folders',
    'Hexalith.Folders.Client',
    'Hexalith.Folders.Aspire',
    'Hexalith.Folders.Testing'
)
$epicMandatedPackages = @(
    'Hexalith.Folders.Contracts',
    'Hexalith.Folders.Client',
    'Hexalith.Folders.Aspire',
    'Hexalith.Folders.Testing'
)
$evidencePaths = @(
    '_bmad-output/gates/baseline-ci/latest.json',
    '_bmad-output/gates/contract-parity-ci/latest.json',
    '_bmad-output/gates/security-redaction-ci/latest.json',
    '_bmad-output/gates/capacity-smoke-ci/latest.json',
    '_bmad-output/gates/capacity-calibration/latest.json',
    '_bmad-output/gates/safety-invariants/latest.json',
    '_bmad-output/gates/governance-completeness/latest.json'
)
$categories = @(
    'version-policy',
    'source-revision-policy',
    'manifest-package-set',
    'restore-build',
    'package-build',
    'package-metadata',
    'symbol-packages',
    'dependency-closure',
    'release-evidence',
    'metadata-only-report',
    'publish'
)
$results = @()
$packageReports = @()
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

function Get-ContractVersion {
    $metadataPath = Join-Path $repositoryRoot $contractMetadataRelativePath
    $metadata = Get-Content -Raw -Path $metadataPath
    $match = [regex]::Match($metadata, 'ContractVersion\s*=\s*"(?<value>[^"]+)"')
    if (-not $match.Success) {
        Fail-Gate -Category 'release-evidence' -Reason 'missing-contract-version'
    }

    return $match.Groups['value'].Value
}

function Write-ReleasePackageReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    $contractVersion = Get-ContractVersion
    $payload = [ordered]@{
        gate = 'release-packages'
        status = $Status
        mode = $Mode
        exit_code = $ExitCode
        report_path = '_bmad-output/gates/release-packages/latest.json'
        diagnostic_policy = 'metadata-only'
        categories = $categories
        package_version = $Version
        release_tag = $ReleaseTag
        source_revision_id = $SourceRevisionId
        contract_version = $contractVersion
        contract_metadata_path = $contractMetadataRelativePath
        openapi_spine_path = $openApiRelativePath
        package_manifest_path = $manifestRelativePath
        package_output_path = '_bmad-output/gates/release-packages/packages'
        release_evidence_paths = $evidencePaths
        pushed_package_ids = $expectedPushedPackages
        package_reports = $script:packageReports
        results = $script:results
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
    Write-ReleasePackageReport -Status 'failed' -ExitCode $ExitCode
    Write-Error "RELEASE-PACKAGES-FAILED: category=$Category reason=$Reason"
    exit $ExitCode
}

function Assert-StrictSemVer {
    if ($Version -match '^(?:v|latest$|main$|master$|next$|alpha$|beta$)') {
        Fail-Gate -Category 'version-policy' -Reason 'mutable-or-prefixed-version'
    }

    $semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
    if ($Version -notmatch $semVerPattern) {
        Fail-Gate -Category 'version-policy' -Reason 'invalid-semver'
    }

    if ($ReleaseTag.Length -gt 0) {
        if ($ReleaseTag -ne "v$Version") {
            Fail-Gate -Category 'version-policy' -Reason 'release-tag-version-mismatch'
        }

        if ($ReleaseTag -notmatch "^v$([regex]::Escape($Version))$") {
            Fail-Gate -Category 'version-policy' -Reason 'invalid-release-tag'
        }
    }

    Add-Result -Category 'version-policy' -Status 'passed' -ExitCode 0
}

function Assert-SourceRevisionId {
    if ([string]::IsNullOrWhiteSpace($SourceRevisionId)) {
        # Assign to script scope so the resolved SHA propagates to packing and metadata
        # assertions; a plain assignment here would only create a function-local copy.
        $script:SourceRevisionId = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
    }

    if ($SourceRevisionId -notmatch '^[0-9a-fA-F]{40}$') {
        Fail-Gate -Category 'source-revision-policy' -Reason 'source-revision-id-must-be-full-sha'
    }

    if ($SourceRevisionId -in @('local', 'NO_VCS')) {
        Fail-Gate -Category 'source-revision-policy' -Reason 'forbidden-source-revision-id'
    }

    Add-Result -Category 'source-revision-policy' -Status 'passed' -ExitCode 0
}

function ConvertTo-BoolValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    return [bool]::Parse($Value.Trim())
}

function Read-ReleasePackageManifest {
    if (-not (Test-Path $manifestPath)) {
        Fail-Gate -Category 'manifest-package-set' -Reason 'missing-manifest'
    }

    $items = @()
    $section = ''
    $current = $null
    foreach ($line in Get-Content -Path $manifestPath) {
        if ($line -match '^(releaseSet|excludedPackableProjects):\s*$') {
            if ($null -ne $current) {
                $items += $current
                $current = $null
            }

            $section = $Matches[1]
            continue
        }

        if ($line -match '^[A-Za-z].*:\s*$') {
            if ($null -ne $current) {
                $items += $current
                $current = $null
            }

            $section = ''
            continue
        }

        if ($section -in @('releaseSet', 'excludedPackableProjects') -and $line -match '^\s{2}-\s+packageId:\s*(?<value>.+?)\s*$') {
            if ($null -ne $current) {
                $items += $current
            }

            $current = [ordered]@{
                section = $section
                packageId = $Matches['value']
            }
            continue
        }

        if ($null -ne $current -and $line -match '^\s{4}(?<key>[A-Za-z0-9_]+):\s*(?<value>.*)$') {
            $key = $Matches['key']
            $value = $Matches['value'].Trim().Trim('"')
            if ($key -in @('pushedInStory79', 'symbolPackageRequired')) {
                $current[$key] = ConvertTo-BoolValue -Value $value
            }
            else {
                $current[$key] = $value
            }
        }
    }

    if ($null -ne $current) {
        $items += $current
    }

    return $items
}

function Assert-ManifestPackageSet {
    param([Parameter(Mandatory = $true)][array]$ManifestItems)

    $pushed = @($ManifestItems | Where-Object { $_.section -eq 'releaseSet' -and $_.pushedInStory79 -eq $true })
    $pushedIds = @($pushed | ForEach-Object { $_.packageId })
    foreach ($actual in $pushedIds) {
        if ($expectedPushedPackages -notcontains $actual) {
            Fail-Gate -Category 'manifest-package-set' -Reason "unexpected-package package_id=$actual"
        }
    }

    foreach ($expected in $expectedPushedPackages) {
        if ($pushedIds -notcontains $expected) {
            Fail-Gate -Category 'manifest-package-set' -Reason "missing-package package_id=$expected"
        }
    }

    foreach ($required in $epicMandatedPackages) {
        if ($pushedIds -notcontains $required) {
            Fail-Gate -Category 'manifest-package-set' -Reason "missing-epic-package package_id=$required"
        }
    }

    foreach ($item in $pushed) {
        $projectPath = Join-Path $repositoryRoot $item.projectPath
        if (-not (Test-Path $projectPath)) {
            Fail-Gate -Category 'manifest-package-set' -Reason "missing-project package_id=$($item.packageId)"
        }

        $project = Get-Content -Raw -Path $projectPath
        if ($project -notmatch '<IsPackable>true</IsPackable>') {
            Fail-Gate -Category 'manifest-package-set' -Reason "project-not-packable package_id=$($item.packageId)"
        }
    }

    foreach ($excluded in @($ManifestItems | Where-Object { $_.section -eq 'excludedPackableProjects' })) {
        if ($excluded.pushedInStory79 -ne $false -or $excluded.publishMode -ne 'excluded') {
            Fail-Gate -Category 'manifest-package-set' -Reason "excluded-project-marked-push package_id=$($excluded.packageId)"
        }
    }

    Add-Result -Category 'manifest-package-set' -Status 'passed' -ExitCode 0
    return $pushed
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "RELEASE-PACKAGES category=$Category status=running"
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Fail-Gate -Category $Category -Reason "dotnet-command-failed exit_code=$exitCode" -ExitCode $exitCode
    }
}

function Invoke-RestoreBuild {
    if ($SkipRestoreBuild) {
        Add-Result -Category 'restore-build' -Status 'skipped-same-run-prerequisite' -ExitCode 0
        return
    }

    Invoke-DotNet -Category 'restore-build' -Arguments @('restore', 'Hexalith.Folders.slnx', '-m:1', '-p:NuGetAudit=false')
    Invoke-DotNet -Category 'restore-build' -Arguments @('build', 'Hexalith.Folders.slnx', '--no-restore', '-m:1')
    Add-Result -Category 'restore-build' -Status 'passed' -ExitCode 0
}

function Invoke-Packages {
    param([Parameter(Mandatory = $true)][array]$Packages)

    if (Test-Path $packagesDirectory) {
        Remove-Item -Recurse -Force -Path $packagesDirectory
    }

    New-Item -ItemType Directory -Force -Path $packagesDirectory | Out-Null
    foreach ($package in $Packages) {
        Invoke-DotNet -Category 'package-build' -Arguments @(
            'pack',
            $package.projectPath,
            '-c',
            'Release',
            '--no-restore',
            '-m:1',
            '-o',
            $packagesDirectory,
            "/p:PackageVersion=$Version",
            "/p:Version=$Version",
            "/p:RepositoryCommit=$SourceRevisionId",
            "/p:SourceRevisionId=$SourceRevisionId",
            '/p:ContinuousIntegrationBuild=true',
            '/p:PublishRepositoryUrl=true',
            '/p:EmbedUntrackedSources=true',
            '/p:IncludeSymbols=true',
            '/p:SymbolPackageFormat=snupkg'
        )
    }

    Add-Result -Category 'package-build' -Status 'passed' -ExitCode 0
}

function Read-NuspecText {
    param([Parameter(Mandatory = $true)][string]$PackagePath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if ($null -eq $entry) {
            Fail-Gate -Category 'package-metadata' -Reason "missing-nuspec package_path=$PackagePath"
        }

        $reader = [System.IO.StreamReader]::new($entry.Open())
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
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

    if ($Value -match '(?i)secrets\.|authorization:|bearer\s+|access_token|refresh_token|api[_-]?key|password\s*=|token\s*=|BEGIN [A-Z ]*PRIVATE KEY|diff --git|raw file contents|provider payload|environment dump|local absolute path') {
        Fail-Gate -Category $Category -Reason 'credential-or-unsafe-diagnostic'
    }
}

function Assert-PackageMetadata {
    param([Parameter(Mandatory = $true)][array]$Packages)

    $contractVersion = Get-ContractVersion
    foreach ($package in $Packages) {
        $nupkgName = "$($package.packageId).$Version.nupkg"
        $snupkgName = "$($package.packageId).$Version.snupkg"
        $nupkgPath = Join-Path $packagesDirectory $nupkgName
        $snupkgPath = Join-Path $packagesDirectory $snupkgName
        if (-not (Test-Path $nupkgPath)) {
            Fail-Gate -Category 'package-metadata' -Reason "missing-nupkg package_id=$($package.packageId)"
        }

        if ($package.symbolPackageRequired -eq $true -and -not (Test-Path $snupkgPath)) {
            Fail-Gate -Category 'symbol-packages' -Reason "missing-snupkg package_id=$($package.packageId)"
        }

        $nuspec = Read-NuspecText -PackagePath $nupkgPath
        foreach ($expected in @(
            "<id>$($package.packageId)</id>",
            "<version>$Version</version>",
            '<authors>Hexalith Contributors</authors>',
            '<license type="expression">MIT</license>',
            '<projectUrl>https://github.com/Hexalith/Hexalith.Folders</projectUrl>',
            '<repository type="git" url="https://github.com/Hexalith/Hexalith.Folders"',
            "commit=`"$SourceRevisionId`"",
            '<readme>README.md</readme>',
            '<tags>folders'
        )) {
            if (-not $nuspec.Contains($expected, [StringComparison]::Ordinal)) {
                Fail-Gate -Category 'package-metadata' -Reason "metadata-drift package_id=$($package.packageId)"
            }
        }

        Assert-MetadataOnlyString -Value $nuspec -Category 'package-metadata'

        $script:packageReports += [ordered]@{
            package_id = $package.packageId
            role = $package.role
            project_path = $package.projectPath
            package_path = "$gateRelativePath/packages/$nupkgName"
            symbol_package_path = "$gateRelativePath/packages/$snupkgName"
            version = $Version
            source_revision_id = $SourceRevisionId
            contract_version = $contractVersion
            openapi_spine_path = $openApiRelativePath
            publish_mode = $package.publishMode
        }
    }

    Add-Result -Category 'package-metadata' -Status 'passed' -ExitCode 0
    Add-Result -Category 'symbol-packages' -Status 'passed' -ExitCode 0
}

function Assert-DependencyClosure {
    param([Parameter(Mandatory = $true)][array]$Packages)

    $pushedIds = @($Packages | ForEach-Object { $_.packageId })
    foreach ($package in $Packages) {
        $nupkgPath = Join-Path $packagesDirectory "$($package.packageId).$Version.nupkg"
        $nuspec = Read-NuspecText -PackagePath $nupkgPath
        foreach ($match in [regex]::Matches($nuspec, '<dependency id="(?<id>Hexalith\.Folders(?:\.[^"]+)?)"')) {
            $dependencyId = $match.Groups['id'].Value
            if ($pushedIds -notcontains $dependencyId) {
                Fail-Gate -Category 'dependency-closure' -Reason "missing-pushed-dependency package_id=$($package.packageId) dependency_id=$dependencyId"
            }
        }
    }

    Add-Result -Category 'dependency-closure' -Status 'passed' -ExitCode 0
}

function Assert-ReleaseEvidence {
    $contractVersion = Get-ContractVersion
    if ($Mode -eq 'Publish' -and $contractVersion -eq '0.0.0-scaffold') {
        Fail-Gate -Category 'release-evidence' -Reason 'contract-version-placeholder-blocks-live-publish'
    }

    if (-not (Test-Path (Join-Path $repositoryRoot $openApiRelativePath))) {
        Fail-Gate -Category 'release-evidence' -Reason 'missing-openapi-spine'
    }

    foreach ($relativePath in $evidencePaths) {
        $fullPath = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path $fullPath)) {
            Fail-Gate -Category 'release-evidence' -Reason "missing-release-evidence path=$relativePath"
        }

        try {
            $evidence = Get-Content -Raw -Path $fullPath | ConvertFrom-Json
        }
        catch {
            Fail-Gate -Category 'release-evidence' -Reason "malformed-release-evidence path=$relativePath"
        }

        if ($evidence.status -ne 'passed') {
            Fail-Gate -Category 'release-evidence' -Reason "failed-release-evidence path=$relativePath"
        }

        if ($relativePath -eq '_bmad-output/gates/capacity-calibration/latest.json') {
            if ($evidence.source_commit -ne $SourceRevisionId) {
                Fail-Gate -Category 'release-evidence' -Reason 'stale-capacity-calibration-evidence'
            }

            foreach ($criterion in @('c1', 'c2', 'c5')) {
                if ($null -eq $evidence.target_comparison.$criterion) {
                    Fail-Gate -Category 'release-evidence' -Reason "missing-capacity-target-comparison criterion=$criterion"
                }
            }
        }
    }

    Add-Result -Category 'release-evidence' -Status 'passed' -ExitCode 0
}

function Assert-MetadataOnlyReport {
    Write-ReleasePackageReport -Status 'validating' -ExitCode 0
    $json = Get-Content -Raw -Path $latestReportPath
    Assert-MetadataOnlyString -Value $json -Category 'metadata-only-report'
    if ($json -match [regex]::Escape($repositoryRoot)) {
        Fail-Gate -Category 'metadata-only-report' -Reason 'absolute-repository-path-in-report'
    }

    Add-Result -Category 'metadata-only-report' -Status 'passed' -ExitCode 0
}

function Invoke-Publish {
    if ($Mode -ne 'Publish') {
        Add-Result -Category 'publish' -Status 'skipped-dry-run' -ExitCode 0
        return
    }

    if ([string]::IsNullOrWhiteSpace($FeedSource)) {
        Fail-Gate -Category 'publish' -Reason 'missing-feed-source'
    }

    $apiKey = [Environment]::GetEnvironmentVariable($ApiKeyEnvironmentVariable)
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Fail-Gate -Category 'publish' -Reason 'missing-api-key-environment-variable'
    }

    foreach ($package in Get-ChildItem -Path $packagesDirectory -Filter '*.nupkg' | Where-Object { $_.Name -notlike '*.symbols.nupkg' }) {
        Invoke-DotNet -Category 'publish' -Arguments @(
            'nuget',
            'push',
            $package.FullName,
            '--source',
            $FeedSource,
            '--api-key',
            $apiKey,
            '--skip-duplicate',
            '--no-symbols'
        )
    }

    Add-Result -Category 'publish' -Status 'passed' -ExitCode 0
}

try {
    Push-Location $repositoryRoot
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    Assert-StrictSemVer
    Assert-SourceRevisionId
    $manifestItems = Read-ReleasePackageManifest
    $packages = Assert-ManifestPackageSet -ManifestItems $manifestItems
    Invoke-RestoreBuild
    Invoke-Packages -Packages $packages
    Assert-PackageMetadata -Packages $packages
    Assert-DependencyClosure -Packages $packages
    Assert-ReleaseEvidence
    Assert-MetadataOnlyReport
    Invoke-Publish
    Write-ReleasePackageReport -Status 'passed' -ExitCode 0
    Write-Host 'RELEASE-PACKAGES status=passed'
}
finally {
    Pop-Location
}
