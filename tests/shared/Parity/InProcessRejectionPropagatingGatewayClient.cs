using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// Shared in-process <see cref="IEventStoreGatewayClient"/> test double for the cross-surface parity
/// integration tests (Story 8.3). It round-trips the submitted command through the same host's
/// <c>/process</c> endpoint and — critically — <b>propagates aggregate rejections</b> instead of flattening
/// them: when the wire result carries <see cref="DomainServiceWireResult.IsRejection"/>, it throws an
/// <see cref="EventStoreGatewayException"/> carrying both the canonical HTTP status <i>and</i> the rejection
/// <c>reasonCode</c> (the <see cref="FolderResultCode"/> name), exactly as the production gateway translates
/// a rejection at the gateway hop. This lets the REST endpoint's <c>ToArchiveGatewayProblem</c> surface the
/// canonical category (e.g. <c>idempotency_conflict</c> → 409, <c>folder_acl_denied</c> → 403) over the wire
/// on every surface (AC #2, AC #3, AC #4).
/// </summary>
/// <remarks>
/// <para>This replaces the prior per-file flattening stubs (which called
/// <c>response.EnsureSuccessStatusCode()</c> and returned a success <see cref="SubmitCommandResponse"/>,
/// discarding the <c>IsRejection</c> body). It is the no-mock acceptance path required by the project
/// testing rules — it does not fake the gateway's behavior, it drives the real
/// REST → gateway → <c>/process</c> → processor → gate round-trip and fans the result back exactly as the
/// production gateway would.</para>
/// <para>The <c>clientFactory</c> yields an <see cref="HttpClient"/> bound to the in-process host (a
/// <c>TestServer</c> client or a loopback-Kestrel client); the <c>principalIdAccessor</c> is read per call so
/// a host that mutates the acting principal mid-test is honored. The <c>FolderResultCode</c> → HTTP-status
/// table mirrors the one proven green in <c>ArchiveFolderProcessWiringTests</c>.</para>
/// </remarks>
internal sealed class InProcessRejectionPropagatingGatewayClient(
    Func<HttpClient> clientFactory,
    Func<string?> principalIdAccessor) : IEventStoreGatewayClient
{
    /// <summary>Gets the number of <c>/process</c> round-trips performed (one per submitted command).</summary>
    public int ProcessCalls { get; private set; }

    /// <summary>Gets the framework-event count returned by the last <c>/process</c> round-trip.</summary>
    public int LastWireEventCount { get; private set; }

    /// <inheritdoc/>
    public async Task<SubmitCommandResponse> SubmitCommandAsync(
        SubmitCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ProcessCalls++;
        using HttpClient client = clientFactory();
        CommandEnvelope envelope = new(
            request.MessageId,
            request.Tenant,
            request.Domain,
            request.AggregateId,
            request.CommandType,
            JsonSerializer.SerializeToUtf8Bytes(request.Payload),
            request.CorrelationId ?? request.MessageId,
            CausationId: null,
            principalIdAccessor() ?? "actor-present",
            request.Extensions);

        HttpResponseMessage response = await client
            .PostAsJsonAsync("/process", new DomainServiceRequest(envelope, CurrentState: null), cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new EventStoreGatewayException((int)response.StatusCode, response.ReasonPhrase ?? "Process failed", correlationId: request.CorrelationId);
        }

        DomainServiceWireResult result = (await response.Content
            .ReadFromJsonAsync<DomainServiceWireResult>(cancellationToken)
            .ConfigureAwait(false))!;
        LastWireEventCount = result.Events.Count;

        if (result.IsRejection)
        {
            throw ToGatewayException(result, request.CorrelationId ?? request.MessageId);
        }

        return new SubmitCommandResponse(request.CorrelationId ?? request.MessageId);
    }

    /// <inheritdoc/>
    public Task<EventStoreQueryResult> SubmitQueryAsync(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(
        SubmitQueryRequest request,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<StreamReadPage> ReadStreamAsync(
        StreamReadRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private static EventStoreGatewayException ToGatewayException(DomainServiceWireResult result, string correlationId)
    {
        DomainServiceWireEvent rejection = result.Events.Single();
        using JsonDocument document = JsonDocument.Parse(rejection.Payload);
        string code = document.RootElement.TryGetProperty("code", out JsonElement camelCode)
            ? camelCode.GetString() ?? nameof(FolderResultCode.MalformedEvidence)
            : document.RootElement.GetProperty("Code").GetString() ?? nameof(FolderResultCode.MalformedEvidence);
        int status = code switch
        {
            nameof(FolderResultCode.IdempotencyConflict) => 409,
            nameof(FolderResultCode.FolderNotFound) => 404,
            nameof(FolderResultCode.ProviderRateLimited) => 429,
            nameof(FolderResultCode.ValidationFailed)
                or nameof(FolderResultCode.MalformedJsonPayload)
                or nameof(FolderResultCode.InvalidFolderId)
                or nameof(FolderResultCode.InvalidTenant)
                or nameof(FolderResultCode.ReservedTenant) => 400,
            nameof(FolderResultCode.StaleProjection)
                or nameof(FolderResultCode.UnavailableProjection)
                or nameof(FolderResultCode.PolicyEvidenceUnavailable)
                or nameof(FolderResultCode.PolicyEvidenceStale)
                or nameof(FolderResultCode.AclEvidenceUnavailable) => 503,
            _ => 403,
        };

        // Carry the rejection code as the gateway reasonCode so the REST endpoint's reasonCode-keyed
        // mapping (ToArchiveGatewayProblem → SafeGatewayReasonCode) surfaces the canonical category
        // (idempotency_conflict, folder_acl_denied, …) — not just the HTTP status. This is the
        // difference that makes the cross-surface category/exit-code/failure-kind assertions real.
        return new EventStoreGatewayException(status, "Rejected", correlationId: correlationId, reasonCode: code);
    }
}
