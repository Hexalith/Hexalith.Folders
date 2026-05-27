using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Authentication;
using Hexalith.Folders.Server.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Folders.Server;

public static class FoldersServerServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersDomainServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventStore(typeof(FoldersModule).Assembly);
        services.AddEventStoreGatewayClient();
        services.AddHttpContextAccessor();
        services.AddOptions<TenantContextOptions>();
        // Register the authentication core services so the validator can introspect scheme registrations.
        // Concrete schemes (JWT bearer, OIDC) are added by the host composition (Story 7.2).
        _ = services.AddAuthentication();
        services.TryAddSingleton<ITenantContextAccessor, HttpContextTenantContextAccessor>();
        services.TryAddSingleton<IEventStoreClaimTransformEvidenceAccessor, HttpContextEventStoreClaimTransformEvidenceAccessor>();
        services.TryAddSingleton<IFolderCommandActionTokenMapper, FolderCommandActionTokenMapper>();
        services.TryAddScoped<ILayeredFolderAuthorizationResultAccessor, ScopedLayeredFolderAuthorizationResultAccessor>();
        services.TryAddScoped<IFolderArchiveAclEvidenceProvider, LayeredAuthBackedFolderArchiveAclEvidenceProvider>();
        services.TryAddScoped<IFolderArchivePolicyEvidenceProvider, BaselineFolderArchivePolicyEvidenceProvider>();
        services.TryAddScoped<IRepositoryCreationReadinessValidator, ProviderReadinessRepositoryCreationValidator>();
        services.TryAddScoped<IRepositoryBindingReadinessValidator, ProviderReadinessRepositoryBindingValidator>();
        services.TryAddScoped<IBranchRefPolicyReadinessValidator, ProviderReadinessBranchRefPolicyValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<FolderArchiveTenantGate>();
        services.TryAddScoped<FolderAccessTenantGate>();
        services.TryAddScoped<RepositoryBackedFolderCreationService>();
        services.TryAddScoped<RepositoryBindingService>();
        services.TryAddScoped<BranchRefPolicyConfigurationService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDomainProcessor, FolderDomainProcessor>());
        services.TryAddScoped<FoldersDomainServiceRequestHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FoldersAuthSchemeValidator>());

        return services;
    }
}
