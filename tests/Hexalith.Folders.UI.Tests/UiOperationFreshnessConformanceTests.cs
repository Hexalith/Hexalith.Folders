using System.Reflection;
using System.Runtime.Serialization;

using Bunit;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.UI.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.UI.Tests;

public sealed class UiOperationFreshnessConformanceTests
{
    [Theory]
    [InlineData("folder-detail")]
    [InlineData("workspace")]
    [InlineData("provider")]
    [InlineData("audit-trail")]
    [InlineData("operation-timeline")]
    [InlineData("incident-stream")]
    [InlineData("provider-support")]
    [InlineData("indexing-status")]
    public void EveryPageSendsEachOperationsAcceptedFreshness(string page)
    {
        (BunitContext ctx, IClient client, _) = DiagnosticTestContext.Create();
        using BunitContext _ctx = ctx;
        switch (page)
        {
            case "folder-detail": ConsoleSweepFixtures.StubFolderDetail(client); break;
            case "workspace": ConsoleSweepFixtures.StubWorkspace(client); break;
            case "provider": ConsoleSweepFixtures.StubProvider(client); break;
            case "audit-trail": ConsoleSweepFixtures.StubAuditTrail(client); break;
            case "operation-timeline": ConsoleSweepFixtures.StubOperationTimeline(client); break;
            case "incident-stream": ConsoleSweepFixtures.StubIncidentStream(client); break;
            case "provider-support": ConsoleSweepFixtures.StubProviderSupport(client); break;
        }

        client.ClearReceivedCalls();
        switch (page)
        {
            case "folder-detail":
                _ = ctx.Render<FolderDetail>(parameters => parameters.Add(component => component.FolderId, ConsoleSweepFixtures.FolderId));
                break;
            case "workspace":
                _ = ctx.Render<Workspace>(parameters => parameters
                    .Add(component => component.FolderId, ConsoleSweepFixtures.FolderId)
                    .Add(component => component.WorkspaceId, ConsoleSweepFixtures.WorkspaceId));
                break;
            case "provider":
                _ = ctx.Render<Provider>(parameters => parameters.Add(component => component.FolderId, ConsoleSweepFixtures.FolderId));
                break;
            case "audit-trail":
                _ = ctx.Render<AuditTrail>(parameters => parameters.Add(component => component.FolderId, ConsoleSweepFixtures.FolderId));
                break;
            case "operation-timeline":
                _ = ctx.Render<OperationTimeline>(parameters => parameters.Add(component => component.FolderId, ConsoleSweepFixtures.FolderId));
                break;
            case "incident-stream":
                ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(ConsoleSweepFixtures.IncidentStreamRoute);
                _ = ctx.Render<IncidentStream>();
                break;
            case "provider-support":
                _ = ctx.Render<ProviderSupport>();
                break;
            case "indexing-status":
                _ = ctx.Render<IndexingStatus>(parameters => parameters.Add(component => component.FolderId, ConsoleSweepFixtures.FolderId));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(page));
        }

        var reads = client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().GetParameters().Any(parameter => parameter.ParameterType == typeof(ReadConsistencyClass?)))
            .ToArray();
        reads.ShouldNotBeEmpty($"{page} must exercise its SDK read path");
        foreach (var call in reads)
        {
            MethodInfo method = call.GetMethodInfo();
            string operationId = method.Name[..^"Async".Length];
            string expected = HexalithFoldersGeneratedOperationCatalog.AcceptedFreshness(operationId)
                .ShouldNotBeNull($"{operationId} must declare an accepted freshness");
            int index = Array.FindIndex(method.GetParameters(), parameter => parameter.ParameterType == typeof(ReadConsistencyClass?));
            ReadConsistencyClass actual = ((ReadConsistencyClass?)call.GetArguments()[index]).ShouldNotBeNull();
            string wire = typeof(ReadConsistencyClass).GetMember(actual.ToString()).Single()
                .GetCustomAttribute<EnumMemberAttribute>()?.Value ?? string.Empty;
            wire.ShouldBe(expected, $"{page} sent an unsupported freshness for {operationId}");
        }
    }
}
