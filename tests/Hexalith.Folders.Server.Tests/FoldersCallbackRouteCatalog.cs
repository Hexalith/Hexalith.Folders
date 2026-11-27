using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Provisioning;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Retains one validated isolated catalog for the actual production callback registrations.</summary>
/// <param name="services">The isolated production-composed services.</param>
/// <param name="endpoints">The actual mapped callback endpoints.</param>
/// <param name="includeCreateFolder">Whether this isolated fixture also serves the signed Folder create callbacks and legacy projection.</param>
/// <param name="tenant">The tenant for the isolated receiver binding.</param>
internal sealed class FoldersCallbackRouteCatalog(IServiceProvider services, Func<IEnumerable<Endpoint>> endpoints,
    bool includeCreateFolder = false, string tenant = "tenant-a") : IRouteCatalogAuthority
{
    private CommittedRouteCatalogSnapshot? _snapshot;

    /// <summary>Gets the immutable generation selected by this source fixture.</summary>
    internal CommittedRouteCatalogSnapshot Snapshot => _snapshot ??= CreateSnapshot();

    /// <inheritdoc/>
    public Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RouteCatalogDispatchLease(Snapshot, static () => Task.CompletedTask));
    }

    /// <inheritdoc/>
    public Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RouteCatalogEnvelope envelope = Snapshot.Envelope;
        return Task.FromResult(new RouteCatalogGenerationReadback(Snapshot.Epoch, envelope.Generation,
            Snapshot.RootDigest, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest, envelope.Profile, [], true));
    }

    /// <inheritdoc/>
    public Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default) => Task.FromException(Immutable());

    /// <inheritdoc/>
    public Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default)
        => Task.FromException<RouteCatalogHostAttestation>(Immutable());

    /// <inheritdoc/>
    public Task CommitAsync(string attemptId, CancellationToken cancellationToken = default) => Task.FromException(Immutable());

    /// <inheritdoc/>
    public Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default) => Task.FromException(Immutable());

    /// <inheritdoc/>
    public Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default) => Task.FromException(Immutable());

    private CommittedRouteCatalogSnapshot CreateSnapshot()
    {
        using IServiceScope scope = services.CreateScope();
        IServiceProvider local = scope.ServiceProvider;
        DomainProjectionIdentityOptions identity = local.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        List<RouteContractDeclaration> declarations = [];
        List<RouteCatalogEntry> targets = [];
        List<RouteOperationContract> operations = [];
        List<IdempotencyCatalogEntry> admission = [];
        if (includeCreateFolder)
        {
            // The receiver fixture binds the existing owner adapter's static identity and semantic fields.
            // No gateway admission, digest-key availability or installation authority is inferred here.
            RouteContractDeclaration command = Add("folders-create", "folders", FoldersServerModule.CreateFolderCommandType,
                null, typeof(FolderDomainProcessor), "process", EventStoreOperationIdentifiers.DomainServiceProcess,
                typeof(DomainServiceRequest), typeof(DomainServiceWireResult));
            IdempotencyDescriptorSchema descriptor = new("folders-create-folder-v1", 1,
                [new("folder-metadata-display-name", "string"), new("parent-folder-id", "string"), new("request-schema-version", "string")]);
            IdempotencyCatalogEntry entry = new(command.RouteId, 1, command.Domain, command.MessageType!,
                "hexalith-folders", "create-folder", "folders-contract-spine-v2", descriptor,
                RouteCatalogCodec.ComputeDescriptorDigest(descriptor), "mutation", "fixture-v1", ["fixture-v1"],
                "fixture-keys", "fixture-idempotency-ring", "fixture-v1", [new("fixture-v1", "fixture-v1")], "eventstore", string.Empty);
            admission.Add(entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) });
            CommandIntentDeclaration provision = FoldersRuntimeCatalogDeclarations.Commands
                .Single(declaration => declaration.Route.MessageType == FoldersServerModule.ProvisionFolderCommandType);
            RouteContractDeclaration provisionRoute = Add(provision.Route.RouteId, "folders", FoldersServerModule.ProvisionFolderCommandType,
                null, typeof(FolderProvisioningRequest), "process", EventStoreOperationIdentifiers.DomainServiceProcess,
                typeof(DomainServiceRequest), typeof(DomainServiceWireResult));
            IdempotencyCatalogEntry provisionEntry = entry with
            {
                RouteId = provisionRoute.RouteId,
                CommandType = FoldersServerModule.ProvisionFolderCommandType,
                OperationId = provision.OperationId,
                DescriptorSchema = provision.DescriptorSchema,
                DescriptorDigest = RouteCatalogCodec.ComputeDescriptorDigest(provision.DescriptorSchema),
                ContentDigest = string.Empty,
            };
            admission.Add(provisionEntry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(provisionEntry) });
            Add("folders-legacy", "folders", null, "folder-state", typeof(FolderDomainProjectionHandler), "project",
                EventStoreOperationIdentifiers.DomainServiceProject, typeof(ProjectionRequest), typeof(ProjectionResponse),
                ["folder-state", "organization-provider-binding"]);
        }
        var publicQueries = FoldersRuntimeCatalogDeclarations.Routes.Where(route =>
            route is { Kind: "query", Execution.QueryKind: "projection" }).ToDictionary(route => route.MessageType!, StringComparer.Ordinal);
        var publicOperations = FoldersRuntimeCatalogDeclarations.Operations.Where(operation =>
            operation is { Kind: "query", Ingress: "public" }).ToDictionary(operation => operation.RouteId, StringComparer.Ordinal);
        foreach (IDomainQueryHandler handler in local.GetServices<IDomainQueryHandler>())
        {
            if (handler.Domain == "folders" && publicQueries.TryGetValue(handler.QueryType, out RouteContractDeclaration? declaredRoute))
            {
                Bind(declaredRoute, publicOperations[declaredRoute.RouteId], "query");
            }
            else
            {
                Add("query-" + declarations.Count, handler.Domain, handler.QueryType, null, handler.GetType(),
                    "query", EventStoreOperationIdentifiers.DomainServiceQuery, typeof(QueryEnvelope), typeof(QueryResult));
            }
        }
        foreach (IAsyncDomainProjectionHandler handler in local.GetServices<IAsyncDomainProjectionHandler>())
        {
            Add("projection-" + declarations.Count, handler.Domain, null, handler.ProjectionType, handler.GetType(),
                "project/v2", EventStoreOperationIdentifiers.DomainServiceProject, typeof(ProjectionDispatchRequest), typeof(ProjectionDispatchResponse));
        }
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new(3, 1, "Production", false,
            declarations, targets, admission, string.Empty, string.Empty, operations, []));
        IReadOnlyList<RouteCatalogAuxiliaryOperation> auxiliary = RouteCatalogDomainObservation.Auxiliary(local, endpoints());
        if (includeCreateFolder)
        {
            auxiliary = [.. auxiliary, new("g1-tasks", "internal/durable-tasks/v1/steps/lookup",
                "eventstore:durable-task-step-lookup", "folders")];
        }

        return new(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            auxiliary, RouteCatalogDomainObservation.Capabilities(local), identity.AppId);

        RouteContractDeclaration Add(string id, string domain, string? query, string? projection, Type handler, string method,
            string operationName, Type request, Type response, IReadOnlyList<string>? outputs = null)
        {
            RouteContractDeclaration declaration = new(id, method == "process" ? "command" : query is null ? "projection" : "query", domain, query,
                projection, "v1", method == "process" ? 1 : null, "workload", operationName, null, [],
                new(handler.FullName!, "exact", [query ?? projection!], "domain-service", query is null || method == "process" ? null : "computed",
                    outputs ?? (projection is null ? [] : [projection])));
            RouteOperationContract operation = RouteOperationSchema.Create(declaration, request, response,
                "internal", "agent-eligible", JsonSerializerOptions.Web);
            Bind(declaration, operation, method);
            return declaration;
        }

        void Bind(RouteContractDeclaration declaration, RouteOperationContract operation, string method)
        {
            declarations.Add(declaration);
            operations.Add(operation);
            targets.Add(new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), identity.AppId, method,
                identity.ServiceVersion, declaration.CredentialKind, declaration.Operation, false, tenant, operation.Digest));
        }
    }

    private static RouteCatalogUnavailableException Immutable() => new("The isolated callback fixture catalog is immutable.");
}
