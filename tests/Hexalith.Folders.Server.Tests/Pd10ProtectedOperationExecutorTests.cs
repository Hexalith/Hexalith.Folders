using Hexalith.Folders.Server.Authorization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

/// <summary>
/// Proves the non-routed PD10 candidate gate never performs protected observation before authorization.
/// </summary>
public sealed class Pd10ProtectedOperationExecutorTests
{
    [Fact]
    public async Task UnauthenticatedRequestReturnsCanonical401WithoutAnyLookup()
    {
        Probe probe = new();

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            Allowed() with { IsAuthenticated = false },
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.AuthenticationRequired);
        probe.BindingReads.ShouldBe(0);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Theory]
    [InlineData((int)Pd10AuthorityEvidenceState.Stale)]
    [InlineData((int)Pd10AuthorityEvidenceState.Unavailable)]
    [InlineData((int)Pd10AuthorityEvidenceState.Conflicting)]
    [InlineData((int)Pd10AuthorityEvidenceState.Incomplete)]
    public async Task UnusableAuthorityReturnsCanonical503WithoutAnyLookup(int stateValue)
    {
        Probe probe = new();
        Pd10AuthorityEvidenceState state = (Pd10AuthorityEvidenceState)stateValue;

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            Allowed() with { AuthorityEvidence = state },
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.AuthorityUnavailable);
        probe.BindingReads.ShouldBe(0);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(FreshNegativeStates))]
    public async Task FreshNegativeAuthorityReturnsByteEquivalent404WithoutAnyLookup(
        int accessStateValue)
    {
        V2AccessState accessState = (V2AccessState)accessStateValue;
        Probe probe = new();

        foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
        {
            Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
                Allowed(accessState),
                probe);

            result.Outcome.ShouldBe(Pd10AuthorizationOutcome.SafeDenial, $"{family}:{accessState}");
            probe.BindingReads.ShouldBe(0);
            probe.ProtectedReads.ShouldBe(0);
        }
    }

    [Fact]
    public async Task EveryPositiveAccessStateCanObserveEveryFamilyOnlyWhenAllConjunctsHold()
    {
        V2AccessState[] positiveStates =
        [
            V2AccessState.TenantAdministrator,
            V2AccessState.TenantMember,
            V2AccessState.DelegatedServiceAgent,
            V2AccessState.TenantScopedOperator,
            V2AccessState.AuditReviewer,
            V2AccessState.IncidentAdministrator,
        ];
        Enum.GetValues<V2AccessState>().Length.ShouldBe(14);

        foreach (V2AccessState state in positiveStates)
        {
            foreach (V2ProtectedOperationFamily family in Enum.GetValues<V2ProtectedOperationFamily>())
            {
                Probe allowedProbe = new();
                Pd10ProtectedOperationResult<string> allowed = await ExecuteAsync(Allowed(state), allowedProbe);
                allowed.Outcome.ShouldBe(Pd10AuthorizationOutcome.Allowed, $"{family}:{state}");
                allowedProbe.ProtectedReads.ShouldBe(1);

                Probe deniedProbe = new();
                Pd10ProtectedOperationResult<string> denied = await ExecuteAsync(
                    Allowed(state) with { DelegationAllowed = false },
                    deniedProbe);
                denied.Outcome.ShouldBe(Pd10AuthorizationOutcome.SafeDenial, $"{family}:{state}:intersection");
                deniedProbe.ProtectedReads.ShouldBe(0);
            }
        }
    }

    [Fact]
    public async Task StaleCanonicalAccessStateReturns503WithoutObservation()
    {
        Probe probe = new();
        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(Allowed(V2AccessState.Stale), probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.AuthorityUnavailable);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Fact]
    public async Task TaskBindingFailureOccursAfterParentAuthorityAndBeforeProtectedRead()
    {
        Probe probe = new() { TaskBelongsToFolder = false };

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            Allowed() with { RequiresTaskFolderBinding = true },
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.SafeDenial);
        probe.BindingReads.ShouldBe(1);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Fact]
    public async Task FullyAuthorizedBoundTaskPerformsProtectedReadOnce()
    {
        Probe probe = new() { TaskBelongsToFolder = true };

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            Allowed() with { RequiresTaskFolderBinding = true },
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.Allowed);
        result.Value.ShouldBe("observed");
        probe.BindingReads.ShouldBe(1);
        probe.ProtectedReads.ShouldBe(1);
    }

    [Fact]
    public async Task InvalidEnvelopeIsCheckedAfterAuthorizationAndBeforeTaskBindingOrProtectedRead()
    {
        Probe probe = new() { TaskBelongsToFolder = true, EnvelopeValid = false };

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            Allowed() with { RequiresTaskFolderBinding = true },
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.Allowed);
        result.Value.ShouldBeNull();
        probe.EnvelopeChecks.ShouldBe(1);
        probe.BindingReads.ShouldBe(0);
        probe.ProtectedReads.ShouldBe(0);
    }

    private static ValueTask<Pd10ProtectedOperationResult<string>> ExecuteAsync(
        Pd10AuthorizationContext context,
        Probe probe)
        => Pd10ProtectedOperationExecutor.ExecuteAsync(
            context,
            probe.ValidateEnvelopeAsync,
            probe.VerifyBindingAsync,
            probe.ObserveAsync);

    private static Pd10AuthorizationContext Allowed(V2AccessState accessState = V2AccessState.TenantMember)
        => new(
            IsAuthenticated: true,
            AccessState: accessState,
            AuthorityEvidence: Pd10AuthorityEvidenceState.Fresh,
            TenantAllowed: true,
            FolderAllowed: true,
            FamilyAllowed: true,
            ScopeAllowed: true,
            DelegationAllowed: true);

    public static TheoryData<int> FreshNegativeStates => new()
    {
        (int)V2AccessState.WrongTenant,
        (int)V2AccessState.Revoked,
        (int)V2AccessState.Disabled,
        (int)V2AccessState.Unknown,
        (int)V2AccessState.HiddenResource,
        (int)V2AccessState.AbsentResource,
        (int)V2AccessState.InsufficientScope,
    };

    private sealed class Probe
    {
        public int BindingReads { get; private set; }

        public int ProtectedReads { get; private set; }

        public int EnvelopeChecks { get; private set; }

        public bool EnvelopeValid { get; init; } = true;

        public bool TaskBelongsToFolder { get; init; }

        public ValueTask<bool> ValidateEnvelopeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnvelopeChecks++;
            return ValueTask.FromResult(EnvelopeValid);
        }

        public ValueTask<Pd10TaskFolderBindingState> VerifyBindingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BindingReads++;
            return ValueTask.FromResult(TaskBelongsToFolder
                ? Pd10TaskFolderBindingState.Bound
                : Pd10TaskFolderBindingState.NotBound);
        }

        public ValueTask<string> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProtectedReads++;
            return ValueTask.FromResult("observed");
        }
    }
}
