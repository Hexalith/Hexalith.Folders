using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Owns a supported disposable Forgejo remote, private TLS trust and a private secret-store lease.</summary>
internal sealed class OperationalProviderFixture(string directory, string runId) : IAsyncDisposable
{
    private string? _container;
    private string? _administrativeCredential;
    private string? _observerCredential;
    internal string BaseUrl { get; private set; } = string.Empty;
    internal string TrustFile => Path.Combine(directory, "provider-ca-bundle.pem");
    internal string SecretFile => Path.Combine(directory, "provider-credentials.json");
    internal const string CredentialReference = "operational-provider-lease";
    internal const string Owner = "pipeline";
    private const string Observer = "pipeline-observer";
    internal const string Repository = "operational-workspace";
    internal const string Branch = "private-operational-ref-sentinel";

    internal async Task StartAsync()
    {
        string certificates = Path.Combine(directory, "provider-tls");
        Directory.CreateDirectory(certificates);
        string certificate = Path.Combine(certificates, "certificate.pem");
        string key = Path.Combine(certificates, "key.pem");
        _ = await FolderDurabilityProcess.RunAsync("openssl", "req", "-x509", "-newkey", "rsa:2048",
            "-sha256", "-nodes", "-days", "2", "-subj", "/CN=localhost",
            "-addext", "subjectAltName=IP:127.0.0.1,DNS:localhost", "-keyout", key, "-out", certificate).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(key, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        string systemTrust = "/etc/ssl/certs/ca-certificates.crt";
        await File.WriteAllTextAsync(TrustFile, (File.Exists(systemTrust) ? await File.ReadAllTextAsync(systemTrust).ConfigureAwait(false) : string.Empty)
            + await File.ReadAllTextAsync(certificate).ConfigureAwait(false)).ConfigureAwait(false);
        _container = "folders-provider-" + runId;
        _ = await FolderDurabilityProcess.RunAsync("docker", "run", "--detach", "--rm", "--name", _container,
            "--publish", "127.0.0.1::3000", "--mount", $"type=bind,source={certificates},target=/certs,readonly",
            "--env", "FORGEJO__security__INSTALL_LOCK=true", "--env", "FORGEJO__database__DB_TYPE=sqlite3",
            "--env", "FORGEJO__server__PROTOCOL=https", "--env", "FORGEJO__server__HTTP_PORT=3000",
            "--env", "FORGEJO__server__CERT_FILE=/certs/certificate.pem", "--env", "FORGEJO__server__KEY_FILE=/certs/key.pem",
            "--env", "FORGEJO__server__DISABLE_SSH=true", "--env", "FORGEJO__service__DISABLE_REGISTRATION=true",
            "codeberg.org/forgejo/forgejo:16.0.3").ConfigureAwait(false);
        string address = await FolderDurabilityProcess.RunAsync("docker", "port", _container, "3000/tcp").ConfigureAwait(false);
        BaseUrl = "https://" + address + "/";
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(certificate);
        using HttpClientHandler handler = new();
        handler.ServerCertificateCustomValidationCallback = (_, server, _, errors) =>
        {
            if (server is null || (errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(server);
        };
        using HttpClient client = new(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(90));
        while (true)
        {
            try
            {
                using HttpResponseMessage health = await client.GetAsync("api/v1/version", deadline.Token).ConfigureAwait(false);
                if (health.IsSuccessStatusCode) break;
            }
            catch (HttpRequestException) { }
            await Task.Delay(500, deadline.Token).ConfigureAwait(false);
        }
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _ = await FolderDurabilityProcess.RunAsync("docker", "exec", "--user", "git", _container,
            "forgejo", "--config", "/data/gitea/conf/app.ini", "admin", "user", "create", "--username", Owner,
            "--email", "pipeline@example.invalid", "--password", password, "--admin", "--must-change-password=false").ConfigureAwait(false);
        string credential = await FolderDurabilityProcess.RunAsync("docker", "exec", "--user", "git", _container,
            "forgejo", "--config", "/data/gitea/conf/app.ini", "admin", "user", "generate-access-token",
            "--username", Owner, "--token-name", "operational-acceptance", "--scopes", "all", "--raw").ConfigureAwait(false);
        _administrativeCredential = credential;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using HttpResponseMessage created = await client.PostAsJsonAsync("api/v1/user/repos",
            new { name = Repository, @private = true, auto_init = true, default_branch = Branch }, deadline.Token).ConfigureAwait(false);
        created.EnsureSuccessStatusCode();
        _ = await FolderDurabilityProcess.RunAsync("docker", "exec", "--user", "git", _container,
            "forgejo", "--config", "/data/gitea/conf/app.ini", "admin", "user", "create", "--username", Observer,
            "--email", "observer@example.invalid", "--password", password, "--must-change-password=false").ConfigureAwait(false);
        credential = await FolderDurabilityProcess.RunAsync("docker", "exec", "--user", "git", _container,
            "forgejo", "--config", "/data/gitea/conf/app.ini", "admin", "user", "generate-access-token",
            "--username", Observer, "--token-name", "operational-observation", "--scopes", "all", "--raw").ConfigureAwait(false);
        _observerCredential = credential;
        using HttpResponseMessage granted = await client.PutAsJsonAsync($"api/v1/repos/{Owner}/{Repository}/collaborators/{Observer}",
            new { permission = "read" }, deadline.Token).ConfigureAwait(false);
        granted.EnsureSuccessStatusCode();
        await File.WriteAllTextAsync(SecretFile, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [CredentialReference] = new Dictionary<string, string> { ["access_token"] = credential },
        })).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(SecretFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            _ = await FolderDurabilityProcess.RunAsync("docker", "rm", "--force", "--volumes", _container).ConfigureAwait(false);
            _container = null;
        }
        _administrativeCredential = null;
        _observerCredential = null;
    }

    /// <summary>Observes the exact bound target using the same credential leased to Folders.</summary>
    internal async Task<(int Status, bool CanRead)> ObserveRepositoryAccessAsync(CancellationToken cancellationToken)
    {
        string credential = _observerCredential ?? throw new InvalidOperationException("The disposable provider is unavailable.");
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(directory, "provider-tls", "certificate.pem"));
        using HttpClientHandler handler = new();
        handler.ServerCertificateCustomValidationCallback = (_, server, _, errors) =>
        {
            if (server is null || (errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(server);
        };
        using HttpClient client = new(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using HttpResponseMessage response = await client.GetAsync($"api/v1/repos/{Owner}/{Repository}", cancellationToken).ConfigureAwait(false);
        bool canRead = false;
        if (response.IsSuccessStatusCode)
        {
            JsonElement repository = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            canRead = repository.TryGetProperty("permissions", out JsonElement permissions)
                && permissions.TryGetProperty("pull", out JsonElement pull) && pull.ValueKind == JsonValueKind.True;
        }
        return ((int)response.StatusCode, canRead);
    }

    /// <summary>Changes only the observer's access to the exact fixture-owned repository.</summary>
    internal async Task SetRepositoryAccessAsync(bool available, CancellationToken cancellationToken)
    {
        string credential = _administrativeCredential ?? throw new InvalidOperationException("The disposable provider is unavailable.");
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(directory, "provider-tls", "certificate.pem"));
        using HttpClientHandler handler = new();
        handler.ServerCertificateCustomValidationCallback = (_, server, _, errors) =>
        {
            if (server is null || (errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(server);
        };
        using HttpClient client = new(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        string endpoint = $"api/v1/repos/{Owner}/{Repository}/collaborators/{Observer}";
        using HttpResponseMessage response = available
            ? await client.PutAsJsonAsync(endpoint, new { permission = "read" }, cancellationToken).ConfigureAwait(false)
            : await client.DeleteAsync(endpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
