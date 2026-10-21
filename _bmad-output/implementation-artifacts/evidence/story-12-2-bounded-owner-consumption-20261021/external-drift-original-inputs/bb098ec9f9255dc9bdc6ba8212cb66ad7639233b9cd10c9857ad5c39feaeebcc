// <copyright file="ProjectsServerModule.cs" company="Hexalith">
// Copyright (c) Hexalith. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Projects.Server;

/// <summary>
/// Server host anchor and shared constants for Hexalith.Projects (Story 1.4 tracer bullet). The full
/// <c>TenantAccessProjection</c> / layered claim-transform authorization chain is Story 1.6; this
/// story wires a minimal tenant-context guard sufficient to prove fail-closed-on-missing-tenant + the
/// command-async 202 + safe-denial 404 mapping. Only <c>Server</c> takes Dapr/EventStore-runtime
/// dependencies; the domain core stays pure.
/// </summary>
public static class ProjectsServerModule
{
    /// <summary>Gets the module name used in server diagnostics and registration.</summary>
    public static string Name => "Hexalith.Projects.Server";

    /// <summary>The canonical EventStore domain segment for project aggregates.</summary>
    public const string DomainName = "projects";

    /// <summary>The fully qualified <c>CreateProject</c> command type discriminator on the wire.</summary>
    public const string CreateProjectCommandType = "Hexalith.Projects.Commands.CreateProject";

    /// <summary>The fully qualified <c>UpdateProjectSetup</c> command type discriminator on the wire.</summary>
    public const string UpdateProjectSetupCommandType = "Hexalith.Projects.Commands.UpdateProjectSetup";

    /// <summary>The fully qualified <c>ArchiveProject</c> command type discriminator on the wire.</summary>
    public const string ArchiveProjectCommandType = "Hexalith.Projects.Commands.ArchiveProject";

    /// <summary>The fully qualified <c>RestoreProject</c> command type discriminator on the wire.</summary>
    public const string RestoreProjectCommandType = "Hexalith.Projects.Commands.RestoreProject";

    /// <summary>The supported Conversation-start setup query type.</summary>
    public const string GetConversationStartSetupQueryType = "Hexalith.Projects.Queries.GetConversationStartSetup.v1";

    /// <summary>The supported Get Project Context query type.</summary>
    public const string GetProjectContextQueryType = "Hexalith.Projects.Queries.GetProjectContext.v1";

    /// <summary>The supported Refresh Project Context query type.</summary>
    public const string RefreshProjectContextQueryType = "Hexalith.Projects.Queries.RefreshProjectContext.v1";

    /// <summary>The supported Explain Context Selection query type.</summary>
    public const string ExplainContextSelectionQueryType = "Hexalith.Projects.Queries.ExplainContextSelection.v1";

    /// <summary>The fully qualified <c>SetProjectFolder</c> command type discriminator on the wire.</summary>
    public const string SetProjectFolderCommandType = "Hexalith.Projects.Commands.SetProjectFolder";

    /// <summary>The fully qualified <c>LinkFileReference</c> command type discriminator on the wire.</summary>
    public const string LinkFileReferenceCommandType = "Hexalith.Projects.Commands.LinkFileReference";

    /// <summary>The fully qualified <c>UnlinkFileReference</c> command type discriminator on the wire.</summary>
    public const string UnlinkFileReferenceCommandType = "Hexalith.Projects.Commands.UnlinkFileReference";

    /// <summary>The fully qualified <c>LinkMemory</c> command type discriminator on the wire.</summary>
    public const string LinkMemoryCommandType = "Hexalith.Projects.Commands.LinkMemory";

    /// <summary>The fully qualified <c>UnlinkMemory</c> command type discriminator on the wire.</summary>
    public const string UnlinkMemoryCommandType = "Hexalith.Projects.Commands.UnlinkMemory";

    /// <summary>The fully qualified <c>ConfirmProjectResolution</c> command type discriminator on the wire.</summary>
    public const string ConfirmProjectResolutionCommandType = "Hexalith.Projects.Commands.ConfirmProjectResolution";

    /// <summary>The exact protected project-grant issuance discriminator.</summary>
    public const string GrantProjectChatBotAuthorityCommandType = "Hexalith.Projects.Contracts.Commands.GrantProjectChatBotAuthority";

    /// <summary>The exact protected project-grant revocation discriminator.</summary>
    public const string RevokeProjectChatBotAuthorityCommandType = "Hexalith.Projects.Contracts.Commands.RevokeProjectChatBotAuthority";

    /// <summary>The aggregate-callback route the EventStore command pipeline invokes.</summary>
    public const string ProcessRoute = "/process";

    /// <summary>The full-replay projection callback route the EventStore projection pipeline invokes.</summary>
    public const string ProjectRoute = "/project";

    /// <summary>The stable EventStore projection type for the Project aggregate state.</summary>
    public const string ProjectionType = "project";

    /// <summary>The reserved platform tenant; never a valid project-data tenant context.</summary>
    public const string ReservedSystemTenant = "system";

    /// <summary>Maximum length for canonical envelope identifiers (idempotency key, correlation id, task id, project id).</summary>
    public const int MaxCanonicalIdentifierLength = 128;
}
