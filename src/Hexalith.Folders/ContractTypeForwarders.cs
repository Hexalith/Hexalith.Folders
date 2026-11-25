using System.Runtime.CompilerServices;

using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Routing;

// Keep the published domain package's existing type identities after moving the contracts.
[assembly: TypeForwardedTo(typeof(FoldersCommandCatalog))]
[assembly: TypeForwardedTo(typeof(GrantOrganizationAclPrincipal))]
[assembly: TypeForwardedTo(typeof(IOrganizationAclCommand))]
[assembly: TypeForwardedTo(typeof(InitializeOrganizationAclBaseline))]
[assembly: TypeForwardedTo(typeof(OrganizationAclAction))]
[assembly: TypeForwardedTo(typeof(OrganizationAclCommandValidationResult))]
[assembly: TypeForwardedTo(typeof(OrganizationAclCommandValidator))]
[assembly: TypeForwardedTo(typeof(OrganizationAclEntryKey))]
[assembly: TypeForwardedTo(typeof(OrganizationAclOperation))]
[assembly: TypeForwardedTo(typeof(OrganizationAclOperationIntent))]
[assembly: TypeForwardedTo(typeof(OrganizationAclPrincipalKind))]
[assembly: TypeForwardedTo(typeof(OrganizationAclPrincipalKindParser))]
[assembly: TypeForwardedTo(typeof(OrganizationAclResultCode))]
[assembly: TypeForwardedTo(typeof(OrganizationProviderBindingSecretDetector))]
[assembly: TypeForwardedTo(typeof(OrganizationStreamName))]
[assembly: TypeForwardedTo(typeof(ProviderBindingActorId))]
[assembly: TypeForwardedTo(typeof(RevokeOrganizationAclPrincipal))]
