using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Server.Content;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Shouldly;
using Xunit;
using PathMetadata = Hexalith.Folders.Aggregates.Folder.PathMetadata;

namespace Hexalith.Folders.Server.Tests;

internal sealed class WorkspaceContentProductionPermissionProvider : IFolderPermissionEvidenceProvider
{
    internal bool Allowed = true;
    /// <summary>Allows loss of current permission evidence during a protected read.</summary>
    internal string? Watermark = "permission_watermark_v1";
    public Task<FolderPermissionEvidenceResult> GetEvidenceAsync(FolderPermissionEvidenceRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Allowed ? FolderPermissionEvidenceResult.Allowed(Watermark, organizationId: "tenant-a") : FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Denied, null));
}
