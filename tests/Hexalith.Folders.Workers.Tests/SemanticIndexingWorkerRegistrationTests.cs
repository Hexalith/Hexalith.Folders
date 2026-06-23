using System.Text.RegularExpressions;

using Dapr;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Projections.SemanticIndexing;
using Hexalith.Folders.Workers;
using Hexalith.Folders.Workers.SemanticIndexing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

// Alias the Memories contracts used here: their V1 namespace also defines a Case type that collides with
// Shouldly.Case used by the metadata-safety assertions.
using SearchIndexEntryChanged = Hexalith.Memories.Contracts.V1.SearchIndexEntryChanged;
using SearchIndexEntryRemoved = Hexalith.Memories.Contracts.V1.SearchIndexEntryRemoved;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class SemanticIndexingWorkerRegistrationTests
{
    [Fact]
    public void AddFoldersSemanticIndexingWorkersShouldRegisterPortAndDaprClient()
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
        provider.GetRequiredService<DaprClient>().ShouldNotBeNull();
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
        provider.GetRequiredService<DaprClient>().ShouldNotBeNull();
    }

    [Fact]
    public async Task SemanticIndexingPortShouldPublishCuratedSearchIndexEntryChangedCloudEvent()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);

        SemanticIndexingResult result = await port.IndexFileVersionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        const string expectedSourceUri = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";
        result.Status.ShouldBe(SemanticIndexingStatus.Accepted);
        result.Retryable.ShouldBeFalse();
        result.ReasonCode.ShouldBe("memories_accepted");
        result.PublishedEventId.ShouldBe(expectedSourceUri);

        object?[] arguments = dapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        arguments[0].ShouldBe(FoldersSemanticIndexingDefaults.PubSubName);
        arguments[1].ShouldBe(FoldersSemanticIndexingDefaults.EventsTopicName);
        SearchIndexEntryChanged entry = arguments[2].ShouldBeOfType<SearchIndexEntryChanged>();
        Dictionary<string, string> metadata = arguments[3].ShouldBeOfType<Dictionary<string, string>>();

        entry.TenantId.ShouldBe("folders-index");

        // The upsert key is per-file-version (not folder-level), so two files in the same folder do not collapse
        // onto one search-index entry. '/' is excluded from segment ids, so the join cannot collide across tenants.
        entry.AggregateId.ShouldBe("tenant-a/organization-a/folder-a/version-a");
        entry.Text.ShouldContain("version-a", Case.Sensitive);
        entry.Text.ShouldNotContain("C:/", Case.Sensitive);
        entry.CorrelationId.ShouldBe("correlation-a");
        entry.CausationId.ShouldBe("task-a");
        entry.Attributes["folders.managedTenantId"].ShouldBe("tenant-a");
        entry.Attributes["folders.fileVersionId"].ShouldBe("version-a");
        entry.Attributes["folders.sensitivityClassification"].ShouldBe("tenant-sensitive");
        // The live upsert path stamps folders.status = active so the Story 10.5 facade can distinguish live vs archived.
        entry.Attributes["folders.status"].ShouldBe("active");
        // Plain-string attributes only: correlation/task/idempotency belong on the event header, not the filter map.
        entry.Attributes.ShouldNotContainKey("folders.idempotencyKey");
        entry.Attributes.ShouldNotContainKey("folders.correlationId");

        metadata["cloudevent.id"].ShouldBe(expectedSourceUri);
        metadata["cloudevent.type"].ShouldBe(nameof(SearchIndexEntryChanged));
        metadata["cloudevent.source"].ShouldBe("hexalith-folders");

        result.IndexedText.ShouldBe(entry.Text);
        result.IndexedAttributes.ShouldNotBeNull();
        result.IndexedAttributes!["folders.status"].ShouldBe("active");
    }

    [Fact]
    public async Task SemanticIndexingPortShouldPublishSearchIndexEntryRemovedForHardDelete()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        const string indexedEventId = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";

        SemanticIndexingResult result = await port.RemoveFileVersionAsync(
            new SemanticIndexingRemovalRequest("tenant-a", "organization-a", "folder-a", "version-a", indexedEventId, "correlation-a", "task-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Accepted);
        result.Retryable.ShouldBeFalse();
        result.PublishedEventId.ShouldBe(indexedEventId);

        object?[] arguments = dapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        arguments[0].ShouldBe(FoldersSemanticIndexingDefaults.PubSubName);
        arguments[1].ShouldBe(FoldersSemanticIndexingDefaults.EventsTopicName);
        SearchIndexEntryRemoved removed = arguments[2].ShouldBeOfType<SearchIndexEntryRemoved>();
        Dictionary<string, string> metadata = arguments[3].ShouldBeOfType<Dictionary<string, string>>();

        removed.TenantId.ShouldBe("folders-index");
        // The removal AggregateId is reconstructed identically to the upsert's, so it targets the same document.
        removed.AggregateId.ShouldBe("tenant-a/organization-a/folder-a/version-a");
        removed.CorrelationId.ShouldBe("correlation-a");
        removed.CausationId.ShouldBe("task-a");

        metadata["cloudevent.id"].ShouldBe(indexedEventId);
        metadata["cloudevent.type"].ShouldBe(nameof(SearchIndexEntryRemoved));
        metadata["cloudevent.source"].ShouldBe("hexalith-folders");
    }

    [Fact]
    public async Task RemoveAndUpsertShouldTargetByteIdenticalIdentityForTheSameFileVersion()
    {
        // Identity equivalence (AC5): the AggregateId and cloudevent.id the removal publishes MUST be byte-identical to
        // the upsert's, or the delete misses (or hits the wrong doc) under the composite (TenantId, AggregateId) key.
        DaprClient upsertDapr = Substitute.For<DaprClient>();
        DaprClient removeDapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort upsertPort = new MemoriesSemanticIndexingPort(upsertDapr);
        ISemanticIndexingPort removePort = new MemoriesSemanticIndexingPort(removeDapr);

        SemanticIndexingResult upsert = await upsertPort.IndexFileVersionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        object?[] upsertArguments = upsertDapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        SearchIndexEntryChanged changed = upsertArguments[2].ShouldBeOfType<SearchIndexEntryChanged>();
        string upsertCloudEventId = ((Dictionary<string, string>)upsertArguments[3]!)["cloudevent.id"];

        await removePort.RemoveFileVersionAsync(
            new SemanticIndexingRemovalRequest("tenant-a", "organization-a", "folder-a", "version-a", upsert.PublishedEventId!, "correlation-a", "task-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        object?[] removeArguments = removeDapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        SearchIndexEntryRemoved removed = removeArguments[2].ShouldBeOfType<SearchIndexEntryRemoved>();
        string removeCloudEventId = ((Dictionary<string, string>)removeArguments[3]!)["cloudevent.id"];

        removed.AggregateId.ShouldBe(changed.AggregateId);
        removeCloudEventId.ShouldBe(upsertCloudEventId);
    }

    [Fact]
    public async Task SemanticIndexingPortShouldReSendFullArchivedDocumentForSoftDelete()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        const string indexedEventId = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";
        Dictionary<string, string> originalAttributes = new(StringComparer.Ordinal)
        {
            ["folders.managedTenantId"] = "tenant-a",
            ["folders.fileVersionId"] = "version-a",
            ["folders.contentDescriptor"] = "authorized-file-version",
            ["folders.status"] = "active",
        };

        SemanticIndexingResult result = await port.SoftDeleteFileVersionAsync(
            new SemanticIndexingArchiveRequest(
                "tenant-a",
                "organization-a",
                "folder-a",
                "version-a",
                indexedEventId,
                "authorized-file-version version-a text",
                originalAttributes,
                "correlation-a",
                "task-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Accepted);
        result.PublishedEventId.ShouldBe(indexedEventId);

        object?[] arguments = dapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        SearchIndexEntryChanged entry = arguments[2].ShouldBeOfType<SearchIndexEntryChanged>();
        Dictionary<string, string> metadata = arguments[3].ShouldBeOfType<Dictionary<string, string>>();

        entry.TenantId.ShouldBe("folders-index");
        entry.AggregateId.ShouldBe("tenant-a/organization-a/folder-a/version-a");
        // The full original document is re-sent (the Memories upsert is a destructive full-field overwrite) with only
        // folders.status flipped to archived.
        entry.Text.ShouldBe("authorized-file-version version-a text");
        entry.Attributes["folders.contentDescriptor"].ShouldBe("authorized-file-version");
        entry.Attributes["folders.status"].ShouldBe("archived");

        metadata["cloudevent.id"].ShouldBe(indexedEventId);
        metadata["cloudevent.type"].ShouldBe(nameof(SearchIndexEntryChanged));
    }

    [Fact]
    public async Task SemanticIndexingPortShouldFallBackToDescriptorTextWhenArchiveEvidenceIsAbsent()
    {
        // When the preserved evidence retains no index-time text/attributes (legacy entries), the archive re-send falls
        // back to a C9-safe descriptor form; the loss of rich text is accepted (archived units are filtered by 10.5).
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        const string indexedEventId = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";

        await port.SoftDeleteFileVersionAsync(
            new SemanticIndexingArchiveRequest("tenant-a", "organization-a", "folder-a", "version-a", indexedEventId, null, null, "correlation-a", "task-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        object?[] arguments = dapr.ReceivedCalls().ShouldHaveSingleItem().GetArguments();
        SearchIndexEntryChanged entry = arguments[2].ShouldBeOfType<SearchIndexEntryChanged>();
        entry.Text.ShouldNotBeNullOrWhiteSpace();
        entry.Text.ShouldContain("version-a", Case.Sensitive);
        entry.Text.ShouldNotContain("C:/", Case.Sensitive);
        entry.Attributes["folders.status"].ShouldBe("archived");
    }

    [Fact]
    public async Task SemanticIndexingPortRemovalShouldMapDaprPublishFailureToRetryableFailure()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        dapr.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<SearchIndexEntryRemoved>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DaprException("memories pub/sub unavailable")));
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        const string indexedEventId = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";

        SemanticIndexingResult result = await port.RemoveFileVersionAsync(
            new SemanticIndexingRemovalRequest("tenant-a", "organization-a", "folder-a", "version-a", indexedEventId, "correlation-a", "task-a"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Failed);
        result.Retryable.ShouldBeTrue();
        result.ReasonCode.ShouldBe("memories_publish_error");
        result.PublishedEventId.ShouldBeNull();
    }

    [Fact]
    public async Task SemanticIndexingPortRemovalShouldHonorCancelledTokenBeforePublishing()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        const string indexedEventId = "folders://tenant-a/organizations/organization-a/folders/folder-a/versions/version-a";

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await port.RemoveFileVersionAsync(
                new SemanticIndexingRemovalRequest("tenant-a", "organization-a", "folder-a", "version-a", indexedEventId, "correlation-a", "task-a"),
                cancellation.Token).ConfigureAwait(true)).ConfigureAwait(true);
        dapr.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SemanticIndexingPortShouldMapDaprPublishFailureToRetryableFailure()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        dapr.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<SearchIndexEntryChanged>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DaprException("memories pub/sub unavailable")));
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);

        SemanticIndexingResult result = await port.IndexFileVersionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(SemanticIndexingStatus.Failed);
        result.Retryable.ShouldBeTrue();
        result.ReasonCode.ShouldBe("memories_publish_error");
        result.PublishedEventId.ShouldBeNull();
    }

    [Fact]
    public async Task SemanticIndexingPortShouldHonorCancelledTokenBeforePublishing()
    {
        DaprClient dapr = Substitute.For<DaprClient>();
        ISemanticIndexingPort port = new MemoriesSemanticIndexingPort(dapr);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await port.IndexFileVersionAsync(CreateRequest(), cancellation.Token).ConfigureAwait(true)).ConfigureAwait(true);
        dapr.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void SemanticIndexingRequestShouldRejectBlankPublicBoundaryIdentifiers()
    {
        Should.Throw<ArgumentException>(() => CreateRequest(managedTenantId: " "))
            .ParamName.ShouldBe("managedTenantId");
        Should.Throw<ArgumentException>(() => CreateRequest(correlationId: " "))
            .ParamName.ShouldBe("correlationId");
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
                "task-a"))
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
                "task-a"))
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
                "task-a"))
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

        // Story 10.3 (D1): the worker's subscribed topic must equal the AppHost's EventStore publish-topic
        // override (FoldersAspireModule.FolderDomainEventsTopic) or the cross-process binding silently breaks.
        FoldersSemanticIndexingDefaults.DomainEventsTopicName.ShouldBe(ReadConstant(source, "FolderDomainEventsTopic"));
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
        string correlationId = "correlation-a")
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
            "task-a");

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
}
