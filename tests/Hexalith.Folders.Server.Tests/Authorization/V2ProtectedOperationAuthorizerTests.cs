using System.Text.Json;

using Hexalith.Folders.Server.Authorization;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests.Authorization;

public sealed class V2ProtectedOperationAuthorizerTests
{
    private static readonly V2AccessState[] FreshNegativeStates =
    [
        V2AccessState.WrongTenant,
        V2AccessState.Revoked,
        V2AccessState.Disabled,
        V2AccessState.Unknown,
        V2AccessState.HiddenResource,
        V2AccessState.AbsentResource,
        V2AccessState.InsufficientScope,
    ];

    private static readonly V2AuthorityEvidenceState[] UnusableAuthorityStates =
    [
        V2AuthorityEvidenceState.Stale,
        V2AuthorityEvidenceState.Unavailable,
        V2AuthorityEvidenceState.Conflicting,
        V2AuthorityEvidenceState.Incomplete,
    ];

    [Fact]
    public async Task UnauthenticatedRequestReturnsExact401BeforeEveryProtectedFamilyObservation()
    {
        foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
        {
            int observations = 0;
            V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
                AllowedContext(family) with { Authenticated = false },
                _ =>
                {
                    observations++;
                    return ValueTask.FromResult("protected");
                },
                TestContext.Current.CancellationToken);

            observations.ShouldBe(0, family.ToString());
            result.Value.ShouldBeNull();
            result.Authorization.ShouldBe(V2AuthorizationOutcome.AuthenticationFailure());
        }
    }

    [Fact]
    public async Task EveryFreshNegativeStateReturnsOneByteEquivalent404BeforeEveryProtectedFamilyObservation()
    {
        string expected = JsonSerializer.Serialize(V2AuthorizationOutcome.SafeDenial());
        foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
        {
            foreach (V2AccessState state in FreshNegativeStates)
            {
                int observations = 0;
                V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
                    AllowedContext(family) with { AccessState = state },
                    _ =>
                    {
                        observations++;
                        return ValueTask.FromResult("protected");
                    },
                    TestContext.Current.CancellationToken);

                observations.ShouldBe(0, $"{family}:{state}");
                JsonSerializer.Serialize(result.Authorization).ShouldBe(expected, $"{family}:{state}");
            }
        }
    }

    [Fact]
    public async Task EveryUnusableAuthorityStateReturnsExact503BeforeEveryProtectedFamilyObservation()
    {
        foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
        {
            foreach (V2AuthorityEvidenceState evidence in UnusableAuthorityStates)
            {
                int observations = 0;
                V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
                    AllowedContext(family) with { AuthorityEvidence = evidence },
                    _ =>
                    {
                        observations++;
                        return ValueTask.FromResult("protected");
                    },
                    TestContext.Current.CancellationToken);

                observations.ShouldBe(0, $"{family}:{evidence}");
                result.Authorization.ShouldBe(V2AuthorizationOutcome.AuthorityUnavailable());
            }
        }
    }

    [Fact]
    public async Task StaleCanonicalAccessStateReturnsExact503BeforeObservation()
    {
        int observations = 0;
        V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
            AllowedContext(V2ProtectedOperationFamily.ConsoleView) with { AccessState = V2AccessState.Stale },
            _ =>
            {
                observations++;
                return ValueTask.FromResult("protected");
            },
            TestContext.Current.CancellationToken);

        observations.ShouldBe(0);
        result.Authorization.ShouldBe(V2AuthorizationOutcome.AuthorityUnavailable());
    }

    [Fact]
    public async Task MissingFolderAuthorityOrTaskBindingReturns404BeforeLookup()
    {
        foreach (V2AuthorizationContext context in new[]
        {
            AllowedContext(V2ProtectedOperationFamily.ConsoleView) with { FolderAuthorityEstablished = false },
            AllowedContext(V2ProtectedOperationFamily.StatusPermissionAndLockInspection) with
            {
                TaskBindingRequired = true,
                TaskBelongsToFolder = false,
            },
        })
        {
            int observations = 0;
            V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
                context,
                _ =>
                {
                    observations++;
                    return ValueTask.FromResult("protected");
                },
                TestContext.Current.CancellationToken);

            observations.ShouldBe(0);
            result.Authorization.ShouldBe(V2AuthorizationOutcome.SafeDenial());
        }
    }

    [Fact]
    public async Task FullyAuthorizedContextExecutesObservationExactlyOnce()
    {
        int observations = 0;
        V2ProtectedReadResult<string> result = await V2ProtectedOperationAuthorizer.ExecuteAsync(
            AllowedContext(V2ProtectedOperationFamily.ContextRead),
            _ =>
            {
                observations++;
                return ValueTask.FromResult("protected");
            },
            TestContext.Current.CancellationToken);

        observations.ShouldBe(1);
        result.Authorization.Allowed.ShouldBeTrue();
        result.Value.ShouldBe("protected");
    }

    private static V2AuthorizationContext AllowedContext(V2ProtectedOperationFamily family) =>
        new(
            Authenticated: true,
            AccessState: V2AccessState.TenantMember,
            AuthorityEvidence: V2AuthorityEvidenceState.Fresh,
            OperationFamily: family,
            FamilyGrantSatisfied: true,
            FolderScopeRequired: true,
            FolderAuthorityEstablished: true,
            TaskBindingRequired: false,
            TaskBelongsToFolder: true);
}
