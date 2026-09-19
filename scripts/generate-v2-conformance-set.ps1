[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BaselineCommit = '3f1056d998ac4688f36eb869c516812c1a4ddb71'
)

$ErrorActionPreference = 'Stop'
$outputRelativePath = '_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml'
$outputPath = Join-Path $RepositoryRoot $outputRelativePath
$matrixRelativePath = 'docs/contract/authorization-matrix.md'
$v1RelativePath = 'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml'
$approvalRegisterRelativePath = '_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml'

$tracked = @(git -C $RepositoryRoot diff --name-only --diff-filter=ACMRT $BaselineCommit --)
$untracked = @(git -C $RepositoryRoot ls-files --others --exclude-standard)
$candidatePaths = @($tracked + $untracked) |
    Where-Object {
        $_ -and
        $_ -ne $outputRelativePath -and
        # The register binds this manifest's digest, so including it would create an impossible circular hash.
        $_ -ne $approvalRegisterRelativePath -and
        $_ -notlike '_bmad-output/implementation-artifacts/spec-*' -and
        $_ -notmatch '(^|/)(bin|obj)/'
    } |
    Sort-Object -Unique

if ($candidatePaths.Count -eq 0) {
    throw 'The v2 conformance set cannot be empty.'
}

$entries = foreach ($relativePath in $candidatePaths) {
    $fullPath = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Candidate artifact is not a file: $relativePath"
    }

    [pscustomobject]@{
        Path = $relativePath.Replace('\', '/')
        Bytes = (Get-Item -LiteralPath $fullPath).Length
        Sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$matrix = $entries | Where-Object Path -eq $matrixRelativePath
if ($null -eq $matrix) {
    throw "The candidate set does not contain $matrixRelativePath."
}

$v1Path = Join-Path $RepositoryRoot $v1RelativePath
$v1Sha256 = (Get-FileHash -LiteralPath $v1Path -Algorithm SHA256).Hash.ToLowerInvariant()
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("schema_version: '1.0.0'")
$lines.Add("evidence_id: PD10-V2-CONFORMANCE-SET")
$lines.Add("candidate_version: '2.0.0'")
$lines.Add("generated_for_date: '2026-09-17'")
$lines.Add("baseline_commit: '$BaselineCommit'")
$lines.Add("historical_v1_path: $v1RelativePath")
$lines.Add("historical_v1_sha256: $v1Sha256")
$lines.Add("authorization_matrix_path: $matrixRelativePath")
$lines.Add("authorization_matrix_sha256: $($matrix.Sha256)")
$lines.Add("approval_status: pending-a6b")
$lines.Add("production_exposure: disabled")
$lines.Add("story_closure_claimed: false")
$lines.Add("artifact_count: $($entries.Count)")
$lines.Add('artifacts:')
foreach ($entry in $entries) {
    $lines.Add("  - path: $($entry.Path)")
    $lines.Add("    bytes: $($entry.Bytes)")
    $lines.Add("    sha256: $($entry.Sha256)")
}

[System.IO.File]::WriteAllLines($outputPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Output "Wrote $($entries.Count) candidate artifact digests to $outputRelativePath"
