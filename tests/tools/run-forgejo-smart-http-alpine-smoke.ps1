[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$runId = [Guid]::NewGuid().ToString('N')
$networkName = "hxf-forgejo-smoke-$runId"
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "hxf-forgejo-smoke-$runId"
$certificatePath = Join-Path $temporaryDirectory 'cert.pem'
$keyPath = Join-Path $temporaryDirectory 'key.pem'
$testProject = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
$testOutput = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/bin/Release/net10.0/linux-musl-x64'
$testMethod = 'Hexalith.Folders.Tests.Providers.Forgejo.ForgejoSmartHttpGitTransportIntegrationTests.AlpineTlsProfileReceivesExactPackAndRejectsPostAdvertisementStaleOld'
$containers = [System.Collections.Generic.List[string]]::new()
$dockerAvailable = $false

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    $hostUid = (& id -u).Trim()
    $hostGid = (& id -g).Trim()
    if ($LASTEXITCODE -ne 0 -or $hostUid -notmatch '^\d+$' -or $hostGid -notmatch '^\d+$') {
        throw 'Could not determine the host checkout UID/GID for the private TLS key handoff.'
    }

    Invoke-Checked 'openssl' @(
        'req', '-x509', '-newkey', 'rsa:2048', '-sha256', '-nodes', '-days', '1',
        '-subj', '/CN=forgejo-smoke',
        '-addext', 'subjectAltName=DNS:forgejo-smoke,DNS:localhost,IP:127.0.0.1',
        '-keyout', $keyPath,
        '-out', $certificatePath)
    $fingerprintOutput = & openssl x509 -in $certificatePath -noout -fingerprint -sha256
    if ($LASTEXITCODE -ne 0 -or $fingerprintOutput -notmatch '=([0-9A-F:]+)$') {
        throw 'Could not calculate the smoke certificate fingerprint.'
    }

    $certificateSha256 = $Matches[1].Replace(':', '')
    Invoke-Checked 'docker' @('version', '--format', '{{.Server.Version}}')
    $dockerAvailable = $true
    Invoke-Checked 'docker' @('network', 'create', $networkName)
    Invoke-Checked 'dotnet' @(
        'build', $testProject,
        '--configuration', 'Release',
        '--runtime', 'linux-musl-x64',
        '-m:1',
        '-p:MinVerVersionOverride=1.0.0',
        '-p:NuGetAudit=false')

    foreach ($version in @('16.0.3', '15.0.7')) {
        $containerName = "hxf-forgejo-smoke-$version-$runId"
        $containers.Add($containerName)
        Invoke-Checked 'docker' @('pull', "codeberg.org/forgejo/forgejo:$version")
        Invoke-Checked 'docker' @(
            'run', '--detach',
            '--name', $containerName,
            '--network', $networkName,
            '--network-alias', 'forgejo-smoke',
            '--volume', "${certificatePath}:/certs/cert.pem:ro",
            '--volume', "${keyPath}:/certs/key.pem:ro",
            '--env', "USER_UID=$hostUid",
            '--env', "USER_GID=$hostGid",
            '--env', 'FORGEJO__database__DB_TYPE=sqlite3',
            '--env', 'FORGEJO__server__PROTOCOL=https',
            '--env', 'FORGEJO__server__CERT_FILE=/certs/cert.pem',
            '--env', 'FORGEJO__server__KEY_FILE=/certs/key.pem',
            '--env', 'FORGEJO__server__ROOT_URL=https://forgejo-smoke:3000/',
            '--env', 'FORGEJO__server__HTTP_PORT=3000',
            '--env', 'FORGEJO__server__DISABLE_SSH=true',
            '--env', 'FORGEJO__security__INSTALL_LOCK=true',
            "codeberg.org/forgejo/forgejo:$version")

        $started = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            $logs = & docker logs $containerName 2>&1
            if ($logs -match 'Starting new Web server') {
                $started = $true
                break
            }

            Start-Sleep -Seconds 1
        }

        if (-not $started) {
            throw "Forgejo $version did not start within 60 seconds."
        }

        Invoke-Checked 'docker' @(
            'exec', '--user', 'git', $containerName,
            'forgejo', 'admin', 'user', 'create',
            '--username', 'smoke-admin',
            '--password', 'SmokePassword123!',
            '--email', 'smoke@localhost.invalid',
            '--admin',
            '--must-change-password=false')
        $tokenOutput = & docker exec --user git $containerName forgejo admin user generate-access-token --username smoke-admin --token-name smoke --scopes all
        if ($LASTEXITCODE -ne 0 -or $tokenOutput -notmatch 'created:\s+([a-f0-9]+)$') {
            throw "Forgejo $version did not issue the isolated smoke token."
        }

        $env:HEXALITH_FORGEJO_SMART_HTTP_BASE_URL = 'https://forgejo-smoke:3000/'
        $env:HEXALITH_FORGEJO_SMART_HTTP_TOKEN = $Matches[1]
        $env:HEXALITH_FORGEJO_SMART_HTTP_CERT_SHA256 = $certificateSha256
        $env:HEXALITH_FORGEJO_SMART_HTTP_VERSION = $version
        $smokeArguments = @(
            'run', '--rm',
            '--user', 'app',
            '--network', $networkName,
            '--volume', "${testOutput}:/app:ro",
            '--volume', "${certificatePath}:/certs/cert.pem:ro",
            '--workdir', '/app',
            '--env', 'SSL_CERT_FILE=/certs/cert.pem',
            '--env', 'HEXALITH_FORGEJO_SMART_HTTP_BASE_URL',
            '--env', 'HEXALITH_FORGEJO_SMART_HTTP_TOKEN',
            '--env', 'HEXALITH_FORGEJO_SMART_HTTP_CERT_SHA256',
            '--env', 'HEXALITH_FORGEJO_SMART_HTTP_VERSION',
            'mcr.microsoft.com/dotnet/aspnet:10.0-alpine',
            'dotnet', 'Hexalith.Folders.Tests.dll',
            '-noLogo', '-noColor', '-method', $testMethod)
        $smokeOutput = & docker @smokeArguments 2>&1
        $smokeExitCode = $LASTEXITCODE
        $smokeOutput | ForEach-Object { Write-Host $_ }
        if ($smokeExitCode -ne 0) {
            throw "Forgejo $version Alpine smart-HTTP smoke failed with exit code $smokeExitCode."
        }

        $smokeSummary = $smokeOutput -join [Environment]::NewLine
        if ($smokeSummary -notmatch '(?i)Total:\s*1' -or $smokeSummary -notmatch '(?i)Skipped:\s*0') {
            throw "Forgejo $version Alpine smart-HTTP smoke did not execute exactly one non-skipped test."
        }

        Invoke-Checked 'docker' @('rm', '--force', $containerName)
        [void]$containers.Remove($containerName)
    }
}
finally {
    Remove-Item Env:HEXALITH_FORGEJO_SMART_HTTP_BASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:HEXALITH_FORGEJO_SMART_HTTP_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:HEXALITH_FORGEJO_SMART_HTTP_CERT_SHA256 -ErrorAction SilentlyContinue
    Remove-Item Env:HEXALITH_FORGEJO_SMART_HTTP_VERSION -ErrorAction SilentlyContinue
    if ($dockerAvailable) {
        foreach ($containerName in $containers) {
            try {
                & docker rm --force $containerName 2>$null | Out-Null
            }
            catch {
                Write-Warning "Could not remove isolated Forgejo smoke container '$containerName'."
            }
        }

        try {
            & docker network rm $networkName 2>$null | Out-Null
        }
        catch {
            Write-Warning "Could not remove isolated Forgejo smoke network '$networkName'."
        }
    }

    if (Test-Path $temporaryDirectory) {
        Remove-Item -Recurse -Force $temporaryDirectory
    }
}
