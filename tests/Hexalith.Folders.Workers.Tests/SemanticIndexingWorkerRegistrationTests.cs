using System.Text.RegularExpressions;
using System.Net;

using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Workers;
using Hexalith.Folders.Workers.SemanticIndexing;
using Hexalith.EventStore.Client.Projections;
using Hexalith.Memories.Client.Rest;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class SemanticIndexingWorkerRegistrationTests
{
    [Fact]
    public void AddFoldersSemanticIndexingWorkersShouldRegisterPortAndMemoriesTypedClient()
    {
        ServiceCollection services = CreateServiceCollection();

        services.AddFoldersSemanticIndexingWorkers();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        provider.GetRequiredService<ISemanticIndexingPort>().ShouldNotBeNull();
        provider.GetRequiredService<ISemanticIndexingPolicyEvaluator>().ShouldNotBeNull();
        provider.GetRequiredService<ISemanticIndexingContentMaterializer>().ShouldNotBeNull();
        provider.GetRequiredService<SemanticIndexingProcessManager>().ShouldNotBeNull();
        provider.GetRequiredService<FoldersSemanticIndexingEventProcessor>().ShouldNotBeNull();
        provider.GetRequiredService<MemoriesClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddFoldersSemanticIndexingWorkersShouldRegisterEventStoreBackedBridge()
    {
        ServiceCollection services = CreateServiceCollection();

        services.AddFoldersSemanticIndexingWorkers();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        provider.GetRequiredService<IReadModelStore>().ShouldNotBeNull();
        provider.GetRequiredService<ISemanticIndexingBridgeReadModel>().ShouldBeOfType<EventStoreSemanticIndexingBridgeStore>();
        provider.GetRequiredService<ISemanticIndexingBridgeWriter>().ShouldBeOfType<EventStoreSemanticIndexingBridgeStore>();
    }

    [Fact]
    public void AddFoldersTenantEventWorkersShouldIncludeSemanticIndexingRegistration()
    {
        ServiceCollection services = CreateServiceCollection();

        services.AddFoldersTenantEventWorkers();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        provider.GetRequiredService<ISemanticIndexingPort>().ShouldNotBeNull();
        provider.GetRequiredService<MemoriesClient>().ShouldNotBeNull();
    }

    [Fact]
    public async Task SemanticIndexingPortShouldCallMemoriesTypedClientIngestion()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, """{"instanceId":"workflow-a"}""");
        MemoriesClient client = new(
            new HttpClient(handler) { BaseAddress = new Uri("http://memories.local/") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance);
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(client);

        SemanticIndexingResult result = await port.IndexFileVersionAsync(
            CreateRequest(contentBytes: [1, 2, 3]),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Accepted);
        result.Retryable.ShouldBeFalse();
        result.ReasonCode.ShouldBe("memories_accepted");
        result.WorkflowInstanceId.ShouldBe("workflow-a");
        handler.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe("/api/ingest");
        handler.Payloads.ShouldHaveSingleItem().ShouldContain("folders-index", Case.Sensitive);
        handler.Payloads[0].ShouldContain("folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a", Case.Sensitive);
        handler.Payloads[0].ShouldNotContain("C:/", Case.Sensitive);
    }

    [Fact]
    public async Task SemanticIndexingPortShouldMapRemoteErrorsToRetryableFailure()
    {
        CapturingHandler handler = new(
            HttpStatusCode.ServiceUnavailable,
            """{"code":"UNAVAILABLE","message":"service unavailable","suggestion":"retry"}""");
        MemoriesClient client = new(
            new HttpClient(handler) { BaseAddress = new Uri("http://memories.local/") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance);
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(client);

        SemanticIndexingResult result = await port.IndexFileVersionAsync(
            CreateRequest(contentBytes: [1, 2, 3]),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Failed);
        result.Retryable.ShouldBeTrue();
        result.ReasonCode.ShouldBe("memories_remote_error");
    }

    [Fact]
    public async Task SemanticIndexingPortShouldMapInvalidAcceptedResponseToRetryableFailure()
    {
        CapturingHandler handler = new(HttpStatusCode.Accepted, "{}");
        MemoriesClient client = new(
            new HttpClient(handler) { BaseAddress = new Uri("http://memories.local/") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance);
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(client);

        SemanticIndexingResult result = await port.IndexFileVersionAsync(
            CreateRequest(contentBytes: [1, 2, 3]),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Failed);
        result.Retryable.ShouldBeTrue();
        result.ReasonCode.ShouldBe("memories_invalid_response");
    }

    [Fact]
    public async Task SemanticIndexingPortShouldHonorCancelledTokenBeforeCallingMemories()
    {
        ServiceCollection services = CreateServiceCollection();
        services.AddFoldersSemanticIndexingWorkers();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        ISemanticIndexingPort port = provider.GetRequiredService<ISemanticIndexingPort>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await port.IndexFileVersionAsync(CreateRequest(), cancellation.Token).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public void SemanticIndexingRequestShouldRejectBlankPublicBoundaryIdentifiers()
    {
        Should.Throw<ArgumentException>(() => CreateRequest(managedTenantId: " "))
            .ParamName.ShouldBe("managedTenantId");
        Should.Throw<ArgumentException>(() => CreateRequest(correlationId: " "))
            .ParamName.ShouldBe("correlationId");
        Should.Throw<ArgumentException>(() => CreateRequest(idempotencyKey: " "))
            .ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void SemanticIndexingRequestShouldRejectMissingNestedBoundaryRecords()
    {
        Should.Throw<ArgumentNullException>(() => new SemanticIndexingRequest(
                "tenant-a",
                "organization-a",
                "folder-a",
                "version-a",
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                null!,
                new SemanticIndexingContentDescriptor("authorized-file-version", 1024, "text/markdown", "small", "text"),
                new SemanticIndexingPolicyOutcome(true, "tenant-sensitive", "allowed"),
                "correlation-a",
                "task-a",
                "idempotency-a"))
            .ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => new SemanticIndexingRequest(
                "tenant-a",
                "organization-a",
                "folder-a",
                "version-a",
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                new SemanticIndexingSourceIdentity("folders", "tenant-a", "organizations/organization-a/folders/folder-a/versions/version-a"),
                null!,
                new SemanticIndexingPolicyOutcome(true, "tenant-sensitive", "allowed"),
                "correlation-a",
                "task-a",
                "idempotency-a"))
            .ParamName.ShouldBe("content");
        Should.Throw<ArgumentNullException>(() => new SemanticIndexingRequest(
                "tenant-a",
                "organization-a",
                "folder-a",
                "version-a",
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                new SemanticIndexingSourceIdentity("folders", "tenant-a", "organizations/organization-a/folders/folder-a/versions/version-a"),
                new SemanticIndexingContentDescriptor("authorized-file-version", 1024, "text/markdown", "small", "text"),
                null!,
                "correlation-a",
                "task-a",
                "idempotency-a"))
            .ParamName.ShouldBe("policy");
    }

    [Fact]
    public void SemanticIndexingSourceIdentityShouldRejectRawFilesystemIdentity()
    {
        Should.Throw<ArgumentException>(() => new SemanticIndexingSourceIdentity("file", "tenant-a", "organizations/organization-a"))
            .ParamName.ShouldBe("sourceScheme");
        Should.Throw<ArgumentException>(() => new SemanticIndexingSourceIdentity("folders", "tenant-a", "/tmp/folders/version-a"))
            .ParamName.ShouldBe("sourceResourceId");
        Should.Throw<ArgumentException>(() => new SemanticIndexingSourceIdentity("folders", "tenant-a", "C:\\folders\\version-a"))
            .ParamName.ShouldBe("sourceResourceId");
        Should.Throw<ArgumentException>(() => new SemanticIndexingSourceIdentity("folders", "tenant-a", "C:/folders/version-a"))
            .ParamName.ShouldBe("sourceResourceId");
    }

    [Fact]
    public void SemanticIndexingSourceIdentityShouldCreateStableNonFileUri()
    {
        SemanticIndexingSourceIdentity source = new(
            "folders",
            "tenant-a",
            "organizations/organization-a/folders/folder-a/versions/version-a");

        source.ToUriString().ShouldBe("folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a");
    }

    [Fact]
    public void SemanticIndexingContentAndPolicyRecordsShouldRejectInvalidDescriptorData()
    {
        Should.Throw<ArgumentException>(() => new SemanticIndexingContentDescriptor(" ", 1024, "text/markdown", "small", "text"))
            .ParamName.ShouldBe("indexingTextDescriptor");
        Should.Throw<ArgumentOutOfRangeException>(() => new SemanticIndexingContentDescriptor("authorized-file-version", -1, "text/markdown", "small", "text"))
            .ParamName.ShouldBe("lengthBytes");
        Should.Throw<ArgumentException>(() => new SemanticIndexingPolicyOutcome(true, " ", "allowed"))
            .ParamName.ShouldBe("sensitivityClassification");
        Should.Throw<ArgumentException>(() => new SemanticIndexingResult(SemanticIndexingStatus.Deferred, " ", retryable: true))
            .ParamName.ShouldBe("reasonCode");
    }

    [Fact]
    public void SemanticIndexingPublicContractShouldNotExposeMemoriesDtoTypes()
    {
        Type[] publicSemanticIndexingTypes = typeof(ISemanticIndexingPort).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, "Hexalith.Folders.Workers.SemanticIndexing", StringComparison.Ordinal) && type.IsPublic)
            .ToArray();

        foreach (Type semanticIndexingType in publicSemanticIndexingTypes)
        {
            Type[] exposedTypes = semanticIndexingType.GetConstructors()
                .SelectMany(ctor => ctor.GetParameters().Select(parameter => parameter.ParameterType))
                .Concat(semanticIndexingType.GetProperties().Select(property => property.PropertyType))
                .Concat(semanticIndexingType.GetMethods().Select(method => method.ReturnType))
                .Concat(semanticIndexingType.GetMethods().SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)))
                .SelectMany(ExpandTypeGraph)
                .ToArray();

            exposedTypes.Any(type => type.Namespace?.StartsWith("Hexalith.Memories", StringComparison.Ordinal) == true)
                .ShouldBeFalse(
                $"{semanticIndexingType.FullName} must not expose Memories DTOs through the worker-owned port contract.");
        }
    }

    [Fact]
    public void WorkerLocalMemoriesRoutingDefaultsShouldMatchAspireConstants()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Hexalith.Folders.Aspire", "FoldersAspireModule.cs"));

        FoldersSemanticIndexingDefaults.CloudEventsSource.ShouldBe(ReadConstant(source, "MemoriesSourceId"));
        FoldersSemanticIndexingDefaults.IndexTenant.ShouldBe(ReadConstant(source, "MemoriesIndexTenant"));
        FoldersSemanticIndexingDefaults.PubSubName.ShouldBe("pubsub");
        FoldersSemanticIndexingDefaults.EventsTopicName.ShouldBe("memories-events");
        FoldersSemanticIndexingDefaults.DomainEventsTopicName.ShouldBe("folders.events");
        FoldersSemanticIndexingDefaults.DomainEventsRoute.ShouldBe("/folders/events");
    }

    private static ServiceCollection CreateServiceCollection()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        return services;
    }

    private static SemanticIndexingRequest CreateRequest(
        string managedTenantId = "tenant-a",
        string correlationId = "correlation-a",
        string idempotencyKey = "idempotency-a",
        byte[]? contentBytes = null)
        => new(
            managedTenantId,
            "organization-a",
            "folder-a",
            "version-a",
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            // Use a fixed, valid source authority so this helper isolates the parameter under test.
            // Threading managedTenantId here would make the nested SemanticIndexingSourceIdentity validation
            // fire first (paramName "sourceAuthority") and mask the SemanticIndexingRequest boundary check.
            new SemanticIndexingSourceIdentity("folders", "tenant-a", "organizations/organization-a/folders/folder-a/versions/version-a"),
            new SemanticIndexingContentDescriptor("authorized-file-version", 1024, "text/markdown", "small", "text"),
            new SemanticIndexingPolicyOutcome(true, "tenant-sensitive", "allowed"),
            correlationId,
            "task-a",
            idempotencyKey,
            contentBytes);

    private static IEnumerable<Type> ExpandTypeGraph(Type type)
    {
        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments().SelectMany(ExpandTypeGraph))
            {
                yield return argument;
            }
        }

        if (type.HasElementType)
        {
            foreach (Type elementType in ExpandTypeGraph(type.GetElementType()!))
            {
                yield return elementType;
            }
        }

        yield return type;
    }

    private static string ReadConstant(string source, string constantName)
    {
        Match match = Regex.Match(source, $$"""public const string {{constantName}} = "([^"]+)";""", RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue($"{constantName} should be declared as a string constant.");
        return match.Groups[1].Value;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _payload;
        private readonly HttpStatusCode _statusCode;

        public CapturingHandler(HttpStatusCode statusCode, string payload)
        {
            _statusCode = statusCode;
            _payload = payload;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Payloads.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true));
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
