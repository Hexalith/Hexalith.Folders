#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptRoot '../..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/forgejo-provider-evidence'
$reportPath = Join-Path $reportDirectory 'latest.json'
$started = [DateTimeOffset]::UtcNow

if (Test-Path -LiteralPath $reportPath) {
    Remove-Item -LiteralPath $reportPath -Force
}

function Fail-EvidenceGate {
    param(
        [Parameter(Mandatory = $true)][string]$Scenario,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    Write-Error "FORGEJO-EVIDENCE-FAILED: scenario=$Scenario reason=$Reason"
    exit 1
}

function Get-RequiredEnvironmentValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        Fail-EvidenceGate -Scenario 'prerequisites' -Reason "missing-environment-reference name=$Name"
    }

    return $value
}

$approval = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_APPROVAL'
if ($approval -cne 'approved-isolated') {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'approval-not-isolated'
}

$baseUrl = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BASE_URL'
$positiveToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_TOKEN'
$deniedToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_DENIED_TOKEN'
$isolationToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_TOKEN'
$owner = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_OWNER'
$bindRepository = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY'
$bindRepositoryId = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY_ID'
$bindBranch = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_BRANCH'
$bindVisibility = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_VISIBILITY'
$expectedVersion = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_EXPECTED_VERSION'
$isolationOwner = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_OWNER'
$isolationRepository = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_REPOSITORY'

if ($positiveToken -ceq $deniedToken -or
    $positiveToken -ceq $isolationToken -or
    $deniedToken -ceq $isolationToken) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'credential-scenarios-not-distinct'
}

if (($isolationOwner -ceq $owner) -and ($isolationRepository -ceq $bindRepository)) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'isolation-target-not-distinct'
}

if ($expectedVersion -cne '16.0.3' -and $expectedVersion -cne '15.0.7') {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'expected-version-unsupported'
}

if ($bindVisibility -cnotin @('public', 'private', 'internal')) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'binding-visibility-unsupported'
}

$baseUri = $null
if (-not [Uri]::TryCreate($baseUrl, [UriKind]::Absolute, [ref]$baseUri) -or
    $baseUri.Scheme -cne 'https' -or
    -not [string]::IsNullOrEmpty($baseUri.UserInfo) -or
    -not [string]::IsNullOrEmpty($baseUri.Query) -or
    -not [string]::IsNullOrEmpty($baseUri.Fragment)) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'base-url-policy-rejected'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'dotnet-sdk-not-found'
}

$libraryProject = Join-Path $repositoryRoot 'src/Hexalith.Folders/Hexalith.Folders.csproj'
$testProject = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
if (-not $SkipRestoreBuild) {
    & dotnet build $libraryProject --configuration Debug -m:1 -p:UseNuGetDeps=false
    if ($LASTEXITCODE -ne 0) {
        Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'provider-build-failed'
    }

    # The test project also references the UI assembly, which currently collides on
    # Hexalith.FrontComposer.Shell. Build the already-compiled provider output only.
    & dotnet build $testProject --configuration Debug -m:1 -p:UseNuGetDeps=false -p:BuildProjectReferences=false
    if ($LASTEXITCODE -ne 0) {
        Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'live-test-build-failed'
    }
}

$runnerName = if ($IsWindows) { 'Hexalith.Folders.Tests.exe' } else { 'Hexalith.Folders.Tests' }
$testRunner = Join-Path $repositoryRoot "tests/Hexalith.Folders.Tests/bin/Debug/net10.0/$runnerName"
if (-not (Test-Path -LiteralPath $testRunner)) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'live-test-runner-missing'
}

$output = & $testRunner -noLogo -noColor -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoLiveEvidenceTests 2>&1
$testExit = $LASTEXITCODE
$output | Out-Host

$text = ($output | Out-String)
if ($text -match '(?im)^\s*total:\s*(\d+)') {
    $total = [int]$Matches[1]
}
elseif ($text -match '(?im)Total:\s*(\d+)') {
    $total = [int]$Matches[1]
}
else {
    $total = 0
}

if ($text -match '(?im)^\s*skipped:\s*(\d+)') {
    $skipped = [int]$Matches[1]
}
elseif ($text -match '(?im)Skipped:\s*(\d+)') {
    $skipped = [int]$Matches[1]
}
else {
    $skipped = -1
}

if ($testExit -ne 0 -or $total -eq 0 -or $skipped -ne 0) {
    Fail-EvidenceGate -Scenario 'live-provider' -Reason "test-lane-failed exit=$testExit total=$total skipped=$skipped"
}

if (-not (Test-Path -LiteralPath $reportPath)) {
    Fail-EvidenceGate -Scenario 'report' -Reason 'report-missing'
}

$writtenAt = [DateTimeOffset](Get-Item -LiteralPath $reportPath).LastWriteTimeUtc
if ($writtenAt -lt $started.AddSeconds(-2)) {
    Fail-EvidenceGate -Scenario 'report' -Reason 'report-not-fresh'
}

$raw = Get-Content -LiteralPath $reportPath -Raw
foreach ($secret in @(
        $baseUrl, $positiveToken, $deniedToken, $isolationToken, $owner, $bindRepository,
        $bindRepositoryId, $bindBranch, $isolationOwner, $isolationRepository)) {
    if ($secret.Length -ge 3 -and $raw.Contains($secret, [StringComparison]::Ordinal)) {
        Fail-EvidenceGate -Scenario 'report' -Reason 'report-contains-target-or-credential-material'
    }
}

$report = $raw | ConvertFrom-Json
if ($report.gate -cne 'forgejo-provider-evidence' -or
    $report.schema_version -cne 'forgejo-provider-evidence-v1' -or
    $report.status -cne 'passed' -or
    $report.diagnostic_policy -cne 'metadata-only' -or
    $report.execution_class -cne 'operator-approved-isolated-deployment' -or
    $report.elapsed_ms -lt 0) {
    Fail-EvidenceGate -Scenario 'report' -Reason 'report-contract-rejected'
}

$versions = @($report.supported_versions)
if ($versions.Count -ne 2 -or $versions[0] -cne '16.0.3' -or $versions[1] -cne '15.0.7') {
    Fail-EvidenceGate -Scenario 'report' -Reason 'supported-versions-rejected'
}

$expected = [ordered]@{
    positive = 'live-provider-mutation-and-observation'
    conflict = 'live-provider-controlled-conflict'
    denial = 'live-provider-authentication-or-permission-denial'
    'tenant-isolation' = 'live-provider-authentication-or-permission-denial'
    'binding-ref' = 'live-provider-observation'
    version = 'live-provider-observation'
    boundary = 'live-provider-https-same-origin-bounded-json'
    replay = 'production-provider-admission'
    'known-failure' = 'production-provider-admission'
    'timeout-unknown' = 'production-provider-admission'
    cancellation = 'production-provider-admission'
    'durable-boundary' = 'production-provider-admission'
    'unknown-credential' = 'production-provider-admission'
}

$actual = @{}
foreach ($row in @($report.results)) {
    if ($actual.ContainsKey($row.scenario)) {
        Fail-EvidenceGate -Scenario 'report' -Reason 'duplicate-scenario'
    }

    $actual[$row.scenario] = $row.evidence_class
    if ($row.status -cne 'passed') {
        Fail-EvidenceGate -Scenario 'report' -Reason 'scenario-not-passed'
    }
}

if ($actual.Count -ne $expected.Count) {
    Fail-EvidenceGate -Scenario 'report' -Reason 'scenario-set-rejected'
}

foreach ($scenario in $expected.Keys) {
    if (-not $actual.ContainsKey($scenario) -or $actual[$scenario] -cne $expected[$scenario]) {
        Fail-EvidenceGate -Scenario 'report' -Reason 'evidence-class-rejected'
    }
}
