using System.Security.Claims;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class HumanAuthorityReceiptVerifierTests
{
    private static readonly string SigningKey = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());

    [Fact]
    public void VerifiedWorkloadAndBoundReceiptProduceScopedHumanClaims()
    {
        CommandEnvelope command = Command();
        string receipt = HumanAuthorityReceiptCodec.Issue(
            command, "tenant-a", "human-a", ["archive_folder"], SigningKey, DateTimeOffset.UtcNow);
        DefaultHttpContext context = WorkloadContext();
        ClaimsPrincipal workload = context.User;
        HumanAuthorityReceiptVerifier verifier = CreateVerifier(context);

        verifier.TryEnter(new DomainServiceRequest(command, null) { HumanAuthorityReceipt = receipt },
            out IDisposable? scope).ShouldBeTrue();
        context.User.FindFirstValue("eventstore:tenant").ShouldBe("tenant-a");
        context.User.FindFirstValue("sub").ShouldBe("human-a");
        context.User.FindFirstValue("eventstore:permission").ShouldBe("archive_folder");
        scope.ShouldNotBeNull();
        scope.Dispose();
        context.User.ShouldBeSameAs(workload);
        context.Items.ShouldNotContainKey(HumanAuthorityReceiptVerifier.VerifiedClaimsItemKey);
    }

    [Fact]
    public void MissingOrMismatchedProofNeverEstablishesHumanAuthority()
    {
        CommandEnvelope command = Command();
        string receipt = HumanAuthorityReceiptCodec.Issue(
            command, "tenant-a", "human-a", ["archive_folder"], SigningKey, DateTimeOffset.UtcNow);
        DefaultHttpContext context = WorkloadContext();
        HumanAuthorityReceiptVerifier verifier = CreateVerifier(context);

        verifier.TryEnter(new DomainServiceRequest(command, null), out _).ShouldBeFalse();
        verifier.TryEnter(new DomainServiceRequest(command with { AggregateId = "other-folder" }, null)
            { HumanAuthorityReceipt = receipt }, out _).ShouldBeFalse();
        context.User.FindFirstValue("sub").ShouldBeNull();
        context.Items.ShouldNotContainKey(HumanAuthorityReceiptVerifier.VerifiedClaimsItemKey);

        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        verifier.TryEnter(new DomainServiceRequest(command, null) { HumanAuthorityReceipt = receipt }, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task MissingReceiptDeniesBeforeTheDomainProcessorOrRepositoryCanRun()
    {
        DefaultHttpContext context = WorkloadContext();
        FoldersDomainServiceRequestHandler handler = new(
            [], null!, null!, null!, null!, null!, CreateVerifier(context));

        IResult result = await handler.ProcessAsync(
            new DomainServiceRequest(Command(), null), TestContext.Current.CancellationToken);

        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        await result.ExecuteAsync(context);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private static HumanAuthorityReceiptVerifier CreateVerifier(DefaultHttpContext context)
    {
        ConfigurationManager configuration = new();
        configuration["Authentication:HumanAuthorityReceipt:SigningKey"] = SigningKey;
        return new HumanAuthorityReceiptVerifier(
            new HttpContextAccessor { HttpContext = context }, configuration, TimeProvider.System);
    }

    private static DefaultHttpContext WorkloadContext()
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore")],
                "EventStoreWorkload")),
        };

    private static CommandEnvelope Command()
        => new("message-a", "tenant-a", "folders", "folder-a", "ArchiveFolder",
            Encoding.UTF8.GetBytes("{}"), "correlation-a", "message-a", "human-a", null);
}
