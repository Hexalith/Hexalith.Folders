using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal static class ForgejoOperationSourceBindings
{
    public static string ResolvedTarget(ProviderFileMutationRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-forgejo:v1:mutation-target", request.AuthorizationEvidence.Fingerprint, request.CorrelationId, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string ResolvedTarget(ProviderCommitRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-forgejo:v1:commit-target", request.AuthorizationEvidence.Fingerprint, request.CorrelationId, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string ResolvedTarget(ProviderOperationStatusRequest request, ProviderGitOperationResolvedTarget target)
        => Target("hxf-forgejo:v1:status-target", request.AuthorizationEvidence.Fingerprint, request.OperationReference, request.ManagedTenantId, request.OrganizationId, request.FolderId, request.DelegatedTaskId, request.ProviderBindingRef, request.CredentialReferenceId, request.RepositoryBindingId, target);

    public static string Path(ProviderFileMutationRequest request, ProviderOrderedFileChange declared, string path)
        => ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:path",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.ChangeSetReference,
            declared.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            declared.PathReference,
            path);

    public static string Content(ProviderFileMutationRequest request, ProviderOrderedFileChange declared, ReadOnlyMemory<byte> content)
        => ForgejoProviderSafeOperationEvidence.Compute("hxf-forgejo:v1:content", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.CorrelationId);
            writer.AppendString(request.ChangeSetReference);
            writer.AppendUInt32(checked((uint)declared.Sequence));
            writer.AppendString(declared.ContentReference);
            writer.AppendBytes(content.Span);
        });

    public static string ChangeSet(ProviderFileMutationRequest request, IReadOnlyList<ProviderResolvedFileChange> changes)
        => Changes("hxf-forgejo:v1:change-set", request.AuthorizationEvidence.Fingerprint, request.CorrelationId, request.ChangeSetReference, changes);

    public static string StagedChanges(
        ProviderCommitRequest request,
        string treeSha,
        IReadOnlyList<ProviderResolvedFileChange> changes)
        => ForgejoProviderSafeOperationEvidence.Compute("hxf-forgejo:v1:staged-changes", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.CorrelationId);
            writer.AppendString(request.StagedChangeSetReference);
            writer.AppendString(treeSha);
            AppendChanges(writer, changes);
        });

    public static string CommitMessage(ProviderCommitRequest request, string commitMessage)
        => ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:commit-message",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.CommitMessageReference,
            commitMessage);

    public static string ExpectedHead(ProviderCommitRequest request, string expectedHeadSha)
        => ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:expected-head",
            request.AuthorizationEvidence.Fingerprint,
            request.CorrelationId,
            request.StagedChangeSetReference,
            expectedHeadSha);

    public static string FullRef(ProviderOperationStatusRequest request, string fullRef)
        => ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:full-ref",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            fullRef);

    public static string ExpectedHead(ProviderOperationStatusRequest request, string expectedHeadSha)
        => ForgejoProviderSafeOperationEvidence.Create(
            "hxf-forgejo:v1:status-expected-head",
            request.AuthorizationEvidence.Fingerprint,
            request.OperationReference,
            expectedHeadSha);

    public static string IntendedCommit(
        ProviderOperationStatusRequest request,
        string? intendedCommitSha,
        IReadOnlyList<ProviderResolvedFileChange> changes,
        string commitMessage)
        => ForgejoProviderSafeOperationEvidence.Compute("hxf-forgejo:v1:intended-commit", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.OperationReference);
            writer.AppendString(intendedCommitSha);
            writer.AppendString(commitMessage);
            AppendChanges(writer, changes);
        });

    public static string CheckWindow(ProviderOperationStatusRequest request)
        => ForgejoProviderSafeOperationEvidence.Compute("hxf-forgejo:v1:status-window", writer =>
        {
            writer.AppendString(request.AuthorizationEvidence.Fingerprint);
            writer.AppendString(request.OperationReference);
            writer.AppendUInt32(checked((uint)request.CheckNumber));
            writer.AppendString(request.ReconciliationStartedAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        });

    private static string Changes(
        string domain,
        string authorizationFingerprint,
        string operationIdentity,
        string changeSetReference,
        IReadOnlyList<ProviderResolvedFileChange> changes)
        => ForgejoProviderSafeOperationEvidence.Compute(domain, writer =>
        {
            writer.AppendString(authorizationFingerprint);
            writer.AppendString(operationIdentity);
            writer.AppendString(changeSetReference);
            AppendChanges(writer, changes);
        });

    private static void AppendChanges(ForgejoCanonicalEvidenceWriter writer, IReadOnlyList<ProviderResolvedFileChange> changes)
    {
        writer.AppendCollectionCount(changes.Count);
        foreach (ProviderResolvedFileChange change in changes)
        {
            writer.AppendUInt32(checked((uint)change.Sequence));
            writer.AppendUInt32(checked((uint)change.Kind));
            writer.AppendString(change.Path);
            writer.AppendBytes(change.Content.Span);
            writer.AppendUInt32(checked((uint)change.ContentType));
            writer.AppendString(change.SourceObjectId);
        }
    }

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
        => ForgejoProviderSafeOperationEvidence.Create(
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
