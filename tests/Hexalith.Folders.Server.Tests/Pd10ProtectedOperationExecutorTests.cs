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
            new(false, Pd10AuthorityEvidenceState.Unavailable, false, false, false, false),
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
            new(true, state, true, true, true, true),
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.AuthorityUnavailable);
        probe.BindingReads.ShouldBe(0);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Theory]
    [InlineData("wrong-tenant", false, true, true, true)]
    [InlineData("revoked", true, true, false, true)]
    [InlineData("hidden-resource", true, false, true, true)]
    [InlineData("absent-resource", true, false, true, true)]
    [InlineData("disabled", true, true, false, true)]
    [InlineData("unknown", true, true, true, false)]
    [InlineData("insufficient-scope", true, true, true, false)]
    public async Task FreshNegativeAuthorityReturnsByteEquivalent404WithoutAnyLookup(
        string accessState,
        bool tenantAllowed,
        bool folderAllowed,
        bool familyAllowed,
        bool scopeAllowed)
    {
        Probe probe = new();

        accessState.ShouldNotBeNullOrWhiteSpace();

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            new(true, Pd10AuthorityEvidenceState.Fresh, tenantAllowed, folderAllowed, familyAllowed, scopeAllowed),
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.SafeDenial);
        probe.BindingReads.ShouldBe(0);
        probe.ProtectedReads.ShouldBe(0);
    }

    [Fact]
    public async Task TaskBindingFailureOccursAfterParentAuthorityAndBeforeProtectedRead()
    {
        Probe probe = new() { TaskBelongsToFolder = false };

        Pd10ProtectedOperationResult<string> result = await ExecuteAsync(
            new(true, Pd10AuthorityEvidenceState.Fresh, true, true, true, true, RequiresTaskFolderBinding: true),
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
            new(true, Pd10AuthorityEvidenceState.Fresh, true, true, true, true, RequiresTaskFolderBinding: true),
            probe);

        result.Outcome.ShouldBe(Pd10AuthorizationOutcome.Allowed);
        result.Value.ShouldBe("observed");
        probe.BindingReads.ShouldBe(1);
        probe.ProtectedReads.ShouldBe(1);
    }

    private static ValueTask<Pd10ProtectedOperationResult<string>> ExecuteAsync(
        Pd10AuthorizationContext context,
        Probe probe)
        => Pd10ProtectedOperationExecutor.ExecuteAsync(
            context,
            probe.VerifyBindingAsync,
            probe.ObserveAsync);

    private sealed class Probe
    {
        public int BindingReads { get; private set; }

        public int ProtectedReads { get; private set; }

        public bool TaskBelongsToFolder { get; init; }

        public ValueTask<bool> VerifyBindingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BindingReads++;
            return ValueTask.FromResult(TaskBelongsToFolder);
        }

        public ValueTask<string> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProtectedReads++;
            return ValueTask.FromResult("observed");
        }
    }
}
