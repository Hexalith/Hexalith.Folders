using System.Net;
using System.Text;
using System.Text.Json;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoHttpApiClientTests
{
    [Fact]
    public async Task CreateRepositoryDispatchesOneExactMutationAndReturnsCanonicalIdentity()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.Created,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeFalse();
        result.CanonicalRepositoryId.ShouldBe("42");
        RecordedHttpRequest request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsoluteUri.ShouldBe("https://forgejo.example.test/api/v1/orgs/forgejo-owner/repos");
        request.ContentType.ShouldStartWith("application/json");
        using JsonDocument body = JsonDocument.Parse(request.Body.ShouldNotBeNull());
        body.RootElement.GetProperty("name").GetString().ShouldBe("forgejo-repository");
        body.RootElement.GetProperty("private").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("auto_init").GetBoolean().ShouldBeFalse();
        body.RootElement.EnumerateObject().Select(static property => property.Name)
            .ShouldBe(["auto_init", "name", "private"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateRepositoryCancellationBeforeDispatchMakesNoHttpCall()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(HttpStatusCode.Created, """{"id":42}"""));
        ForgejoHttpApiClient client = CreateClient(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            cancellation.Token);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.CancellationBeforeDispatch);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateConflictPerformsOneIdentityObservationAndNeverRetriesMutation()
    {
        RecordingHttpMessageHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.Conflict),
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(Target(expectedCanonicalRepositoryId: "42", equivalentExistingAuthorized: true)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("42");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests[1].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[1].Uri.AbsoluteUri.ShouldBe(
            "https://forgejo.example.test/api/v1/repos/forgejo-owner/forgejo-repository");
    }

    [Fact]
    public async Task DocumentedBadRequestCanReconcileAnAuthorizedExistingIdentity()
    {
        RecordingHttpMessageHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(Target(expectedCanonicalRepositoryId: "42", equivalentExistingAuthorized: true)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.FailureCondition.ToString());
        result.EquivalentExisting.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("42");
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests.Count(static request => request.Method == HttpMethod.Get).ShouldBe(1);
    }

    [Fact]
    public async Task CreateConflictWithoutExactAuthorizedIdentityRemainsConflict()
    {
        RecordingHttpMessageHandler handler = new(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity),
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":43,"name":"forgejo-repository","private":true,"internal":false}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(Target(expectedCanonicalRepositoryId: "42", equivalentExistingAuthorized: true)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.RepositoryConflict);
        handler.Requests.Count(static request => request.Method == HttpMethod.Post).ShouldBe(1);
        handler.Requests.Count(static request => request.Method == HttpMethod.Get).ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AmbiguousMutationResponsesAreUnknownAndNeverRetried(HttpStatusCode statusCode)
    {
        HttpResponseMessage response = new(statusCode);
        response.Headers.TryAddWithoutValidation("Retry-After", "30");
        RecordingHttpMessageHandler handler = new(response);
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.AmbiguousMutationResponse);
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task MalformedCreatedResponseIsAmbiguousAndDoesNotLeakOrRetry()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(HttpStatusCode.Created, """{"id":"repo-secret","name":"wrong"}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.AmbiguousMutationResponse);
        JsonSerializer.Serialize(result).ShouldNotContain("repo-secret", Case.Sensitive);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task MutationTimeoutAfterDispatchIsUnknownAndNeverRetried()
    {
        RecordingHttpMessageHandler handler = new(new TaskCanceledException("provider-secret-timeout"));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.TimeoutDuringMutation);
        JsonSerializer.Serialize(result).ShouldNotContain("provider-secret-timeout", Case.Sensitive);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task ValidateBindingObservesExactRepositoryBranchAndProtection()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}"""),
            JsonResponse(
                HttpStatusCode.OK,
                """{"name":"release/1.0","protected":true,"effective_branch_protection_name":"release/*"}"""),
            JsonResponse(HttpStatusCode.OK, """{"rule_name":"release/*"}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(Target(
                expectedCanonicalRepositoryId: "42",
                equivalentExistingAuthorized: true,
                selectedRef: "release/1.0")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.EquivalentExisting.ShouldBeTrue();
        result.CanonicalRepositoryId.ShouldBe("42");
        handler.Requests.Select(static request => request.Method).ShouldAllBe(static method => method == HttpMethod.Get);
        handler.Requests.Select(static request => request.Uri.AbsoluteUri).ShouldBe(
        [
            "https://forgejo.example.test/api/v1/repos/forgejo-owner/forgejo-repository",
            "https://forgejo.example.test/api/v1/repos/forgejo-owner/forgejo-repository/branches/release%2F1.0",
            "https://forgejo.example.test/api/v1/repos/forgejo-owner/forgejo-repository/branch_protections/release%2F%2A",
        ]);
    }

    [Theory]
    [InlineData("default", "DefaultBranchConflict")]
    [InlineData("contents", "ContentsPermissionInsufficient")]
    [InlineData("administration", "AdministrationPermissionInsufficient")]
    [InlineData("visibility", "RepositoryConflict")]
    public async Task ValidateBindingFailsClosedOnRepositoryPolicyMismatch(
        string mismatch,
        string expectedFailureName)
    {
        string response = mismatch switch
        {
            "default" => """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"trunk","permissions":{"pull":true,"admin":true}}""",
            "contents" => """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":false,"admin":true}}""",
            "administration" => """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":false}}""",
            _ => """{"id":42,"name":"forgejo-repository","private":false,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}""",
        };
        RecordingHttpMessageHandler handler = new(JsonResponse(HttpStatusCode.OK, response));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(Enum.Parse<ForgejoApiFailureCondition>(expectedFailureName));
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task UnsupportedRefKindFailsBeforeAnyProviderObservation()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(HttpStatusCode.OK, """{"id":42}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(Target(refKind: ProviderRepositoryRefKind.Tag)),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.UnsupportedRefOperation);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossOriginRedirectIsRejectedWithoutFollowingTheCredential()
    {
        HttpResponseMessage redirect = new(HttpStatusCode.TemporaryRedirect);
        redirect.Headers.Location = new Uri("https://attacker.example.test/api/v1/repositories");
        RecordingHttpMessageHandler handler = new(redirect);
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.RedirectCrossOrigin);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task OversizedRepositoryObservationFailsClosedBeforeBranchAccess()
    {
        string oversized = "{\"padding\":\"" + new string('a', (256 * 1024) + 1) + "\"}";
        RecordingHttpMessageHandler handler = new(JsonResponse(HttpStatusCode.OK, oversized));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.MalformedResponse);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task ConcreteFactoryAppliesBearerProductHeaderAndCredentialFreeUri()
    {
        const string credential = "provider-secret-bearer-a";
        RecordingHttpMessageHandler handler = new(
            JsonResponse(HttpStatusCode.OK, """{"version":"16.0.3"}"""));
        ForgejoHttpApiClientFactory factory = new(() => new HttpClient(handler));
        await using ForgejoCredentialLease lease = ForgejoCredentialLease.CreateForTesting(credential);
        IForgejoApiClient client = await factory.CreateAsync(
            new ForgejoApiClientRequest(
                ForgejoProviderConstants.ProductHeader,
                new Uri("https://forgejo.example.test/"),
                ForgejoProviderConstants.ApiSurfaceVersion,
                ProviderCredentialMode.UserDelegatedReference,
                "binding-a",
                "correlation-a"),
            lease,
            TestContext.Current.CancellationToken);

        ForgejoReadinessResult result = await client.GetReadinessAsync(
            new ForgejoReadinessRequest(
                "tenant-a",
                "organization-a",
                "binding-a",
                ProviderCredentialMode.UserDelegatedReference,
                ForgejoProviderConstants.ApiSurfaceVersion,
                "16.0.3",
                "safe-target-a",
                "correlation-a"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        RecordedHttpRequest request = handler.Requests.ShouldHaveSingleItem();
        request.AuthorizationScheme.ShouldBe("Bearer");
        request.AuthorizationParameter.ShouldBe(credential);
        request.UserAgent.ShouldBe(ForgejoProviderConstants.ProductHeader);
        request.Uri.AbsoluteUri.ShouldNotContain(credential, Case.Sensitive);
        request.Uri.Query.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"version\":42}")]
    [InlineData("[]")]
    public async Task ReadinessMapsMalformedVersionEvidenceWithoutThrowing(string responseBody)
    {
        RecordingHttpMessageHandler handler = new(JsonResponse(HttpStatusCode.OK, responseBody));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoReadinessResult result = await client.GetReadinessAsync(
            new ForgejoReadinessRequest(
                "tenant-a",
                "organization-a",
                "binding-a",
                ProviderCredentialMode.UserDelegatedReference,
                ForgejoProviderConstants.ApiSurfaceVersion,
                "16.0.3",
                "safe-target-a",
                "correlation-a"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.MalformedResponse);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ObservationRequiresExactOkStatus()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.Accepted,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.MalformedResponse);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WrongReturnedBranchNameFailsAsMissingBranch()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}"""),
            JsonResponse(
                HttpStatusCode.OK,
                """{"name":"other","protected":true,"effective_branch_protection_name":"main"}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.MissingBranchOrPath);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WrongReturnedProtectionRuleFailsAsProtectionConflict()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}"""),
            JsonResponse(
                HttpStatusCode.OK,
                """{"name":"main","protected":true,"effective_branch_protection_name":"main"}"""),
            JsonResponse(HttpStatusCode.OK, """{"rule_name":"other"}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.BranchProtectionConflict);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task UnboundedProtectionRuleNameFailsBeforeProtectionRequest()
    {
        string unboundedProtectionName = new('x', 257);
        RecordingHttpMessageHandler handler = new(
            JsonResponse(
                HttpStatusCode.OK,
                """{"id":42,"name":"forgejo-repository","private":true,"internal":false,"default_branch":"main","permissions":{"pull":true,"admin":true}}"""),
            JsonResponse(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    name = "main",
                    @protected = true,
                    effective_branch_protection_name = unboundedProtectionName,
                })));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(
            BindingRequest(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.BranchProtectionConflict);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InvalidExpectedCanonicalIdentityFailsBeforeCreateDispatch()
    {
        RecordingHttpMessageHandler handler = new(
            JsonResponse(HttpStatusCode.Created, """{"id":42}"""));
        ForgejoHttpApiClient client = CreateClient(handler);

        ForgejoRepositoryCreationResult result = await client.CreateRepositoryAsync(
            CreationRequest(Target(expectedCanonicalRepositoryId: "not-numeric")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.FailureCondition.ShouldBe(ForgejoApiFailureCondition.ValidationFailure);
        handler.Requests.ShouldBeEmpty();
    }

    private static ForgejoHttpApiClient CreateClient(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://forgejo.example.test/"),
        };
        return new ForgejoHttpApiClient(httpClient, httpClient.BaseAddress);
    }

    private static ForgejoRepositoryCreationRequest CreationRequest(ProviderRepositoryResolvedTarget? target = null)
        => new(
            "tenant-a",
            "organization-a",
            "binding-a",
            "repository-binding-a",
            target ?? Target(),
            ProviderCredentialMode.UserDelegatedReference,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "16.0.3",
            "safe-target-a",
            "correlation-a",
            "idempotency-a");

    private static ForgejoRepositoryBindingRequest BindingRequest(ProviderRepositoryResolvedTarget? target = null)
        => new(
            "tenant-a",
            "organization-a",
            "binding-a",
            "repository-binding-a",
            target ?? Target(),
            ProviderCredentialMode.UserDelegatedReference,
            ForgejoProviderConstants.ApiSurfaceVersion,
            "16.0.3",
            "safe-target-a",
            "correlation-a",
            "idempotency-a");

    private static ProviderRepositoryResolvedTarget Target(
        string? expectedCanonicalRepositoryId = null,
        bool equivalentExistingAuthorized = false,
        ProviderRepositoryRefKind refKind = ProviderRepositoryRefKind.Branch,
        string selectedRef = "main")
        => new(
            Owner: "forgejo-owner",
            RepositoryName: "forgejo-repository",
            Visibility: ProviderRepositoryVisibility.Private,
            DefaultBranch: "main",
            SelectedRef: selectedRef,
            RequireProtectedRef: true,
            RequireContentsPermission: true,
            RequireAdministrationPermission: true,
            ExpectedCanonicalRepositoryId: expectedCanonicalRepositoryId,
            EquivalentExistingAuthorized: equivalentExistingAuthorized,
            SelectedRefKind: refKind);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed record RecordedHttpRequest(
        HttpMethod Method,
        Uri Uri,
        string? Body,
        string? ContentType,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? UserAgent);

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> _outcomes;

        public RecordingHttpMessageHandler(params object[] outcomes)
        {
            _outcomes = new Queue<object>(outcomes);
        }

        public List<RecordedHttpRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string? body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new RecordedHttpRequest(
                request.Method,
                request.RequestUri.ShouldNotBeNull(),
                body,
                request.Content?.Headers.ContentType?.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.UserAgent.ToString()));

            object outcome = _outcomes.Dequeue();
            return outcome switch
            {
                HttpResponseMessage response => response,
                Exception exception => throw exception,
                _ => throw new InvalidOperationException("Unsupported HTTP test outcome."),
            };
        }
    }
}
