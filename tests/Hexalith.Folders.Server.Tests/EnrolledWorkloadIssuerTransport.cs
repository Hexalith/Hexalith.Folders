using System.Net.Http.Json;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Uses the disposable component CA and real Keycloak exchanges; optionally corrupts the returned assertion.</summary>
internal sealed class EnrolledWorkloadIssuerTransport : DelegatingHandler
{
    private readonly string _fault;
    private readonly string? _expiredToken;

    /// <summary>Creates a real HTTPS transport scoped only to this component's temporary CA.</summary>
    internal EnrolledWorkloadIssuerTransport(string certificatePath, string fault, string expiredTokenFile)
    {
        _fault = fault;
        _expiredToken = fault == "expired" ? File.ReadAllText(expiredTokenFile) : null;
        InnerHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
                using var root = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(root);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(certificate);
            },
        };
    }

    /// <summary>Corrupts only tokens actually returned by the real disposable issuer.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_fault == "scope" && request.Method == HttpMethod.Post)
        {
            string encoded = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> form = encoded.Split('&').Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]), StringComparer.Ordinal);
            JsonArray details = JsonNode.Parse(form["authorization_details"])!.AsArray();
            details[0]!["bindings"]!["eventstore:bound-tenant"] = "tenant-b";
            form["authorization_details"] = details.ToJsonString();
            request.Content.Dispose();
            request.Content = new FormUrlEncodedContent(form);
        }
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || request.Method != HttpMethod.Post || _fault is not ("signature" or "expired" or "malformed")) return response;
        JsonObject body = (await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken).ConfigureAwait(false))!;
        string token = body["access_token"]!.GetValue<string>();
        string[] parts = token.Split('.');
        if (_fault == "malformed") token = "malformed-component-assertion";
        else if (_fault == "signature") token = parts[0] + "." + parts[1] + "." + (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        else token = _expiredToken!; // Originally signed by this real issuer for this same client; naturally expired.
        body["access_token"] = token;
        response.Content.Dispose();
        response.Content = JsonContent.Create(body);
        return response;
    }
}
