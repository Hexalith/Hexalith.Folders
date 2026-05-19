namespace Hexalith.Folders.Authorization;

public sealed record EventStoreAuthorizationValidationRequest(
    LayeredFolderAuthorizationAllowedContext SafeContext,
    string OperationPolicyClass);
