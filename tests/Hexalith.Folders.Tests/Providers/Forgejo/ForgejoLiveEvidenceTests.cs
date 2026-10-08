using System.Text.Json;

using Hexalith.Folders;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoLiveEvidenceTests
{
    private static readonly IReadOnlyDictionary<string, string> EvidenceClasses =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["positive"] = "live-provider-mutation-and-observation",
            ["conflict"] = "live-provider-controlled-conflict",
            ["denial"] = "live-provider-authentication-or-permission-denial",
            ["tenant-isolation"] = "live-provider-authentication-or-permission-denial",
            ["binding-ref"] = "live-provider-observation",
            ["version"] = "live-provider-observation",
            ["boundary"] = "live-provider-https-same-origin-bounded-json",
            ["replay"] = "production-provider-admission",
            ["known-failure"] = "production-provider-admission",
            ["timeout-unknown"] = "production-provider-admission",
            ["cancellation"] = "production-provider-admission",
            ["durable-boundary"] = "production-provider-admission",
            ["unknown-credential"] = "production-provider-admission",
        };

    [Fact]
    public async Task ProductionProviderEmitsMetadataOnlyEvidence()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("HEXALITH_FORGEJO_EVIDENCE_APPROVAL"), "approved-isolated", StringComparison.Ordinal))
        {
            Assert.Skip("Forgejo live evidence is an operator lane.");
        }

        string baseUrl = Required("HEXALITH_FORGEJO_EVIDENCE_BASE_URL");
        string positiveToken = Required("HEXALITH_FORGEJO_EVIDENCE_TOKEN");
        string deniedToken = Required("HEXALITH_FORGEJO_EVIDENCE_DENIED_TOKEN");
        string isolationToken = Required("HEXALITH_FORGEJO_EVIDENCE_ISOLATION_TOKEN");
        string owner = Required("HEXALITH_FORGEJO_EVIDENCE_OWNER");
        string bindRepository = Required("HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY");
        string bindRepositoryId = Required("HEXALITH_FORGEJO_EVIDENCE_BIND_REPOSITORY_ID");
        string bindBranch = Required("HEXALITH_FORGEJO_EVIDENCE_BIND_BRANCH");
        string bindVisibility = Required("HEXALITH_FORGEJO_EVIDENCE_BIND_VISIBILITY");
        string expectedVersion = Required("HEXALITH_FORGEJO_EVIDENCE_EXPECTED_VERSION");
        string isolationOwner = Required("HEXALITH_FORGEJO_EVIDENCE_ISOLATION_OWNER");
        string isolationRepository = Required("HEXALITH_FORGEJO_EVIDENCE_ISOLATION_REPOSITORY");
        (positiveToken == deniedToken || positiveToken == isolationToken || deniedToken == isolationToken)
            .ShouldBeFalse();
        expectedVersion.ShouldBeOneOf("16.0.3", "15.0.7");

        CountingForgejoCredentialResolver credentials = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["credential-positive"] = positiveToken,
            ["credential-denied"] = deniedToken,
            ["credential-isolation"] = isolationToken,
        });
        CountingTargetResolver targets = new();
        ServiceCollection services = new();
        services.AddFoldersProviderReadiness();
        services.RemoveAll<IForgejoCredentialResolver>();
        services.AddSingleton<IForgejoCredentialResolver>(credentials);
        services.RemoveAll<IProviderRepositoryTargetResolver>();
        services.AddSingleton<IProviderRepositoryTargetResolver>(targets);
        await using ServiceProvider provider = services.BuildServiceProvider();
        IGitProvider git = provider.GetServices<IGitProvider>().Single(static candidate => candidate.ProviderFamily == "forgejo");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        List<EvidenceRow> rows = [];

        try
        {
            ProviderCapabilityDiscoveryResult version = await git.DiscoverCapabilitiesAsync(
                Discovery("tenant-positive", "credential-positive", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            version.IsSuccess.ShouldBeTrue(version.ReasonCode);
            version.Profile.ShouldNotBeNull().Evidence["forgejo_product_version"].ShouldBe(expectedVersion);
            rows.Add(Row("version"));

            string repositoryName = "hexalith-evidence-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            targets.Creation = Target(owner, repositoryName, ProviderRepositoryVisibility.Private, "main", protect: false);
            ProviderRepositoryCreationResult created = await git.CreateRepositoryAsync(
                Creation("tenant-positive", "credential-positive", baseUrl, expectedVersion, FreshAdmission()),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            created.IsSuccess.ShouldBeTrue(created.ReasonCode);
            created.CanonicalRepositoryId.ShouldNotBeNullOrWhiteSpace();
            rows.Add(Row("positive"));
            rows.Add(Row("boundary"));

            int callsAfterCreate = targets.CreationCalls;
            targets.Creation = Target(
                owner,
                repositoryName,
                ProviderRepositoryVisibility.Private,
                "main",
                protect: false,
                expectedId: created.CanonicalRepositoryId,
                equivalent: true);
            ProviderRepositoryCreationResult conflict = await git.CreateRepositoryAsync(
                Creation("tenant-positive", "credential-positive", baseUrl, expectedVersion, FreshAdmission("intent-conflict")),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            conflict.IsSuccess.ShouldBeTrue(conflict.ReasonCode);
            conflict.EquivalentExisting.ShouldBeTrue();
            conflict.CanonicalRepositoryId.ShouldBe(created.CanonicalRepositoryId);
            targets.CreationCalls.ShouldBe(callsAfterCreate + 1);
            rows.Add(Row("conflict"));

            ProviderRepositoryCreationResult replay = await git.CreateRepositoryAsync(
                Creation(
                    "tenant-positive",
                    "credential-positive",
                    baseUrl,
                    expectedVersion,
                    Replay(created.CanonicalRepositoryId!)),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            replay.IsSuccess.ShouldBeTrue(replay.ReasonCode);
            replay.CanonicalRepositoryId.ShouldBe(created.CanonicalRepositoryId);
            targets.CreationCalls.ShouldBe(callsAfterCreate + 1);
            rows.Add(Row("replay"));

            ProviderRepositoryCreationResult knownFailure = await git.CreateRepositoryAsync(
                Creation("tenant-positive", "credential-positive", baseUrl, expectedVersion, KnownFailureReplay()),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            knownFailure.IsSuccess.ShouldBeFalse();
            knownFailure.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConflict);
            knownFailure.ReasonCode.ShouldBe("forgejo_repository_conflict");
            knownFailure.Retryable.ShouldBeFalse();
            targets.CreationCalls.ShouldBe(callsAfterCreate + 1);
            rows.Add(Row("known-failure"));

            ProviderRepositoryCreationResult unknown = await git.CreateRepositoryAsync(
                Creation("tenant-positive", "credential-positive", baseUrl, expectedVersion, UnknownReplay()),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            unknown.FailureCategory.ShouldBe(ProviderFailureCategory.UnknownProviderOutcome);
            unknown.Retryable.ShouldBeFalse();
            targets.CreationCalls.ShouldBe(callsAfterCreate + 1);
            rows.Add(Row("timeout-unknown"));

            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await git.CreateRepositoryAsync(
                    Creation("tenant-positive", "credential-positive", baseUrl, expectedVersion, FreshAdmission("intent-cancel")),
                    cancelled.Token).ConfigureAwait(true)).ConfigureAwait(true);
            rows.Add(Row("cancellation"));

            ProviderRepositoryCreationResult malformed = await git.CreateRepositoryAsync(
                Creation(
                    "tenant-positive",
                    "credential-positive",
                    baseUrl,
                    expectedVersion,
                    new ProviderIdempotencyAdmission(ProviderIdempotencyDisposition.Fresh, " ")),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            malformed.IsSuccess.ShouldBeFalse();
            malformed.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderValidationFailed);
            targets.CreationCalls.ShouldBe(callsAfterCreate + 1);
            rows.Add(Row("durable-boundary"));

            int credentialCalls = credentials.Calls;
            ProviderRepositoryCreationResult unknownCredential = await git.CreateRepositoryAsync(
                Creation("tenant-positive", "credential-unknown", baseUrl, expectedVersion, FreshAdmission("intent-unknown-credential")),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            unknownCredential.IsSuccess.ShouldBeFalse();
            unknownCredential.FailureCategory.ShouldBe(ProviderFailureCategory.ProviderConfigurationMissing);
            credentials.Calls.ShouldBe(credentialCalls + 1);
            targets.CreationCalls.ShouldBe(callsAfterCreate + 2);
            rows.Add(Row("unknown-credential"));

            (isolationOwner == owner && isolationRepository == bindRepository).ShouldBeFalse();
            ProviderCapabilityDiscoveryResult deniedAuthenticated = await git.DiscoverCapabilitiesAsync(
                Discovery("tenant-denied", "credential-denied", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            deniedAuthenticated.IsSuccess.ShouldBeTrue(deniedAuthenticated.ReasonCode);
            targets.Binding = Target(
                owner,
                bindRepository,
                Visibility(bindVisibility),
                bindBranch,
                protect: true,
                expectedId: bindRepositoryId);
            ProviderRepositoryBindingResult denied = await git.ValidateRepositoryBindingAsync(
                Binding("tenant-denied", "credential-denied", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            denied.IsSuccess.ShouldBeFalse();
            denied.FailureCategory.ShouldBeOneOf(
                ProviderFailureCategory.ProviderAuthenticationRequired,
                ProviderFailureCategory.ProviderPermissionInsufficient);
            rows.Add(Row("denial"));

            ProviderCapabilityDiscoveryResult isolationAuthenticated = await git.DiscoverCapabilitiesAsync(
                Discovery("tenant-isolation", "credential-isolation", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            isolationAuthenticated.IsSuccess.ShouldBeTrue(isolationAuthenticated.ReasonCode);
            targets.Binding = Target(
                owner,
                bindRepository,
                Visibility(bindVisibility),
                bindBranch,
                protect: true,
                expectedId: bindRepositoryId);
            ProviderRepositoryBindingResult isolated = await git.ValidateRepositoryBindingAsync(
                Binding("tenant-isolation", "credential-isolation", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            isolated.IsSuccess.ShouldBeFalse();
            isolated.FailureCategory.ShouldBeOneOf(
                ProviderFailureCategory.ProviderAuthenticationRequired,
                ProviderFailureCategory.ProviderPermissionInsufficient);
            rows.Add(Row("tenant-isolation"));

            ProviderRepositoryBindingResult bound = await git.ValidateRepositoryBindingAsync(
                Binding("tenant-positive", "credential-positive", baseUrl, expectedVersion),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            bound.IsSuccess.ShouldBeTrue(bound.ReasonCode);
            bound.CanonicalRepositoryId.ShouldBe(bindRepositoryId);
            rows.Add(Row("binding-ref"));

            WriteReport("passed", started, rows);
        }
        catch
        {
            WriteReport("failed", started, rows);
            throw;
        }
    }

    private static EvidenceRow Row(string scenario)
        => new(scenario, "passed", EvidenceClasses[scenario]);

    private static void WriteReport(string status, DateTimeOffset started, IReadOnlyList<EvidenceRow> rows)
    {
        string directory = Path.Combine(FindRepositoryRoot(), "_bmad-output", "gates", "forgejo-provider-evidence");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "latest.json");
        var payload = new
        {
            gate = "forgejo-provider-evidence",
            schema_version = "forgejo-provider-evidence-v1",
            status,
            diagnostic_policy = "metadata-only",
            execution_class = "operator-approved-isolated-deployment",
            supported_versions = new[] { "16.0.3", "15.0.7" },
            report_path = "_bmad-output/gates/forgejo-provider-evidence/latest.json",
            elapsed_ms = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            results = rows.Select(static row => new
            {
                scenario = row.Scenario,
                status = row.Status,
                evidence_class = row.EvidenceClass,
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("A required Forgejo evidence reference is missing.");
        }

        return value;
    }

    private static ProviderRepositoryVisibility Visibility(string value)
        => value switch
        {
            "public" => ProviderRepositoryVisibility.Public,
            "private" => ProviderRepositoryVisibility.Private,
            "internal" => ProviderRepositoryVisibility.Internal,
            _ => throw new InvalidOperationException("Binding visibility is not supported."),
        };

    private static ProviderRepositoryResolvedTarget Target(
        string owner,
        string repository,
        ProviderRepositoryVisibility visibility,
        string branch,
        bool protect,
        string? expectedId = null,
        bool equivalent = false)
        => new(
            owner,
            repository,
            visibility,
            branch,
            branch,
            protect,
            RequireContentsPermission: true,
            RequireAdministrationPermission: true,
            expectedId,
            equivalent);

    private static ProviderIdempotencyAdmission FreshAdmission(string intent = "intent-live")
        => new(ProviderIdempotencyDisposition.Fresh, intent);

    private static ProviderIdempotencyAdmission Replay(string canonicalRepositoryId)
        => new(
            ProviderIdempotencyDisposition.EquivalentReplay,
            "intent-replay",
            PriorSafeOutcomeFingerprint: new string('a', 64),
            PriorOperationReference: "operation-replay",
            PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Success,
            PriorCanonicalRepositoryId: canonicalRepositoryId);

    private static ProviderIdempotencyAdmission KnownFailureReplay()
        => new(
            ProviderIdempotencyDisposition.EquivalentReplay,
            "intent-known-failure",
            PriorSafeOutcomeFingerprint: new string('b', 64),
            PriorOperationReference: "operation-known-failure",
            PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.KnownFailure,
            PriorFailureCategory: ProviderFailureCategory.ProviderConflict,
            PriorReasonCode: "forgejo_repository_conflict",
            PriorRemediationCode: "provider_conflict_remediation");

    private static ProviderIdempotencyAdmission UnknownReplay()
        => new(
            ProviderIdempotencyDisposition.EquivalentReplay,
            "intent-unknown",
            PriorOperationReference: "operation-unknown",
            PriorOutcomeDisposition: ProviderPriorOutcomeDisposition.Unknown,
            PriorReconciliationReference: "reconciliation-unknown");

    private static ProviderCapabilityDiscoveryRequest Discovery(
        string tenantId,
        string credentialReferenceId,
        string baseUrl,
        string version)
        => new(
            tenantId,
            "organization-live",
            "binding-live",
            credentialReferenceId,
            "forgejo",
            "forgejo",
            "v1",
            Evidence(baseUrl, version, "readiness"),
            [ProviderCredentialMode.UserDelegatedReference],
            Authorization(),
            "correlation-live");

    private static ProviderRepositoryCreationRequest Creation(
        string tenantId,
        string credentialReferenceId,
        string baseUrl,
        string version,
        ProviderIdempotencyAdmission admission)
        => new(
            tenantId,
            "organization-live",
            "binding-live",
            credentialReferenceId,
            "repository-binding-live",
            "forgejo",
            "forgejo",
            Evidence(baseUrl, version, "repository_creation"),
            [ProviderCredentialMode.UserDelegatedReference],
            Authorization(),
            "correlation-live",
            "idempotency-live",
            admission);

    private static ProviderRepositoryBindingRequest Binding(
        string tenantId,
        string credentialReferenceId,
        string baseUrl,
        string version)
        => new(
            tenantId,
            "organization-live",
            "binding-live",
            credentialReferenceId,
            "repository-binding-live",
            "external-repository-live",
            new string('c', 64),
            "branch-policy-live",
            "forgejo",
            "forgejo",
            Evidence(baseUrl, version, "existing_repository_binding"),
            [ProviderCredentialMode.UserDelegatedReference],
            Authorization(),
            "correlation-live",
            "idempotency-live",
            FreshAdmission("intent-bind"));

    private static ProviderAuthorizationEvidenceSnapshot Authorization()
        => new("authz-live-fingerprint", DateTimeOffset.UtcNow, "fresh");

    private static ProviderTargetEvidence Evidence(string baseUrl, string version, string scope)
        => new(
            "forgejo",
            version,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "provider_binding_v1",
            false,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authorized_base_url"] = baseUrl,
                ["operation_scope"] = scope,
                ["safe_target_fingerprint"] = "safe-target-live",
            });

    private sealed record EvidenceRow(string Scenario, string Status, string EvidenceClass);

    private sealed class CountingForgejoCredentialResolver(IReadOnlyDictionary<string, string> tokens) : IForgejoCredentialResolver
    {
        public int Calls { get; private set; }

        public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
            ForgejoCredentialResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (!tokens.TryGetValue(request.CredentialReferenceId, out string? token))
            {
                return ValueTask.FromResult(ForgejoCredentialResolutionResult.Failure(
                    ProviderFailureCategory.ProviderConfigurationMissing,
                    "forgejo_credential_reference_unknown"));
            }

            return ValueTask.FromResult(ForgejoCredentialResolutionResult.Success(
                ForgejoCredentialLease.CreateForTesting(token)));
        }
    }

    private sealed class CountingTargetResolver : IProviderRepositoryTargetResolver
    {
        public ProviderRepositoryResolvedTarget? Creation { get; set; }

        public ProviderRepositoryResolvedTarget? Binding { get; set; }

        public int CreationCalls { get; private set; }

        public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveCreationAsync(
            ProviderRepositoryCreationTargetResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreationCalls++;
            return ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Success(Creation!));
        }

        public ValueTask<ProviderRepositoryTargetResolutionResult> ResolveBindingAsync(
            ProviderRepositoryBindingTargetResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ProviderRepositoryTargetResolutionResult.Success(Binding!));
        }
    }
}
