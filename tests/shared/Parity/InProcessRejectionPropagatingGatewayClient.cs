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
using Hexalith.Folders.Server;

namespace Hexalith.Folders.Parity.Testing;

/// <summary>
/// Shared in-process <see cref="IEventStoreGatewayClient"/> test double for the cross-surface parity
/// integration tests (Story 8.3). It round-trips the submitted command through the same host's
/// <c>/process</c> endpoint and — critically — <b>propagates aggregate rejections</b> instead of flattening
/// them: when the wire result carries <see cref="DomainServiceWireResult.IsRejection"/>, it throws an
/// <see cref="EventStoreGatewayException"/> carrying both the canonical HTTP status <i>and</i> the rejection
/// <c>reasonCode</c> (the canonical snake-case category) from the production Folders mapper. This fidelity
/// claim ends at the gateway exception boundary; downstream handlers remain responsible for their own
/// remapping.
/// </summary>
/// <remarks>
/// <para>This replaces the prior per-file flattening stubs (which called
/// <c>response.EnsureSuccessStatusCode()</c> and returned a success <see cref="SubmitCommandResponse"/>,
/// discarding the <c>IsRejection</c> body). It is the no-mock acceptance path required by the project
/// testing rules — it drives the real REST → gateway → <c>/process</c> → processor → gate round-trip and
/// preserves rejection metadata at the gateway exception boundary.</para>
/// <para>The <c>clientFactory</c> yields an <see cref="HttpClient"/> bound to the in-process host (a
/// <c>TestServer</c> client or a loopback-Kestrel client); the <c>principalIdAccessor</c> is read per call so
/// a host that mutates the acting principal mid-test is honored.</para>
/// <para>Successful round-trips propagate the <c>/process</c> result payload into the returned
/// <see cref="SubmitCommandResponse"/> rather than discarding it, so endpoints that derive caller-visible
/// fields from it (e.g. <c>idempotentReplay</c>) behave as they do over the production gateway. Two known
/// fidelity gaps remain: the returned <c>MessageId</c> echoes the submitted request instead of the wire
/// result, and the production client's oversized-payload guard is not reproduced.</para>
/// <para><c>envelopeTenantTransform</c> is an opt-in seam for tenant-smuggling rows: it rewrites only the
/// <c>/process</c> envelope tenant, leaving the authenticated REST tenant intact. It defaults to null, so
/// existing callers observe unchanged envelope behavior.</para>
/// <para>Rejection status, reason-code, and success-code membership all resolve through
/// <see cref="FolderCanonicalErrorMapper"/>, so this boundary keeps no result-code table of its own and
/// cannot drift from the production Folders error surface as result codes are added. This does not assert
/// downstream transport fidelity.</para>
/// </remarks>
internal sealed class InProcessRejectionPropagatingGatewayClient(
    Func<HttpClient> clientFactory,
    Func<string?> principalIdAccessor,
    Func<string, string>? envelopeTenantTransform = null) : IEventStoreGatewayClient
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
        string envelopeTenant = envelopeTenantTransform?.Invoke(request.Tenant) ?? request.Tenant;
        ProcessCalls++;
        using HttpClient client = clientFactory();
        CommandEnvelope envelope = new(
            request.MessageId,
            envelopeTenant,
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

        JsonElement? resultPayload = null;
        if (!string.IsNullOrWhiteSpace(result.ResultPayload))
        {
            using JsonDocument document = JsonDocument.Parse(result.ResultPayload);
            resultPayload = document.RootElement.Clone();
        }

        return new SubmitCommandResponse(
            request.CorrelationId ?? request.MessageId,
            resultPayload,
            request.MessageId);
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
        FolderResultCode resultCode = ParseRejectionCode(rejection.Payload);
        string category = FolderCanonicalErrorMapper.CategoryFor(resultCode);
        int status = FolderCanonicalErrorMapper.StatusFor(category);

        return new EventStoreGatewayException(status, "Rejected", correlationId: correlationId, reasonCode: category);
    }

    private static FolderResultCode ParseRejectionCode(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            // A rejection carrying no evidence bytes is unusable. A null payload survives the wire
            // round-trip (the record is positional, so JSON null binds straight through) and would make
            // JsonDocument.Parse throw ArgumentNullException, which the JsonException catch below cannot
            // absorb; an empty body is the same absence of evidence.
            return FolderResultCode.MalformedEvidence;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            // A rejection body that is not well-formed JSON is unusable evidence. Degrade to the canonical
            // malformed-evidence mapping so the caller still observes a deterministic gateway rejection
            // instead of a parse exception replacing it.
            return FolderResultCode.MalformedEvidence;
        }

        using (document)
        {
            return ParseRejectionCode(document.RootElement);
        }
    }

    private static FolderResultCode ParseRejectionCode(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return FolderResultCode.MalformedEvidence;
        }

        // Exactly one of the two accepted spellings must be present. Both absent is missing evidence;
        // both present is ambiguous evidence -- deliberately including the case where the two values
        // agree, because the boundary cannot tell an agreeing duplicate from a serializer defect.
        bool hasCamelCode = root.TryGetProperty("code", out JsonElement camelCode);
        bool hasPascalCode = root.TryGetProperty("Code", out JsonElement pascalCode);
        if (hasCamelCode == hasPascalCode)
        {
            return FolderResultCode.MalformedEvidence;
        }

        JsonElement codeElement = hasCamelCode ? camelCode : pascalCode;
        if (codeElement.ValueKind != JsonValueKind.String)
        {
            return FolderResultCode.MalformedEvidence;
        }

        // Exact ordinal name match. With ignoreCase: false, Enum.TryParse already rejects wrong casing,
        // but it still accepts numeric strings, surrounding whitespace, and comma-separated member lists.
        // Enum.IsDefined rejects undefined ordinals such as "999"; the ordinal ToString() comparison
        // rejects the remainder, including an alias name that is not the canonical name for its value.
        string? code = codeElement.GetString();
        if (code is null
            || !Enum.TryParse(code, ignoreCase: false, out FolderResultCode resultCode)
            || !Enum.IsDefined(resultCode)
            || !string.Equals(code, resultCode.ToString(), StringComparison.Ordinal))
        {
            return FolderResultCode.MalformedEvidence;
        }

        // A success code is not rejection evidence. Ask the canonical mapper which codes are successes
        // rather than restating its membership here, so a newly added success code cannot drift between
        // this boundary and the production mapper.
        return string.Equals(FolderCanonicalErrorMapper.CategoryFor(resultCode), "success", StringComparison.Ordinal)
            ? FolderResultCode.MalformedEvidence
            : resultCode;
    }
}
