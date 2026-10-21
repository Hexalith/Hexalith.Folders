using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>Folds only explicit native owner mutation identity. Delete/add and undeclared path changes never imply rename continuity.</summary>
public static class FolderFileIdentityState
{
    /// <summary>The existing isolated read-model bound, including tombstones; writes never silently drop history.</summary>
    internal const int MaximumIdentities = 10000;

    /// <summary>Checks an explicit binding without reinterpreting legacy paths, operation IDs or hashes as File identities.</summary>
    public static bool IsCanonical(string? value) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim() && value.Length <= 128 && value.All(character => character is >= '!' and <= '~');

    /// <summary>Preserves the Projects-owned reference namespace, including Unicode letters and digits.</summary>
    public static bool IsProjectReference(string? value) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim() && value.Length <= 128 && char.IsLetterOrDigit(value[0])
        && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');

    /// <summary>Applies a native mutation and invalidates every prior incarnation that the atomic target replaces.</summary>
    public static IReadOnlyDictionary<string, FolderFileIdentity> Apply(
        IReadOnlyDictionary<string, FolderFileIdentity> current, WorkspaceFileMutationAccepted mutation)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(mutation);
        if (mutation.ManagedTenantId != mutation.OrganizationId || string.IsNullOrWhiteSpace(mutation.PathMetadataDigest))
            throw new InvalidOperationException("Invalid File owner scope.");
        bool identified = mutation.OwnerFileId is not null || mutation.ProjectFileReferenceId is not null;
        if (identified && (!IsCanonical(mutation.OwnerFileId)
            || mutation.ProjectFileReferenceId is not null && !IsProjectReference(mutation.ProjectFileReferenceId)))
            throw new InvalidOperationException("Invalid File owner identity.");
        var next = new Dictionary<string, FolderFileIdentity>(current, StringComparer.Ordinal);
        if (mutation.FileOperationKind == "add" && identified
            && (next.ContainsKey(mutation.OwnerFileId!) || next.Count >= MaximumIdentities))
            throw new InvalidOperationException("File owner identity cannot be reused or exceed its retained-history bound.");
        if (mutation.FileOperationKind == "add" && identified && mutation.ProjectFileReferenceId is not null
            && next.Values.Any(item => item.ProjectFileReferenceId == mutation.ProjectFileReferenceId))
            throw new InvalidOperationException("Projects File reference is already bound.");
        bool exactTarget = mutation.MutationTargetDigest is { Length: 64 } target
            && target.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!exactTarget)
        {
            // A historic event has no exact native target. Its descriptive digest cannot establish
            // continuity; conservatively invalidate retained facts rather than guessing a path.
            foreach (var item in current.Values.Where(item => item.Active)) next[item.OwnerFileId] = item with { Active = false };
            if (identified && mutation.FileOperationKind == "add")
                next.Add(mutation.OwnerFileId!, new(mutation.OwnerFileId!, mutation.ProjectFileReferenceId,
                    mutation.PathMetadataDigest, false)
                { NativeMutationId = mutation.IdempotencyKey });
            return next.ToFrozenDictionary(StringComparer.Ordinal);
        }
        if (identified && mutation.FileOperationKind != "add")
        {
            if (!next.TryGetValue(mutation.OwnerFileId!, out var prior) || !prior.Active
                || prior.MutationTargetDigest != mutation.MutationTargetDigest
                || prior.ProjectFileReferenceId is not null && prior.ProjectFileReferenceId != mutation.ProjectFileReferenceId)
                throw new InvalidOperationException("File owner binding does not match the native mutation.");
        }
        foreach (var item in current.Values.Where(item => item.Active
            && (item.MutationTargetDigest is null || item.MutationTargetDigest == mutation.MutationTargetDigest)))
        {
            if (mutation.FileOperationKind == "change" && identified && item.OwnerFileId == mutation.OwnerFileId) continue;
            next[item.OwnerFileId] = item with { Active = false };
        }
        if (mutation.FileOperationKind == "add" && identified)
        {
            if (mutation.ProjectFileReferenceId is not null && next.Values.Any(item => item.ProjectFileReferenceId == mutation.ProjectFileReferenceId))
                throw new InvalidOperationException("Projects File reference is already bound.");
            next.Add(mutation.OwnerFileId!, new(mutation.OwnerFileId!, mutation.ProjectFileReferenceId,
                mutation.PathMetadataDigest, true)
            {
                NativeMutationId = mutation.IdempotencyKey,
                MutationTargetDigest = mutation.MutationTargetDigest
            });
        }
        if (identified && mutation.FileOperationKind == "change")
        {
            if (mutation.ProjectFileReferenceId is not null && next.Values.Any(item => item.OwnerFileId != mutation.OwnerFileId && item.ProjectFileReferenceId == mutation.ProjectFileReferenceId))
                throw new InvalidOperationException("Projects File reference is already bound.");
            next[mutation.OwnerFileId!] = next[mutation.OwnerFileId!] with
            {
                ProjectFileReferenceId = mutation.ProjectFileReferenceId,
                PathMetadataDigest = mutation.PathMetadataDigest,
                NativeMutationId = mutation.IdempotencyKey
            };
        }
        return next.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
