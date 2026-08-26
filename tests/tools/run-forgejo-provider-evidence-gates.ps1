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
$results = @()
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$httpClient = $null

function Add-EvidenceResult {
    param(
        [Parameter(Mandatory = $true)][string]$Scenario,
        [Parameter(Mandatory = $true)][string]$Status,
        [Parameter(Mandatory = $true)][string]$EvidenceClass
    )

    $script:results += [ordered]@{
        scenario = $Scenario
        status = $Status
        evidence_class = $EvidenceClass
    }
}

function Write-EvidenceReport {
    param([Parameter(Mandatory = $true)][string]$Status)

    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $payload = [ordered]@{
        gate = 'forgejo-provider-evidence'
        schema_version = 'forgejo-provider-evidence-v1'
        status = $Status
        diagnostic_policy = 'metadata-only'
        execution_class = 'operator-approved-isolated-deployment'
        supported_versions = @('16.0.3', '15.0.7')
        report_path = '_bmad-output/gates/forgejo-provider-evidence/latest.json'
        elapsed_ms = [int64]$stopwatch.ElapsedMilliseconds
        results = $script:results
    }
    $payload | ConvertTo-Json -Depth 8 | Set-Content -Path $reportPath -Encoding utf8NoBOM
}

function Fail-EvidenceGate {
    param(
        [Parameter(Mandatory = $true)][string]$Scenario,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    Add-EvidenceResult -Scenario $Scenario -Status 'failed' -EvidenceClass 'metadata-only'
    Write-EvidenceReport -Status 'failed'
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

function Escape-PathSegment {
    param([Parameter(Mandatory = $true)][string]$Value)

    return [Uri]::EscapeDataString($Value)
}

function Read-BoundedJsonDocument {
    param(
        [Parameter(Mandatory = $true)][System.Net.Http.HttpContent]$Content,
        [Parameter(Mandatory = $true)][string]$Scenario
    )

    if ($Content.Headers.ContentLength -gt (256 * 1024)) {
        Fail-EvidenceGate -Scenario $Scenario -Reason 'response-size-limit-exceeded'
    }

    $contentType = $Content.Headers.ContentType
    $mediaType = if ($null -eq $contentType) { $null } else { $contentType.MediaType }
    if ($mediaType -notin @('application/json', 'text/json', 'application/problem+json')) {
        Fail-EvidenceGate -Scenario $Scenario -Reason 'response-content-type-rejected'
    }

    $bodyCancellation = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(15))
    $stream = $null
    $buffer = [byte[]]::new(8192)
    $body = [IO.MemoryStream]::new()
    try {
        try {
            $stream = $Content.ReadAsStreamAsync($bodyCancellation.Token).GetAwaiter().GetResult()
            while ($true) {
                $read = $stream.ReadAsync($buffer, 0, $buffer.Length, $bodyCancellation.Token).GetAwaiter().GetResult()
                if ($read -eq 0) {
                    break
                }

                if ($body.Length + $read -gt (256 * 1024)) {
                    Fail-EvidenceGate -Scenario $Scenario -Reason 'response-size-limit-exceeded'
                }

                $body.Write($buffer, 0, $read)
            }
        }
        catch {
            Fail-EvidenceGate -Scenario $Scenario -Reason 'response-read-failed-without-retained-provider-details'
        }

        if ($body.Length -eq 0) {
            Fail-EvidenceGate -Scenario $Scenario -Reason 'missing-json-object'
        }

        try {
            return [Text.Encoding]::UTF8.GetString($body.ToArray()) | ConvertFrom-Json
        }
        catch {
            Fail-EvidenceGate -Scenario $Scenario -Reason 'malformed-json-response'
        }
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        $body.Dispose()
        $bodyCancellation.Dispose()
    }
}

function Get-RequiredJsonProperty {
    param(
        [Parameter(Mandatory = $true)]$Document,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Scenario
    )

    if ($null -eq $Document) {
        Fail-EvidenceGate -Scenario $Scenario -Reason 'missing-json-object'
    }

    $property = $Document.PSObject.Properties[$Name]
    if ($null -eq $property) {
        Fail-EvidenceGate -Scenario $Scenario -Reason "missing-json-property name=$Name"
    }

    return $property.Value
}

function Send-ForgejoRequest {
    param(
        [Parameter(Mandatory = $true)][System.Net.Http.HttpMethod]$Method,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Token,
        [AllowNull()][string]$JsonBody
    )

    $requestUri = [Uri]::new($script:baseUri, "api/v1/$RelativePath")
    if ($requestUri.Scheme -ne $script:baseUri.Scheme -or
        $requestUri.Host -ne $script:baseUri.Host -or
        $requestUri.Port -ne $script:baseUri.Port) {
        Fail-EvidenceGate -Scenario 'boundary' -Reason 'request-origin-mismatch'
    }

    $request = [System.Net.Http.HttpRequestMessage]::new($Method, $requestUri)
    try {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
        $request.Headers.UserAgent.ParseAdd('Hexalith-Folders')
        if ($null -ne $JsonBody) {
            $request.Content = [System.Net.Http.StringContent]::new(
                $JsonBody,
                [Text.Encoding]::UTF8,
                'application/json')
        }

        try {
            $response = $script:httpClient.SendAsync(
                $request,
                [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        }
        catch {
            Fail-EvidenceGate -Scenario 'transport' -Reason 'request-failed-without-retained-provider-details'
        }

        try {
            if ([int]$response.StatusCode -ge 300 -and [int]$response.StatusCode -lt 400) {
                Fail-EvidenceGate -Scenario 'boundary' -Reason 'redirect-rejected'
            }

            $document = $null
            if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 300) {
                $document = Read-BoundedJsonDocument -Content $response.Content -Scenario 'boundary'
            }

            return [pscustomobject]@{
                StatusCode = [int]$response.StatusCode
                Document = $document
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-SuccessStatus {
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)][int]$ExpectedStatus,
        [Parameter(Mandatory = $true)][string]$Scenario
    )

    if ($Response.StatusCode -ne $ExpectedStatus) {
        Fail-EvidenceGate -Scenario $Scenario -Reason "unexpected-status class=$($Response.StatusCode)"
    }
}

if ((Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_APPROVAL') -ne 'approved-isolated') {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'isolated-deployment-approval-not-confirmed'
}

$baseUrl = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BASE_URL'
$positiveToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_TOKEN'
$deniedToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_DENIED_TOKEN'
$isolationToken = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_TOKEN'
$isolationOwner = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_OWNER'
$isolationRepository = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_ISOLATION_REPOSITORY'
$owner = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_OWNER'
$bindingRepository = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY'
$bindingRepositoryId = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY_ID'
$bindingBranch = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_BRANCH'
$bindingVisibility = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_BIND_VISIBILITY'
$expectedVersion = Get-RequiredEnvironmentValue 'HEXALITH_FORGEJO_EVIDENCE_EXPECTED_VERSION'

if ($expectedVersion -notin @('16.0.3', '15.0.7')) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'unsupported-expected-version'
}

if ($bindingVisibility -notin @('public', 'private', 'internal')) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'unsupported-binding-visibility'
}

if ($bindingRepositoryId -notmatch '^[1-9][0-9]*$') {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'binding-repository-id-not-canonical'
}

if ($positiveToken -ceq $deniedToken -or
    $positiveToken -ceq $isolationToken -or
    $deniedToken -ceq $isolationToken) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'credential-scenarios-not-distinct'
}

try {
    $script:baseUri = [Uri]$baseUrl
}
catch {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'base-url-invalid'
}

if (-not $script:baseUri.IsAbsoluteUri -or
    $script:baseUri.Scheme -ne 'https' -or
    -not [string]::IsNullOrEmpty($script:baseUri.UserInfo) -or
    -not [string]::IsNullOrEmpty($script:baseUri.Query) -or
    -not [string]::IsNullOrEmpty($script:baseUri.Fragment)) {
    Fail-EvidenceGate -Scenario 'boundary' -Reason 'base-url-policy-rejected'
}

if (-not $script:baseUri.AbsoluteUri.EndsWith('/', [StringComparison]::Ordinal)) {
    $script:baseUri = [Uri]::new($script:baseUri.AbsoluteUri + '/')
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail-EvidenceGate -Scenario 'prerequisites' -Reason 'dotnet-sdk-not-found'
}

$testProject = Join-Path $repositoryRoot 'tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj'
if (-not $SkipRestoreBuild) {
    & dotnet build $testProject --configuration Debug -m:1
    if ($LASTEXITCODE -ne 0) {
        Fail-EvidenceGate -Scenario 'hermetic-matrix' -Reason 'focused-build-failed'
    }
}

$runnerName = if ($IsWindows) { 'Hexalith.Folders.Tests.exe' } else { 'Hexalith.Folders.Tests' }
$testRunner = Join-Path $repositoryRoot "tests/Hexalith.Folders.Tests/bin/Debug/net10.0/$runnerName"
if (-not (Test-Path $testRunner)) {
    Fail-EvidenceGate -Scenario 'hermetic-matrix' -Reason 'focused-test-runner-missing'
}

& $testRunner -noLogo -noColor `
    -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoProviderTests `
    -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoHttpApiClientTests `
    -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests `
    -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoDependencyGuardTests
if ($LASTEXITCODE -ne 0) {
    Fail-EvidenceGate -Scenario 'hermetic-matrix' -Reason 'focused-tests-failed'
}

foreach ($scenario in @('replay', 'known-failure', 'timeout-unknown', 'cancellation', 'durable-boundary')) {
    Add-EvidenceResult -Scenario $scenario -Status 'passed' -EvidenceClass 'hermetic-provider-adapter'
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $false
$script:httpClient = [System.Net.Http.HttpClient]::new($handler, $true)
$script:httpClient.Timeout = [TimeSpan]::FromSeconds(15)

try {
    $versionResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath 'version' -Token $positiveToken -JsonBody $null
    Assert-SuccessStatus -Response $versionResponse -ExpectedStatus 200 -Scenario 'version'
    $observedVersion = Get-RequiredJsonProperty -Document $versionResponse.Document -Name 'version' -Scenario 'version'
    if ($observedVersion -isnot [string] -or $observedVersion -cne $expectedVersion) {
        Fail-EvidenceGate -Scenario 'version' -Reason 'version-evidence-mismatch'
    }
    Add-EvidenceResult -Scenario 'version' -Status 'passed' -EvidenceClass 'live-provider-observation'

    $createdRepository = 'hexalith-evidence-' + [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $creationBody = [ordered]@{
        auto_init = $false
        name = $createdRepository
        private = $true
    } | ConvertTo-Json -Compress
    $ownerPath = Escape-PathSegment $owner
    $createdRepositoryPath = Escape-PathSegment $createdRepository

    $createResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Post) -RelativePath "orgs/$ownerPath/repos" -Token $positiveToken -JsonBody $creationBody
    Assert-SuccessStatus -Response $createResponse -ExpectedStatus 201 -Scenario 'positive-create'
    $createdIdValue = Get-RequiredJsonProperty -Document $createResponse.Document -Name 'id' -Scenario 'positive-create'
    $createdName = Get-RequiredJsonProperty -Document $createResponse.Document -Name 'name' -Scenario 'positive-create'
    $createdPrivate = Get-RequiredJsonProperty -Document $createResponse.Document -Name 'private' -Scenario 'positive-create'
    $createdInternal = Get-RequiredJsonProperty -Document $createResponse.Document -Name 'internal' -Scenario 'positive-create'
    if ($createdIdValue -isnot [long] -or
        $createdIdValue -le 0 -or
        $createdName -isnot [string] -or
        $createdName -cne $createdRepository -or
        $createdPrivate -isnot [bool] -or
        -not $createdPrivate -or
        $createdInternal -isnot [bool] -or
        $createdInternal) {
        Fail-EvidenceGate -Scenario 'positive-create' -Reason 'canonical-create-evidence-mismatch'
    }
    $createdId = [string]$createdIdValue

    $identityResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$createdRepositoryPath" -Token $positiveToken -JsonBody $null
    Assert-SuccessStatus -Response $identityResponse -ExpectedStatus 200 -Scenario 'positive-identity'
    $identityIdValue = Get-RequiredJsonProperty -Document $identityResponse.Document -Name 'id' -Scenario 'positive-identity'
    $identityName = Get-RequiredJsonProperty -Document $identityResponse.Document -Name 'name' -Scenario 'positive-identity'
    $identityPrivate = Get-RequiredJsonProperty -Document $identityResponse.Document -Name 'private' -Scenario 'positive-identity'
    $identityInternal = Get-RequiredJsonProperty -Document $identityResponse.Document -Name 'internal' -Scenario 'positive-identity'
    if ($identityIdValue -isnot [long] -or
        [string]$identityIdValue -cne $createdId -or
        $identityName -isnot [string] -or
        $identityName -cne $createdRepository -or
        $identityPrivate -isnot [bool] -or
        -not $identityPrivate -or
        $identityInternal -isnot [bool] -or
        $identityInternal) {
        Fail-EvidenceGate -Scenario 'positive-identity' -Reason 'canonical-identity-observation-mismatch'
    }
    Add-EvidenceResult -Scenario 'positive' -Status 'passed' -EvidenceClass 'live-provider-mutation-and-observation'

    $conflictResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Post) -RelativePath "orgs/$ownerPath/repos" -Token $positiveToken -JsonBody $creationBody
    if ($conflictResponse.StatusCode -notin @(400, 409, 422)) {
        Fail-EvidenceGate -Scenario 'conflict' -Reason "unexpected-status class=$($conflictResponse.StatusCode)"
    }
    Add-EvidenceResult -Scenario 'conflict' -Status 'passed' -EvidenceClass 'live-provider-controlled-conflict'

    $bindingRepositoryPath = Escape-PathSegment $bindingRepository
    $bindingBranchPath = Escape-PathSegment $bindingBranch
    $bindingResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$bindingRepositoryPath" -Token $positiveToken -JsonBody $null
    Assert-SuccessStatus -Response $bindingResponse -ExpectedStatus 200 -Scenario 'binding'
    $bindingIdValue = Get-RequiredJsonProperty -Document $bindingResponse.Document -Name 'id' -Scenario 'binding'
    $bindingDefaultBranch = Get-RequiredJsonProperty -Document $bindingResponse.Document -Name 'default_branch' -Scenario 'binding'
    $bindingPrivate = Get-RequiredJsonProperty -Document $bindingResponse.Document -Name 'private' -Scenario 'binding'
    $bindingInternal = Get-RequiredJsonProperty -Document $bindingResponse.Document -Name 'internal' -Scenario 'binding'
    $bindingPermissions = Get-RequiredJsonProperty -Document $bindingResponse.Document -Name 'permissions' -Scenario 'binding'
    $bindingPull = Get-RequiredJsonProperty -Document $bindingPermissions -Name 'pull' -Scenario 'binding'
    $bindingAdmin = Get-RequiredJsonProperty -Document $bindingPermissions -Name 'admin' -Scenario 'binding'
    if ($bindingIdValue -isnot [long] -or
        $bindingIdValue -le 0 -or
        $bindingPrivate -isnot [bool] -or
        $bindingInternal -isnot [bool] -or
        $bindingPull -isnot [bool] -or
        $bindingAdmin -isnot [bool]) {
        Fail-EvidenceGate -Scenario 'binding' -Reason 'boolean-evidence-type-mismatch'
    }
    $visibilityMatches = switch ($bindingVisibility) {
        'public' { -not $bindingPrivate -and -not $bindingInternal }
        'private' { $bindingPrivate -and -not $bindingInternal }
        'internal' { -not $bindingPrivate -and $bindingInternal }
    }
    if ([string]$bindingIdValue -cne $bindingRepositoryId -or
        $bindingDefaultBranch -isnot [string] -or
        $bindingDefaultBranch -cne $bindingBranch -or
        -not $visibilityMatches -or
        -not $bindingPull -or
        -not $bindingAdmin) {
        Fail-EvidenceGate -Scenario 'binding' -Reason 'repository-policy-evidence-mismatch'
    }

    $branchResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$bindingRepositoryPath/branches/$bindingBranchPath" -Token $positiveToken -JsonBody $null
    Assert-SuccessStatus -Response $branchResponse -ExpectedStatus 200 -Scenario 'branch-ref'
    $observedBranch = Get-RequiredJsonProperty -Document $branchResponse.Document -Name 'name' -Scenario 'branch-ref'
    $branchProtected = Get-RequiredJsonProperty -Document $branchResponse.Document -Name 'protected' -Scenario 'branch-ref'
    $protectionName = Get-RequiredJsonProperty -Document $branchResponse.Document -Name 'effective_branch_protection_name' -Scenario 'branch-ref'
    if ($observedBranch -isnot [string] -or
        $observedBranch -cne $bindingBranch -or
        $branchProtected -isnot [bool] -or
        -not $branchProtected -or
        $protectionName -isnot [string] -or
        [string]::IsNullOrWhiteSpace($protectionName)) {
        Fail-EvidenceGate -Scenario 'branch-ref' -Reason 'branch-policy-evidence-mismatch'
    }

    $protectionPath = Escape-PathSegment $protectionName
    $protectionResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$bindingRepositoryPath/branch_protections/$protectionPath" -Token $positiveToken -JsonBody $null
    Assert-SuccessStatus -Response $protectionResponse -ExpectedStatus 200 -Scenario 'branch-protection'
    $observedProtectionName = Get-RequiredJsonProperty -Document $protectionResponse.Document -Name 'rule_name' -Scenario 'branch-protection'
    if ($observedProtectionName -isnot [string] -or $observedProtectionName -cne $protectionName) {
        Fail-EvidenceGate -Scenario 'branch-protection' -Reason 'protection-rule-evidence-mismatch'
    }
    Add-EvidenceResult -Scenario 'binding-ref' -Status 'passed' -EvidenceClass 'live-provider-observation'

    $deniedResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$bindingRepositoryPath" -Token $deniedToken -JsonBody $null
    if ($deniedResponse.StatusCode -notin @(401, 403, 404)) {
        Fail-EvidenceGate -Scenario 'denial' -Reason "unexpected-status class=$($deniedResponse.StatusCode)"
    }
    Add-EvidenceResult -Scenario 'denial' -Status 'passed' -EvidenceClass 'live-provider-negative-observation'

    $isolationOwnerPath = Escape-PathSegment $isolationOwner
    $isolationRepositoryPath = Escape-PathSegment $isolationRepository
    $isolationControl = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$isolationOwnerPath/$isolationRepositoryPath" -Token $isolationToken -JsonBody $null
    Assert-SuccessStatus -Response $isolationControl -ExpectedStatus 200 -Scenario 'tenant-isolation-positive-control'
    $isolationControlId = Get-RequiredJsonProperty -Document $isolationControl.Document -Name 'id' -Scenario 'tenant-isolation-positive-control'
    if ($isolationControlId -isnot [long] -or $isolationControlId -le 0) {
        Fail-EvidenceGate -Scenario 'tenant-isolation-positive-control' -Reason 'canonical-identity-evidence-mismatch'
    }

    $isolationResponse = Send-ForgejoRequest -Method ([System.Net.Http.HttpMethod]::Get) -RelativePath "repos/$ownerPath/$bindingRepositoryPath" -Token $isolationToken -JsonBody $null
    if ($isolationResponse.StatusCode -notin @(401, 403, 404)) {
        Fail-EvidenceGate -Scenario 'tenant-isolation' -Reason "unexpected-status class=$($isolationResponse.StatusCode)"
    }
    Add-EvidenceResult -Scenario 'tenant-isolation' -Status 'passed' -EvidenceClass 'live-provider-negative-observation'
    Add-EvidenceResult -Scenario 'boundary' -Status 'passed' -EvidenceClass 'live-provider-https-same-origin-bounded-json'
}
finally {
    if ($null -ne $script:httpClient) {
        $script:httpClient.Dispose()
    }
}

Write-EvidenceReport -Status 'passed'
Write-Host 'FORGEJO-EVIDENCE-PASSED: report=_bmad-output/gates/forgejo-provider-evidence/latest.json diagnostic_policy=metadata-only'
