using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Owns a disposable authenticated Folders topology and an isolated retained Redis store. An opted-in
/// infrastructure or authorization failure fails acceptance; it never becomes an unavailable-fixture skip.
/// </summary>
public sealed class FolderDurabilityAcceptanceFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);
    private readonly string _runId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), "folders-durability-" + Path.GetRandomFileName());
    private IDistributedApplicationTestingBuilder? _builder;
    private DistributedApplication? _app;
    private DaprClient? _dapr;
    private string? _redisName;
    private string _redisAddress = string.Empty;
    private string _humanToken = string.Empty;
    private string _foreignToken = string.Empty;
    private string _adminToken = string.Empty;
    private string _workloadToken = string.Empty;

    /// <summary>Gets whether the explicit integration opt-in is present.</summary>
    public bool Enabled { get; } = Environment.GetEnvironmentVariable(AspireFoldersAppHostFixture.OptInEnvironmentVariable)
        is string value && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    /// <summary>Gets the fresh managed tenant created by this fixture's real Tenants commands.</summary>
    public string TenantId => "durability-" + _runId;

    /// <summary>Gets the subject authenticated by the local identity provider.</summary>
    public string PrincipalId { get; private set; } = string.Empty;

    /// <summary>Gets the isolated standalone Dapr namespace used for service and actor discovery.</summary>
    public string DaprNamespace => "folders-durability-" + _runId;

    private string AppId(string name) => name + "-" + _runId;

    /// <summary>Gets the SDK store used only to inspect persisted derived rows.</summary>
    public IReadModelStore Store { get; private set; } = null!;

    /// <summary>Gets the running task-owned topology.</summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("Acceptance topology is not running.");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        Directory.CreateDirectory(_temporaryDirectory);
        SetEnvironment("EnableKeycloak", "true");
        SetEnvironment("KeycloakPersistent", "false");
        SetEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
        SetEnvironment("DOTNET_ENVIRONMENT", "Development");
        SetEnvironment("NAMESPACE", DaprNamespace);
        try
        {
            _redisName = "folders-durability-" + _runId;
            _ = await FolderDurabilityProcess.RunAsync("docker", "run", "--detach", "--rm", "--name", _redisName,
                "--publish", "127.0.0.1::6379", "redis:7-alpine", "redis-server", "--appendonly", "yes").ConfigureAwait(false);
            _redisAddress = await FolderDurabilityProcess.RunAsync("docker", "port", _redisName, "6379/tcp").ConfigureAwait(false);
            await StartAsync().ConfigureAwait(false);
            await BootstrapAsync().ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Creates a discovered public Folders client with the real human bearer.</summary>
    public HttpClient CreateFoldersClient(string? token = null)
    {
        HttpClient client = App.CreateHttpClient("folders", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _humanToken);
        return client;
    }

    /// <summary>Sends a metadata-only public mutation with fresh request tracing.</summary>
    public async Task<HttpResponseMessage> MutateAsync(string path, object payload, string key, string? token = null)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken).ConfigureAwait(false);
        using HttpClient client = CreateFoldersClient(token);
        using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-Id", NewId());
        request.Headers.Add("X-Hexalith-Task-Id", NewId());
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Submits an organization command through the authenticated public EventStore gateway.</summary>
    public async Task<JsonElement> OrganizationCommandAsync(string command, string[] actions)
    {
        JsonElement result = default;
        string[][] batches = command == "InitializeOrganizationAclBaseline" ? [actions]
            : actions.Select(action => new[] { action }).ToArray();
        foreach (string[] batch in batches)
        {
            // The baseline accepts a set; each grant/revocation has one authoritative operation.
            result = await SubmitAsync(TenantId, "folders", TenantId, "Hexalith.Folders.Commands." + command,
                new { requestSchemaVersion = "v1", operations = batch.Select(action => new { principalKind = "User", principalId = PrincipalId, action }).ToArray() }, _humanToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Reads complete retained bytes through EventStore's authenticated stream seam.</summary>
    public async Task<StreamReadPage> ReadStreamAsync(string aggregateId)
    {
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/streams/read",
            new StreamReadRequest(TenantId, "folders", aggregateId, PageSize: 1000), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "Authenticated retained stream read").ConfigureAwait(false);
        StreamReadPage page = (await response.Content.ReadFromJsonAsync<StreamReadPage>(Json, TestContext.Current.CancellationToken).ConfigureAwait(false))!;
        if (page.Metadata.IsTruncated)
        {
            throw new InvalidOperationException("Acceptance requires a complete retained prefix.");
        }

        return page;
    }

    /// <summary>Proves a failed public create produced neither an accepted event stream nor a derived folder row.</summary>
    public async Task RequireCreateAbsentAsync(string key)
    {
        string folderId = CreatedFolderId(key);
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _humanToken);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/streams/read",
            new StreamReadRequest(TenantId, "folders", folderId), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.NotFound, "Denied create retained stream absence").ConfigureAwait(false);
        if ((await Store.GetAsync<FolderPermissionProjection>("statestore", "projection:folders:folder-permissions:" + TenantId + ":" + folderId,
            TestContext.Current.CancellationToken).ConfigureAwait(false)).Value is not null)
        {
            throw new InvalidOperationException("A failed create unexpectedly persisted a folder projection.");
        }
    }

    /// <summary>Projects a complete zero-checkpoint prefix under a real operation-bound workload token.</summary>
    public async Task<HttpResponseMessage> ProjectAsync(string aggregateId, ProjectionEventDto[] events)
    {
        using HttpClient client = App.CreateHttpClient("folders", "http");
        using HttpRequestMessage request = new(HttpMethod.Post, "/project")
        {
            Content = JsonContent.Create(new ProjectionRequest(TenantId, "folders", aggregateId, events)),
        };
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, _workloadToken);
        request.Headers.Add("dapr-api-token", await ParameterAsync("folders-app-api-token").ConfigureAwait(false));
        return await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restarts the domain hosts while retaining the same identity provider, broker and state store.</summary>
    public async Task<IReadOnlyDictionary<string, (int Before, int After)>> RestartHostsAsync()
    {
        Dictionary<string, (int Before, int After)> identities = [];
        foreach (string resource in new[] { "folders", "folders-workers", "eventstore" })
        {
            int before = ResourceProcessId(resource);
            await ResourceCommandAsync(resource, KnownResourceCommands.StopCommand).ConfigureAwait(false);
            await ResourceCommandAsync(resource, KnownResourceCommands.StartCommand).ConfigureAwait(false);
            int after = ResourceProcessId(resource);
            if (before == after)
            {
                throw new InvalidOperationException($"Task-owned resource {resource} did not change process identity.");
            }

            identities.Add(resource, (before, after));
        }

        return identities;
    }

    private int ResourceProcessId(string name)
    {
        if (!App.ResourceNotifications.TryGetCurrentState(name, out ResourceEvent? state))
        {
            throw new InvalidOperationException($"Task-owned resource {name} has no observed lifecycle state.");
        }

        object? value = state.Snapshot.Properties.Single(property => property.Name == "executable.pid").Value;
        int processId = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        return processId > 0 ? processId : throw new InvalidOperationException($"Task-owned resource {name} has no process identity.");
    }

    /// <summary>Stops or starts a task-owned resource, bounding the command and readiness wait.</summary>
    public async Task ResourceCommandAsync(string name, string command)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
        IResource resource = _builder!.Resources.Single(item => item.Name == name);
        ExecuteCommandResult result = await App.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(resource, command, timeout.Token).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Task-owned resource {name} command {command} failed.");
        }

        if (command == KnownResourceCommands.StartCommand || command == KnownResourceCommands.RestartCommand)
        {
            await App.ResourceNotifications.WaitForResourceHealthyAsync(name, timeout.Token).ConfigureAwait(false);
        }
        else
        {
            await App.ResourceNotifications.WaitForResourceAsync(name, KnownResourceStates.Finished, timeout.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Pauses or resumes only this fixture's isolated Redis dependency.</summary>
    public async Task SetStoragePausedAsync(bool paused)
        => _ = await FolderDurabilityProcess.RunAsync("docker", paused ? "pause" : "unpause", _redisName!).ConfigureAwait(false);

    /// <summary>Gets a real JWT whose tenant claim is foreign to the fixture's folder.</summary>
    public Task<string> ForeignTokenAsync() => Task.FromResult(_foreignToken);

    /// <summary>Creates a unique request identifier without relying on a caller-selected domain identity.</summary>
    public static string NewId() => Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();

    /// <summary>Returns the existing v1 server's deterministic actor identity for a create request.</summary>
    public string CreatedFolderId(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return "fld-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{TenantId.Length}:{TenantId}|{key.Length}:{key}")))[..40].ToLowerInvariant();
    }

    /// <summary>Compares an HTTP status without including credentials or protected response payloads.</summary>
    public static async Task RequireStatusAsync(HttpResponseMessage response, HttpStatusCode expected, string operation)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode != expected)
        {
            string code = "unavailable";
            try
            {
                JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
                if (body.TryGetProperty("code", out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    code = value.GetString()!;
                }
            }
            catch (JsonException) { }

            throw new InvalidOperationException($"{operation}: expected {(int)expected}, observed {(int)response.StatusCode}; code={code}.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        ExceptionDispatchInfo? firstFailure = null;
        void Record(Exception exception) => firstFailure ??= ExceptionDispatchInfo.Capture(exception);

        try
        {
            _dapr?.Dispose();
        }
        catch (Exception exception)
        {
            Record(exception);
        }
        finally
        {
            _dapr = null;
        }

        if (_app is not null)
        {
            try
            {
                // Flush terminal resource diagnostics, including failed callbacks after EventsStored.
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
                await _app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _app = null;
            }
        }

        if (_builder is not null)
        {
            try
            {
                await _builder.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _builder = null;
            }
        }

        if (_redisName is not null)
        {
            try
            {
                // rm --force also handles a dependency left paused by a failed assertion.
                _ = await FolderDurabilityProcess.RunAsync("docker", "rm", "--force", "--volumes", _redisName).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
            finally
            {
                _redisName = null;
            }
        }

        foreach ((string name, string? value) in _environment)
        {
            try
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            catch (Exception exception)
            {
                Record(exception);
            }
        }

        _environment.Clear();
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Record(exception);
        }

        firstFailure?.Throw();
    }

    private async Task StartAsync()
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        _builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_Folders_AppHost>(
            ["--EnableKeycloak=true", "--KeycloakPersistent=false"], timeout.Token).ConfigureAwait(false);
        // An unavailable optional telemetry receiver must not keep these disposable hosts alive
        // beyond DCP's process-stop deadline. Production exporter defaults stay unchanged.
        foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
        {
            IResourceBuilder<ProjectResource> host = _builder.CreateResourceBuilder(project);
            foreach (string signal in new[] { "", "TRACES_", "METRICS_", "LOGS_" })
            {
                host.WithEnvironment("OTEL_EXPORTER_OTLP_" + signal + "TIMEOUT", "1000");
            }
        }

        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore"))
            .WithEnvironment("Logging__LogLevel__Hexalith.EventStore.Server.Commands.CommandRouter", "Debug");
        Dictionary<IDaprComponentResource, DaprComponentResource> replacements = [];
        foreach (DaprComponentResource component in _builder.Resources.OfType<DaprComponentResource>().ToArray())
        {
            if (component.Options?.LocalPath is string source)
            {
                string path = Path.Combine(_temporaryDirectory, Path.GetFileName(source));
                string content = await File.ReadAllTextAsync(source, timeout.Token).ConfigureAwait(false);
                if (component.Name is "statestore" or "pubsub" && !content.Contains("localhost:6379", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Shared component shape changed; acceptance isolation must be revalidated.");
                }

                await File.WriteAllTextAsync(path, IsolateDaprYaml(content), timeout.Token).ConfigureAwait(false);
                _builder.Resources.Remove(component);
                DaprComponentResource isolated = (DaprComponentResource)_builder.AddDaprComponent(component.Name, component.Type,
                    new DaprComponentOptions { LocalPath = path }).Resource;
                foreach (IResourceAnnotation annotation in component.Annotations.Where(annotation => annotation is not
                    ResourceSnapshotAnnotation and not ManifestPublishingCallbackAnnotation))
                {
                    isolated.Annotations.Add(annotation);
                }

                replacements.Add(component, isolated);
            }
        }

        foreach (ProjectResource project in _builder.Resources.OfType<ProjectResource>())
        {
            foreach (DaprSidecarAnnotation annotation in project.Annotations.OfType<DaprSidecarAnnotation>())
            {
                DaprSidecarOptionsAnnotation optionsAnnotation = annotation.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                DaprSidecarOptions options = optionsAnnotation.Options;
                string? isolatedConfig = null;
                if (options.Config is string config)
                {
                    foreach (string source in Directory.EnumerateFiles(Path.GetDirectoryName(config)!))
                    {
                        string target = Path.Combine(_temporaryDirectory, Path.GetFileName(source));
                        if (!File.Exists(target))
                        {
                            await File.WriteAllTextAsync(target, IsolateDaprYaml(await File.ReadAllTextAsync(source, timeout.Token).ConfigureAwait(false)), timeout.Token).ConfigureAwait(false);
                        }
                    }

                    isolatedConfig = Path.Combine(_temporaryDirectory, Path.GetFileName(config));
                }

                annotation.Sidecar.Annotations.Remove(optionsAnnotation);
                annotation.Sidecar.Annotations.Add(new DaprSidecarOptionsAnnotation(options with
                {
                    AppId = AppId(project.Name),
                    Config = isolatedConfig,
                    ResourcesPaths = [_temporaryDirectory],
                }));
                annotation.Sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context => context.EnvironmentVariables["NAMESPACE"] = DaprNamespace));
            }
        }

        IResourceBuilder<ProjectResource> gateway = _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore"));
        foreach ((string registration, string target) in new[]
        {
            ("system|tenants|v1", "tenants"), ("system|global-administrators|v1", "tenants"),
            ("*|counter|v1", "unhosted-sample"), ("*|greeting|v1", "unhosted-sample"),
        })
        {
            gateway.WithEnvironment("EventStore__DomainServices__Registrations__" + registration + "__AppId", AppId(target));
        }

        const string folderRegistration = "EventStore__DomainServices__Registrations__*|folders|v1__";
        gateway.WithEnvironment(folderRegistration + "AppId", AppId("folders"))
            .WithEnvironment(folderRegistration + "MethodName", "process")
            .WithEnvironment(folderRegistration + "TenantId", "*")
            .WithEnvironment(folderRegistration + "Domain", "folders")
            .WithEnvironment(folderRegistration + "Version", "v1");
        gateway.WithEnvironment("Authentication__WorkloadIssuer__ClientId", AppId("eventstore"))
            .WithEnvironment("Authentication__WorkloadIssuer__Workload", AppId("eventstore"));
        foreach (string name in new[] { "folders", "tenants" })
        {
            _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == name))
                .WithEnvironment("EventStore__DomainService__AppId", AppId(name))
                .WithEnvironment("Authentication__Workload__Audience", AppId(name))
                .WithEnvironment("Authentication__Workload__AllowedCallers__0", AppId("eventstore"));
        }

        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "tenants"))
            .WithEnvironment("Tenants__BootstrapGlobalAdminEnabled", "false");
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories"))
            .WithEnvironment("EventStoreIntegration__CommandGateway__BaseAddress",
                _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "eventstore")).GetEndpoint("http"));
        _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "folders"))
            .WithEnvironment("Authentication__HumanAuthorityReceipt__ExpectedWorkload", AppId("eventstore"))
            .WithEnvironment("Memories__BaseAddress",
                _builder.CreateResourceBuilder(_builder.Resources.OfType<ProjectResource>().Single(project => project.Name == "memories")).GetEndpoint("http"));

        IResource[] composedResources = [.. _builder.Resources,
            .. _builder.Resources.OfType<ProjectResource>().SelectMany(project => project.Annotations.OfType<DaprSidecarAnnotation>().Select(annotation => annotation.Sidecar))];
        foreach (IResource resource in composedResources)
        {
            foreach (DaprComponentReferenceAnnotation reference in resource.Annotations.OfType<DaprComponentReferenceAnnotation>().ToArray())
            {
                if (replacements.TryGetValue(reference.Component, out DaprComponentResource? isolated))
                {
                    resource.Annotations.Remove(reference);
                    resource.Annotations.Add(new DaprComponentReferenceAnnotation(isolated));
                }
            }

            foreach (WaitAnnotation wait in resource.Annotations.OfType<WaitAnnotation>().ToArray())
            {
                if (wait.Resource is IDaprComponentResource component && replacements.TryGetValue(component, out DaprComponentResource? isolated))
                {
                    resource.Annotations.Remove(wait);
                    resource.Annotations.Add(new WaitAnnotation(isolated, wait.WaitType, wait.ExitCode) { WaitBehavior = wait.WaitBehavior });
                }
            }
        }

        _app = await _builder.BuildAsync(timeout.Token).ConfigureAwait(false);
        await _app.StartAsync(timeout.Token).ConfigureAwait(false);
        foreach (string name in new[] { "security", "eventstore", "tenants", "folders", "folders-workers" })
        {
            await _app.ResourceNotifications.WaitForResourceHealthyAsync(name, timeout.Token).ConfigureAwait(false);
        }

        foreach (string name in new[] { "eventstore", "tenants", "folders", "folders-workers" })
        {
            string sidecar = _builder.Resources.OfType<ProjectResource>().Single(project => project.Name == name)
                .Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
            await App.ResourceNotifications.WaitForResourceAsync(sidecar, KnownResourceStates.Running, timeout.Token).ConfigureAwait(false);
            DaprClient client = new DaprClientBuilder()
                .UseHttpEndpoint(App.GetEndpoint(sidecar, "http").ToString())
                .UseGrpcEndpoint(App.GetEndpoint(sidecar, "grpc").ToString())
                .Build();
            try
            {
                await client.WaitForSidecarAsync(timeout.Token).ConfigureAwait(false);
                // Outbound readiness precedes application/actor initialization. Full health is required
                // before the first authenticated command can depend on an actor or callback.
                while (!await client.CheckHealthAsync(timeout.Token).ConfigureAwait(false))
                {
                    await Task.Delay(200, timeout.Token).ConfigureAwait(false);
                }

                if (name == "folders-workers")
                {
                    _dapr = client;
                    client = null!;
                }
            }
            finally
            {
                client?.Dispose();
            }
        }

        Store = new DaprReadModelStore(_dapr ?? throw new InvalidOperationException("Worker sidecar discovery did not complete."));
    }

    private async Task BootstrapAsync()
    {
        _adminToken = await TokenAsync("hexalith-eventstore", "admin-user", "admin-pass").ConfigureAwait(false);
        _foreignToken = await TokenAsync("hexalith-eventstore", "tenant-a-user", "tenant-a-pass").ConfigureAwait(false);
        await ConfigureIdentityAsync().ConfigureAwait(false);
        await SubmitAsync("system", "global-administrators", "global-administrators", "BootstrapGlobalAdmin", new { userId = "admin-user" }, _adminToken).ConfigureAwait(false);
        await SubmitAsync("system", "tenants", TenantId, "CreateTenant", new { tenantId = TenantId, name = "Durability acceptance", description = "Disposable local acceptance tenant" }, _adminToken).ConfigureAwait(false);
        _humanToken = await TokenAsync("hexalith-eventstore", "tenant-a-user", "tenant-a-pass").ConfigureAwait(false);
        JsonWebToken human = new(_humanToken);
        if (!human.Claims.Any(claim => claim.Type == "eventstore:tenant" && claim.Value == TenantId)
            || !human.Claims.Any(claim => claim.Type == "eventstore:permission" && claim.Value == "manage_organization_acl"))
        {
            throw new InvalidOperationException("The real identity provider did not issue the required fresh-tenant/action claims.");
        }

        await SubmitAsync("system", "tenants", TenantId, "AddUserToTenant", new { tenantId = TenantId, userId = PrincipalId, role = "TenantOwner" }, _adminToken).ConfigureAwait(false);
        using HttpClient client = App.CreateHttpClient("security", "http");
        using HttpResponseMessage workload = await client.PostAsync("/realms/hexalith/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = AppId("eventstore"),
                ["client_secret"] = await ParameterAsync("eventstore-workload-client-secret").ConfigureAwait(false),
                ["scope"] = "eventstore-audience." + AppId("folders") + " eventstore-operation.domain-service.project",
            }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(workload, HttpStatusCode.OK, "Real project workload token").ConfigureAwait(false);
        _workloadToken = (await workload.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString()!;
    }

    private async Task ConfigureIdentityAsync()
    {
        using HttpClient client = App.CreateHttpClient("security", "http");
        KeycloakResource security = _builder!.Resources.OfType<KeycloakResource>().Single(resource => resource.Name == "security");
        using HttpResponseMessage login = await client.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = await security.AdminPasswordParameter.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Local IdP administrative credential is unavailable."),
        }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(login, HttpStatusCode.OK, "Local IdP administration login").ConfigureAwait(false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString());
        JsonObject realm = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_builder!.AppHostDirectory,
            "KeycloakRealms", "hexalith-realm.json"), TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsObject();
        JsonArray clients = JsonNode.Parse(await client.GetStringAsync("/admin/realms/hexalith/clients?clientId=eventstore",
            TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsArray();
        string workloadClientId = clients.Single()!["id"]!.GetValue<string>();
        JsonObject workloadClient = clients.Single()!.AsObject();
        workloadClient["clientId"] = AppId("eventstore");
        using HttpResponseMessage renameWorkload = await client.PutAsJsonAsync("/admin/realms/hexalith/clients/" + workloadClientId,
            workloadClient, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(renameWorkload, HttpStatusCode.NoContent, "Isolated real workload identity").ConfigureAwait(false);
        foreach (string name in new[] { "folders", "tenants" })
        {
            JsonObject scope = realm["clientScopes"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "eventstore-audience." + name)!.DeepClone().AsObject();
            scope["name"] = "eventstore-audience." + AppId(name);
            scope["protocolMappers"]!.AsArray().Single()!["config"]!["included.custom.audience"] = AppId(name);
            using HttpResponseMessage added = await client.PostAsJsonAsync("/admin/realms/hexalith/client-scopes", scope,
                TestContext.Current.CancellationToken).ConfigureAwait(false);
            await RequireStatusAsync(added, HttpStatusCode.Created, "Isolated workload audience scope").ConfigureAwait(false);
            string scopeId = added.Headers.Location!.Segments.Last();
            using HttpResponseMessage assigned = await client.PutAsync($"/admin/realms/hexalith/clients/{workloadClientId}/optional-client-scopes/{scopeId}", null,
                TestContext.Current.CancellationToken).ConfigureAwait(false);
            await RequireStatusAsync(assigned, HttpStatusCode.NoContent, "Isolated workload audience assignment").ConfigureAwait(false);
        }
        // Keycloak's managed user profile otherwise discards unconfigured custom attributes in Admin REST
        // updates. Only administrators of this disposable realm may edit these existing mapper inputs.
        JsonObject profile = JsonNode.Parse(await client.GetStringAsync("/admin/realms/hexalith/users/profile", TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsObject();
        JsonArray attributes = profile["attributes"]!.AsArray();
        foreach (string name in new[] { "tenants", "permissions" })
        {
            if (!attributes.Any(attribute => attribute?["name"]?.GetValue<string>() == name))
            {
                attributes.Add(new JsonObject
                {
                    ["name"] = name,
                    ["multivalued"] = true,
                    ["permissions"] = new JsonObject { ["view"] = new JsonArray("admin"), ["edit"] = new JsonArray("admin") },
                });
            }
        }

        using HttpResponseMessage configureProfile = await client.PutAsJsonAsync("/admin/realms/hexalith/users/profile", profile,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(configureProfile, HttpStatusCode.OK, "Disposable identity claim profile").ConfigureAwait(false);
        JsonArray users = (JsonNode.Parse(await client.GetStringAsync("/admin/realms/hexalith/users?username=tenant-a-user&exact=true", TestContext.Current.CancellationToken).ConfigureAwait(false))!.AsArray());
        JsonObject user = users.Single()!.AsObject();
        PrincipalId = user["id"]!.GetValue<string>();
        user["attributes"] = new JsonObject
        {
            ["tenants"] = new JsonArray(TenantId),
            ["permissions"] = new JsonArray("command:submit", "command:query", "command:replay", "create_folder", "manage_organization_acl", "read_metadata", "manage_folder_access"),
        };
        using HttpResponseMessage update = await client.PutAsJsonAsync("/admin/realms/hexalith/users/" + PrincipalId, user, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(update, HttpStatusCode.NoContent, "Fresh-tenant identity claims").ConfigureAwait(false);
    }

    private async Task<string> TokenAsync(string clientId, string username, string password)
    {
        using HttpClient client = App.CreateHttpClient("security", "http");
        using HttpResponseMessage response = await client.PostAsync("/realms/hexalith/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
        }), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.OK, "Real human JWT acquisition").ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false)).GetProperty("access_token").GetString()!;
    }

    private async Task<JsonElement> SubmitAsync(string tenant, string domain, string aggregate, string command, object payload, string token)
    {
        // Each command fans out into internal actor/projection requests. Keep the acceptance driver
        // below the existing consumer limiter; rate-limit/load qualification is a separate concern.
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken).ConfigureAwait(false);
        using HttpClient client = App.CreateHttpClient("eventstore", "http");
        client.Timeout = TimeSpan.FromSeconds(40);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(new SubmitCommandRequest(NewId(), tenant, domain, aggregate, command, JsonSerializer.SerializeToElement(payload),
                Extensions: new Dictionary<string, string> { ["taskId"] = NewId() },
                IdempotencyKey: domain == "folders" ? NewId() : null)),
        };
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await RequireStatusAsync(response, HttpStatusCode.Accepted, "Authenticated " + command).ConfigureAwait(false);
        JsonElement accepted = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        string statusKey = accepted.GetProperty("messageId").GetString()!;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(45));
        while (true)
        {
            using HttpResponseMessage status = await client.GetAsync("/api/v1/commands/status/" + statusKey, deadline.Token).ConfigureAwait(false);
            if (status.IsSuccessStatusCode)
            {
                JsonElement current = await status.Content.ReadFromJsonAsync<JsonElement>(deadline.Token).ConfigureAwait(false);
                string? state = current.GetProperty("status").GetString();
                if (state == "Completed")
                {
                    return current;
                }

                if (state is "Rejected" or "Failed" or "PublishFailed" or "TimedOut")
                {
                    throw new InvalidOperationException($"Authenticated {command} ended in {state}.");
                }
            }

            await Task.Delay(500, deadline.Token).ConfigureAwait(false);
        }
    }

    private async Task<string> ParameterAsync(string name)
        => await _builder!.Resources.OfType<ParameterResource>().Single(item => item.Name == name)
            .GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Required local security parameter is unavailable.");

    private string IsolateDaprYaml(string content)
    {
        string isolated = content.Replace("localhost:6379", _redisAddress, StringComparison.Ordinal);
        isolated = Regex.Replace(isolated,
            @"(?m)(?<prefix>^\s*-\s+(?:appId:\s*)?|^\s*appId:\s*)(?<id>eventstore|tenants|folders-workers|folders-ui|folders|memories)(?<suffix>\s*$)",
            match => match.Groups["prefix"].Value + AppId(match.Groups["id"].Value) + match.Groups["suffix"].Value);
        isolated = Regex.Replace(isolated, @"(?m)^(\s*)eventstore:(\s*)$",
            match => match.Groups[1].Value + AppId("eventstore") + ":" + match.Groups[2].Value);
        return isolated.Replace("namespace: \"default\"", "namespace: \"" + DaprNamespace + "\"", StringComparison.Ordinal);
    }

    private void SetEnvironment(string name, string value)
    {
        _environment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }
}
