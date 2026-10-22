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

internal sealed class WorkspaceContentProductionClock : TimeProvider { internal DateTimeOffset Now = new(2026, 5, 27, 8, 45, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
