using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.FrontComposer.Contracts.Lifecycle;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Services.Gateways;

public sealed class TenantCommandGatewayTests
{
    [Theory]
    [InlineData("create", 408)]
    [InlineData("create", 429)]
    [InlineData("create", 500)]
    [InlineData("create", 503)]
    [InlineData("create", 0)]
    [InlineData("create", -1)]
    [InlineData("create", -2)]
    [InlineData("create", -3)]
    [InlineData("create", -4)]
    [InlineData("add", 408)]
    [InlineData("add", 429)]
    [InlineData("add", 500)]
    [InlineData("add", 503)]
    [InlineData("add", 0)]
    [InlineData("add", -1)]
    [InlineData("add", -2)]
    [InlineData("add", -3)]
    [InlineData("add", -4)]
    [InlineData("change", 408)]
    [InlineData("change", 429)]
    [InlineData("change", 500)]
    [InlineData("change", 503)]
    [InlineData("change", 0)]
    [InlineData("change", -1)]
    [InlineData("change", -2)]
    [InlineData("change", -3)]
    [InlineData("change", -4)]
    public async Task AmbiguousTenantDeliveryKeepsTheOriginalUlidAndPayload(string kind, int statusCode)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        Exception failure = statusCode switch
        {
            0 => new HttpRequestException("unsafe transport payload"),
            -1 => new TimeoutException("unsafe transport payload"),
            -2 => new JsonException("unsafe transport payload"),
            -3 => new OperationCanceledException("unsafe transport payload"),
            -4 => new TaskCanceledException("unsafe transport payload"),
            _ => new EventStoreGatewayException(statusCode, "unsafe transport payload"),
        };
        CapturingGatewayClient client = new(failure);
        TenantCommandGateway gateway = CreateGateway(client);
        TenantCommandSubmissionResult result = kind switch
        {
            "create" => await gateway.CreateTenantAsync(new("tenant.alpha", "Original", "Original intent"), messageId),
            "add" => await gateway.AddUserToTenantAsync(new("tenant.alpha", "literal-user", TenantRole.TenantReader), messageId),
            _ => await gateway.ChangeUserRoleAsync(new("tenant.alpha", "literal-user", TenantRole.TenantContributor), messageId),
        };
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.CorrelationId.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNull().ShouldNotContain("unsafe transport payload");
        string expectedKey = (kind, statusCode == 503) switch
        {
            ("create", true) => "Tenants.Create.Submission.Unavailable",
            ("create", false) => "Tenants.Create.Submission.Failed",
            ("add", true) => "Tenants.AddMember.Submission.Unavailable",
            ("add", false) => "Tenants.AddMember.Submission.Failed",
            ("change", true) => "Tenants.ChangeRole.Submission.Unavailable",
            _ => "Tenants.ChangeRole.Submission.Failed",
        };
        result.SafeMessageKey.ShouldBe(expectedKey);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        ResourceManager resources = new("Hexalith.Tenants.UI.Resources.TenantsResources", typeof(TenantsResources).Assembly);
        resources.GetString(expectedKey, CultureInfo.GetCultureInfo("en")).ShouldBe(result.SafeMessage);
        SubmitCommandRequest dispatched = client.SubmittedCommands.ShouldHaveSingleItem();
        dispatched.MessageId.ShouldBe(messageId);
        dispatched.AggregateId.ShouldBe("tenant.alpha");
        dispatched.Domain.ShouldBe("tenants");
        CapturingGatewayClient retryClient = new(new SubmitCommandResponse("correlation-original"));
        TenantCommandGateway retry = CreateGateway(retryClient);
        TenantCommandSubmissionResult accepted = kind switch
        {
            "create" => await retry.CreateTenantAsync(new("tenant.alpha", "Original", "Original intent"), result.MessageId),
            "add" => await retry.AddUserToTenantAsync(new("tenant.alpha", "literal-user", TenantRole.TenantReader), result.MessageId),
            _ => await retry.ChangeUserRoleAsync(new("tenant.alpha", "literal-user", TenantRole.TenantContributor), result.MessageId),
        };
        accepted.MessageId.ShouldBe(messageId);
        accepted.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        SubmitCommandRequest replay = retryClient.SubmittedCommands.ShouldHaveSingleItem();
        replay.MessageId.ShouldBe(dispatched.MessageId);
        replay.Tenant.ShouldBe(dispatched.Tenant);
        replay.Domain.ShouldBe(dispatched.Domain);
        replay.AggregateId.ShouldBe(dispatched.AggregateId);
        replay.CommandType.ShouldBe(dispatched.CommandType);
        replay.Payload.GetRawText().ShouldBe(dispatched.Payload.GetRawText());
    }

    [Fact]
    public void FixedScopeGatewayCapabilitiesFailClosedOrAdvertiseOnlyConcreteSupport()
    {
        TenantCommandGateway configured = CreateLifecycleGateway(new SubmitCommandResponse("correlation"));
        var unavailable = new UnavailableTenantCommandGateway();

        configured.SupportsGlobalAdministratorDispatch.ShouldBeTrue();
        configured.SupportsTrackedGlobalAdministratorDispatch.ShouldBeTrue();
        configured.SupportsTrackedGlobalAdministratorRemoveDispatch.ShouldBeTrue();
        configured.SupportsCommandStatusLookup.ShouldBeTrue();
        unavailable.SupportsGlobalAdministratorDispatch.ShouldBeFalse();
        unavailable.SupportsTrackedGlobalAdministratorDispatch.ShouldBeFalse();
        unavailable.SupportsTrackedGlobalAdministratorRemoveDispatch.ShouldBeFalse();
        unavailable.SupportsCommandStatusLookup.ShouldBeFalse();
    }

    [Fact]
    public async Task TrackedGlobalAdministratorGrantPreservesExactLiteralAndCallerUlid()
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        const string target = "  User/CaseSensitive.01  ";
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = CreateGateway(client);

        TenantCommandSubmissionResult result = await gateway.SetGlobalAdministratorTrackedAsync(
            new SetGlobalAdministrator(target),
            messageId,
            CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe(messageId);
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("global-administrators");
        submitted.AggregateId.ShouldBe("global-administrators");
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe(target);
        result.MessageId.ShouldBe(messageId);
    }

    [Theory]
    [InlineData("not-a-ulid")]
    [InlineData("01arz3ndektsv4rrffq69g5fav")]
    [InlineData(" 01ARZ3NDEKTSV4RRFFQ69G5FAV")]
    public async Task TrackedGlobalAdministratorGrantRejectsNonCanonicalMessageId(string messageId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-unused"));

        TenantCommandSubmissionResult result = await CreateGateway(client)
            .SetGlobalAdministratorTrackedAsync(
                new SetGlobalAdministrator("target-admin"),
                messageId,
                CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe("Tenants.Commands.Unavailable.InvalidTrackingReference");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task SameIdRedispatchUsesNoReplacementIdentity()
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = CreateGateway(client);
        var request = new SetGlobalAdministrator("target-admin");

        _ = await gateway.SetGlobalAdministratorTrackedAsync(request, messageId, CancellationToken.None);
        _ = await gateway.SetGlobalAdministratorTrackedAsync(request, messageId, CancellationToken.None);

        client.SubmittedCommands.Count.ShouldBe(2);
        client.SubmittedCommands.ShouldAllBe(command => command.MessageId == messageId);
    }

    public static IEnumerable<object[]> AmbiguousGrantExceptions()
    {
        foreach (int statusCode in new[] { 408, 429, 500, 503, 599 })
        {
            foreach (bool? retryable in new bool?[] { null, false, true })
            {
                yield return [new EventStoreGatewayException(statusCode, "InsufficientPermissionsRejection",
                    detail: $"backend-secret-Canary status={statusCode} retryable={retryable?.ToString() ?? "null"}", retryable: retryable)];
            }
        }

        yield return [new EventStoreGatewayException(400, "InsufficientPermissionsRejection",
            detail: "backend-secret-Canary status=400 retryable=True", retryable: true)];
        yield return [new EventStoreGatewayException(409, "InsufficientPermissionsRejection",
            detail: "backend-secret-Canary status=409 retryable=True", retryable: true)];
        yield return [new OperationCanceledException("backend-secret-Canary")];
        yield return [new TaskCanceledException("backend-secret-Canary")];
        yield return [new HttpRequestException("backend-secret-Canary")];
        yield return [new TimeoutException("backend-secret-Canary")];
    }

    private static readonly string[] _trackedOperations =
        ["set-tracked", "remove-configuration-tracked", "enable-tracked", "disable-tracked", "grant-tracked", "remove-global-tracked"];

    public static IEnumerable<object[]> TrackedAmbiguousCases()
    {
        foreach (string operation in _trackedOperations)
        {
            foreach (object[] input in AmbiguousGrantExceptions())
            {
                foreach (bool faultedTask in new[] { false, true })
                {
                    yield return [operation, input[0], faultedTask];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TrackedAmbiguousCases))]
    public async Task TrackedAmbiguityRetainsExactEnvelopeForExplicitRetry(
        string operation, Exception exception, bool faultedTask)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FB0";
        List<SubmitCommandRequest> dispatched = [];
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = CreateTrackedFailureGateway(exception, faultedTask, dispatched, ulids, acceptRetry: true);

        TenantCommandSubmissionResult result = await SubmitOperation(gateway, operation, messageId: messageId);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.CorrelationId.ShouldBeNull();
        result.RejectionCode.ShouldBeNull();
        result.SafeMessage.ShouldBeNull();
        (string key, string english, string french) = TrackedAmbiguityMessage(operation);
        result.SafeMessageKey.ShouldBe(key);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        ResourceManager resources = new("Hexalith.Tenants.UI.Resources.TenantsResources", typeof(TenantsResources).Assembly);
        resources.GetString(key, CultureInfo.GetCultureInfo("en")).ShouldBe(english);
        resources.GetString(key, CultureInfo.GetCultureInfo("fr")).ShouldBe(french);
        JsonSerializer.Serialize(result).ShouldNotContain("backend-secret-Canary");
        SubmitCommandRequest original = dispatched.ShouldHaveSingleItem();
        original.MessageId.ShouldBe(messageId);
        original.Tenant.ShouldBe("system");
        original.Domain.ShouldBe(operation is "grant-tracked" or "remove-global-tracked" ? "global-administrators" : "tenants");
        original.AggregateId.ShouldBe(operation is "grant-tracked" or "remove-global-tracked" ? "global-administrators" : "tenant.alpha");

        TenantCommandSubmissionResult accepted = await SubmitOperation(gateway, operation, messageId: result.MessageId!);

        accepted.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        accepted.MessageId.ShouldBe(messageId);
        accepted.CorrelationId.ShouldBe("correlation-retry");
        accepted.IsAmbiguousFailure.ShouldBeFalse();
        dispatched.Count.ShouldBe(2);
        SubmitCommandRequest replay = dispatched[1];
        replay.MessageId.ShouldBe(original.MessageId);
        replay.Tenant.ShouldBe(original.Tenant);
        replay.Domain.ShouldBe(original.Domain);
        replay.AggregateId.ShouldBe(original.AggregateId);
        replay.CommandType.ShouldBe(original.CommandType);
        replay.Payload.GetRawText().ShouldBe(original.Payload.GetRawText());
        ulids.CallCount.ShouldBe(0);
    }

    public static IEnumerable<object[]> TrackedCancellationAndPropagationCases()
    {
        foreach (string operation in _trackedOperations)
        {
            foreach (Exception exception in new Exception[]
            {
                new OperationCanceledException("backend-secret-Canary"),
                new TaskCanceledException("backend-secret-Canary"),
                new JsonException("backend-secret-Canary"),
                new InvalidOperationException("backend-secret-Canary"),
            })
            {
                foreach (bool faultedTask in new[] { false, true })
                {
                    yield return [operation, exception, faultedTask, true];
                    if (exception is not OperationCanceledException)
                    {
                        yield return [operation, exception, faultedTask, false];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TrackedCancellationAndPropagationCases))]
    public async Task TrackedDispatchPreservesCallerCancellationAndUnrelatedExceptions(
        string operation, Exception exception, bool faultedTask, bool callerCanceled)
    {
        List<SubmitCommandRequest> dispatched = [];
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = CreateTrackedFailureGateway(exception, faultedTask, dispatched, ulids);
        using var cancellation = new CancellationTokenSource();
        if (callerCanceled)
        {
            cancellation.Cancel();
        }

        Exception? actual = await Record.ExceptionAsync(() => SubmitOperation(
            gateway, operation, cancellationToken: cancellation.Token));

        actual.ShouldBeSameAs(exception);
        dispatched.ShouldHaveSingleItem();
        ulids.CallCount.ShouldBe(0);
    }

    /// <summary>Caller cancellation during an awaited transport task preserves the forwarded token and single dispatch.</summary>
    [Fact]
    public async Task TrackedDispatchPropagatesCallerCancellationWhileAwaitingTransport()
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        List<SubmitCommandRequest> dispatched = [];
        var transport = new TaskCompletionSource<SubmitCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        CancellationToken forwardedToken = default;
        IEventStoreGatewayClient client = Substitute.For<IEventStoreGatewayClient>();
        client.SubmitCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            dispatched.Add(call.Arg<SubmitCommandRequest>());
            forwardedToken = call.Arg<CancellationToken>();
            return transport.Task;
        });
        StubUlidFactory ulids = new(messageId);
        using HttpClient statusClient = new(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") };
        TenantCommandGateway gateway = new(client, ulids, statusClient);

        Task<TenantCommandSubmissionResult> submission = gateway.SetTenantConfigurationTrackedAsync(
            new SetTenantConfiguration("tenant.alpha", "key", "value"), messageId, cancellation.Token);

        dispatched.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
        forwardedToken.ShouldBe(cancellation.Token);
        submission.IsCompleted.ShouldBeFalse();

        cancellation.Cancel();
        transport.SetCanceled(cancellation.Token);
        Exception? actual = await Record.ExceptionAsync(() => submission);

        actual.ShouldBeOfType<TaskCanceledException>().CancellationToken.ShouldBe(cancellation.Token);
        dispatched.ShouldHaveSingleItem();
        ulids.CallCount.ShouldBe(0);
    }

    public static IEnumerable<object[]> TrackedTerminalCases()
    {
        foreach (string operation in _trackedOperations)
        {
            foreach (int statusCode in new[] { 400, 401, 403, 409, 418 })
            {
                foreach (bool faultedTask in new[] { false, true })
                {
                    yield return [operation, statusCode, faultedTask];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TrackedTerminalCases))]
    public async Task TrackedNonretryableHttpFailuresKeepPermanentOperationMappings(
        string operation, int statusCode, bool faultedTask)
    {
        List<SubmitCommandRequest> dispatched = [];
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = CreateTrackedFailureGateway(new EventStoreGatewayException(
            statusCode, "backend-secret-Canary", retryable: false), faultedTask, dispatched, ulids);

        TenantCommandSubmissionResult result = await SubmitOperation(gateway, operation);

        bool unauthorized = statusCode is 401 or 403;
        result.State.ShouldBe(unauthorized ? TenantCommandLifecycleState.Rejected : TenantCommandLifecycleState.Failed);
        result.IsAmbiguousFailure.ShouldBeFalse();
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBeNull();
        result.RejectionCode.ShouldBe(unauthorized ? "InsufficientPermissions" : null);
        string key = operation switch
        {
            "set-tracked" => "Tenants.Configuration.Set." + (unauthorized ? "Unavailable.Authorization" : statusCode == 400 ? "Submission.Invalid" : "Submission.VerificationFailed"),
            "remove-configuration-tracked" => "Tenants.Configuration.Remove." + (unauthorized ? "Unavailable.Authorization" : statusCode == 400 ? "Submission.Invalid" : "Submission.VerificationFailed"),
            "grant-tracked" => "Tenants.GlobalAdministrators.Grant.Submission." + (unauthorized ? "Rejected" : statusCode == 400 ? "Invalid" : "Failed"),
            "remove-global-tracked" => "Tenants.GlobalAdministrators.Remove.Submission." + (unauthorized ? "Unauthorized" : statusCode == 400 ? "Invalid" : "Failed"),
            _ => "Tenants.Lifecycle." + (unauthorized ? "Message.Rejected.InsufficientPermissions" : statusCode == 400 ? "Submission.Invalid" : "Submission.Failed"),
        };
        result.SafeMessageKey.ShouldBe(key);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        JsonSerializer.Serialize(result).ShouldNotContain("backend-secret-Canary");
        dispatched.ShouldHaveSingleItem();
        ulids.CallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("set-tracked", "ConfigurationLimitExceeded", "Tenants.Configuration.Set.Rejection.ConfigurationLimitExceeded")]
    [InlineData("remove-configuration-tracked", "ConfigurationKeyNotFound", "Tenants.Commands.Rejection.ConfigurationKeyNotFound")]
    [InlineData("enable-tracked", "TenantLifecycleStateAlreadySet", "Tenants.Commands.Rejection.TenantLifecycleStateAlreadySet")]
    [InlineData("disable-tracked", "TenantDisabled", "Tenants.Lifecycle.Rejection.TenantDisabled")]
    [InlineData("grant-tracked", "GlobalAdministratorAlreadyExists", "Tenants.GlobalAdministrators.Grant.Submission.Rejected")]
    [InlineData("remove-global-tracked", "LastGlobalAdministrator", "Tenants.Commands.Rejection.LastGlobalAdministrator")]
    public async Task TrackedDomainRejectionsStayTerminalAndKeepOperationCodes(string operation, string code, string key)
    {
        foreach (bool faultedTask in new[] { false, true })
        {
            foreach (bool? retryable in new bool?[] { null, false })
            {
                List<SubmitCommandRequest> dispatched = [];
                StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
                TenantCommandGateway gateway = CreateTrackedFailureGateway(new EventStoreGatewayException(
                    409, code + "Rejection", detail: "backend-secret-Canary", retryable: retryable), faultedTask, dispatched, ulids);

                TenantCommandSubmissionResult result = await SubmitOperation(gateway, operation);

                result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
                result.IsAmbiguousFailure.ShouldBeFalse();
                result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
                result.CorrelationId.ShouldBeNull();
                result.RejectionCode.ShouldBe(code);
                result.SafeMessageKey.ShouldBe(key);
                AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
                JsonSerializer.Serialize(result).ShouldNotContain("backend-secret-Canary");
                dispatched.ShouldHaveSingleItem();
                ulids.CallCount.ShouldBe(0);
            }
        }
    }

    [Theory]
    [InlineData("set-tracked", "Tenants.Configuration.Set.Validation.RequiredFields")]
    [InlineData("remove-configuration-tracked", "Tenants.Configuration.Remove.Validation.RequiredFields")]
    [InlineData("enable-tracked", "Tenants.Lifecycle.Validation.RequiredFields")]
    [InlineData("disable-tracked", "Tenants.Lifecycle.Validation.RequiredFields")]
    [InlineData("grant-tracked", "Tenants.GlobalAdministrators.Grant.Validation.UserIdInvalid")]
    [InlineData("remove-global-tracked", "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Target")]
    public async Task TrackedValidationAndMalformedIdentityNeverDispatch(string operation, string validationKey)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("unused"));
        TenantCommandGateway gateway = CreateGateway(client);
        TenantCommandSubmissionResult invalid = await SubmitOperation(gateway, operation, invalid: true);
        TenantCommandSubmissionResult malformed = await SubmitOperation(gateway, operation, messageId: "not-a-ulid");

        foreach ((TenantCommandSubmissionResult result, string key) in new[]
        {
            (invalid, validationKey),
            (malformed, "Tenants.Commands.Unavailable.InvalidTrackingReference"),
        })
        {
            result.State.ShouldBe(TenantCommandLifecycleState.Failed);
            result.IsAmbiguousFailure.ShouldBeFalse();
            result.MessageId.ShouldBeNull();
            result.CorrelationId.ShouldBeNull();
            result.RejectionCode.ShouldBeNull();
            result.SafeMessageKey.ShouldBe(key);
            AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        }
        client.SubmittedCommands.ShouldBeEmpty();
    }

    private static TenantCommandGateway CreateTrackedFailureGateway(
        Exception exception, bool faultedTask, List<SubmitCommandRequest> dispatched, StubUlidFactory ulids, bool acceptRetry = false)
    {
        IEventStoreGatewayClient client = Substitute.For<IEventStoreGatewayClient>();
        client.SubmitCommandAsync(Arg.Any<SubmitCommandRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            dispatched.Add(call.Arg<SubmitCommandRequest>());
            if (acceptRetry && dispatched.Count > 1)
            {
                return Task.FromResult(new SubmitCommandResponse("correlation-retry"));
            }
            if (faultedTask)
            {
                return Task.FromException<SubmitCommandResponse>(exception);
            }
            throw exception;
        });
        return new(client, ulids, new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });
    }

    private static (string Key, string English, string French) TrackedAmbiguityMessage(string operation)
        => operation switch
        {
            "set-tracked" => ("Tenants.Configuration.Set.SubmissionEvidence.Ambiguous",
                "Submission delivery is uncertain. Do not submit again; refresh status or stop local reconciliation.",
                "La remise de la commande est incertaine. Ne la soumettez pas à nouveau ; actualisez l’état ou arrêtez la réconciliation locale."),
            "remove-configuration-tracked" => ("Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous",
                "Submission delivery is uncertain. Do not submit again; refresh status or stop local reconciliation.",
                "La livraison de la commande est incertaine. Ne la renvoyez pas ; actualisez l'état ou arrêtez le rapprochement local."),
            "grant-tracked" => ("Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous",
                "Grant delivery is ambiguous. Refresh or retry with the same retained command identity.",
                "La livraison de l'attribution est ambiguë. Actualisez ou réessayez avec la même identité de commande conservée."),
            "remove-global-tracked" => ("Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous",
                "Delivery is uncertain; retry only with this attempt’s retained identifier.",
                "La livraison est incertaine ; réessayez uniquement avec l’identifiant conservé de cette tentative."),
            _ => ("Tenants.Lifecycle.SubmissionEvidence.Ambiguous",
                "The lifecycle command may have been delivered, but no acceptance response was received. Keep this attempt open and refresh to retry safely with the same command identity.",
                "La commande de cycle de vie a peut-être été remise, mais aucune réponse d'acceptation n'a été reçue. Gardez cette tentative ouverte et actualisez pour réessayer en toute sécurité avec la même identité de commande."),
        };

    [Theory]
    [MemberData(nameof(AmbiguousGrantExceptions))]
    public async Task AmbiguousGrantTransportRetainsCallerMessageId(Exception exception)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(exception);

        TenantCommandSubmissionResult result = await CreateGateway(client)
            .SetGlobalAdministratorTrackedAsync(
                new SetGlobalAdministrator("target-admin"),
                messageId,
                CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
    }

    [Theory]
    [MemberData(nameof(AmbiguousGrantExceptions))]
    public async Task AmbiguousRemoveTransportRetainsCallerMessageId(Exception exception)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(exception);

        TenantCommandSubmissionResult result = await CreateGateway(client)
            .RemoveGlobalAdministratorTrackedAsync(
                new RemoveGlobalAdministrator("target-admin"),
                messageId,
                CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.SafeMessageKey.ShouldBe(
            "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous");
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
    }

    [Theory]
    [InlineData(
        HttpStatusCode.BadRequest,
        TenantCommandLifecycleState.Failed,
        "Tenants.GlobalAdministrators.Grant.Submission.Invalid")]
    [InlineData(
        HttpStatusCode.Unauthorized,
        TenantCommandLifecycleState.Rejected,
        "Tenants.GlobalAdministrators.Grant.Submission.Rejected")]
    [InlineData(
        HttpStatusCode.Forbidden,
        TenantCommandLifecycleState.Rejected,
        "Tenants.GlobalAdministrators.Grant.Submission.Rejected")]
    [InlineData(
        HttpStatusCode.Conflict,
        TenantCommandLifecycleState.Failed,
        "Tenants.GlobalAdministrators.Grant.Submission.Failed")]
    public async Task NonRetryableGrantRejectionsStayTerminal(
        HttpStatusCode statusCode,
        TenantCommandLifecycleState expectedState,
        string expectedMessageKey)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)statusCode,
            "definite rejection",
            retryable: false));

        TenantCommandSubmissionResult result = await CreateGateway(client)
            .SetGlobalAdministratorTrackedAsync(
                new SetGlobalAdministrator("target-admin"),
                messageId,
                CancellationToken.None);

        result.State.ShouldBe(expectedState);
        result.IsAmbiguousFailure.ShouldBeFalse();
        result.MessageId.ShouldBe(messageId);
        result.SafeMessageKey.ShouldBe(expectedMessageKey);
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
    }

    [Fact]
    public async Task GlobalAdministratorStatusVerifiesFixedAggregateTrackingHandle()
    {
        const string body = """
            {
              "correlationId": "correlation-global-admin",
              "status": "EventsPublished",
              "statusCode": 3,
              "timestamp": "2026-08-31T02:00:00Z",
              "aggregateId": "global-administrators",
              "eventCount": 1,
              "messageId": "01ARZ3NDEKTSV4RRFFQ69G5FAV"
            }
            """;
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("unused")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler(body)) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "correlation-global-admin",
                "global-administrators"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.EventsPublished);
        result.EventCount.ShouldBe(1);
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
    }

    [Theory]
    [InlineData("global-administrators", true)]
    [InlineData("tenants", false)]
    public async Task GlobalAdministratorStatusRequiresItsOwnDomainWhenStatusDeclaresScope(string domain, bool accepted)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        string body = $$"""
            {
              "correlationId": "correlation-global-admin",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-08-31T02:00:00Z",
              "tenantId": "system",
              "domain": "{{domain}}",
              "aggregateId": "global-administrators",
              "eventCount": 1,
              "messageId": "{{messageId}}"
            }
            """;
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("unused")),
            new StubUlidFactory(messageId),
            new HttpClient(new StatusHandler(body)) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle(messageId, "correlation-global-admin", "global-administrators"),
            CancellationToken.None);

        if (accepted)
        {
            result.Status.ShouldBe(CommandStatus.Completed);
            result.HasVerifiedCommandIdentity.ShouldBeTrue();
        }
        else
        {
            result.HasVerifiedCommandIdentity.ShouldBeFalse();
            result.Status.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Set_global_administrator_submits_fixed_scope_command_with_literal_user_payload()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator("User/CaseSensitive.01"),
            CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("global-administrators");
        submitted.AggregateId.ShouldBe("global-administrators");
        submitted.CommandType.ShouldBe(nameof(SetGlobalAdministrator));
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe("User/CaseSensitive.01");
        submitted.Payload.TryGetProperty("TenantId", out _).ShouldBeFalse();
        submitted.Payload.TryGetProperty("Role", out _).ShouldBeFalse();
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-global-admin");
    }

    [Fact]
    public async Task Remove_global_administrator_submits_fixed_scope_command_with_literal_user_payload()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin-remove"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveGlobalAdministratorAsync(
            new RemoveGlobalAdministrator("User/CaseSensitive.01"),
            CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("global-administrators");
        submitted.AggregateId.ShouldBe("global-administrators");
        submitted.CommandType.ShouldBe(nameof(RemoveGlobalAdministrator));
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe("User/CaseSensitive.01");
        submitted.Payload.TryGetProperty("TenantId", out _).ShouldBeFalse();
        submitted.Payload.TryGetProperty("Role", out _).ShouldBeFalse();
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-global-admin-remove");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Remove_global_administrator_validation_failure_does_not_submit_to_eventstore(string? userId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin-remove"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveGlobalAdministratorAsync(
            new RemoveGlobalAdministrator(userId!),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Target");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Global_administrator_gateway_accepts_256_character_literal_for_both_discriminators()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });
        string userId = new('A', 256);

        TenantCommandSubmissionResult grant = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator(userId), CancellationToken.None);
        TenantCommandSubmissionResult remove = await gateway.RemoveGlobalAdministratorAsync(
            new RemoveGlobalAdministrator(userId), CancellationToken.None);

        grant.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        remove.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        client.SubmittedCommands.Count.ShouldBe(2);
        client.SubmittedCommands.ShouldAllBe(command =>
            command.Payload.GetProperty("UserId").GetString() == userId);
    }

    [Fact]
    public async Task Global_administrator_gateway_rejects_257_character_literal_for_both_discriminators()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });
        string userId = new('A', 257);

        TenantCommandSubmissionResult grant = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator(userId), CancellationToken.None);
        TenantCommandSubmissionResult remove = await gateway.RemoveGlobalAdministratorAsync(
            new RemoveGlobalAdministrator(userId), CancellationToken.None);

        grant.State.ShouldBe(TenantCommandLifecycleState.Failed);
        remove.State.ShouldBe(TenantCommandLifecycleState.Failed);
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("LastGlobalAdministratorRejection", "LastGlobalAdministrator", "last global administrator")]
    [InlineData("GlobalAdministratorNotFoundRejection", "GlobalAdministratorNotFound", "not a global administrator")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "platform governance")]
    public async Task Remove_global_administrator_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-global-admin UserId=secret-user"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveGlobalAdministratorAsync(
            new RemoveGlobalAdministrator("secret-user"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-global-admin", Case.Insensitive);
        safeMessage.ShouldNotContain("secret-user", Case.Insensitive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Set_global_administrator_validation_failure_does_not_submit_to_eventstore(string? userId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-global-admin"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator(userId!),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Validation.UserIdInvalid");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Set_global_administrator_service_unavailable_uses_platform_governance_copy()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "gateway unavailable",
            detail: "raw payload bearer-token stack trace correlation-global-admin"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator("target-user"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous");
        result.SafeMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData("GlobalAdministratorAlreadyExistsRejection", "GlobalAdministratorAlreadyExists")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions")]
    public async Task Set_global_administrator_maps_rejection_to_stable_resource_key(
        string reason,
        string expectedCode)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-global-admin UserId=secret-user"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetGlobalAdministratorAsync(
            new SetGlobalAdministrator("secret-user"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        result.SafeMessage.ShouldBeNull();
        result.SafeMessageKey.ShouldBe("Tenants.GlobalAdministrators.Grant.Submission.Rejected");
    }

    [Fact]
    public async Task Status_lookup_maps_global_administrator_already_exists_to_rejected_safe_text()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-global-admin",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "global-administrators",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.GlobalAdministratorAlreadyExistsRejection",
              "failureReason": "raw payload token stack trace correlation-global-admin UserId=secret-user",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global-admin")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-global-admin", "correlation-global-admin"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe("GlobalAdministratorAlreadyExists");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("already a global administrator", Case.Insensitive);
        safeMessage.ShouldNotContain("already applied", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-global-admin", Case.Insensitive);
        safeMessage.ShouldNotContain("secret-user", Case.Insensitive);
    }

    [Theory]
    [InlineData("Hexalith.Tenants.Contracts.Events.Rejections.LastGlobalAdministratorRejection", "LastGlobalAdministrator", "last global administrator")]
    [InlineData("Hexalith.Tenants.Contracts.Events.Rejections.GlobalAdministratorNotFoundRejection", "GlobalAdministratorNotFound", "not a global administrator")]
    public async Task Status_lookup_maps_global_administrator_remove_rejections_to_safe_text(
        string rejectionEventType,
        string expectedCode,
        string expectedText)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-global-admin-remove",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "global-administrators",
              "eventCount": 0,
              "rejectionEventType": "{{rejectionEventType}}",
              "failureReason": "raw payload token stack trace correlation-global-admin-remove UserId=secret-user",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global-admin-remove")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-global-admin-remove", "correlation-global-admin-remove"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("success", Case.Insensitive);
        safeMessage.ShouldNotContain("remove member", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-global-admin-remove", Case.Insensitive);
        safeMessage.ShouldNotContain("secret-user", Case.Insensitive);
    }

    [Theory]
    [InlineData(TenantLifecycleOperation.EnableTenant, nameof(EnableTenant), "correlation-enable")]
    [InlineData(TenantLifecycleOperation.DisableTenant, nameof(DisableTenant), "correlation-disable")]
    public async Task Lifecycle_command_submits_literal_tenant_id_payload_and_captures_correlation_id(
        TenantLifecycleOperation operation,
        string expectedCommandType,
        string correlationId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse(correlationId));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });
        var request = new TenantLifecycleCommandRequest("Tenant.Mixed-01", operation);
        const string stableMessageId = "01ARZ3NDEKTSV4RRFFQ69G5FB0";

        TenantCommandSubmissionResult result = operation is TenantLifecycleOperation.EnableTenant
            ? await gateway.EnableTenantTrackedAsync(request, stableMessageId, CancellationToken.None)
            : await gateway.DisableTenantTrackedAsync(request, stableMessageId, CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe(stableMessageId);
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(expectedCommandType);
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe(stableMessageId);
        result.CorrelationId.ShouldBe(correlationId);
    }

    [Theory]
    [InlineData("TenantLifecycleStateAlreadySetRejection", "TenantLifecycleStateAlreadySet", "already matches")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    public async Task Lifecycle_command_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-life"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.DisableTenantAsync(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-life", Case.Insensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Lifecycle_submission_maps_ambiguous_http_failures_to_same_identity_retry(
        HttpStatusCode statusCode)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        TenantCommandGateway gateway = CreateLifecycleGateway(
            new EventStoreGatewayException((int)statusCode, "raw transport token"));

        TenantCommandSubmissionResult result = await gateway.DisableTenantTrackedAsync(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            messageId,
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.SafeMessageKey.ShouldBe("Tenants.Lifecycle.SubmissionEvidence.Ambiguous");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, TenantCommandLifecycleState.Failed)]
    [InlineData(HttpStatusCode.Unauthorized, TenantCommandLifecycleState.Rejected)]
    [InlineData(HttpStatusCode.Forbidden, TenantCommandLifecycleState.Rejected)]
    public async Task Lifecycle_submission_keeps_permanent_http_rejections_terminal(
        HttpStatusCode statusCode,
        TenantCommandLifecycleState expectedState)
    {
        TenantCommandGateway gateway = CreateLifecycleGateway(
            new EventStoreGatewayException((int)statusCode, "raw rejection token"));

        TenantCommandSubmissionResult result = await gateway.DisableTenantTrackedAsync(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            CancellationToken.None);

        result.State.ShouldBe(expectedState);
        result.IsAmbiguousFailure.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AmbiguousTransportExceptions))]
    public async Task Lifecycle_submission_maps_transport_and_timeout_exceptions_to_same_identity_retry(
        Exception exception)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        TenantCommandGateway gateway = CreateLifecycleGateway(exception);

        TenantCommandSubmissionResult result = await gateway.EnableTenantTrackedAsync(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.EnableTenant),
            messageId,
            CancellationToken.None);

        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
    }

    public static TheoryData<Exception> AmbiguousTransportExceptions
        => new()
        {
            new HttpRequestException("transport unavailable"),
            new TaskCanceledException("gateway timeout"),
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Tracked_lifecycle_dispatch_rejects_blank_explicit_message_id_before_transport(
        string? messageId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-life"));
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandSubmissionResult result = await gateway.DisableTenantTrackedAsync(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            messageId!,
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe("Tenants.Commands.Unavailable.InvalidTrackingReference");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_tenant_submits_literal_command_with_payload_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-update"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.UpdateTenantAsync(
            new UpdateTenant("Tenant.Mixed-01", "Updated tenant", string.Empty),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(UpdateTenant));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("Name").GetString().ShouldBe("Updated tenant");
        submitted.Payload.GetProperty("Description").GetString().ShouldBe(string.Empty);
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-update");
    }

    [Fact]
    public async Task Update_tenant_reuses_provided_message_id_instead_of_minting()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-reuse"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.UpdateTenantAsync(
            new UpdateTenant("tenant.alpha", "Alpha", null),
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_tenant_gateway_exception_retains_resolved_message_id_for_exact_retry()
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "gateway unavailable"));
        StubUlidFactory ulids = new(messageId);
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.UpdateTenantAsync(
            new UpdateTenant("tenant.alpha", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.MessageId.ShouldBe(messageId);
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
        ulids.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Update_tenant_validation_failure_does_not_submit_to_eventstore(string? name)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-update"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.UpdateTenantAsync(
            new UpdateTenant("tenant.alpha", name!, "description"),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("Tenant id and name are required");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Update_tenant_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-update"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.UpdateTenantAsync(
            new UpdateTenant("tenant.alpha", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-update", Case.Insensitive);
    }

    [Fact]
    public async Task Set_tenant_configuration_submits_literal_command_with_payload_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-config"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetTenantConfigurationAsync(
            new SetTenantConfiguration("Tenant.Mixed-01", "billing.mode", "enterprise"),
            CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(SetTenantConfiguration));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("Key").GetString().ShouldBe("billing.mode");
        submitted.Payload.GetProperty("Value").GetString().ShouldBe("enterprise");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-config");
    }

    [Fact]
    public async Task Tracked_set_configuration_uses_the_callers_exact_message_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-config"));
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandSubmissionResult result = await gateway.SetTenantConfigurationTrackedAsync(
            new SetTenantConfiguration("tenant.alpha", "Billing.Mode", "enterprise"),
            "01ARZ3NDEKTSV4RRFFQ69G5FAA",
            CancellationToken.None);

        gateway.SupportsTrackedSetConfigurationDispatch.ShouldBeTrue();
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAA");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAA");
    }

    [Fact]
    public async Task Tracked_set_configuration_retains_message_identity_when_delivery_is_ambiguous()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "temporarily-unavailable",
            detail: "raw payload Value=super-secret"));
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandSubmissionResult result = await gateway.SetTenantConfigurationTrackedAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "super-secret"),
            "01ARZ3NDEKTSV4RRFFQ69G5FAA",
            CancellationToken.None);

        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAA");
        result.SafeMessageKey.ShouldBe("Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        result.SafeMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Set_tenant_configuration_validation_failure_does_not_submit_to_eventstore(string? key)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-config"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetTenantConfigurationAsync(
            new SetTenantConfiguration("tenant.alpha", key!, "value"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("configuration key");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ConfigurationLimitExceededRejection", "ConfigurationLimitExceeded", "limits")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Set_tenant_configuration_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-config Value=super-secret"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.SetTenantConfigurationAsync(
            new SetTenantConfiguration("tenant.alpha", "billing.mode", "super-secret"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-config", Case.Insensitive);
        safeMessage.ShouldNotContain("super-secret", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_maps_configuration_limit_rejection_to_command_neutral_safe_text()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-config",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.ConfigurationLimitExceededRejection",
              "failureReason": "raw payload token stack trace correlation-config Value=super-secret",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-config")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-config", "correlation-config"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe("ConfigurationLimitExceeded");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("configuration limits", Case.Insensitive);
        safeMessage.ShouldNotContain("set configuration", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-config", Case.Insensitive);
        safeMessage.ShouldNotContain("super-secret", Case.Insensitive);
    }

    [Fact]
    public async Task Remove_tenant_configuration_submits_literal_command_with_payload_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-remove-config"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationAsync(
            new RemoveTenantConfiguration("Tenant.Mixed-01", "billing.mode"),
            CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(RemoveTenantConfiguration));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("Key").GetString().ShouldBe("billing.mode");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-remove-config");
    }

    [Fact]
    public async Task Tracked_remove_configuration_uses_the_callers_exact_message_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-remove-config"));
        var ulidFactory = new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(
            client,
            ulidFactory,
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAA";

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationTrackedAsync(
            new RemoveTenantConfiguration("Tenant.Mixed-01", "Billing.Mode"),
            messageId,
            CancellationToken.None);

        gateway.SupportsTrackedRemoveConfigurationDispatch.ShouldBeTrue();
        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe(messageId);
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("Key").GetString().ShouldBe("Billing.Mode");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe(messageId);
        result.CorrelationId.ShouldBe("correlation-remove-config");
        ulidFactory.CallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Tracked_remove_configuration_retains_message_identity_when_http_delivery_is_ambiguous(
        HttpStatusCode statusCode)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)statusCode,
            "temporarily-unavailable",
            detail: "raw payload Key=billing.secret Value=super-secret"));
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAA";

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationTrackedAsync(
            new RemoveTenantConfiguration("tenant.alpha", "billing.secret"),
            messageId,
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.SafeMessageKey.ShouldBe("Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous");
        result.SafeMessage.ShouldBeNull();
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
    }

    [Theory]
    [MemberData(nameof(AmbiguousTransportExceptions))]
    public async Task Tracked_remove_configuration_retains_message_identity_for_ambiguous_transport_exceptions(
        Exception exception)
    {
        CapturingGatewayClient client = new(exception);
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAA";

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationTrackedAsync(
            new RemoveTenantConfiguration("tenant.alpha", "billing.mode"),
            messageId,
            CancellationToken.None);

        result.IsAmbiguousFailure.ShouldBeTrue();
        result.MessageId.ShouldBe(messageId);
        result.SafeMessageKey.ShouldBe("Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous");
        result.SafeMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Remove_tenant_configuration_validation_failure_does_not_submit_to_eventstore(string? key)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-remove-config"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationAsync(
            new RemoveTenantConfiguration("tenant.alpha", key!),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("configuration key");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ConfigurationKeyNotFoundRejection", "ConfigurationKeyNotFound", "not found")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Remove_tenant_configuration_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-config Key=billing.secret Value=super-secret"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveTenantConfigurationAsync(
            new RemoveTenantConfiguration("tenant.alpha", "billing.secret"),
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-config", Case.Insensitive);
        safeMessage.ShouldNotContain("super-secret", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_maps_configuration_key_not_found_to_rejected_safe_text()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-remove-config",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.ConfigurationKeyNotFoundRejection",
              "failureReason": "raw payload token stack trace correlation-remove-config Value=super-secret",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-remove-config")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-config", "correlation-remove-config"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe("ConfigurationKeyNotFound");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("not found", Case.Insensitive);
        safeMessage.ShouldNotContain("success", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-remove-config", Case.Insensitive);
        safeMessage.ShouldNotContain("super-secret", Case.Insensitive);
    }

    [Fact]
    public async Task Remove_user_from_tenant_submits_literal_command_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-999"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("Tenant.Mixed-01", "User/CaseSensitive.01"),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(RemoveUserFromTenant));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe("User/CaseSensitive.01");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-999");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Remove_user_from_tenant_validation_failure_does_not_submit_to_eventstore(string? userId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-999"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", userId!),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("Tenant id and user id are required");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("UserNotInTenantRejection", "UserNotInTenant", "already applied")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Remove_user_from_tenant_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-999"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", "literal-user"),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-999", Case.Insensitive);
    }

    [Fact]
    public async Task Change_user_role_submits_literal_command_with_new_role_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-789"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("Tenant.Mixed-01", "User/CaseSensitive.01", TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(ChangeUserRole));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe("User/CaseSensitive.01");
        submitted.Payload.GetProperty("NewRole").GetString().ShouldBe(nameof(TenantRole.TenantReader));
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-789");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Change_user_role_validation_failure_does_not_submit_to_eventstore(string? userId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-789"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", userId!, TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("Tenant id, user id, and new role are required");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Change_user_role_rejects_unknown_role_before_submission()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-789"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.Unknown),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("new role");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RoleEscalationRejection", "RoleEscalation", "role cannot be assigned")]
    [InlineData("UserNotInTenantRejection", "UserNotInTenant", "not a visible member")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Change_user_role_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-789"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantContributor),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-789", Case.Insensitive);
    }

    [Theory]
    [InlineData("RoleEscalationRejection", "RoleEscalation", "role cannot be assigned")]
    [InlineData("UserNotInTenantRejection", "UserNotInTenant", "not a visible member")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Status_lookup_maps_change_role_rejections_to_safe_text(
        string rejectionType,
        string expectedCode,
        string expectedText)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-789",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.{{rejectionType}}",
              "failureReason": "raw payload token stack trace correlation-789",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-789")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-789", "correlation-789"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-789", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_exposes_missing_member_rejection_code_without_success_or_raw_details()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-remove",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.UserNotInTenantRejection",
              "failureReason": "raw payload token stack trace correlation-remove",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-remove")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-remove", "correlation-remove"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe("UserNotInTenant");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("not a visible member", Case.Insensitive);
        safeMessage.ShouldNotContain("already applied", Case.Insensitive);
        safeMessage.ShouldNotContain("success", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-remove", Case.Insensitive);
    }

    [Theory]
    [InlineData("InsufficientPermissionsRejection")]
    [InlineData("TenantDisabledRejection")]
    [InlineData("TenantNotFoundRejection")]
    [InlineData("RoleEscalationRejection")]
    public async Task Status_lookup_keeps_shared_rejection_copy_command_neutral(string rejectionType)
    {
        // GetStatusAsync is shared across create-tenant, add-member, and change-role and only sees
        // a correlation id, so a rejection type that several commands can produce must not surface
        // one command's wording inside another command's lifecycle panel.
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-shared",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.{{rejectionType}}",
              "failureReason": "raw payload token stack trace correlation-shared",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-shared")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-shared", "correlation-shared"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldNotContain("add members", Case.Insensitive);
        safeMessage.ShouldNotContain("members cannot be added", Case.Insensitive);
        safeMessage.ShouldNotContain("change member roles", Case.Insensitive);
        safeMessage.ShouldNotContain("member roles cannot be changed", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-shared", Case.Insensitive);
    }

    [Fact]
    public async Task Add_user_to_tenant_submits_literal_command_with_explicit_role_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-456"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("Tenant.Mixed-01", "User/CaseSensitive.01", TenantRole.TenantContributor),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(AddUserToTenant));
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("UserId").GetString().ShouldBe("User/CaseSensitive.01");
        submitted.Payload.GetProperty("Role").GetString().ShouldBe(nameof(TenantRole.TenantContributor));
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-456");
    }

    [Theory]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "tracking-safe", 8L, 1, true)]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "tracking-safe", null, 1, false)]
    [InlineData("other", "tenants", "tenant.alpha", "message-safe", "tracking-safe", 8L, 1, false)]
    [InlineData("system", "global-administrators", "tenant.alpha", "message-safe", "tracking-safe", 8L, 1, false)]
    [InlineData("system", "tenants", "other", "message-safe", "tracking-safe", 8L, 1, false)]
    [InlineData("system", "tenants", "tenant.alpha", "other", "tracking-safe", 8L, 1, false)]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "other", 8L, 1, false)]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "tracking-safe", -1L, 1, false)]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "tracking-safe", 8L, 0, false)]
    [InlineData("system", "tenants", "tenant.alpha", "message-safe", "tracking-safe", 8L, 9, false)]
    public async Task Committed_proof_requires_exact_attempt_and_system_tenant_aggregate_scope(
        string tenant, string domain, string aggregate, string message, string correlation, long? sequence, int count, bool proof)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new {
            tenantId = tenant, domain, aggregateId = aggregate, messageId = message, correlationId = correlation,
            status = "Completed", statusCode = (int)CommandStatus.Completed, eventCount = count, committedEventSequence = sequence,
        });
        TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse("tracking-safe")),
            new StubUlidFactory("message-safe"), new HttpClient(new StatusHandler(json)) { BaseAddress = new Uri("https://eventstore.example/") });
        TenantCommandStatusResult result = await gateway.GetStatusAsync(new("message-safe", "tracking-safe", "tenant.alpha"));
        result.CommittedEventSequence.ShouldBe(proof ? sequence : null);
        result.ToString().ShouldBe($"TenantCommandStatusResult {{ Status = {result.Status}, IsPending = {result.IsPending}, IsRetryableFailure = {result.IsRetryableFailure} }}");
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("domain")]
    [InlineData("statusCode")]
    public async Task IncompleteStatusScopeCannotAdmitCommittedCorrectionProof(string omittedField)
    {
        Dictionary<string, object> envelope = new(StringComparer.Ordinal)
        {
            ["tenantId"] = "system",
            ["domain"] = "tenants",
            ["aggregateId"] = "tenant.alpha",
            ["messageId"] = "message-safe",
            ["correlationId"] = "tracking-safe",
            ["status"] = "Completed",
            ["statusCode"] = (int)CommandStatus.Completed,
            ["eventCount"] = 1,
            ["committedEventSequence"] = 8L,
        };
        envelope.Remove(omittedField);
        string json = System.Text.Json.JsonSerializer.Serialize(envelope);
        TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse("tracking-safe")),
            new StubUlidFactory("unused"), new HttpClient(new StatusHandler(json)) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(new("message-safe", "tracking-safe", "tenant.alpha"));

        result.Status.ShouldBe(CommandStatus.Completed);
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        result.CommittedEventSequence.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, HttpStatusCode.BadRequest, false, false)]
    [InlineData(false, HttpStatusCode.BadRequest, true, true)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable, false, true)]
    [InlineData(false, HttpStatusCode.RequestTimeout, false, true)]
    [InlineData(false, HttpStatusCode.TooManyRequests, false, true)]
    [InlineData(false, HttpStatusCode.InternalServerError, false, true)]
    [InlineData(false, HttpStatusCode.BadGateway, false, true)]
    [InlineData(false, HttpStatusCode.GatewayTimeout, false, true)]
    [InlineData(false, (HttpStatusCode)0, false, true)]
    [InlineData(false, HttpStatusCode.Conflict, false, false)]
    [InlineData(true, HttpStatusCode.BadRequest, false, false)]
    [InlineData(true, HttpStatusCode.BadRequest, true, true)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable, false, true)]
    [InlineData(true, HttpStatusCode.RequestTimeout, false, true)]
    [InlineData(true, HttpStatusCode.TooManyRequests, false, true)]
    [InlineData(true, HttpStatusCode.InternalServerError, false, true)]
    [InlineData(true, HttpStatusCode.BadGateway, false, true)]
    [InlineData(true, HttpStatusCode.GatewayTimeout, false, true)]
    [InlineData(true, (HttpStatusCode)0, false, true)]
    [InlineData(true, HttpStatusCode.Conflict, false, false)]
    public async Task Correction_membership_gateway_classifies_only_uncertain_delivery_as_ambiguous(
        bool changeRole, HttpStatusCode statusCode, bool retryable, bool ambiguous)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(new EventStoreGatewayException((int)statusCode, "gateway error",
            retryable: retryable));
        TenantCommandGateway gateway = CreateGateway(client);

        TenantCommandSubmissionResult result = changeRole
            ? await gateway.ChangeUserRoleAsync(new ChangeUserRole("tenant.alpha", "target-user", TenantRole.TenantReader),
                messageId, CancellationToken.None)
            : await gateway.AddUserToTenantAsync(new AddUserToTenant("tenant.alpha", "target-user", TenantRole.TenantReader),
                messageId, CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.MessageId.ShouldBe(messageId);
        result.IsAmbiguousFailure.ShouldBe(ambiguous);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Add_user_to_tenant_validation_failure_does_not_submit_to_eventstore(string? userId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-456"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", userId!, TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("Tenant id, user id, and role are required");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_user_to_tenant_rejects_unknown_role_before_submission()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-456"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.Unknown),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldNotBeNull().ShouldContain("role");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("UserAlreadyInTenantRejection", "UserAlreadyInTenant", "already a member")]
    [InlineData("RoleEscalationRejection", "RoleEscalation", "role cannot be assigned")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Add_user_to_tenant_maps_safe_rejection_text(
        string reason,
        string expectedCode,
        string expectedText)
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            reason,
            detail: "raw payload bearer-token stack trace correlation-456"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-456", Case.Insensitive);
    }

    [Fact]
    public async Task Add_user_to_tenant_reuses_provided_message_id_instead_of_minting()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-reuse"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-reuse");
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Add_user_to_tenant_mints_message_id_only_when_absent()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-new"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        ulids.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("not-a-ulid")]
    [InlineData("8ZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAI")]
    public async Task Add_user_to_tenant_rejects_noncanonical_reusable_message_id(string messageId)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-unused"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
            messageId,
            CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe("Tenants.Commands.Unavailable.InvalidTrackingReference");
        result.SafeMessage.ShouldBeNull();
        client.SubmittedCommands.ShouldBeEmpty();
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Add_user_to_tenant_retains_minted_message_id_when_submission_is_indeterminate()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "gateway unavailable"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(result.MessageId);
        ulids.CallCount.ShouldBe(1);
    }

    // Retention was proven for add-member only. The same indeterminate-submission contract carries the
    // idempotency key for every submit path: without it a 503 loses the key, the flow's retry mints a new
    // ULID, and a command that may already have applied is dispatched twice.
    [Fact]
    public async Task Change_user_role_retains_minted_message_id_when_submission_is_indeterminate()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "gateway unavailable"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantOwner),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(result.MessageId);
        ulids.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_user_from_tenant_retains_minted_message_id_when_submission_is_indeterminate()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.ServiceUnavailable,
            "gateway unavailable"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", "literal-user"),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(result.MessageId);
        ulids.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Change_user_role_reuses_provided_message_id_instead_of_minting()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-reuse"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantContributor),
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-reuse");
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Change_user_role_mints_message_id_only_when_absent()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-new"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantContributor),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        ulids.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_user_from_tenant_reuses_provided_message_id_instead_of_minting()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-reuse"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", "literal-user"),
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-reuse");
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_user_from_tenant_mints_message_id_only_when_absent()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-new"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", "literal-user"),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        ulids.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("UserAlreadyInTenantRejection", "UserAlreadyInTenant", "already a member")]
    [InlineData("RoleEscalationRejection", "RoleEscalation", "role cannot be assigned")]
    [InlineData("InsufficientPermissionsRejection", "InsufficientPermissions", "not authorized")]
    [InlineData("TenantDisabledRejection", "TenantDisabled", "disabled")]
    [InlineData("TenantNotFoundRejection", "TenantNotFound", "not found")]
    public async Task Status_lookup_maps_add_member_rejections_to_safe_text(
        string rejectionType,
        string expectedCode,
        string expectedText)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-456",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 1,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.{{rejectionType}}",
              "failureReason": "raw payload token stack trace correlation-456",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-456")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-456", "correlation-456"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe(expectedCode);
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedText, Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-456", Case.Insensitive);
    }

    [Fact]
    public async Task Create_tenant_submits_literal_command_with_ulid_message_id_and_captures_correlation_id()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-123"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("Tenant.Mixed-01", "Mixed Tenant", "literal id"),
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        submitted.Tenant.ShouldBe("system");
        submitted.Domain.ShouldBe("tenants");
        submitted.AggregateId.ShouldBe("Tenant.Mixed-01");
        submitted.CommandType.ShouldBe(nameof(CreateTenant));
        submitted.CorrelationId.ShouldBeNull();
        submitted.Payload.GetProperty("TenantId").GetString().ShouldBe("Tenant.Mixed-01");
        submitted.Payload.GetProperty("Name").GetString().ShouldBe("Mixed Tenant");
        result.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.CorrelationId.ShouldBe("correlation-123");
    }

    [Fact]
    public async Task Create_tenant_reuses_provided_message_id_instead_of_minting()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-reuse"));
        StubUlidFactory ulids = new("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        TenantCommandGateway gateway = new(client, ulids, new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("tenant.alpha", "Alpha", null),
            messageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            cancellationToken: CancellationToken.None);

        SubmitCommandRequest submitted = client.SubmittedCommands.ShouldHaveSingleItem();
        submitted.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        ulids.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Create_tenant_maps_already_exists_rejection_to_safe_text()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            "TenantAlreadyExistsRejection",
            detail: "raw payload token stack trace correlation-123"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("tenant.alpha", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe("TenantAlreadyExists");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("already exists");
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-123", Case.Insensitive);
    }

    [Fact]
    public async Task Create_tenant_maps_problem_details_rejection_type_extension_to_duplicate_safe_text()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Conflict,
            "Domain rejection",
            detail: "Domain rejection returned by the aggregate.",
            extensions: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [GatewayProblemDetailsExtensions.RejectionType] = JsonSerializer.SerializeToElement(
                    "Hexalith.Tenants.Contracts.Events.Rejections.TenantAlreadyExistsRejection"),
                [GatewayProblemDetailsExtensions.CorrectiveAction] = JsonSerializer.SerializeToElement(
                    "Refresh the list or open the existing tenant."),
            }));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("tenant.alpha", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe("TenantAlreadyExists");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("already exists", Case.Insensitive);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation", Case.Insensitive);
    }

    [Fact]
    public async Task Create_tenant_validation_failure_does_not_submit_to_eventstore()
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-123"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessage.ShouldBe("Tenant id and name are required before the command can be submitted.");
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("   ", "Alpha")]
    [InlineData("tenant.alpha", "   ")]
    public async Task Create_tenant_whitespace_validation_failure_does_not_submit_to_eventstore(string tenantId, string name)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-123"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant(tenantId, name, null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_tenant_maps_forbidden_rejection_to_safe_authorization_text()
    {
        CapturingGatewayClient client = new(new EventStoreGatewayException(
            (int)HttpStatusCode.Forbidden,
            "InsufficientPermissionsRejection",
            detail: "raw payload bearer-token stack trace correlation-123"));
        TenantCommandGateway gateway = new(client, new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"), new HttpClient(new StatusHandler("{}"))
        {
            BaseAddress = new Uri("https://eventstore.example/"),
        });

        TenantCommandSubmissionResult result = await gateway.CreateTenantAsync(
            new CreateTenant("tenant.alpha", "Alpha", null),
            cancellationToken: CancellationToken.None);

        result.State.ShouldBe(TenantCommandLifecycleState.Rejected);
        result.RejectionCode.ShouldBe("InsufficientPermissions");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("not authorized");
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-123", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_uses_retained_message_id_and_validates_returned_correlation()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-123",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "Tenant.Mixed-01",
              "eventCount": 1,
              "rejectionEventType": null,
              "failureReason": null,
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-123"),
            CancellationToken.None);

        handler.RequestUri.ShouldNotBeNull().AbsolutePath.ShouldBe("/api/v1/commands/status/01ARZ3NDEKTSV4RRFFQ69G5FAV");
        result.Status.ShouldBe(CommandStatus.Completed);
    }

    [Fact]
    public async Task Shared_correlation_recovers_each_retained_message_independently()
    {
        StatusHandler handler = new("{}", bodyForRequest: request => System.Text.Json.JsonSerializer.Serialize(new {
            correlationId = "shared-correlation", messageId = request.RequestUri!.Segments.Last(),
            tenantId = "system", domain = "tenants", aggregateId = "tenant.alpha", status = "Completed",
            statusCode = (int)CommandStatus.Completed, eventCount = 1,
            committedEventSequence = request.RequestUri.Segments.Last() == "message-first" ? 6 : 8,
        }));
        TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse("shared-correlation")),
            new StubUlidFactory("unused"), new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult first = await gateway.GetStatusAsync(new("message-first", "shared-correlation", "tenant.alpha"));
        handler.RequestUri!.AbsolutePath.ShouldBe("/api/v1/commands/status/message-first");
        TenantCommandStatusResult second = await gateway.GetStatusAsync(new("message-second", "shared-correlation", "tenant.alpha"));

        handler.RequestUri!.AbsolutePath.ShouldBe("/api/v1/commands/status/message-second");
        first.HasVerifiedCommandIdentity.ShouldBeTrue();
        first.CommittedEventSequence.ShouldBe(6);
        second.HasVerifiedCommandIdentity.ShouldBeTrue();
        second.CommittedEventSequence.ShouldBe(8);
    }

    [Theory]
    [InlineData("Completed", "tenant")]
    [InlineData("Completed", "domain")]
    [InlineData("Completed", "statusCode")]
    [InlineData("Rejected", "tenant")]
    [InlineData("Rejected", "domain")]
    [InlineData("Rejected", "statusCode")]
    public async Task Explicit_status_contradictions_cannot_prove_noop_or_rejection(string status, string contradiction)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new {
            correlationId = "tracking-safe", messageId = "message-safe", aggregateId = "tenant.alpha",
            tenantId = contradiction == "tenant" ? "other" : "system",
            domain = contradiction == "domain" ? "other" : "tenants", status,
            statusCode = contradiction == "statusCode" ? (int)CommandStatus.Received : (int)Enum.Parse<CommandStatus>(status),
            eventCount = 0, rejectionEventType = "TenantDisabledRejection",
        });
        TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse("tracking-safe")),
            new StubUlidFactory("unused"), new HttpClient(new StatusHandler(json)) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(new("message-safe", "tracking-safe", "tenant.alpha"));

        result.Status.ShouldBeNull();
        result.HasVerifiedCommandIdentity.ShouldBeFalse();
        result.RejectionCode.ShouldBeNull();
        result.EventCount.ShouldBeNull();
        result.CommittedEventSequence.ShouldBeNull();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Rejected")]
    public async Task Absent_optional_legacy_scope_and_status_code_remain_compatible(string status)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new {
            correlationId = "tracking-safe", messageId = "message-safe", aggregateId = "tenant.alpha",
            status, eventCount = 0, rejectionEventType = status == "Rejected" ? "TenantDisabledRejection" : null,
        });
        TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse("tracking-safe")),
            new StubUlidFactory("unused"), new HttpClient(new StatusHandler(json)) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(new("message-safe", "tracking-safe", "tenant.alpha"));

        result.Status.ShouldBe(Enum.Parse<CommandStatus>(status));
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
        result.EventCount.ShouldBe(0);
        result.RejectionCode.ShouldBe(status == "Rejected" ? "TenantDisabled" : null);
        result.CommittedEventSequence.ShouldBeNull();
    }

    [Fact]
    public async Task Status_lookup_maps_rejection_without_raw_failure_details()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-123",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 1,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.TenantAlreadyExistsRejection",
              "failureReason": "raw payload token stack trace correlation-123",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe("TenantAlreadyExists");
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain("already exists");
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-123", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_maps_not_found_to_pending_without_raw_details()
    {
        StatusHandler handler = new("{}", HttpStatusCode.NotFound);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBeNull();
        result.IsPending.ShouldBeTrue();
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldBe("Command status is not available yet.");
        safeMessage.ShouldNotContain("correlation-123", Case.Insensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Tenants.GlobalAdministrators.Grant.Status.Pending")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Tenants.GlobalAdministrators.Grant.Status.Unknown")]
    public async Task FixedGlobalAdministratorUnavailableStatusUsesExactGrantKey(
        HttpStatusCode statusCode,
        string expectedKey)
    {
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}", statusCode))
            {
                BaseAddress = new Uri("https://eventstore.example/"),
            });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle(
                "message-global",
                "correlation-global",
                GlobalAdministratorGrantPreview.FixedAggregateId),
            CancellationToken.None);

        result.SafeMessageKey.ShouldBe(expectedKey);
    }

    [Fact]
    public async Task FixedGlobalAdministratorTrackingMismatchUsesExactGrantKey()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-global",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "global-administrators",
              "eventCount": 1,
              "messageId": "different-message"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle(
                "message-global",
                "correlation-global",
                GlobalAdministratorGrantPreview.FixedAggregateId),
            CancellationToken.None);

        result.SafeMessageKey.ShouldBe(
            "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch");
    }

    [Theory]
    [InlineData("Rejected", "Tenants.GlobalAdministrators.Grant.Status.Rejected")]
    [InlineData("PublishFailed", "Tenants.GlobalAdministrators.Grant.Status.PublishFailed")]
    [InlineData("TimedOut", "Tenants.GlobalAdministrators.Grant.Status.TimedOut")]
    public async Task FixedGlobalAdministratorTerminalStatusUsesExactGrantKey(
        string status,
        string expectedKey)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-global",
              "status": "{{status}}",
              "statusCode": {{(int)Enum.Parse<CommandStatus>(status)}},
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "global-administrators",
              "eventCount": 0,
              "messageId": "message-global"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle(
                "message-global",
                "correlation-global",
                GlobalAdministratorGrantPreview.FixedAggregateId),
            CancellationToken.None);

        result.SafeMessageKey.ShouldBe(expectedKey);
    }

    [Theory]
    [InlineData("tenants", true)]
    [InlineData("global-administrators", false)]
    public async Task TenantNamedGlobalAdministratorsKeepsItsTenantDomainProof(string responseDomain, bool validScope)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-123",
              "status": "Completed",
              "statusCode": 4,
              "tenantId": "system",
              "domain": "{{responseDomain}}",
              "aggregateId": "global-administrators",
              "eventCount": 1,
              "committedEventSequence": 8,
              "messageId": "message-123"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123", "global-administrators")
            {
                ExpectedDomain = "tenants",
            },
            CancellationToken.None);

        result.Status.ShouldBe(validScope ? CommandStatus.Completed : null);
        result.HasVerifiedCommandIdentity.ShouldBe(validScope);
        result.CommittedEventSequence.ShouldBe(validScope ? 8 : null);
    }

    [Fact]
    public async Task GlobalAdministratorStatusRetainsVerifiedCommittedSequence()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-global",
              "status": "Completed",
              "statusCode": 4,
              "tenantId": "system",
              "domain": "global-administrators",
              "aggregateId": "global-administrators",
              "eventCount": 1,
              "committedEventSequence": 8,
              "messageId": "message-global"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-global")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-global", "correlation-global", "global-administrators")
            {
                ExpectedDomain = "global-administrators",
            },
            CancellationToken.None);

        result.HasVerifiedCommandIdentity.ShouldBeTrue();
        result.CommittedEventSequence.ShouldBe(8);
    }

    [Fact]
    public async Task Lifecycle_status_lookup_verifies_message_and_aggregate_identity()
    {
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-123",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 1,
              "rejectionEventType": null,
              "failureReason": null,
              "timeoutDuration": null,
              "messageId": "message-123"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123", "tenant.alpha"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Completed);
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
    }

    [Theory]
    [InlineData("message-other", "tenant.alpha")]
    [InlineData("message-123", "Tenant.Alpha")]
    // CommandStatusRecord.MessageId is contractually null for a legacy record. Identity must stay unproven
    // rather than fall back to the correlation id, which is matched earlier and is not command-exact.
    [InlineData("", "tenant.alpha")]
    public async Task Lifecycle_status_lookup_rejects_mismatched_command_identity(
        string messageId,
        string aggregateId)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-123",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "{{aggregateId}}",
              "eventCount": 1,
              "rejectionEventType": null,
              "failureReason": null,
              "timeoutDuration": null,
              "messageId": "{{messageId}}"
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123", "tenant.alpha"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Completed);
        result.HasVerifiedCommandIdentity.ShouldBeFalse();
        result.SafeMessage.ShouldBe("Command status response did not match the tracked lifecycle command.");
    }

    [Fact]
    public async Task Status_lookup_maps_malformed_payload_to_retryable_failure()
    {
        StatusHandler handler = new("{ not-json");
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBeNull();
        result.IsRetryableFailure.ShouldBeTrue();
        result.SafeMessage.ShouldBe("Command status response was unavailable.");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task Status_lookup_retries_only_transient_http_failures(
        HttpStatusCode statusCode,
        bool expectedRetryable)
    {
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}", statusCode))
            {
                BaseAddress = new Uri("https://eventstore.example/"),
            });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBeNull();
        result.IsRetryableFailure.ShouldBe(expectedRetryable);
    }

    [Fact]
    public async Task Status_lookup_maps_http_transport_exception_to_retryable_failure()
    {
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new ThrowingStatusHandler(new HttpRequestException("transport unavailable")))
            {
                BaseAddress = new Uri("https://eventstore.example/"),
            });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBeNull();
        result.IsRetryableFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("PublishFailed", CommandStatus.PublishFailed, "publication could not be verified")]
    [InlineData("TimedOut", CommandStatus.TimedOut, "timed out")]
    public async Task Status_lookup_maps_terminal_non_success_states_to_safe_messages(
        string statusText,
        CommandStatus expectedStatus,
        string expectedMessage)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-123",
              "status": "{{statusText}}",
              "statusCode": {{(int)expectedStatus}},
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": null,
              "failureReason": "raw payload token stack trace correlation-123",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-123")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-123", "correlation-123"),
            CancellationToken.None);

        result.Status.ShouldBe(expectedStatus);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldContain(expectedMessage);
        safeMessage.ShouldNotContain("raw payload", Case.Insensitive);
        safeMessage.ShouldNotContain("token", Case.Insensitive);
        safeMessage.ShouldNotContain("correlation-123", Case.Insensitive);
    }

    [Fact]
    public async Task Status_lookup_generic_rejection_stays_command_neutral_for_shared_status_path()
    {
        // GetStatusAsync is shared by create-tenant and add-member; an unrecognized rejection type
        // must not surface create-tenant-specific copy in another command's lifecycle panel.
        StatusHandler handler = new("""
            {
              "correlationId": "correlation-456",
              "status": "Rejected",
              "statusCode": 5,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": "Hexalith.Tenants.Contracts.Events.Rejections.SomeUnmappedRejection",
              "failureReason": null,
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-456")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-456", "correlation-456"),
            CancellationToken.None);

        result.Status.ShouldBe(CommandStatus.Rejected);
        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldBe("The command was rejected.");
        safeMessage.ShouldNotContain("create tenant", Case.Insensitive);
    }

    [Theory]
    [InlineData("leaked bearer value")]
    [InlineData("decoded jwt eyJhbGciOiJ")]
    [InlineData("internal cursor 00ff")]
    [InlineData("eventstore etag abc123")]
    public async Task Status_lookup_redacts_unsafe_support_markers_in_failure_reason(string failureReason)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "correlation-456",
              "status": "Processing",
              "statusCode": 1,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 0,
              "rejectionEventType": null,
              "failureReason": "{{failureReason}}",
              "timeoutDuration": null
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-456")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-456", "correlation-456"),
            CancellationToken.None);

        string safeMessage = result.SafeMessage.ShouldNotBeNull();
        safeMessage.ShouldBe("The command status included an unavailable support detail.");
        safeMessage.ShouldNotContain("bearer", Case.Insensitive);
        safeMessage.ShouldNotContain("jwt", Case.Insensitive);
        safeMessage.ShouldNotContain("cursor", Case.Insensitive);
        safeMessage.ShouldNotContain("etag", Case.Insensitive);
    }

    /// <summary>Unknown backend details change safe copy while retaining existing commit and rejection evidence.</summary>
    [Theory]
    [InlineData(CommandStatus.Completed, "ordinary-private-detail-Canary")]
    [InlineData(CommandStatus.Completed, "%73%65%63%72%65%74-Canary")]
    [InlineData(CommandStatus.Rejected, "ordinary-private-detail-Canary")]
    [InlineData(CommandStatus.Rejected, "%73%65%63%72%65%74-Canary")]
    public async Task UnknownFailureDetailsPreserveCommittedAndRecognizedRejectionEvidence(CommandStatus status, string failureReason)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        const string correlationId = "tracking-evidence";
        int eventCount = status is CommandStatus.Completed ? 3 : 1;
        string? rejectionType = status is CommandStatus.Rejected
            ? "Hexalith.Tenants.Contracts.Events.Rejections.InsufficientPermissionsRejection" : null;

        Task<TenantCommandStatusResult> ReadStatusAsync(string? detail)
        {
            string json = JsonSerializer.Serialize(new
            {
                tenantId = "system",
                domain = "tenants",
                aggregateId = "tenant.alpha",
                messageId,
                correlationId,
                status = status.ToString(),
                statusCode = (int)status,
                eventCount,
                committedEventSequence = 17L,
                rejectionEventType = rejectionType,
                failureReason = detail,
            });
            TenantCommandGateway gateway = new(new CapturingGatewayClient(new SubmitCommandResponse(correlationId)),
                new StubUlidFactory(messageId), new HttpClient(new StatusHandler(json))
                {
                    BaseAddress = new Uri("https://eventstore.example/"),
                });
            return gateway.GetStatusAsync(new(messageId, correlationId, "tenant.alpha"));
        }

        TenantCommandStatusResult baseline = await ReadStatusAsync(null);
        TenantCommandStatusResult result = await ReadStatusAsync(failureReason);
        result.Status.ShouldBe(status);
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
        result.EventCount.ShouldBe(eventCount);
        result.CommittedEventSequence.ShouldBe(status is CommandStatus.Completed ? 17L : null);
        result.RejectionCode.ShouldBe(status is CommandStatus.Rejected ? "InsufficientPermissions" : null);
        result.IsPending.ShouldBeFalse();
        result.IsRetryableFailure.ShouldBeFalse();
        result.SafeMessage.ShouldBe(status is CommandStatus.Completed
            ? "The command status included an unavailable support detail."
            : "You are not authorized to submit this command.");
        result.SafeMessageKey.ShouldBe(status is CommandStatus.Completed
            ? "Tenants.Commands.Failure.UnavailableSupportDetail" : "Tenants.Commands.Rejection.InsufficientPermissions");
        JsonSerializer.Serialize(result).ShouldNotContain(failureReason);
        (result with { SafeMessage = baseline.SafeMessage, SafeMessageKey = baseline.SafeMessageKey }).ShouldBe(baseline);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Membership_commands_reject_blank_identifiers_without_dispatch(string blank)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("correlation-unused"));
        TenantCommandGateway gateway = new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}")) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandSubmissionResult add = await gateway.AddUserToTenantAsync(
            new AddUserToTenant("tenant.alpha", blank, TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);
        TenantCommandSubmissionResult change = await gateway.ChangeUserRoleAsync(
            new ChangeUserRole(blank, "literal-user", TenantRole.TenantReader),
            cancellationToken: CancellationToken.None);
        TenantCommandSubmissionResult remove = await gateway.RemoveUserFromTenantAsync(
            new RemoveUserFromTenant("tenant.alpha", blank),
            cancellationToken: CancellationToken.None);

        add.State.ShouldBe(TenantCommandLifecycleState.Failed);
        change.State.ShouldBe(TenantCommandLifecycleState.Failed);
        remove.State.ShouldBe(TenantCommandLifecycleState.Failed);
        client.SubmittedCommands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("different-correlation")]
    public async Task Status_lookup_fails_closed_when_response_correlation_is_not_the_requested_handle(string responseCorrelation)
    {
        StatusHandler handler = new($$"""
            {
              "correlationId": "{{responseCorrelation}}",
              "status": "Completed",
              "statusCode": 4,
              "timestamp": "2026-06-06T02:00:00Z",
              "aggregateId": "tenant.alpha",
              "eventCount": 1
            }
            """);
        TenantCommandGateway gateway = new(
            new CapturingGatewayClient(new SubmitCommandResponse("correlation-requested")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

        TenantCommandStatusResult result = await gateway.GetStatusAsync(
            new TenantCommandTrackingHandle("message-1", "correlation-requested"),
            CancellationToken.None);

        result.Status.ShouldBeNull();
        result.IsRetryableFailure.ShouldBeFalse();
        result.SafeMessage.ShouldBe("Command status response did not match the tracked command.");
    }

    [Fact]
    public async Task Stable_lifecycle_overload_fails_closed_for_a_legacy_style_gateway()
    {
        var legacy = new LegacyLifecycleGateway();
        ITenantCommandGateway gateway = legacy;
        var disable = new TenantLifecycleCommandRequest("Tenant.Mixed-01", TenantLifecycleOperation.DisableTenant);
        var enable = new TenantLifecycleCommandRequest("Tenant.Mixed-01", TenantLifecycleOperation.EnableTenant);
        using var cancellation = new CancellationTokenSource();

        TenantCommandSubmissionResult legacyResult = await gateway.DisableTenantAsync(disable);
        TenantCommandSubmissionResult stableDisableResult = await gateway.DisableTenantTrackedAsync(
            disable,
            "01ARZ3NDEKTSV4RRFFQ69G5FAZ",
            cancellation.Token);
        TenantCommandSubmissionResult stableResult = await gateway.EnableTenantTrackedAsync(
            enable,
            "01ARZ3NDEKTSV4RRFFQ69G5FB0",
            cancellation.Token);

        legacy.DisableCalls.ShouldBe(1);
        legacy.EnableCalls.ShouldBe(0);
        legacy.LastEnableRequest.ShouldBeNull();
        legacyResult.MessageId.ShouldBe("legacy-disable-message");
        stableResult.State.ShouldBe(TenantCommandLifecycleState.Failed);
        stableResult.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.CommandSurface");
        stableDisableResult.State.ShouldBe(TenantCommandLifecycleState.Failed);
        stableDisableResult.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.CommandSurface");
    }

    [Fact]
    public async Task Unavailable_gateway_tracked_lifecycle_methods_use_localized_command_surface_key()
    {
        var gateway = new UnavailableTenantCommandGateway();
        var request = new TenantLifecycleCommandRequest("Tenant.Mixed-01", TenantLifecycleOperation.DisableTenant);

        TenantCommandSubmissionResult enable = await gateway.EnableTenantTrackedAsync(
            request with { Operation = TenantLifecycleOperation.EnableTenant },
            "01ARZ3NDEKTSV4RRFFQ69G5FAZ",
            CancellationToken.None);
        TenantCommandSubmissionResult disable = await gateway.DisableTenantTrackedAsync(
            request,
            "01ARZ3NDEKTSV4RRFFQ69G5FAZ",
            CancellationToken.None);

        enable.State.ShouldBe(TenantCommandLifecycleState.Failed);
        enable.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.CommandSurface");
        enable.SafeMessage.ShouldBeNull();
        disable.State.ShouldBe(TenantCommandLifecycleState.Failed);
        disable.SafeMessageKey.ShouldBe("Tenants.Lifecycle.Unavailable.CommandSurface");
        disable.SafeMessage.ShouldBeNull();
    }




    /// <summary>Every validation path keeps dispatch closed and carries localized operation copy.</summary>
    [Theory]
    [InlineData("create", "Tenants.Create.Validation.RequiredFields")]
    [InlineData("add", "Tenants.AddMember.Validation.RequiredFields")]
    [InlineData("change", "Tenants.ChangeRole.Validation.RequiredFields")]
    [InlineData("remove", "Tenants.RemoveMember.Validation.RequiredFields")]
    [InlineData("metadata", "Tenants.EditMetadata.Validation.RequiredFields")]
    [InlineData("set", "Tenants.Configuration.Set.Validation.RequiredFields")]
    [InlineData("remove-configuration", "Tenants.Configuration.Remove.Validation.RequiredFields")]
    [InlineData("enable", "Tenants.Lifecycle.Validation.RequiredFields")]
    [InlineData("disable", "Tenants.Lifecycle.Validation.RequiredFields")]
    public async Task ValidationFailuresCarryOperationKeysWithoutDispatch(string operation, string expectedKey)
    {
        CapturingGatewayClient client = new(new SubmitCommandResponse("unused"));
        TenantCommandSubmissionResult result = await SubmitOperation(CreateGateway(client), operation, invalid: true);
        result.State.ShouldBe(TenantCommandLifecycleState.Failed);
        result.SafeMessageKey.ShouldBe(expectedKey);
        result.MessageId.ShouldBeNull();
        result.CorrelationId.ShouldBeNull();
        result.RejectionCode.ShouldBeNull();
        result.IsAmbiguousFailure.ShouldBeFalse();
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        client.SubmittedCommands.ShouldBeEmpty();
    }

    /// <summary>Operation-specific submission classifications and identities survive localization.</summary>
    public static TheoryData<string, int, string> LocalizedSubmissionCases
    {
        get
        {
            TheoryData<string, int, string> cases = [];
            foreach ((string operation, string invalid, string forbidden, string unavailable, string failed) in new[]
            {
                ("create", "Tenants.Create.Submission.Invalid", "Tenants.Create.Unavailable.Authorization", "Tenants.Create.Submission.Unavailable", "Tenants.Create.Submission.Failed"),
                ("add", "Tenants.AddMember.Submission.Invalid", "Tenants.AddMember.Unavailable.Authorization", "Tenants.AddMember.Submission.Unavailable", "Tenants.AddMember.Submission.Failed"),
                ("change", "Tenants.ChangeRole.Submission.Invalid", "Tenants.ChangeRole.Unavailable.Authorization", "Tenants.ChangeRole.Submission.Unavailable", "Tenants.ChangeRole.Submission.Failed"),
                ("remove", "Tenants.RemoveMember.Submission.Invalid", "Tenants.RemoveMember.Unavailable.Authorization", "Tenants.RemoveMember.Submission.Unavailable", "Tenants.RemoveMember.Submission.Failed"),
                ("metadata", "Tenants.EditMetadata.Submission.Invalid", "Tenants.EditMetadata.Unavailable.Authorization", "Tenants.EditMetadata.Submission.Unavailable", "Tenants.EditMetadata.Submission.Failed"),
                ("set", "Tenants.Configuration.Set.Submission.Invalid", "Tenants.Configuration.Set.Unavailable.Authorization", "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous", "Tenants.Configuration.Set.Submission.VerificationFailed"),
                ("remove-configuration", "Tenants.Configuration.Remove.Submission.Invalid", "Tenants.Configuration.Remove.Unavailable.Authorization", "Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous", "Tenants.Configuration.Remove.Submission.VerificationFailed"),
                ("enable", "Tenants.Lifecycle.Submission.Invalid", "Tenants.Lifecycle.Message.Rejected.InsufficientPermissions", "Tenants.Lifecycle.SubmissionEvidence.Ambiguous", "Tenants.Lifecycle.Submission.Failed"),
                ("disable", "Tenants.Lifecycle.Submission.Invalid", "Tenants.Lifecycle.Message.Rejected.InsufficientPermissions", "Tenants.Lifecycle.SubmissionEvidence.Ambiguous", "Tenants.Lifecycle.Submission.Failed"),
                ("grant", "Tenants.GlobalAdministrators.Grant.Submission.Invalid", "Tenants.GlobalAdministrators.Grant.Submission.Rejected", "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous", "Tenants.GlobalAdministrators.Grant.Submission.Failed"),
                ("remove-global", "Tenants.GlobalAdministrators.Remove.Submission.Invalid", "Tenants.GlobalAdministrators.Remove.Submission.Unauthorized", "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous", "Tenants.GlobalAdministrators.Remove.Submission.Failed"),
            })
            {
                cases.Add(operation, 400, invalid);
                cases.Add(operation, 403, forbidden);
                cases.Add(operation, 503, unavailable);
                cases.Add(operation, 418, failed);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(LocalizedSubmissionCases))]
    public async Task SubmissionFailuresRemainClassifiedAndLocalized(string operation, int statusCode, string expectedKey)
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        CapturingGatewayClient client = new(new EventStoreGatewayException(statusCode, "backend-secret-Canary"));
        TenantCommandSubmissionResult result = await SubmitOperation(CreateGateway(client), operation);
        bool trackedAmbiguity = statusCode == 503 && operation is "set" or "remove-configuration" or "enable" or "disable" or "grant" or "remove-global";
        bool legacyAmbiguity = statusCode == 503 && operation is "create" or "add" or "change";
        result.State.ShouldBe(trackedAmbiguity ? TenantCommandLifecycleState.RequestSent
            : statusCode == 403 ? TenantCommandLifecycleState.Rejected : TenantCommandLifecycleState.Failed);
        result.IsAmbiguousFailure.ShouldBe(trackedAmbiguity || legacyAmbiguity);
        result.MessageId.ShouldBe(messageId);
        result.CorrelationId.ShouldBeNull();
        result.RejectionCode.ShouldBe(statusCode == 403 ? "InsufficientPermissions" : null);
        result.SafeMessageKey.ShouldBe(expectedKey);
        if (statusCode == 418 && operation is "set" or "remove-configuration")
        {
            ResourceManager resources = new("Hexalith.Tenants.UI.Resources.TenantsResources", typeof(TenantsResources).Assembly);
            resources.GetString(expectedKey, CultureInfo.GetCultureInfo("en"))
                .ShouldBe("Tenant command submission failed before it could be verified.");
        }
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        JsonSerializer.Serialize(result).ShouldNotContain("backend-secret-Canary");
        client.SubmittedCommands.ShouldHaveSingleItem().MessageId.ShouldBe(messageId);
    }

    [Theory]
    [InlineData("TenantAlreadyExists")]
    [InlineData("UserAlreadyInTenant")]
    [InlineData("UserNotInTenant")]
    [InlineData("RoleEscalation")]
    [InlineData("InsufficientPermissions")]
    [InlineData("TenantDisabled")]
    [InlineData("TenantNotFound")]
    [InlineData("ConfigurationLimitExceeded")]
    [InlineData("ConfigurationKeyNotFound")]
    [InlineData("TenantLifecycleStateAlreadySet")]
    [InlineData("GlobalAdministratorAlreadyExists")]
    [InlineData("LastGlobalAdministrator")]
    [InlineData("GlobalAdministratorNotFound")]
    public async Task RecognizedSharedStatusRejectionsCarryNeutralResourceKeys(string code)
    {
        string body = StatusJson("Rejected", (int)CommandStatus.Rejected, rejection: code);
        TenantCommandStatusResult result = await StatusGateway(new StatusHandler(body)).GetStatusAsync(
            new("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-original", "tenant.alpha"));
        result.Status.ShouldBe(CommandStatus.Rejected);
        result.RejectionCode.ShouldBe(code);
        result.SafeMessageKey.ShouldBe("Tenants.Commands.Rejection." + code);
        result.IsPending.ShouldBeFalse();
        result.IsRetryableFailure.ShouldBeFalse();
        result.HasVerifiedCommandIdentity.ShouldBeTrue();
        result.EventCount.ShouldBe(0);
        result.CommittedEventSequence.ShouldBeNull();
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
    }

    [Theory]
    [InlineData("pending", "Tenants.Commands.Status.Pending", true, false)]
    [InlineData("http-transient", "Tenants.Commands.Status.RetryableFailure", false, true)]
    [InlineData("http-rejected", "Tenants.Commands.Status.RequestRejected", false, false)]
    [InlineData("malformed", "Tenants.Commands.Status.ResponseUnavailable", false, true)]
    [InlineData("null", "Tenants.Commands.Status.ResponseUnavailable", false, true)]
    [InlineData("unknown-name", "Tenants.Commands.Status.ResponseUnavailable", false, true)]
    [InlineData("unknown-enum", "Tenants.Commands.Status.ResponseUnavailable", false, false)]
    [InlineData("correlation-mismatch", "Tenants.Commands.Status.TrackingMismatch", false, false)]
    [InlineData("scope-mismatch", "Tenants.Commands.Status.TrackingMismatch", false, false)]
    [InlineData("identity-mismatch", "Tenants.Commands.Status.TrackingMismatch", false, false)]
    [InlineData("transport", "Tenants.Commands.Status.RetryableFailure", false, true)]
    [InlineData("timeout", "Tenants.Commands.Status.RetryableFailure", false, true)]
    public async Task StatusLookupMatrixCarriesLocalizedKeysWithoutInventingProof(string scenario, string expectedKey, bool pending, bool retryable)
    {
        HttpMessageHandler handler = scenario switch
        {
            "pending" => new StatusHandler("backend-secret-Canary", HttpStatusCode.NotFound),
            "http-transient" => new StatusHandler("backend-secret-Canary", HttpStatusCode.ServiceUnavailable),
            "http-rejected" => new StatusHandler("backend-secret-Canary", HttpStatusCode.Forbidden),
            "malformed" => new StatusHandler("{backend-secret-Canary"),
            "null" => new StatusHandler("null"),
            "unknown-name" => new StatusHandler(StatusJson("backend-secret-Canary", 999)),
            "unknown-enum" => new StatusHandler(StatusJson("999", 999)),
            "correlation-mismatch" => new StatusHandler(StatusJson("Completed", (int)CommandStatus.Completed, correlation: "backend-secret-Canary")),
            "scope-mismatch" => new StatusHandler(StatusJson("Completed", (int)CommandStatus.Completed, domain: "backend-secret-Canary")),
            "identity-mismatch" => new StatusHandler(StatusJson("Completed", (int)CommandStatus.Completed, messageId: "backend-secret-Canary")),
            "transport" => new ThrowingStatusHandler(new HttpRequestException("backend-secret-Canary")),
            _ => new ThrowingStatusHandler(new TaskCanceledException("backend-secret-Canary")),
        };
        TenantCommandStatusResult result = await StatusGateway(handler).GetStatusAsync(
            new("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-original", "tenant.alpha"));
        result.SafeMessageKey.ShouldBe(expectedKey);
        result.Status.ShouldBe(scenario == "identity-mismatch" ? CommandStatus.Completed : null);
        result.IsPending.ShouldBe(pending);
        result.IsRetryableFailure.ShouldBe(retryable);
        result.HasVerifiedCommandIdentity.ShouldBeFalse();
        result.RejectionCode.ShouldBeNull();
        result.CommittedEventSequence.ShouldBeNull();
        AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        JsonSerializer.Serialize(result).ShouldNotContain("backend-secret-Canary");
    }

    [Fact]
    public async Task EveryConcreteUnavailableEntryCarriesLocalizedCopyAndNoIdentity()
    {
        ITenantCommandGateway gateway = new UnavailableTenantCommandGateway();
        foreach ((string operation, string expectedKey) in new[]
        {
            ("create", "Tenants.Create.Unavailable.GatewayConfiguration"),
            ("add", "Tenants.AddMember.Unavailable.GatewayConfiguration"),
            ("change", "Tenants.ChangeRole.Unavailable.GatewayConfiguration"),
            ("remove", "Tenants.RemoveMember.Unavailable.GatewayConfiguration"),
            ("metadata", "Tenants.EditMetadata.Unavailable.GatewayConfiguration"),
            ("set", "Tenants.Configuration.Set.Unavailable.GatewayConfiguration"),
            ("set-tracked", "Tenants.Configuration.Set.Unavailable.TrackedDispatch"),
            ("remove-configuration", "Tenants.Configuration.Remove.Unavailable.GatewayConfiguration"),
            ("remove-configuration-tracked", "Tenants.Configuration.Remove.Unavailable.TrackedDispatch"),
            ("grant", "Tenants.GlobalAdministrators.Grant.Unavailable.GatewayConfiguration"),
            ("grant-tracked", "Tenants.GlobalAdministrators.Grant.Unavailable.TrackedDispatch"),
            ("remove-global", "Tenants.GlobalAdministrators.Remove.Unavailable.GatewayConfiguration"),
            ("remove-global-tracked", "Tenants.GlobalAdministrators.Remove.Unavailable.TrackedDispatch"),
            ("enable", "Tenants.Lifecycle.Unavailable.GatewayConfiguration"),
            ("enable-tracked", "Tenants.Lifecycle.Unavailable.CommandSurface"),
            ("disable", "Tenants.Lifecycle.Unavailable.GatewayConfiguration"),
            ("disable-tracked", "Tenants.Lifecycle.Unavailable.CommandSurface"),
        })
        {
            TenantCommandSubmissionResult result = await SubmitOperation(gateway, operation);
            result.State.ShouldBe(TenantCommandLifecycleState.Failed);
            result.MessageId.ShouldBeNull();
            result.CorrelationId.ShouldBeNull();
            result.RejectionCode.ShouldBeNull();
            result.IsAmbiguousFailure.ShouldBeFalse();
            result.SafeMessageKey.ShouldBe(expectedKey);
            AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        }

        TenantCommandStatusResult status = await gateway.GetStatusAsync(new("original-message", "original-correlation"));
        status.SafeMessage.ShouldBe("Tenant command status lookup is unavailable.");
        status.SafeMessageKey.ShouldBe("Tenants.Commands.Status.Unavailable");
        AssertShippedWholeMessage(status.SafeMessageKey, status.SafeMessage);
        status.Status.ShouldBeNull();
        status.HasVerifiedCommandIdentity.ShouldBeFalse();
        status.IsPending.ShouldBeFalse();
        status.IsRetryableFailure.ShouldBeFalse();
        status.EventCount.ShouldBeNull();
        status.CommittedEventSequence.ShouldBeNull();
    }

    [Fact]
    public async Task EveryInterfaceDefaultCarriesLocalizedCopyWithoutDispatch()
    {
        ITenantCommandGateway gateway = new DefaultTenantCommandGateway();
        foreach ((string operation, string expectedKey) in new[]
        {
            ("set-tracked", "Tenants.Configuration.Set.Unavailable.TrackedDispatch"),
            ("remove-configuration", "Tenants.Configuration.Remove.Unavailable.Gateway"),
            ("remove-configuration-tracked", "Tenants.Configuration.Remove.Unavailable.TrackedDispatch"),
            ("grant", "Tenants.GlobalAdministrators.Grant.Unavailable.Gateway"),
            ("grant-tracked", "Tenants.GlobalAdministrators.Grant.Unavailable.TrackedDispatch"),
            ("remove-global", "Tenants.GlobalAdministrators.Remove.Unavailable.Gateway"),
            ("remove-global-tracked", "Tenants.GlobalAdministrators.Remove.Unavailable.TrackedDispatch"),
            ("enable", "Tenants.Lifecycle.Unavailable.Gateway"),
            ("enable-tracked", "Tenants.Lifecycle.Unavailable.CommandSurface"),
            ("disable", "Tenants.Lifecycle.Unavailable.Gateway"),
            ("disable-tracked", "Tenants.Lifecycle.Unavailable.CommandSurface"),
        })
        {
            TenantCommandSubmissionResult result = await SubmitOperation(gateway, operation);
            result.State.ShouldBe(TenantCommandLifecycleState.Failed);
            result.MessageId.ShouldBeNull();
            result.CorrelationId.ShouldBeNull();
            result.RejectionCode.ShouldBeNull();
            result.IsAmbiguousFailure.ShouldBeFalse();
            result.SafeMessageKey.ShouldBe(expectedKey);
            AssertShippedWholeMessage(result.SafeMessageKey, result.SafeMessage);
        }
    }

    private static TenantCommandGateway StatusGateway(HttpMessageHandler handler)
        => new(new CapturingGatewayClient(new SubmitCommandResponse("correlation-original")),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(handler) { BaseAddress = new Uri("https://eventstore.example/") });

    private static string StatusJson(string status, int statusCode, string? rejection = null,
        string correlation = "correlation-original", string domain = "tenants", string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV")
        => JsonSerializer.Serialize(new
        {
            messageId, correlationId = correlation, status, statusCode, domain,
            tenantId = "system", aggregateId = "tenant.alpha", eventCount = 0,
            rejectionEventType = rejection, failureReason = "backend-secret-Canary",
        });

    private static Task<TenantCommandSubmissionResult> SubmitOperation(ITenantCommandGateway gateway, string operation, bool invalid = false,
        string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV", CancellationToken cancellationToken = default)
    {
        string tenant = invalid ? "" : "tenant.alpha";
        return operation switch
        {
            "create" => gateway.CreateTenantAsync(new(tenant, "Alpha", null), messageId),
            "add" => gateway.AddUserToTenantAsync(new(tenant, "literal-user", TenantRole.TenantReader), messageId),
            "change" => gateway.ChangeUserRoleAsync(new(tenant, "literal-user", TenantRole.TenantContributor), messageId),
            "remove" => gateway.RemoveUserFromTenantAsync(new(tenant, "literal-user"), messageId),
            "metadata" => gateway.UpdateTenantAsync(new(tenant, "Alpha updated", null), messageId),
            "set" => gateway.SetTenantConfigurationAsync(new(tenant, "key", "value")),
            "set-tracked" => gateway.SetTenantConfigurationTrackedAsync(new(tenant, "key", "value"), messageId, cancellationToken),
            "remove-configuration" => gateway.RemoveTenantConfigurationAsync(new(tenant, "key")),
            "remove-configuration-tracked" => gateway.RemoveTenantConfigurationTrackedAsync(new(tenant, "key"), messageId, cancellationToken),
            "grant" => gateway.SetGlobalAdministratorAsync(new("literal-admin")),
            "grant-tracked" => gateway.SetGlobalAdministratorTrackedAsync(new(invalid ? "" : "literal-admin"), messageId, cancellationToken),
            "remove-global" => gateway.RemoveGlobalAdministratorAsync(new("literal-admin")),
            "remove-global-tracked" => gateway.RemoveGlobalAdministratorTrackedAsync(new(invalid ? "" : "literal-admin"), messageId, cancellationToken),
            "enable" => gateway.EnableTenantAsync(new(tenant, TenantLifecycleOperation.EnableTenant)),
            "enable-tracked" => gateway.EnableTenantTrackedAsync(new(tenant, TenantLifecycleOperation.EnableTenant), messageId, cancellationToken),
            "disable" => gateway.DisableTenantAsync(new(tenant, TenantLifecycleOperation.DisableTenant)),
            "disable-tracked" => gateway.DisableTenantTrackedAsync(new(tenant, TenantLifecycleOperation.DisableTenant), messageId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static void AssertShippedWholeMessage(string? key, string? fallback)
    {
        key.ShouldNotBeNullOrWhiteSpace();
        ResourceManager resources = new("Hexalith.Tenants.UI.Resources.TenantsResources", typeof(TenantsResources).Assembly);
        string english = resources.GetString(key, CultureInfo.GetCultureInfo("en")).ShouldNotBeNull();
        string french = resources.GetString(key, CultureInfo.GetCultureInfo("fr")).ShouldNotBeNull();
        french.ShouldNotBe(english);
        french.ShouldNotBe(key);
        if (fallback is not null)
        {
            fallback.ShouldNotContain("backend-secret-Canary");
        }
    }

    private static TenantCommandGateway CreateLifecycleGateway(object response)
        => new(
            new CapturingGatewayClient(response),
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}"))
            {
                BaseAddress = new Uri("https://eventstore.example/"),
            });

    private static TenantCommandGateway CreateGateway(CapturingGatewayClient client)
        => new(
            client,
            new StubUlidFactory("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            new HttpClient(new StatusHandler("{}"))
            {
                BaseAddress = new Uri("https://eventstore.example/"),
            });








}
