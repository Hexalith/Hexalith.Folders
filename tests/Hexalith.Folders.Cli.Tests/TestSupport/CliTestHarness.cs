using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Client.Generated;

using Xunit;

using GeneratedFoldersClient = Hexalith.Folders.Client.Generated.Client;

namespace Hexalith.Folders.Cli.Tests.TestSupport;

/// <summary>
/// Hermetic driver for the CLI. It builds <see cref="CliDependencies"/> over a capturing console, an
/// injected credential resolver (temp path + an in-memory environment, so <c>~/.hexalith</c> and process
/// env are never read), and a caller-supplied <see cref="IClient"/> factory. No socket is opened unless the
/// caller wires the real client over a <see cref="CapturingHttpHandler"/>.
/// </summary>
internal sealed class CliTestHarness
{
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    /// <summary>Gets the captured console.</summary>
    public TestCliConsole Console { get; } = new();

    /// <summary>Gets or sets the client returned by the factory (a fake or a real client over a handler).</summary>
    public IClient? Client { get; set; }

    /// <summary>Gets a value indicating whether the client factory was invoked (i.e. an HTTP-capable client was built).</summary>
    public bool ClientFactoryInvoked { get; private set; }

    /// <summary>Gets the base address captured by the factory.</summary>
    public Uri? CapturedBaseAddress { get; private set; }

    /// <summary>Gets the token captured by the factory.</summary>
    public string? CapturedToken { get; private set; }

    /// <summary>Gets or sets the credentials-file path (defaults to a guaranteed-nonexistent temp file).</summary>
    public string CredentialsFilePath { get; set; } = Path.Combine(Path.GetTempPath(), $"hexalith-creds-{Guid.NewGuid():N}.json");

    /// <summary>Gets or sets the deterministic auto-key generator used for <c>--allow-auto-key</c>.</summary>
    public Func<string> IdempotencyKeyGenerator { get; set; } = () => "01TESTAUTOKEY00000000000000";

    /// <summary>Sets an in-memory environment variable for the credential resolver.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    public void SetEnvironment(string name, string value) => _environment[name] = value;

    /// <summary>Configures the harness to use the real generated client over a capturing handler.</summary>
    /// <param name="statusCode">The canned HTTP status.</param>
    /// <param name="responseJson">The canned response body.</param>
    /// <returns>The handler so the test can inspect the captured request.</returns>
    public CapturingHttpHandler UseRealClient(HttpStatusCode statusCode, string responseJson)
    {
        CapturingHttpHandler handler = new(statusCode, responseJson);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://folders.test/") };
        Client = new GeneratedFoldersClient(httpClient);
        return handler;
    }

    /// <summary>Runs the CLI with the supplied arguments and returns the exit code.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> RunAsync(params string[] args)
    {
        CredentialResolver credentials = new(
            environment: name => _environment.TryGetValue(name, out string? value) ? value : null,
            credentialsFilePath: CredentialsFilePath);

        CliDependencies dependencies = new()
        {
            Console = Console,
            Credentials = credentials,
            IdempotencyKeyGenerator = IdempotencyKeyGenerator,
            ClientFactory = (baseAddress, token) =>
            {
                ClientFactoryInvoked = true;
                CapturedBaseAddress = baseAddress;
                CapturedToken = token;
                return Client ?? throw new InvalidOperationException("No client configured for this test.");
            },
        };

        CliApplication application = new(dependencies);
        return await application.RunAsync(args, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a credentials file at <see cref="CredentialsFilePath"/> with a token under a tenant section.</summary>
    /// <param name="tenantSection">The tenant section name.</param>
    /// <param name="token">The token to store.</param>
    public void WriteCredentialsFile(string tenantSection, string token)
    {
        string json = "{\"tenants\":{\"" + tenantSection + "\":{\"token\":\"" + token + "\"}}}";
        System.IO.File.WriteAllText(CredentialsFilePath, json);
    }
}
