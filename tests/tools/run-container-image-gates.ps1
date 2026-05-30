#Requires -Version 7

param(
    [Alias('NoRestore')]
    [switch]$SkipRestoreBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'CONTAINER-IMAGE-PREREQUISITE-DRIFT: dotnet SDK not found on PATH. Install .NET SDK per global.json before running the container image gate.'
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsParent = Join-Path $scriptRoot '..'
$repositoryRoot = (Resolve-Path (Join-Path $toolsParent '..')).ProviderPath
$reportDirectory = Join-Path $repositoryRoot '_bmad-output/gates/container-images'
$archiveDirectory = Join-Path $reportDirectory 'archives'
$reportPath = Join-Path $reportDirectory 'latest.json'
$pushed = $false

$services = @(
    [ordered]@{
        service = 'server'
        project_path = 'src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj'
        repository = 'hexalith-folders-server'
        archive_path = '_bmad-output/gates/container-images/archives/hexalith-folders-server.tar.gz'
    },
    [ordered]@{
        service = 'workers'
        project_path = 'src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj'
        repository = 'hexalith-folders-workers'
        archive_path = '_bmad-output/gates/container-images/archives/hexalith-folders-workers.tar.gz'
    },
    [ordered]@{
        service = 'ui'
        project_path = 'src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj'
        repository = 'hexalith-folders-ui'
        archive_path = '_bmad-output/gates/container-images/archives/hexalith-folders-ui.tar.gz'
    }
)

$requiredLabels = @(
    'org.opencontainers.image.source',
    'org.opencontainers.image.revision',
    'org.opencontainers.image.version',
    'org.opencontainers.image.title',
    'org.opencontainers.image.vendor',
    'org.opencontainers.image.licenses',
    'io.hexalith.project',
    'io.hexalith.service'
)

function Get-SourceRevision {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return 'NO_VCS'
    }

    $revision = (& git rev-parse --short HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) {
        return 'NO_VCS'
    }

    return $revision.Trim()
}

function Write-ContainerImageReport {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][array]$Results
    )

    [ordered]@{
        gate = 'container-images'
        status = $Status
        services = $Results
        required_labels = $requiredLabels
        publish_mode = 'sdk-container-archive'
        report_path = '_bmad-output/gates/container-images/latest.json'
        diagnostic_policy = 'metadata-only'
        live_registry = 'not_required'
    } | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

try {
    Push-Location $repositoryRoot
    $pushed = $true

    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $archiveDirectory | Out-Null

    if (-not $SkipRestoreBuild) {
        dotnet restore Hexalith.Folders.slnx
        if ($LASTEXITCODE -ne 0) {
            Write-ContainerImageReport -Status 'failed' -Results @()
            exit $LASTEXITCODE
        }
    }

    $revision = Get-SourceRevision
    $results = @()
    $failedExitCode = 0

    foreach ($service in $services) {
        $archivePath = Join-Path $repositoryRoot $service.archive_path
        if (Test-Path $archivePath) {
            Remove-Item $archivePath -Force
        }

        $publishArgs = @(
            'publish',
            $service.project_path,
            '-c',
            'Release',
            '--os',
            'linux',
            '--arch',
            'x64',
            '/t:PublishContainer',
            "-p:ContainerArchiveOutputPath=$archivePath",
            '-p:ContainerImageTag=local-validation',
            "-p:SourceRevisionId=$revision"
        )

        if ($SkipRestoreBuild) {
            $publishArgs += '--no-restore'
        }

        & dotnet @publishArgs
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0 -and $failedExitCode -eq 0) {
            $failedExitCode = $exitCode
        }

        $results += [ordered]@{
            service = $service.service
            project_path = $service.project_path
            repository = $service.repository
            archive_path = $service.archive_path
            tags = @('local-validation')
            labels_asserted = $requiredLabels
            command_exit_code = $exitCode
        }
    }

    if ($failedExitCode -ne 0) {
        Write-ContainerImageReport -Status 'failed' -Results $results
        exit $failedExitCode
    }

    Write-ContainerImageReport -Status 'passed' -Results $results
}
finally {
    if ($pushed) {
        Pop-Location
    }
}
