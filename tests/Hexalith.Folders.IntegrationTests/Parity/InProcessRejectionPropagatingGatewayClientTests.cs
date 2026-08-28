using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Parity.Testing;
using Hexalith.Folders.Server;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.IntegrationTests.Parity;

public sealed class InProcessRejectionPropagatingGatewayClientTests
{
    [Theory]
    [InlineData(nameof(FolderResultCode.ValidationFailed), 400, "validation_error")]
    [InlineData(nameof(FolderResultCode.FolderAclDenied), 403, "folder_acl_denied")]
    [InlineData(nameof(FolderResultCode.IdempotencyConflict), 409, "idempotency_conflict")]
    [InlineData(nameof(FolderResultCode.LockNotOwned), 409, "lock_not_owned")]
    [InlineData(nameof(FolderResultCode.LockExpired), 410, "lock_expired")]
    [InlineData(nameof(FolderResultCode.StateTransitionInvalid), 422, "state_transition_invalid")]
    [InlineData(nameof(FolderResultCode.ProviderRateLimited), 429, "provider_rate_limited")]
    [InlineData(nameof(FolderResultCode.ProviderUnavailable), 503, "provider_unavailable")]
    [InlineData(nameof(FolderResultCode.FolderNotFound), 404, "not_found")]
    [InlineData(nameof(FolderResultCode.StaleProjection), 503, "projection_stale")]
    public async Task DefinedRejectionCodeShouldUseCanonicalMapping(
        string code,
        int expectedStatus,
        string expectedReasonCode)
    {
        const string correlationId = "correlation-gateway-a";
        string rejectionPayload = JsonSerializer.Serialize(new { Code = code });

        EventStoreGatewayException exception = await SubmitRejectedCommandAsync(rejectionPayload, correlationId).ConfigureAwait(true);

        exception.StatusCode.ShouldBe(expectedStatus);
        exception.ReasonCode.ShouldBe(expectedReasonCode);
        exception.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public async Task LowercaseCodePropertyShouldUseCanonicalMapping()
    {
        const string correlationId = "correlation-lowercase-a";

        EventStoreGatewayException exception = await SubmitRejectedCommandAsync(
            "{\"code\":\"LockExpired\"}",
            correlationId).ConfigureAwait(true);

        exception.StatusCode.ShouldBe(410);
        exception.ReasonCode.ShouldBe("lock_expired");
        exception.CorrelationId.ShouldBe(correlationId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Code\":\"\"}")]
    [InlineData("{\"Code\":42}")]
    [InlineData("{\"Code\":\"NotAResultCode\"}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"ValidationFailed\"")]
    [InlineData("{\"code\":\"LockExpired\",\"Code\":\"LockExpired\"}")]
    [InlineData("{\"Code\":\"0\"}")]
    [InlineData("{\"Code\":\"validationfailed\"}")]
    [InlineData("{\"Code\":\" ValidationFailed \"}")]
    [InlineData("{\"Code\":\"lockexpired\"}")]
    [InlineData("{\"Code\":\"LOCKEXPIRED\"}")]
    [InlineData("{\"Code\":\" LockExpired \"}")]
    [InlineData("{\"Code\":\"999\"}")]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"Code\":\"LockExpired\"")]
    [InlineData("{\"Code\":\"Accepted\"}")]
    [InlineData("{\"Code\":\"Created\"}")]
    [InlineData("{\"Code\":\"IdempotentReplay\"}")]
    [InlineData("{\"Code\":\"AlreadyApplied\"}")]
    public async Task MalformedRejectionCodeShouldUseCanonicalMalformedEvidenceMapping(string rejectionPayload)
    {
        const string correlationId = "correlation-malformed-a";

        EventStoreGatewayException exception = await SubmitRejectedCommandAsync(rejectionPayload, correlationId).ConfigureAwait(true);

        exception.StatusCode.ShouldBe(400);
        exception.ReasonCode.ShouldBe("validation_error");
        exception.CorrelationId.ShouldBe(correlationId);
        exception.Message.ShouldBe("Rejected");
    }

    [Fact]
    public async Task NullRejectionPayloadShouldUseCanonicalMalformedEvidenceMapping()
    {
        const string correlationId = "correlation-null-payload-a";

        EventStoreGatewayException exception = await SubmitRejectedCommandAsync((byte[]?)null, correlationId).ConfigureAwait(true);

        exception.StatusCode.ShouldBe(400);
        exception.ReasonCode.ShouldBe("validation_error");
        exception.CorrelationId.ShouldBe(correlationId);
        exception.Message.ShouldBe("Rejected");
    }

    /// <summary>
    /// Drift guard for the class remark's claim that this boundary cannot diverge from the production
    /// Folders error surface. Every declared <see cref="FolderResultCode"/> name -- not just the ten rows
    /// the focused theory pins -- must survive the strict wire parse and resolve to the mapper's own
    /// category and status. A member added under an alias name, or a strict-parse regression that rejected
    /// a legitimate name, would silently degrade to MalformedEvidence here and fail this test.
    /// </summary>
    [Fact]
    public async Task EveryDeclaredRejectionCodeNameShouldResolveThroughTheCanonicalMapper()
    {
        string currentPayload = "{}";

        await WithRejectionHostAsync<object?>(
            () => Encoding.UTF8.GetBytes(currentPayload),
            async (gateway, cancellationToken) =>
            {
                foreach (string name in Enum.GetNames<FolderResultCode>())
                {
                    string category = FolderCanonicalErrorMapper.CategoryFor(Enum.Parse<FolderResultCode>(name));
                    if (string.Equals(category, "success", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    currentPayload = JsonSerializer.Serialize(new { Code = name });
                    EventStoreGatewayException exception = await SubmitAsync(
                        gateway,
                        "correlation-exhaustive-a",
                        cancellationToken).ConfigureAwait(true);

                    exception.ReasonCode.ShouldBe(category, $"rejection code {name}");
                    exception.StatusCode.ShouldBe(
                        FolderCanonicalErrorMapper.StatusFor(category),
                        $"rejection code {name}");
                }

                return null;
            }).ConfigureAwait(true);
    }

    private static Task<EventStoreGatewayException> SubmitRejectedCommandAsync(
        string rejectionPayload,
        string correlationId)
        => SubmitRejectedCommandAsync(Encoding.UTF8.GetBytes(rejectionPayload), correlationId);

    private static Task<EventStoreGatewayException> SubmitRejectedCommandAsync(
        byte[]? rejectionPayload,
        string correlationId)
        => WithRejectionHostAsync(
            () => rejectionPayload,
            async (gateway, cancellationToken) =>
            {
                EventStoreGatewayException exception = await SubmitAsync(gateway, correlationId, cancellationToken).ConfigureAwait(true);
                gateway.ProcessCalls.ShouldBe(1);
                return exception;
            });

    private static async Task<EventStoreGatewayException> SubmitAsync(
        InProcessRejectionPropagatingGatewayClient gateway,
        string correlationId,
        CancellationToken cancellationToken)
    {
        SubmitCommandRequest request = new(
            MessageId: "message-gateway-a",
            Tenant: "tenant-a",
            Domain: "folders",
            AggregateId: "folder-a",
            CommandType: "TestCommand",
            Payload: JsonSerializer.SerializeToElement(new { }),
            CorrelationId: correlationId);

        return await Should.ThrowAsync<EventStoreGatewayException>(
            () => gateway.SubmitCommandAsync(request, cancellationToken)).ConfigureAwait(true);
    }

    private static async Task<T> WithRejectionHostAsync<T>(
        Func<byte[]?> rejectionPayloadProvider,
        Func<InProcessRejectionPropagatingGatewayClient, CancellationToken, Task<T>> body)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
        });
        builder.WebHost.UseTestServer();

        WebApplication app = builder.Build();
        try
        {
            app.MapPost(
                "/process",
                () => new DomainServiceWireResult(
                    IsRejection: true,
                    Events:
                    [
                        new DomainServiceWireEvent(
                            typeof(FolderCommandRejected).FullName!,
                            rejectionPayloadProvider()!),
                    ]));

            await app.StartAsync(cancellationToken).ConfigureAwait(true);
            InProcessRejectionPropagatingGatewayClient gateway = new(
                app.GetTestClient,
                () => "actor-a");

            return await body(gateway, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            try
            {
                await app.StopAsync(CancellationToken.None).ConfigureAwait(true);
            }
            finally
            {
                await app.DisposeAsync().ConfigureAwait(true);
            }
        }
    }
}
