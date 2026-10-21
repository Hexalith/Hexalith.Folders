using System.Net;
using System.Globalization;
using System.Text.Json;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Retries fixture traffic without regenerating command identity or changing production policy.</summary>
internal static class FolderDurabilityHttp
{
    internal static async Task WaitForCurrentFolderAsync(HttpClient client, HttpRequestMessage request,
        string folderId, string phase,
        Func<CancellationToken, Task<(long SourceHead, long OperationalHead)>> readHeads,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(client, request, true, cancellationToken,
            async (reply, token) =>
            {
                using JsonDocument json = JsonDocument.Parse(await reply.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
                JsonElement value = json.RootElement;
                if (value.GetProperty("folderId").GetString() != folderId)
                    throw new InvalidOperationException("Readiness returned another folder scope.");
                JsonElement freshness = value.GetProperty("freshness");
                bool stale = freshness.GetProperty("stale").GetBoolean();
                if (!long.TryParse(freshness.GetProperty("projectionWatermark").GetString(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out long aclHead) || aclHead < 0)
                    throw new InvalidOperationException("Readiness returned an invalid ACL watermark.");
                bool allowed = value.GetProperty("authorizationOutcome").GetString() == "allowed"
                    && value.GetProperty("permissions").EnumerateArray().Any(permission => permission.GetString() is "write" or "administer");
                if (!stale && !allowed)
                    throw new InvalidOperationException("Current folder evidence does not authorize the positive workflow.");
                var heads = await readHeads(token).ConfigureAwait(false);
                Console.WriteLine($"Readiness {phase}: source={heads.SourceHead}; acl={aclHead}; operational={heads.OperationalHead}; stale={stale}.");
                return stale || heads.SourceHead <= 0 || aclHead != heads.SourceHead || heads.OperationalHead != heads.SourceHead;
            }).ConfigureAwait(false);
        await FolderDurabilityAcceptanceFixture.RequireStatusAsync(response, HttpStatusCode.OK,
            "Current folder evidence before " + phase).ConfigureAwait(false);
    }

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage original,
        bool retryUnavailableRead, CancellationToken cancellationToken,
        Func<HttpResponseMessage, CancellationToken, Task<bool>>? retryOutcomeAsync = null, TimeSpan? retryBudget = null)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(retryBudget ?? TimeSpan.FromSeconds(75));
        byte[]? body = original.Content is null ? null : await original.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
        int retries = 0;
        while (true)
        {
            using HttpRequestMessage request = new(original.Method, original.RequestUri)
            { Version = original.Version, VersionPolicy = original.VersionPolicy };
            foreach (var header in original.Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (body is not null)
            {
                request.Content = new ByteArrayContent(body);
                foreach (var header in original.Content!.Headers)
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HttpResponseMessage response = await client.SendAsync(request, deadline.Token).ConfigureAwait(false);
            bool retry;
            try
            {
                retry = await RetryAsync(response, retryUnavailableRead, deadline.Token).ConfigureAwait(false);
                if (!retry && response.StatusCode == HttpStatusCode.OK && retryOutcomeAsync is not null)
                    retry = await retryOutcomeAsync(response, deadline.Token).ConfigureAwait(false);
            }
            catch { response.Dispose(); throw; }
            if (!retry) return response;
            TimeSpan pause = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } until ? until - DateTimeOffset.UtcNow : (TimeSpan?)null)
                ?? TimeSpan.FromSeconds(Math.Min(5, ++retries));
            response.Dispose();
            await Task.Delay(pause > TimeSpan.Zero ? pause : TimeSpan.FromMilliseconds(100), deadline.Token).ConfigureAwait(false);
        }
    }

    private static async Task<bool> RetryAsync(HttpResponseMessage response, bool retryUnavailableRead, CancellationToken token)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests) return true;
        if (response.StatusCode != HttpStatusCode.ServiceUnavailable) return false;
        if (retryUnavailableRead) return true;
        try
        {
            using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
            JsonElement value = problem.RootElement;
            if (!value.TryGetProperty("retryable", out JsonElement retryable) || retryable.ValueKind != JsonValueKind.True)
                return false;
            string? code = value.TryGetProperty("code", out JsonElement codeValue) && codeValue.ValueKind == JsonValueKind.String ? codeValue.GetString() : null;
            string? category = value.TryGetProperty("category", out JsonElement categoryValue) && categoryValue.ValueKind == JsonValueKind.String ? categoryValue.GetString() : null;
            return code == "idempotency_admission_unavailable"
                || category is "read_model_unavailable" or "policy_evidence_unavailable" or "projection_stale";
        }
        catch (JsonException) { return false; }
    }
}
