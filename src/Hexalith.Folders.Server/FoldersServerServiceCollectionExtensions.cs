using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.Folders;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.ContextSearch;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;
using Hexalith.Folders.Server.ContextSearch;

using Hexalith.Memories.Client.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.Folders.Server;

public static class FoldersServerServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventStore(typeof(FoldersModule).Assembly);
        services.AddEventStoreGatewayClient();
        services.AddFoldersObservability();
        services.AddHttpContextAccessor();
        services.AddOptions<TenantContextOptions>().BindConfiguration(TenantContextOptions.SectionName);
        services.TryAddSingleton<ITenantContextAccessor, HttpContextTenantContextAccessor>();
        services.TryAddSingleton<IEventStoreClaimTransformEvidenceAccessor, HttpContextEventStoreClaimTransformEvidenceAccessor>();
        services.TryAddSingleton<IFolderCommandActionTokenMapper, FolderCommandActionTokenMapper>();
        services.TryAddScoped<ILayeredFolderAuthorizationResultAccessor, ScopedLayeredFolderAuthorizationResultAccessor>();
        services.TryAddScoped<IFolderArchiveAclEvidenceProvider, LayeredAuthBackedFolderArchiveAclEvidenceProvider>();
        services.TryAddScoped<IFolderArchivePolicyEvidenceProvider, BaselineFolderArchivePolicyEvidenceProvider>();
        services.TryAddScoped<IRepositoryCreationReadinessValidator, ProviderReadinessRepositoryCreationValidator>();
        services.TryAddScoped<IRepositoryBindingReadinessValidator, ProviderReadinessRepositoryBindingValidator>();
        services.TryAddScoped<IBranchRefPolicyReadinessValidator, ProviderReadinessBranchRefPolicyValidator>();
        services.TryAddScoped<IWorkspacePreparationReadinessValidator, ProviderReadinessWorkspacePreparationValidator>();
        services.TryAddScoped<IWorkspaceCommitReadinessValidator, ProviderReadinessWorkspaceCommitValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<FolderArchiveTenantGate>();
        services.TryAddScoped<FolderAccessTenantGate>();
        services.TryAddScoped<FolderCreationService>();
        services.TryAddScoped<FolderAccessMutationService>();
        services.TryAddScoped<ConfigureProviderBindingService>();

        // In-memory organization provider-binding write store default, consistent with the other
        // in-memory provider/read-model defaults registered for the server composition. Production
        // hosts override this with an EventStore-backed implementation.
        services.TryAddSingleton<IOrganizationProviderBindingRepository, InMemoryOrganizationProviderBindingRepository>();
        services.TryAddScoped<RepositoryBackedFolderCreationService>();
        services.TryAddScoped<RepositoryBindingService>();
        services.TryAddScoped<BranchRefPolicyConfigurationService>();
        services.TryAddScoped<WorkspacePreparationService>();
        services.TryAddScoped<WorkspaceLockAcquisitionService>();
        services.TryAddScoped<WorkspaceLockReleaseService>();
        services.TryAddScoped<IWorkspacePathPolicyEvidenceProvider, UnavailableWorkspacePathPolicyEvidenceProvider>();
        services.TryAddScoped<IWorkspaceFileContentStore, UnavailableWorkspaceFileContentStore>();
        services.TryAddScoped<IWorkspaceFileDeleteOperationStore, UnavailableWorkspaceFileDeleteOperationStore>();
        services.TryAddScoped<WorkspaceFileMutationService>();
        services.TryAddScoped<IWorkspaceCommitExecutor, UnavailableWorkspaceCommitExecutor>();
        services.TryAddScoped<WorkspaceCommitService>();
        services.TryAddScoped<IWorkspaceFileSensitivityClassifier, WorkspaceFileSensitivityClassifier>();
        services.TryAddScoped<IWorkspaceFileContextSource, UnavailableWorkspaceFileContextSource>();
        services.TryAddScoped<WorkspaceFileContextQueryHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainProcessor, FolderDomainProcessor>());
        services.TryAddScoped<FoldersDomainServiceRequestHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FoldersAuthSchemeValidator>());

        return services;
    }

    /// <summary>
    /// Registers the Story 10.5 authorized context-search facade (Option B). Adds the typed Memories search client
    /// (a direct base-address HttpClient; the production Dapr <c>folders -&gt; memories</c> invoke allow-rule governs the
    /// egress when the base address routes via the sidecar), binds its endpoint/token from configuration, and wires
    /// the live <see cref="MemoriesFolderSearchSource"/> as the <see cref="IFolderSearchSource"/>. The live gateway is
    /// registered before the core defaults so the core <c>TryAdd</c> keeps it; it degrades safely when Memories is
    /// unconfigured. The bridge read model stays the fail-safe <c>Unavailable</c> default until a Server-side
    /// EventStore-backed read model is wired on a DCP-capable lane (the live-boot residual inherited from Epic 9).
    /// </summary>
    public static IServiceCollection AddFoldersContextSearchFacade(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoriesClient();
        services.AddOptions<MemoriesClientOptions>().Configure<IConfiguration>(static (options, configuration) =>
        {
            if (Uri.TryCreate(configuration["Memories:BaseAddress"], UriKind.Absolute, out Uri? endpoint))
            {
                options.Endpoint = endpoint;
            }

            string? apiToken = configuration["HEXALITH_MEMORIES_API_TOKEN"];
            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                options.ApiToken = apiToken;
            }
        });

        // Register the live gateway before the core defaults so the core TryAddScoped keeps it. Scoped gateway over a
        // transient typed MemoriesClient — no captive dependency.
        services.AddScoped<IFolderSearchSource, MemoriesFolderSearchSource>();
        services.AddFoldersContextSearchQueries();

        return services;
    }
}
