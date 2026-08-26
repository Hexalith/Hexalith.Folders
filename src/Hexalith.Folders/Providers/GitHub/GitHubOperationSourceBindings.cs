using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal static class GitHubOperationSourceBindings
{
    public static string ResolvedTarget(ProviderFileMutationRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-github:v1:mutation-target", request.AuthorizationEvidence.Fingerprint, request.CorrelationId, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string ResolvedTarget(ProviderCommitRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-github:v1:commit-target", request.AuthorizationEvidence.Fingerprint, request.CorrelationId, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string ResolvedTarget(ProviderOperationStatusRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-github:v1:status-target", request.AuthorizationEvidence.Fingerprint, request.OperationReference, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string Path(ProviderFileMutationRequest request, ProviderOrderedFileChange declared, string path)
        => GitHubProviderSafeOperationEvidence.Compute("hxf-github:v1:path", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.CorrelationId);
            writer.AppendString(request.ChangeSetReference);
            writer.AppendUInt32(checked((uint)declared.Sequence));
            writer.AppendString(declared.PathReference);
            writer.AppendString(path);
        });

    public static string Content(ProviderFileMutationRequest request, ProviderOrderedFileChange declared, ReadOnlyMemory<byte> content)
        => GitHubProviderSafeOperationEvidence.Compute("hxf-github:v1:content", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.CorrelationId);
            writer.AppendString(request.ChangeSetReference);
            writer.AppendUInt32(checked((uint)declared.Sequence));
            writer.AppendString(declared.ContentReference);
            writer.AppendBytes(content.Span);
        });

    public static string ChangeSet(ProviderFileMutationRequest request, IReadOnlyList<ProviderResolvedFileChange> changes)
        => GitHubProviderSafeOperationEvidence.Compute("hxf-github:v1:change-set", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.CorrelationId);
            writer.AppendString(request.ChangeSetReference);
            writer.AppendCollectionCount(changes.Count);
            foreach (ProviderResolvedFileChange change in changes)
            {
                writer.AppendUInt32(checked((uint)change.Sequence));
                writer.AppendUInt32(checked((uint)change.Kind));
                writer.AppendString(change.Path);
                writer.AppendBytes(change.Content.Span);
                writer.AppendUInt32(checked((uint)change.ContentType));
            }
        });

    public static string StagedTree(ProviderCommitRequest request, string treeSha)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:staged-tree",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.StagedChangeSetReference,
            treeSha);

    public static string CommitMessage(ProviderCommitRequest request, string commitMessage)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:commit-message",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.CommitMessageReference,
            commitMessage);

    public static string ExpectedHead(ProviderCommitRequest request, string expectedHeadSha)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:expected-head",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.StagedChangeSetReference,
            expectedHeadSha);

    public static string FullRef(ProviderOperationStatusRequest request, string fullRef)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:full-ref",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            fullRef);

    public static string ExpectedHead(ProviderOperationStatusRequest request, string expectedHeadSha)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:status-expected-head",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            expectedHeadSha);

    public static string IntendedCommit(ProviderOperationStatusRequest request, string intendedCommitSha)
        => GitHubProviderSafeOperationEvidence.Create(
            "hxf-github:v1:intended-commit",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            intendedCommitSha);

    public static string CheckWindow(ProviderOperationStatusRequest request)
        => GitHubProviderSafeOperationEvidence.Compute("hxf-github:v1:status-window", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.OperationReference);
            writer.AppendUInt32(checked((uint)request.CheckNumber));
            writer.AppendString(request.ReconciliationStartedAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        });

    private static string Target(
        string domain,
        string authorizationFingerprint,
        string operationIdentity,
        string managedTenantId,
        string organizationId,
        string folderId,
        string delegatedTaskId,
        string providerBindingRef,
        string credentialReferenceId,
        string repositoryBindingId,
        ProviderGitOperationResolvedTarget target)
        => GitHubProviderSafeOperationEvidence.Create(
            domain,
            authorizationFingerprint,
            operationIdentity,
            managedTenantId,
            organizationId,
            folderId,
            delegatedTaskId,
            providerBindingRef,
            credentialReferenceId,
            repositoryBindingId,
            target.Owner,
            target.RepositoryName,
            target.FullRef,
            target.ExpectedHeadSha);
}
