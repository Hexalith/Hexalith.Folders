// <copyright file="ProjectPersistedDetailValidator.cs" company="Hexalith">
// Copyright (c) Hexalith. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Projects.Aggregates.Project;

using Hexalith.Projects.Contracts.Models;
using Hexalith.Projects.Contracts.Ui;
using Hexalith.Projects.Projections.ProjectDetail;

/// <summary>Validates persisted Project detail before it can cross a supported read boundary.</summary>
public static class ProjectPersistedDetailValidator
{
    /// <summary>Returns whether a persisted detail is structurally coherent and metadata-safe.</summary>
    /// <param name="detail">The persisted Project detail.</param>
    /// <returns><see langword="true"/> when the detail satisfies the canonical write-boundary rules.</returns>
    public static bool IsValid(ProjectDetailItem detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        if (!IsHeaderValid(detail))
        {
            return false;
        }

        string? folderId = detail.ProjectFolder?.FolderId;
        foreach (ProjectFileReference? file in detail.FileReferences)
        {
            if (file is null
                || !ProjectCommandValidator.IsSafePersistedReferenceIdentifier(file.FileReferenceId)
                || !ProjectCommandValidator.IsSafePersistedReferenceIdentifier(file.FolderId)
                || !string.Equals(file.FolderId, folderId, StringComparison.Ordinal)
                || !ProjectCommandValidator.IsSafePersistedMetadata(file.DisplayName, ProjectCommandValidator.MaxNameLength)
                || !ProjectCommandValidator.IsSafePersistedMetadata(file.ReasonCode, ProjectCommandValidator.MaxNameLength)
                || !Enum.IsDefined(file.ReferenceState)
                || !IsWithinProjectLifetime(file.ObservedAt, detail.CreatedAt, detail.UpdatedAt))
            {
                return false;
            }
        }

        foreach (ProjectMemoryReference? memory in detail.MemoryReferences)
        {
            if (memory is null
                || !ProjectCommandValidator.IsSafePersistedReferenceIdentifier(memory.MemoryReferenceId)
                || !ProjectCommandValidator.IsSafePersistedMetadata(memory.DisplayName, ProjectCommandValidator.MaxNameLength)
                || !ProjectCommandValidator.IsSafePersistedMetadata(memory.ReasonCode, ProjectCommandValidator.MaxNameLength)
                || !Enum.IsDefined(memory.ReferenceState)
                || !IsWithinProjectLifetime(memory.ObservedAt, detail.CreatedAt, detail.UpdatedAt))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns whether bounded Project, Folder, and Setup header evidence is coherent without walking
    /// the reference collections.
    /// </summary>
    /// <param name="detail">The persisted Project detail.</param>
    /// <returns><see langword="true"/> when the bounded header satisfies the supported read rules.</returns>
    public static bool IsHeaderValid(ProjectDetailItem detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return ProjectCommandValidator.IsSafePersistedEnvelopeIdentifier(detail.TenantId)
            && ProjectCommandValidator.IsSafePersistedEnvelopeIdentifier(detail.ProjectId)
            && !string.IsNullOrWhiteSpace(detail.Name)
            && detail.Name.Length <= ProjectCommandValidator.MaxNameLength
            && ProjectCommandValidator.IsSafePersistedMetadata(detail.Name, ProjectCommandValidator.MaxNameLength)
            && ProjectCommandValidator.IsSafePersistedMetadata(detail.Description, ProjectCommandValidator.MaxDescriptionLength)
            && ProjectCommandValidator.IsSafePersistedMetadata(detail.SetupMetadata, ProjectCommandValidator.MaxSetupMetadataLength)
            && detail.Sequence > 0
            && Enum.IsDefined(detail.Lifecycle)
            && detail.CreatedAt != default
            && detail.UpdatedAt >= detail.CreatedAt
            && detail.FileReferences is not null
            && detail.MemoryReferences is not null
            && (detail.Setup is null || ProjectCommandValidator.IsValidPersistedSetup(detail.Setup))
            && (detail.ProjectFolder is null || IsValidFolder(detail.ProjectFolder, detail.CreatedAt, detail.UpdatedAt));
    }

    private static bool IsValidFolder(
        ProjectFolderReference folder,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        => Enum.IsDefined(folder.ReferenceState)
            && IsWithinProjectLifetime(folder.ObservedAt, createdAt, updatedAt)
            && ProjectCommandValidator.IsSafePersistedMetadata(folder.DisplayName, ProjectCommandValidator.MaxNameLength)
            && ProjectCommandValidator.IsSafePersistedMetadata(folder.ReasonCode, ProjectCommandValidator.MaxNameLength)
            && (folder.FolderId is null || ProjectCommandValidator.IsSafePersistedReferenceIdentifier(folder.FolderId))
            && (folder.ReferenceState != ReferenceState.Included
                || ProjectCommandValidator.IsSafePersistedReferenceIdentifier(folder.FolderId));

    private static bool IsWithinProjectLifetime(
        DateTimeOffset observedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        => observedAt >= createdAt && observedAt <= updatedAt;
}
