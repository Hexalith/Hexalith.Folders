#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..', '..'))
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/governance-completeness'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false
$governanceClasses = @(
    'Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests',
    'Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests',
    'Hexalith.Folders.Contracts.Tests.OpenApi.ProviderCompatibilityCatalogContractTests'
)

function Write-GovernanceReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [int]$ExitCode = 0
    )

    $temporaryReportPath = Join-Path $reportDirectory ('.latest.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
        [ordered]@{
            gate = 'governance-completeness'
            status = $Status
            exit_code = $ExitCode
            canonical_inputs = @(
                'docs/exit-criteria/c0-c13-governance-evidence.yaml',
                'docs/exit-criteria/c7-lock-authorization-timing.md',
                'docs/contract/file-context-contract-groups.md',
                'docs/contract/oq2-file-policy-evidence.yaml',
                'docs/contract/authorization-matrix.md',
                'docs/contract/oq3-authorization-evidence.yaml',
                'docs/contract/provider-compatibility-catalog.md',
                'docs/contract/oq4-provider-compatibility-evidence.yaml',
                'docs/governance/approval-policy.md',
                'tests/fixtures/idempotency-encoding-corpus.json',
                'tests/fixtures/idempotency-encoding-corpus-consumption.yaml',
                'tests/fixtures/pattern-example-manifest.yaml',
                'tests/fixtures/cache-key-exceptions.yaml',
                'tests/fixtures/parity-contract.yaml',
                'src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml',
                'src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml'
            )
            report_path = '_bmad-output/gates/governance-completeness/latest.json'
            diagnostic_policy = 'metadata-only'
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporaryReportPath -Encoding utf8NoBOM
        [System.IO.File]::Move($temporaryReportPath, $reportPath, $true)
    }
    catch {
        # Never leave an earlier result looking current after publication fails.
        try {
            if ([System.IO.File]::Exists($reportPath)) {
                Remove-Item -LiteralPath $reportPath -Force -ErrorAction Stop
            }
        }
        catch { }
        [Console]::Error.WriteLine('GOVERNANCE-REPORT-WRITE-FAILED: report publication failed; latest.json is not current.')
        exit 1
    }
    finally {
        try {
            if ([System.IO.File]::Exists($temporaryReportPath)) {
                [System.IO.File]::Delete($temporaryReportPath)
            }
        }
        catch { }
    }
}

function Stop-GovernanceForPrerequisite {
    param([string]$Reason, [int]$ExitCode = 1)

    Write-Host "GOVERNANCE-PREREQUISITE-DRIFT: $Reason"
    Write-GovernanceReport -Status 'prerequisite_drift' -ExitCode $ExitCode
    exit $ExitCode
}

function Invoke-GovernanceTests {
    $testAssembly = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll'
    if (-not [System.IO.File]::Exists($testAssembly)) {
        Stop-GovernanceForPrerequisite 'governance test assembly is missing. Build the Release test project before running this gate.'
    }

    $prerequisitePattern = '(?m)^[ \t]*(?:---- )?System\.(?:InvalidOperationException|FormatException) : ' +
        'GOVERNANCE-PREREQUISITE-DRIFT: (repository root was not found\.|[a-z]+(?:-[a-z]+)+)(?::|[ \t]*\r?$)'
    $summaryPattern = '(?m)^[ \t]*Hexalith\.Folders\.Contracts\.Tests[ \t]+Total: (?<total>[0-9]{1,10}), ' +
        'Errors: [0-9]+, Failed: [0-9]+, Skipped: (?<skipped>[0-9]{1,10}), Not Run: (?<notRun>[0-9]{1,10}), Time:'
    $PSNativeCommandUseErrorActionPreference = $false
    foreach ($testClass in $governanceClasses) {
        $output = & dotnet $testAssembly -noLogo -noColor -class $testClass 2>&1
        $exitCode = $LASTEXITCODE
        $outputText = $output -join "`n"
        if ($exitCode -ne 0) {
            $prerequisite = [regex]::Match($outputText, $prerequisitePattern)
            if ($prerequisite.Success) {
                Stop-GovernanceForPrerequisite "governance test prerequisite failed ($($prerequisite.Groups[1].Value)) class=$testClass" -ExitCode $exitCode
            }
        }

        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            return $exitCode
        }

        $summary = [regex]::Match($outputText, $summaryPattern)
        if (-not $summary.Success -or [long]$summary.Groups['total'].Value -le ([long]$summary.Groups['skipped'].Value + [long]$summary.Groups['notRun'].Value)) {
            Stop-GovernanceForPrerequisite "governance class selection executed zero tests class=$testClass"
        }
    }

    return 0
}

try {
    if (-not [System.IO.File]::Exists((Join-Path $repositoryRoot 'Hexalith.Folders.slnx'))) {
        Stop-GovernanceForPrerequisite 'repository root was not found.'
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Stop-GovernanceForPrerequisite 'dotnet was not found on PATH.'
    }

    Push-Location -LiteralPath $repositoryRoot
    $pushed = $true

    Write-GovernanceReport -Status 'discovered'

    if (-not $SkipRestoreBuild) {
        $PSNativeCommandUseErrorActionPreference = $false
        $null = & dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Stop-GovernanceForPrerequisite 'the .NET SDK required by global.json could not be resolved.' -ExitCode $LASTEXITCODE
        }

        dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }

        dotnet build tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            Write-GovernanceReport -Status 'failed' -ExitCode $LASTEXITCODE
            exit $LASTEXITCODE
        }
    }

    $testExitCode = Invoke-GovernanceTests
    if ($testExitCode -ne 0) {
        Write-GovernanceReport -Status 'failed' -ExitCode $testExitCode
        exit $testExitCode
    }

    Write-GovernanceReport -Status 'passed'
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
