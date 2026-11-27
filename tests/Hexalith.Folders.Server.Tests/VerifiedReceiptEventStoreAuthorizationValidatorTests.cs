using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class VerifiedReceiptEventStoreAuthorizationValidatorTests
{
    [Theory]
    [InlineData("POST", "/api/v1/folders", true)]
    [InlineData("POST", "/api/v1/folders/folder-a/workspaces/workspace-a/native/task-lease", false)]
    [InlineData("POST", "/process", false)]
    [InlineData("POST", "/api/v1/unknown", false)]
    [InlineData("GET", "/api/v1/folders", false)]
    [InlineData("POST", "/api/v2/folders", false)]
    public async Task PublicMutationPreflightRequiresKnownRouteAndBoundHuman(string method, string path, bool allowed)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-a"), new Claim("eventstore:tenant", "tenant-a"),
                new Claim("eventstore:permission", "create_folder")], "Bearer"));
        VerifiedReceiptEventStoreAuthorizationValidator validator = new(new HttpContextAccessor { HttpContext = context });
        LayeredFolderAuthorizationAllowedContext safe = new("tenant-a", "user-a", "create_folder", null,
            null, null, null, [])
        { PrincipalId = "user-a" };
        (await validator.ValidateAsync(new(safe, "mutation"), TestContext.Current.CancellationToken))
            .Status.ShouldBe(allowed ? EventStoreAuthorizationValidationStatus.Allowed : EventStoreAuthorizationValidationStatus.Denied);
        foreach (LayeredFolderAuthorizationAllowedContext wrong in new[]
        {
            safe with { AuthoritativeTenantId = "tenant-b" }, safe with { PrincipalId = "user-b" },
            safe with { ActionToken = "manage_folder_access" },
        })
        {
            (await validator.ValidateAsync(new(wrong, "mutation"), TestContext.Current.CancellationToken))
                .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(context.User.Claims));
        (await validator.ValidateAsync(new(safe, "mutation"), TestContext.Current.CancellationToken))
            .Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);
    }

    [Theory]
    [InlineData("mutate_files")]
    [InlineData("lock_workspace")]
    public async Task NativeMutationPreflightUsesTheCurrentHumanPermission(string action)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-a"),
            new Claim("eventstore:tenant", "tenant-a"), new Claim("eventstore:permission", action)], "Bearer"))
        };
        context.Request.Method = "POST"; context.Request.Path = "/api/v1/folders/folder-a/workspaces/workspace-a/native/task-lease";
        var validator = new VerifiedReceiptEventStoreAuthorizationValidator(new HttpContextAccessor { HttpContext = context });
        var allowed = new LayeredFolderAuthorizationAllowedContext("tenant-a", "user-a", action, "folder-a", null, "task-a", null, []) { PrincipalId = "user-a" };
        (await validator.ValidateAsync(new(allowed, "mutation"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Status.ShouldBe(EventStoreAuthorizationValidationStatus.Allowed);
        ((ClaimsIdentity)context.User.Identity!).RemoveClaim(context.User.FindFirst("eventstore:permission")!);
        (await validator.ValidateAsync(new(allowed, "mutation"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Status.ShouldBe(EventStoreAuthorizationValidationStatus.Denied);
    }
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

    /// <summary>Validates the dispatched caller proof with each public delegation marker and the internal owner origin.</summary>
    [Theory]
    [InlineData("authenticated-caller", false, null, true)]
    [InlineData("authenticated-caller", true, null, false)]
    [InlineData("authenticated-caller", false, "delegate-a", false)]
    [InlineData("delegated-owner-query", true, "delegate-a", true)]
    public async Task VerifiedQueryAuthorityRequiresUndelegatedPublicCaller(string origin, bool isDelegated, string? delegationId, bool expected)
    {
        const string action = "read_metadata";
        DefaultHttpContext context = new();
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = context };
        VerifiedReceiptEventStoreAuthorizationValidator validator = new(accessor);
        LayeredFolderAuthorizationAllowedContext safe = new("tenant-a", "user-a", action, "folder-a", null, null, null, [])
        {
            PrincipalId = "user-a",
        };
        CallbackQueryResultHandler handler = new(async (query, token) =>
        {
            VerifiedQueryCaller.Get(context).ShouldNotBeNull().Query.ShouldBeSameAs(query);
            EventStoreAuthorizationValidationResult result = await validator.ValidateAsync(new(safe, "strict_read"), token).ConfigureAwait(true);
            return QueryResult.FromPayload(
                JsonSerializer.SerializeToElement(result.Status == EventStoreAuthorizationValidationStatus.Allowed), "validation-probe");
        });
        QueryEnvelope envelope = new("tenant-a", "folders", "folder-a", handler.QueryType, "{}"u8.ToArray(), "correlation-a", "user-a")
        {
            OriginalActorId = "user-a",
            CallerAuthority = new(origin, [], [action]),
            IsDelegated = isDelegated,
            DelegationId = delegationId,
        };
        // Supply the minimal authenticated workload identity and exact digest consumed by the SDK dispatcher.
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery),
            .. QueryCallerAuthorityBinding.Bind(envelope).Select(binding => new Claim(binding.Key, binding.Value)),
        ], "EventStoreWorkload"));
        using ServiceProvider services = new ServiceCollection().AddSingleton(accessor)
            .AddSingleton<IDomainQueryHandler>(handler).BuildServiceProvider();

        QueryResult response = await DomainQueryDispatcher.ExecuteAsync(services, envelope, TestContext.Current.CancellationToken);

        response.Success.ShouldBeTrue();
        response.GetPayload().GetBoolean().ShouldBe(expected);
        handler.Calls.ShouldBe(1);
    }
}
