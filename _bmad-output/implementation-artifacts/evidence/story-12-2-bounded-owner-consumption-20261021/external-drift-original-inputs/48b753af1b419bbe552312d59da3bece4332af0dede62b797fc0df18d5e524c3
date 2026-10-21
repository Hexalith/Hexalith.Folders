// <copyright file="ProjectsServerServiceCollectionExtensions.cs" company="Hexalith">
// Copyright (c) Hexalith. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Projects.Server;

using System;
using System.Linq;

using Hexalith.Conversations.Client;
using Hexalith.Folders.Client;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Memories.Client.Rest;
using Hexalith.Projects.Authorization;
using Hexalith.Projects.Contracts.Queries;
using Hexalith.Projects.Infrastructure;
using Hexalith.Projects.Projections.TenantAccess;
using Hexalith.Projects.Server.Conversations;
using Hexalith.Projects.Server.Folders;
using Hexalith.Projects.Server.Memories;
using Hexalith.Projects.Server.Proposals;
using Hexalith.Projects.Server.Projections.ConversationStartSetup;
using Hexalith.Projects.Server.Projections.ProjectReads;
using Hexalith.Projects.Server.Queries;
using Hexalith.Projects.Server.Authentication;
using Hexalith.Projects.Server.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using FoldersClient = Hexalith.Folders.Client.Generated.IClient;

/// <summary>
/// DI registration for the Hexalith.Projects Server command-async + minimal-read slice (Story 1.4).
/// </summary>
public static class ProjectsServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Projects domain processor, the in-memory detail read model, and a
    /// <see cref="TimeProvider"/> for the command-async + minimal-read endpoints. The production
    /// <see cref="IProjectCommandSubmitter"/> (EventStore gateway-backed) and the gateway client are
    /// registered by <see cref="AddProjectsServerRuntimeInfrastructure"/>; a caller may register a fake submitter
    /// for Tier-2 tests.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddProjectsServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddProjectsModule();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InMemoryProjectDetailReadModel>();
        services.TryAddSingleton<IProjectDetailReadModel>(sp => sp.GetRequiredService<InMemoryProjectDetailReadModel>());
        services.TryAddSingleton<InMemoryProjectListReadModel>();
        services.TryAddSingleton<IProjectListReadModel>(sp => sp.GetRequiredService<InMemoryProjectListReadModel>());
        services.TryAddSingleton<InMemoryProjectReferenceIndexReadModel>();
        services.TryAddSingleton<IProjectReferenceIndexReadModel>(sp => sp.GetRequiredService<InMemoryProjectReferenceIndexReadModel>());
        services.TryAddSingleton<InMemoryProjectAuditTimelineReadModel>();
        services.TryAddSingleton<IProjectAuditTimelineReadModel>(sp => sp.GetRequiredService<InMemoryProjectAuditTimelineReadModel>());
        services.TryAddSingleton<InMemoryProjectReadModelStore>();
        services.TryAddSingleton<IReadModelStore>(sp => sp.GetRequiredService<InMemoryProjectReadModelStore>());
        services.TryAddSingleton<IProjectTenantContextAccessor, HttpContextProjectTenantContextAccessor>();
        services.TryAddSingleton<ProjectAuthorizationGate>();
        services.TryAddSingleton<IProjectProposalConfirmationIdempotencyLedger, InMemoryProjectProposalConfirmationIdempotencyLedger>();
        services.TryAddSingleton<IActorPartyResolver, DeterministicActorPartyResolver>();
        services.TryAddTransient<IProjectConversationDirectory>(sp =>
        {
            IConversationClient? client = sp.GetService<IConversationClient>();
            return client is null
                ? new UnavailableProjectConversationDirectory()
                : new ConversationsProjectConversationDirectory(client);
        });
        services.TryAddTransient<IProjectConversationAssignmentDirectory>(sp =>
        {
            IConversationClient? client = sp.GetService<IConversationClient>();
            return client is null
                ? new UnavailableProjectConversationAssignmentDirectory()
                : new ConversationsProjectConversationAssignmentDirectory(
                    client,
                    sp.GetRequiredService<IActorPartyResolver>());
        });
        services.TryAddTransient<IProjectConversationResolutionDirectory>(sp =>
        {
            IConversationClient? client = sp.GetService<IConversationClient>();
            return client is null
                ? new UnavailableProjectConversationResolutionDirectory()
                : new ConversationsProjectConversationResolutionDirectory(client);
        });
        services.AddHttpClient();
        services.TryAddTransient<DelegatedOwnerQueryClient>(sp => ActivatorUtilities.CreateInstance<DelegatedOwnerQueryClient>(sp));
        services.TryAddTransient<IProjectFolderDirectory>(sp =>
        {
            FoldersClient? client = sp.GetService<FoldersClient>();
            IProjectFolderDirectory direct = client is null ? new UnavailableProjectFolderDirectory() : new FoldersProjectFolderDirectory(client);
            return new DelegatedProjectFolderDirectory(() => sp.GetRequiredService<DelegatedOwnerQueryClient>(),
                sp.GetRequiredService<IHttpContextAccessor>(), direct);
        });
        services.TryAddTransient<IProjectFileReferenceDirectory>(sp =>
        {
            FoldersClient? client = sp.GetService<FoldersClient>();
            return client is null
                ? new UnavailableProjectFileReferenceDirectory()
                : new FoldersProjectFileReferenceDirectory(client);
        });
        services.TryAddTransient<IProjectMemoryDirectory>(sp =>
        {
            MemoriesClient? client = sp.GetService<MemoriesClient>();
            return client is null
                ? new UnavailableProjectMemoryDirectory()
                : new MemoriesProjectMemoryDirectory(client);
        });
        services.TryAddSingleton<IProjectEventStoreAuthorizationValidator, DenyAllProjectEventStoreAuthorizationValidator>();
        services.TryAddSingleton<IProjectDaprPolicyEvidenceProvider, DenyAllProjectDaprPolicyEvidenceProvider>();
        services.TryAddSingleton<IClaimsTransformation, ProjectsClaimsTransformation>();
        services.TryAddTransient<IDomainProcessor, ProjectsDomainProcessor>();
        services.AddKeyedTransient<IDomainProcessor>(ProjectsServerModule.DomainName,
            (provider, _) => provider.GetRequiredService<IDomainProcessor>());
        services.AddOptions<ProjectionDispatchOptions>().BindConfiguration("EventStore:ProjectionDispatch");
        services.AddOptions<DomainProjectionIdentityOptions>().BindConfiguration("EventStore:DomainService")
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.AppId)) { options.AppId = ProjectsServerModule.DomainName; }
            });
        services.AddSingleton<IDomainQueryHandler, GetConversationStartSetupQueryHandler>();
        services.AddSingleton<ProjectQueryEnvelopePrincipalBinding>();
        services.AddSingleton<ProjectContextQueryExecutor>();
        services.AddSingleton<IDomainQueryHandler, GetProjectContextQueryHandler>();
        services.AddSingleton<IDomainQueryHandler, ExplainContextSelectionQueryHandler>();
        services.AddDataProtection();
        services.AddEventStoreQueryCursorCodec("Hexalith.Projects.ReadCursor.v1");
        services.AddSingleton<ProjectReadAudit>();
        services.AddSingleton<ProjectReadQueryExecutor>();
        services.AddSingleton<IDomainQueryHandler, ListProjectsQueryHandler>();
        services.AddSingleton<IDomainQueryHandler, GetProjectQueryHandler>();

        return services;
    }

    /// <summary>
    /// Replaces pre-runtime fakes with Dapr/EventStore-backed runtime infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddProjectsServerRuntimeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectsDaprInfrastructure();
        services.AddAuthentication().AddEventStoreWorkloadScheme(
            EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            "Authentication:Workload",
            static (options, _) =>
            {
                options.Audience ??= "projects";
                if (options.AllowedCallers.Count == 0) { options.AllowedCallers.Add("eventstore"); }
            });
        services.AddEventStoreWorkloadPolicies(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme,
            [EventStoreWorkloadOperations.DomainServiceProcess,
                EventStoreWorkloadOperations.DomainServiceQuery,
                EventStoreWorkloadOperations.DomainServiceProject,
                EventStoreWorkloadOperations.DomainServiceMetadata]);
        services.AddSingleton<ProjectAuthorityRuntimeSecurity>();
        services.AddEventStoreWorkloadAssertionIssuer();
        string authorityDaprEndpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT")
            ?? $"http://127.0.0.1:{Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500"}";
        services.AddHttpClient<ProjectChatBotAuthorityGateway>(client => client.BaseAddress = new Uri(
            authorityDaprEndpoint.Replace("localhost", "127.0.0.1", StringComparison.OrdinalIgnoreCase)));
        services.AddTransient<ProjectOwnerBootstrapVerifier>();
        services.AddTransient<IAuthoritativeEventStreamReader>(provider =>
            new AuthoritativeEventStreamReader(provider.GetRequiredService<ProjectChatBotAuthorityGateway>(),
                provider.GetRequiredService<TimeProvider>(), TimeSpan.FromSeconds(10)));
        services.AddTransient<IDomainQueryHandler, ProjectChatBotAuthorityQueryHandler>();
        services.RemoveAll<IReadModelStore>();
        services.RemoveAll<InMemoryProjectReadModelStore>();
        services.AddEventStoreReadModelStore();
        services.AddSingleton<IAsyncDomainProjectionHandler, ConversationStartSetupProjectionHandler>();
        services.AddSingleton<ProjectReadProjectionHandler>();
        services.AddSingleton<IAsyncDomainProjectionHandler>(sp => sp.GetRequiredService<ProjectReadProjectionHandler>());
        services.RemoveAll<IProjectTenantAccessProjectionStore>();
        services.AddSingleton<IProjectTenantAccessProjectionStore, DaprProjectTenantAccessProjectionStore>();
        services.RemoveAll<IProjectEventStoreAuthorizationValidator>();
        services.AddSingleton<IProjectEventStoreAuthorizationValidator, AllowingProjectEventStoreAuthorizationValidator>();
        services.RemoveAll<IProjectDaprPolicyEvidenceProvider>();
        services.AddSingleton<IProjectDaprPolicyEvidenceProvider, AllowingProjectDaprPolicyEvidenceProvider>();
        services.RemoveAll<IProjectListReadModel>();
        services.RemoveAll<InMemoryProjectListReadModel>();
        services.AddSingleton<IProjectListReadModel, DaprProjectListReadModel>();
        services.RemoveAll<IProjectDetailReadModel>();
        services.RemoveAll<InMemoryProjectDetailReadModel>();
        services.AddSingleton<IProjectDetailReadModel, DaprProjectDetailReadModel>();
        services.RemoveAll<IProjectReferenceIndexReadModel>();
        services.RemoveAll<InMemoryProjectReferenceIndexReadModel>();
        services.AddSingleton<IProjectReferenceIndexReadModel, DaprProjectReferenceIndexReadModel>();
        services.RemoveAll<IProjectAuditTimelineReadModel>();
        services.RemoveAll<InMemoryProjectAuditTimelineReadModel>();
        services.AddSingleton<IProjectAuditTimelineReadModel, DaprProjectAuditTimelineReadModel>();
        if (!services.Any(static service => service.ServiceType == typeof(IConversationClient)))
        {
            services.AddHexalithConversationsClient(options => options.Endpoint = new Uri("http://conversations"));
        }

        if (!services.Any(static service => service.ServiceType == typeof(FoldersClient)))
        {
            services.TryAddTransient<EventStoreGatewayTokenForwardingHandler>();
            services.AddFoldersClient(options => options.BaseAddress = new Uri("http://folders"))
                .AddHttpMessageHandler<EventStoreGatewayTokenForwardingHandler>();
        }

        if (!services.Any(static service => service.ServiceType == typeof(MemoriesClient)))
        {
            services.AddMemoriesClient(options => options.Endpoint = new Uri("http://memories"));
        }

        // Forward the authenticated caller's bearer token onto the EventStore command gateway: the
        // command API is [Authorize] and reads the command UserId from the JWT 'sub'. Without this the
        // persist-then-publish hop is rejected (401) and the create collapses to a safe-denial.
        services.AddHttpContextAccessor();
        services.TryAddTransient<EventStoreGatewayTokenForwardingHandler>();
        _ = services
            .AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("http://eventstore"))
            .AddHttpMessageHandler<EventStoreGatewayTokenForwardingHandler>();
        services.TryAddSingleton<IProjectCommandSubmitter, EventStoreProjectCommandSubmitter>();

        return services;
    }

    /// <summary>Maps the Projects Server endpoints (the command-async POST + minimal-read GET).</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapProjectsServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteHandlerBuilder processEndpoint = endpoints.MapPost(ProjectsServerModule.ProcessRoute, async (
            DomainServiceRequest request,
            IDomainProcessor processor) =>
        {
            DomainResult result = await processor
                .ProcessAsync(request.Command, request.CurrentState)
                .ConfigureAwait(false);
            return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
        });
        RouteHandlerBuilder projectEndpoint = endpoints.MapPost(ProjectsServerModule.ProjectRoute, static (ProjectionRequest request) =>
            string.Equals(request.Domain, ProjectsServerModule.DomainName, StringComparison.Ordinal)
                ? Results.Ok(ProjectProjectionHandler.Project(request))
                : Results.NotFound());
        RouteHandlerBuilder queryEndpoint = endpoints.MapPost(
            "/query",
            async (QueryEnvelope query, IServiceProvider serviceProvider, CancellationToken cancellationToken) =>
            {
                QueryResult result = await DomainQueryDispatcher.ExecuteAsync(serviceProvider, query, cancellationToken).ConfigureAwait(false);
                if (query.QueryType is not (ListProjectsQuery.QueryType or GetProjectQuery.QueryType))
                {
                    return result.Success ? Results.Ok(result) : Results.NotFound(result);
                }
                return result.Success ? Results.Ok(result)
                    : result.ErrorMessage is QueryAdapterFailureReason.InvalidEnvelope or QueryAdapterFailureReason.InvalidCursor
                        ? Results.BadRequest(result)
                        : Results.NotFound(QueryResult.Failure("safe-denial"));
            });
        RouteHandlerBuilder metadataEndpoint = endpoints.MapPost(
            "/admin/operational-index-metadata",
            static (AdminOperationalIndexMetadata.Request request, IServiceProvider services) =>
                CreateOperationalIndexMetadata(request, services) is { } response
                    ? Results.Ok(response)
                    : Results.BadRequest());
        IConfiguration configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        IHostEnvironment environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (endpoints.ServiceProvider.GetService<ProjectAuthorityRuntimeSecurity>() is not null)
        {
            _ = processEndpoint.RequireAuthorization(EventStoreDomainServicePolicies.Process);
            _ = projectEndpoint.RequireAuthorization(EventStoreDomainServicePolicies.Project);
            _ = queryEndpoint.RequireAuthorization(EventStoreDomainServicePolicies.Query);
            _ = metadataEndpoint.RequireAuthorization(EventStoreDomainServicePolicies.Metadata);
        }
        else if (!ProjectsAuthenticationServiceCollectionExtensions.IsAnonymousDevelopmentBypass(configuration, environment))
        {
            _ = queryEndpoint.RequireAuthorization();
            _ = metadataEndpoint.RequireAuthorization();
        }

        endpoints.MapProjectsDomainServiceEndpoints();
        endpoints.MapProjectReadPlatformEndpoints();
        return endpoints;
    }

    internal static AdminOperationalIndexMetadata.Response? CreateOperationalIndexMetadata(
        AdminOperationalIndexMetadata.Request request, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Domains is not { Count: 1 } ||
            !string.Equals(request.Domains[0], ProjectsServerModule.DomainName, StringComparison.Ordinal) ||
            !string.Equals(request.AppId, ProjectsServerModule.DomainName, StringComparison.Ordinal) ||
            !string.Equals(request.ServiceVersion, "v1", StringComparison.Ordinal))
        {
            return null;
        }

        if (services is not null && services.GetServices<IAsyncDomainProjectionHandler>().Any())
        {
            DomainProjectionIdentityOptions identity = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
            if (request.AppId != identity.AppId || request.ServiceVersion != identity.ServiceVersion) { return null; }
            return AdminOperationalIndexMetadata.Create(new DiscoveryResult([], []), request.Domains,
                services.GetServices<IDomainQueryHandler>(), services.GetServices<IAsyncDomainProjectionHandler>(),
                identity.AppId, identity.ServiceVersion, services.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value,
                [ProjectsServerModule.DomainName]);
        }

        // Projects uses its own processor rather than an EventStoreAggregate<TState>. Advertise the
        // exact handler routes so EventStore dispatches these reads to /query instead of a projection actor.
        return new AdminOperationalIndexMetadata.Response(
        [
            new AdminOperationalIndexMetadata.DomainMetadata(
                ProjectsServerModule.DomainName,
                [], [], [], [], [],
                [
                    ProjectsServerModule.GetConversationStartSetupQueryType,
                    ProjectsServerModule.GetProjectContextQueryType,
                    ProjectsServerModule.ExplainContextSelectionQueryType,
                    ResolveProjectChatBotAuthority.QueryType,
                    ListProjectsQuery.QueryType,
                    GetProjectQuery.QueryType,
                ]),
        ]);
    }
}
