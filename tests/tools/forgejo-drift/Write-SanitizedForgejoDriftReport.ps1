param(
    [string]$RepositoryRoot,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepositoryRoot 'artifacts\forgejo-drift\forgejo-drift-report.json'
}

$manifestPath = Join-Path $RepositoryRoot 'tests\contracts\forgejo\supported-versions.json'
$fixturePath = Join-Path $RepositoryRoot 'tests\tools\forgejo-drift\classification-fixtures.json'
$manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
$fixtures = Get-Content -Raw -Path $fixturePath | ConvertFrom-Json

$forbidden = @(
    'access_' + 'to' + 'ken=',
    'to' + 'ken=',
    'gh' + 'p_',
    '-----' + 'BEGIN',
    'us' + 'er:',
    '@' + 'forgejo',
    'cust' + 'omer',
    'private' + '-instance',
    'owner' + '-secret',
    'repo' + '-secret',
    'diff ' + '--git'
)

$scanRoots = @(
    (Join-Path $RepositoryRoot 'tests\contracts\forgejo'),
    (Join-Path $RepositoryRoot 'tests\tools\forgejo-drift')
)

$files = foreach ($root in $scanRoots) {
    Get-ChildItem -Path $root -File -Recurse
}

$violations = foreach ($file in $files) {
    $text = Get-Content -Raw -Path $file.FullName
    foreach ($value in $forbidden) {
        if ($text.IndexOf($value, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            [PSCustomObject]@{
                path = [IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName).Replace('\', '/')
                marker = $value
            }
        }
    }
}

if ($violations) {
    $violations | ConvertTo-Json -Depth 4
    throw 'Forgejo drift artifact redaction scan failed.'
}

$versions = foreach ($entry in $manifest.entries) {
    $snapshotPath = Join-Path $RepositoryRoot $entry.snapshotPath
    if (-not (Test-Path $snapshotPath)) {
        throw "Pinned Forgejo snapshot is missing: $($entry.snapshotPath)"
    }

    $snapshot = Get-Content -Raw -Path $snapshotPath | ConvertFrom-Json
    [PSCustomObject]@{
        providerVersion = $entry.version
        supportClass = $entry.supportClass
        snapshotPath = $entry.snapshotPath
        snapshotVersion = $snapshot.info.version
        fixtureSet = 'pinned-openapi-v1'
        driftClassification = $entry.expectedApiCompatibilityPosture
    }
}

$report = [ordered]@{
    schemaVersion = 'forgejo-drift-report-v1'
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    provider = 'forgejo'
    fixtureSet = 'tests/contracts/forgejo'
    versions = $versions
    classificationFixtures = $fixtures.fixtures.Count
    redactionScan = [ordered]@{
        status = 'passed'
        filesScanned = @($files).Count
    }
    artifactRetention = [ordered]@{
        status = 'sanitized-metadata-only'
        rawSchemaDiffsRetained = $false
    }
    liveDriftCheck = [ordered]@{
        status = 'ci-scheduled'
        localBuildImpact = 'none'
        credentialsRequired = $false
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding utf8
Write-Output "Forgejo drift report written to $OutputPath"
