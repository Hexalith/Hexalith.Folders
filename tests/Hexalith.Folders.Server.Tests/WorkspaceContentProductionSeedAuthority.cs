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

internal sealed class WorkspaceContentProductionSeedAuthority(bool accepted = true) : IContentAuthority
{
    public Task AuthorizeAsync(ClaimsPrincipal principal, ContentScope scope, ContentOperation operation, object resource, CancellationToken token) => Task.CompletedTask;
    public Task ValidateUploadAsync(ClaimsPrincipal principal, ContentUploadRequest request, ReadOnlyMemory<byte> bytes, CancellationToken token) => Task.CompletedTask;
    public Task ValidatePreparationAsync(ClaimsPrincipal principal, ContentPublicationRequest request, CancellationToken token) => Task.CompletedTask;
    public Task<ContentPublicationOutcome> GetPublicationOutcomeAsync(ContentPublicationRequest request, CancellationToken token) => Task.FromResult(accepted ? ContentPublicationOutcome.Accepted : ContentPublicationOutcome.Pending);
    public Task<ContentDisposition> GetDispositionAsync(ContentScope scope, ContentReceipt receipt, CancellationToken token) => Task.FromResult(new ContentDisposition(1, true, false, false, false));
    public Task<bool> TryAuthorizeErasureAsync(ContentScope scope, ContentReceipt receipt, long generation, CancellationToken token) => Task.FromResult(true);
}
