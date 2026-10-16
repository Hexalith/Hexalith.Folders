using System.Security.Claims;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class VerifiedReceiptEventStoreAuthorizationValidatorTests
{
    [Fact]
    public async Task SignedMutationReceiptRequiresMatchingTenantSubjectAndAction()
    {
        string key = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());
        CommandEnvelope command = new("message-a", "tenant-a", "folders", "folder-a", "ArchiveFolder",
            Encoding.UTF8.GetBytes("{}"), "correlation-a", "message-a", "user-a", null);
        string signed = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "user-a",
            ["archive_folder"], key, DateTimeOffset.UtcNow);
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore")],
                "EventStoreWorkload")),
        };
        ConfigurationManager settings = new();
        settings["Authentication:HumanAuthorityReceipt:SigningKey"] = key;
        HumanAuthorityReceiptVerifier verifier = new(new HttpContextAccessor { HttpContext = context },
            settings, TimeProvider.System);
        verifier.TryEnter(new DomainServiceRequest(command, null) { HumanAuthorityReceipt = signed },
            out IDisposable? scope).ShouldBeTrue();
        using (scope)
        {
            VerifiedReceiptEventStoreAuthorizationValidator validator = new(new HttpContextAccessor
            {
                HttpContext = context,
            });
            LayeredFolderAuthorizationAllowedContext matching = new("tenant-a", "user-a", "archive_folder",
                "folder-a", null, null, null, [])
            { PrincipalId = "user-a" };
            (await validator.ValidateAsync(new(matching, "mutation"), TestContext.Current.CancellationToken))
                .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Allowed);
            foreach (LayeredFolderAuthorizationAllowedContext mismatch in new[]
            {
                matching with { AuthoritativeTenantId = "tenant-b" },
                matching with { PrincipalId = "user-b" },
                matching with { ActionToken = "manage_folder_access" },
            })
            {
                (await validator.ValidateAsync(new(mismatch, "mutation"), TestContext.Current.CancellationToken))
                    .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);
            }
        }
    }

    [Fact]
    public async Task DirectReadUsesValidatedBearerClaimsWhileMutationStillNeedsReceipt()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-a"), new Claim("eventstore:tenant", "tenant-a"),
                new Claim("eventstore:permission", "read_metadata")], "Bearer")),
        };
        VerifiedReceiptEventStoreAuthorizationValidator validator = new(new HttpContextAccessor
        {
            HttpContext = context,
        });
        LayeredFolderAuthorizationAllowedContext safe = new("tenant-a", "user-a", "read_metadata",
            "folder-a", null, null, null, [])
        { PrincipalId = "user-a" };

        (await validator.ValidateAsync(new(safe, "strict_read"), TestContext.Current.CancellationToken))
            .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Allowed);
        (await validator.ValidateAsync(new(safe, "mutation"), TestContext.Current.CancellationToken))
            .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-a"), new Claim("eventstore:tenant", "tenant-b"),
                new Claim("eventstore:permission", "read_metadata")], "Bearer"));
        (await validator.ValidateAsync(new(safe, "strict_read"), TestContext.Current.CancellationToken))
            .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);
    }
}
