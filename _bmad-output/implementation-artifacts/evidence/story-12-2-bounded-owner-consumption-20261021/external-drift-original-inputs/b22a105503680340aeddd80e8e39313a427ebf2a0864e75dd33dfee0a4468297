using static Hexalith.Tenants.UI.Tests.TenantCommandProofFixtures;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.State;

/// <summary>
/// Pins the one canonical derivation of the audit dimension, and proves every tenant command flow routes
/// through it so command lifecycle, projection confirmation, and audit evidence never collapse.
/// </summary>
public sealed class TenantCommandAuditStatesTests
{
    private const string MessageId = "message-1";

    private static readonly DateTimeOffset AttemptStartedAtUtc = DateTimeOffset.UtcNow;

    private static readonly string[] FlowNames =
    [
        "create", "add-member", "change-role", "remove-member", "edit-metadata", "lifecycle", "set-configuration",
        "remove-configuration",
    ];

    /// <summary>Every command flow, driven to acceptance and then through one status result.</summary>
    public static TheoryData<string> Flows
    {
        get
        {
            TheoryData<string> data = new();
            foreach (string flow in FlowNames)
            {
                data.Add(flow);
            }

            return data;
        }
    }

    public static TheoryData<string, CommandStatus, TenantCommandAuditState> FlowStatusTable
    {
        get
        {
            TheoryData<string, CommandStatus, TenantCommandAuditState> data = new();
            foreach (string flow in FlowNames)
            {
                foreach ((CommandStatus status, TenantCommandAuditState expected) in CanonicalStatusTable)
                {
                    data.Add(flow, status, expected);
                }
            }

            return data;
        }
    }

    private static readonly (CommandStatus Status, TenantCommandAuditState Expected)[] CanonicalStatusTable =
    [
        (CommandStatus.Received, TenantCommandAuditState.NotStarted),
        (CommandStatus.Processing, TenantCommandAuditState.NotStarted),
        (CommandStatus.EventsStored, TenantCommandAuditState.AuditPending),
        (CommandStatus.EventsPublished, TenantCommandAuditState.AuditPending),
        (CommandStatus.Completed, TenantCommandAuditState.AuditPending),
        (CommandStatus.TimedOut, TenantCommandAuditState.AuditDelayed),
        (CommandStatus.PublishFailed, TenantCommandAuditState.AuditDelayed),
        (CommandStatus.Rejected, TenantCommandAuditState.NotStarted),
    ];

    [Fact]
    public void Command_status_follows_the_canonical_table()
    {
        foreach ((CommandStatus status, TenantCommandAuditState expected) in CanonicalStatusTable)
        {
            TenantCommandAuditStates.FromCommandStatus(status).ShouldBe(expected, $"status {status}");
        }

        TenantCommandAuditStates.FromCommandStatus(null).ShouldBe(TenantCommandAuditState.AuditUnavailable);

        // A completed status that stored zero events has no audit record to wait for; an unreported count does.
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Completed, eventCount: 0).ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Completed, eventCount: null).ShouldBe(TenantCommandAuditState.AuditPending);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Completed, eventCount: 2).ShouldBe(TenantCommandAuditState.AuditPending);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.EventsStored, eventCount: 0).ShouldBe(TenantCommandAuditState.AuditPending);
        TenantCommandAuditStates.FromCommandStatus((CommandStatus)99).ShouldBe(TenantCommandAuditState.AuditUnavailable);
        Enum.GetValues<CommandStatus>().Length.ShouldBe(CanonicalStatusTable.Length, "a new status needs a canonical row");

        // A status lookup follows the same table, except that a 404 before the first status is a wait that keeps
        // the current audit state. Any other missing, unknown, or unverifiable status after dispatch is unavailable.
        foreach (TenantCommandAuditState current in Enum.GetValues<TenantCommandAuditState>())
        {
            TenantCommandAuditStates.FromStatusLookup(TenantCommandStatusResult.Pending("Status is propagating."), current)
                .ShouldBe(current, $"404 lag from {current}");
            TenantCommandAuditStates.FromStatusLookup(TenantCommandStatusResult.Unknown("Status could not be read."), current)
                .ShouldBe(TenantCommandAuditState.AuditUnavailable, $"unknown status from {current}");
            TenantCommandAuditStates.FromStatusLookup(TenantCommandStatusResult.RetryableFailure("Status read failed."), current)
                .ShouldBe(TenantCommandAuditState.AuditUnavailable, $"failed status read from {current}");
            foreach ((CommandStatus status, TenantCommandAuditState expected) in CanonicalStatusTable)
            {
                TenantCommandAuditStates.FromStatusLookup(new TenantCommandStatusResult(status, EventCount: 1, HasVerifiedCommandIdentity: true), current)
                    .ShouldBe(expected, $"status {status} from {current}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public void A_status_lag_before_the_first_status_keeps_the_audit_state(string flow)
    {
        // A 404 before the first status is a wait: nothing implied before dispatch evidence, and the pending
        // audit record after stored events stays pending instead of turning unavailable.
        TenantCommandStatusResult lag = TenantCommandStatusResult.Pending("Status is propagating.");
        Accept(flow).ShouldBe(TenantCommandAuditState.NotStarted);
        ApplyStatus(flow, lag).ShouldBe(TenantCommandAuditState.NotStarted);

        TenantCommandStatusResult stored = new(CommandStatus.EventsStored, EventCount: 1, HasVerifiedCommandIdentity: true);
        ApplyStatus(flow, stored).ShouldBe(TenantCommandAuditState.AuditPending);
        ApplyStatuses(flow, stored, lag).ShouldBe(TenantCommandAuditState.AuditPending);

        // Any other missing status after stored events is unverifiable.
        ApplyStatuses(flow, stored, TenantCommandStatusResult.Unknown("Status could not be read."))
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public void A_stale_status_after_stored_events_keeps_the_audit_record_pending(string flow)
    {
        // An out-of-order poll or a stale replica reporting receipt, processing, or a zero-event completion cannot
        // unstore the events an earlier status proved, so the pending audit record never disappears as "not started".
        TenantCommandStatusResult stored = new(CommandStatus.EventsStored, EventCount: 1, HasVerifiedCommandIdentity: true);
        TenantCommandStatusResult[] staleStatuses =
        [
            new(CommandStatus.Received, HasVerifiedCommandIdentity: true),
            new(CommandStatus.Processing, HasVerifiedCommandIdentity: true),
            new(CommandStatus.Completed, EventCount: 0, HasVerifiedCommandIdentity: true),
        ];
        foreach (TenantCommandStatusResult stale in staleStatuses)
        {
            ApplyStatuses(flow, stored, stale)
                .ShouldBe(TenantCommandAuditState.AuditPending, $"{stale.Status} ({stale.EventCount} events) after stored events");
        }
    }

    [Fact]
    public void A_zero_event_completion_after_stored_events_keeps_the_event_evidence()
    {
        TenantCommandStatusResult stored = new(CommandStatus.EventsStored, EventCount: 1, HasVerifiedCommandIdentity: true);
        TenantCommandStatusResult staleCompleted = new(CommandStatus.Completed, EventCount: 0, HasVerifiedCommandIdentity: true);

        // ChangeRole: the stale completion is not "the requested role was already applied"; the attempt stays
        // projection-pending with its event evidence, so the audit control stays visible.
        TenantChangeRoleCommandSnapshot change = ChangeAccepted().ApplyStatus(stored).ApplyStatus(staleCompleted);
        change.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        change.HasCommandEventEvidence.ShouldBeTrue();
        change.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);

        // Set configuration: the stale completion neither clears the evidence nor marks the attempt completed
        // without events.
        TenantSetConfigurationCommandSnapshot set = SetRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ApplyStatus(stored)
            .ApplyStatus(staleCompleted);
        set.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        set.HasCommandEventEvidence.ShouldBeTrue();
        set.CompletedWithoutEvents.ShouldBeFalse();
        set.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);

        // Without earlier evidence, a zero-event completion still reads as completed without events.
        TenantSetConfigurationCommandSnapshot eventless = SetRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ApplyStatus(staleCompleted);
        eventless.HasCommandEventEvidence.ShouldBeFalse();
        eventless.CompletedWithoutEvents.ShouldBeTrue();
        eventless.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    [Fact]
    public void Prior_event_evidence_keeps_a_stale_status_pending_in_the_canonical_table()
    {
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Received, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditPending);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Processing, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditPending);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Completed, eventCount: 0, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditPending);

        // Definitive outcomes keep their canonical state whatever the earlier evidence.
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.Rejected, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.TimedOut, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditDelayed);
        TenantCommandAuditStates.FromCommandStatus(CommandStatus.PublishFailed, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditDelayed);
        TenantCommandAuditStates.FromCommandStatus(null, hasEventEvidence: true)
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add-member-baseline-met")]
    [InlineData("add-member-baseline-missing")]
    [InlineData("change-role-missing-target")]
    [InlineData("change-role-missing-baseline")]
    [InlineData("remove-member-missing-baseline")]
    [InlineData("edit-metadata-missing-baseline")]
    [InlineData("edit-metadata-missing-provenance")]
    public void A_projection_that_cannot_prove_the_attempt_keeps_the_audit_state(string failure)
    {
        // The projection was read but did not prove this attempt, and no audit read happened: the command is
        // unverified, and the audit dimension keeps what the Completed status established.
        (TenantCommandLifecycleState state, TenantCommandAuditState before, TenantCommandAuditState after) = ProvenanceFailure(failure);

        before.ShouldBe(TenantCommandAuditState.AuditPending);
        state.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        after.ShouldBe(before);
    }

    [Theory]
    [InlineData("lifecycle-missing-baseline")]
    [InlineData("lifecycle-unknown-status")]
    [InlineData("lifecycle-no-proof-reader")]
    public void A_terminal_lifecycle_projection_failure_delays_the_pending_audit_record_it_stops_following(string failure)
    {
        // Lifecycle unable-to-verify is terminal: the attempt is released, so no status lookup or projection
        // re-query will ever advance a pending audit record again. It is reported delayed rather than pending
        // forever, and never unavailable, because no audit read failed.
        (TenantCommandLifecycleState state, TenantCommandAuditState before, TenantCommandAuditState after) = ProvenanceFailure(failure);

        before.ShouldBe(TenantCommandAuditState.AuditPending);
        state.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        new TenantLifecycleCommandSnapshot(state).RetainsAttempt.ShouldBeFalse();
        after.ShouldBe(TenantCommandAuditState.AuditDelayed);
    }

    [Fact]
    public void An_attempt_no_longer_followed_delays_only_a_pending_audit_record()
    {
        foreach (TenantCommandAuditState current in Enum.GetValues<TenantCommandAuditState>())
        {
            TenantCommandAuditStates.AfterTrackingEnds(current).ShouldBe(
                current is TenantCommandAuditState.AuditPending ? TenantCommandAuditState.AuditDelayed : current,
                $"from {current}");
        }

        // A lifecycle proof failure keeps a non-pending audit state exactly as command status established it.
        TenantLifecycleCommandSnapshot unverifiable = LifecycleStarted()
            .AmbiguousSubmission("Tenants.Lifecycle.SubmissionEvidence.Ambiguous");
        unverifiable.ProjectionUnverified("Tenants.Lifecycle.UnableToVerify.ProofRead")
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        LifecycleStarted().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ProjectionUnverified("Tenants.Lifecycle.UnableToVerify.ProofRead")
            .AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    [Fact]
    public void Submission_outcomes_follow_the_canonical_table()
    {
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Rejected("Rejected.", "InsufficientPermissions"))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(new TenantCommandSubmissionResult(TenantCommandLifecycleState.AlreadyApplied))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(new TenantCommandSubmissionResult(TenantCommandLifecycleState.DuplicatePrevented))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        // A failure without a message id (pre-dispatch validation, unavailable gateway) dispatched nothing.
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Failed("Tenant id and name are required before the command can be submitted."))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference"))
            .ShouldBe(TenantCommandAuditState.NotStarted);
        // A failure reported after the message was sent carries its id and may have reached the server.
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { MessageId = MessageId })
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Ambiguous(MessageId, "Tenants.Lifecycle.SubmissionEvidence.Ambiguous"))
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Fact]
    public void A_retry_failed_before_dispatch_keeps_the_reused_identity_unverifiable()
    {
        TenantCommandSubmissionResult preDispatchFailure = TenantCommandSubmissionResult.FailedWithKey(
            "Tenants.Commands.Unavailable.InvalidTrackingReference");

        // The earlier attempt with this identity may already have reached the server, so a retry that sent
        // nothing proves no absence of an audit record.
        TenantCommandAuditStates.FromSubmission(preDispatchFailure, MessageId).ShouldBe(TenantCommandAuditState.AuditUnavailable);
        TenantCommandAuditStates.FromSubmission(
                TenantCommandSubmissionResult.Failed("Tenant command submission is unavailable."),
                MessageId)
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);

        // A first attempt that failed before dispatch sent nothing, exactly as the single-argument derivation.
        TenantCommandAuditStates.FromSubmission(preDispatchFailure, retriedMessageId: null).ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(preDispatchFailure, " ").ShouldBe(TenantCommandAuditState.NotStarted);

        // Every definitive server outcome of a retry keeps its canonical state.
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Rejected("Rejected.", "InsufficientPermissions"), MessageId)
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"), MessageId)
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(new TenantCommandSubmissionResult(TenantCommandLifecycleState.AlreadyApplied), MessageId)
            .ShouldBe(TenantCommandAuditState.NotStarted);
        TenantCommandAuditStates.FromSubmission(new TenantCommandSubmissionResult(TenantCommandLifecycleState.DuplicatePrevented), MessageId)
            .ShouldBe(TenantCommandAuditState.NotStarted);
    }

    [Fact]
    public void Only_a_complete_redacted_ready_receipt_yields_audit_available()
    {
        TenantAuditReceipt ready = TenantAuditReceipt.FromRow(UpdateRow(AttemptStartedAtUtc.AddSeconds(1)));
        ready.State.ShouldBe(TenantAuditReceiptState.Ready);
        TenantCommandAuditStates.FromConfirmationEvidence(ready).ShouldBe(TenantCommandAuditState.AuditAvailable);

        TenantAuditReceipt partial = TenantAuditReceipt.FromRow(UpdateRow(AttemptStartedAtUtc.AddSeconds(1).ToOffset(TimeSpan.FromHours(2))));
        partial.State.ShouldNotBe(TenantAuditReceiptState.Ready);
        TenantCommandAuditStates.FromConfirmationEvidence(partial).ShouldBe(TenantCommandAuditState.MissingSupport);

        TenantAuditReceipt stale = TenantAuditReceipt.FromRow(UpdateRow(AttemptStartedAtUtc.AddSeconds(1)) with { Freshness = ReadModelFreshnessState.Stale });
        TenantCommandAuditStates.FromConfirmationEvidence(stale).ShouldBe(TenantCommandAuditState.MissingSupport);

        TenantCommandAuditStates.FromConfirmationEvidence(null).ShouldBe(TenantCommandAuditState.MissingSupport);
    }

    [Theory]
    [MemberData(nameof(FlowStatusTable))]
    public void Every_flow_derives_status_audit_from_the_canonical_table(
        string flow,
        CommandStatus status,
        TenantCommandAuditState expected)
        => ApplyStatus(flow, new TenantCommandStatusResult(status, "Safe status.", EventCount: 1, HasVerifiedCommandIdentity: true))
            .ShouldBe(expected);

    [Theory]
    [InlineData("create", TenantCommandAuditState.NotStarted)]
    [InlineData("add-member", TenantCommandAuditState.NotStarted)]
    [InlineData("change-role", TenantCommandAuditState.NotStarted)]
    [InlineData("remove-member", TenantCommandAuditState.NotStarted)]
    [InlineData("edit-metadata", TenantCommandAuditState.NotStarted)]
    [InlineData("set-configuration", TenantCommandAuditState.NotStarted)]
    [InlineData("lifecycle", TenantCommandAuditState.NotStarted)]
    [InlineData("remove-configuration", TenantCommandAuditState.NotStarted)]
    public void A_completed_status_with_zero_events_never_claims_a_pending_audit_record(
        string flow,
        TenantCommandAuditState expected)
        // Command verification may require event evidence, but zero stored events implies no audit record.
        => ApplyStatus(flow, new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 0, HasVerifiedCommandIdentity: true))
            .ShouldBe(expected);

    [Fact]
    public void A_timed_out_status_after_stored_events_delays_audit_without_changing_the_remove_configuration_lifecycle()
    {
        TenantRemoveConfigurationCommandSnapshot pending = RemoveRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ApplyStatus(new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true));
        pending.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);

        TenantRemoveConfigurationCommandSnapshot timedOut = pending.ApplyStatus(
            new TenantCommandStatusResult(CommandStatus.TimedOut, HasVerifiedCommandIdentity: true));

        timedOut.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        timedOut.HasCommandEventEvidence.ShouldBeTrue();
        timedOut.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
    }

    [Theory]
    [MemberData(nameof(Flows))]
    public void Every_flow_reports_a_missing_status_after_dispatch_as_unavailable(string flow)
        => ApplyStatus(flow, TenantCommandStatusResult.Unknown("Command status could not be read."))
            .ShouldBe(TenantCommandAuditState.AuditUnavailable);

    [Theory]
    [MemberData(nameof(Flows))]
    public void Acceptance_alone_implies_no_audit_state(string flow)
        => Accept(flow).ShouldBe(TenantCommandAuditState.NotStarted);

    [Theory]
    [MemberData(nameof(Flows))]
    public void No_status_event_count_or_signalr_input_ever_yields_audit_available(string flow)
    {
        CommandStatus?[] statuses = [.. Enum.GetValues<CommandStatus>().Cast<CommandStatus?>(), null, (CommandStatus)99];
        int?[] eventCounts = [null, -1, 0, 1, 5];
        foreach (CommandStatus? status in statuses)
        {
            foreach (int? eventCount in eventCounts)
            {
                var result = new TenantCommandStatusResult(status, "Safe status.", EventCount: eventCount, HasVerifiedCommandIdentity: true);
                ApplyStatus(flow, result).ShouldNotBe(TenantCommandAuditState.AuditAvailable, $"{flow} {status} {eventCount}");
                ApplyStatus(flow, result, nudge: true).ShouldNotBe(TenantCommandAuditState.AuditAvailable, $"{flow} {status} {eventCount} + SignalR");
            }
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("add-member")]
    [InlineData("change-role")]
    [InlineData("edit-metadata")]
    [InlineData("lifecycle")]
    [InlineData("set-configuration")]
    [InlineData("remove-configuration")]
    public void Projection_confirmation_without_attempt_specific_proof_is_missing_support_not_pending(string flow)
    {
        (TenantCommandLifecycleState state, TenantCommandAuditState audit) = Confirm(flow);

        // Command, projection, and audit truth stay distinct: the command is confirmed, the audit dimension is
        // honestly unsupported, and neither is promoted into the other.
        state.ShouldBe(TenantCommandLifecycleState.Confirmed);
        audit.ShouldBe(TenantCommandAuditState.MissingSupport);
        TenantAuditAvailability availability = TenantAuditAvailability.FromCommandAuditState(audit);
        availability.IsAuditAvailable.ShouldBeFalse();
        availability.RecoveryVerbs.ShouldBe(
            [TenantAuditRecoveryVerb.ContinueReadOnly, TenantAuditRecoveryVerb.InspectAudit, TenantAuditRecoveryVerb.Escalate]);
    }

    [Fact]
    public void Remove_member_keeps_its_proof_walk_after_confirmation()
    {
        (TenantCommandLifecycleState state, TenantCommandAuditState audit) = Confirm("remove-member");

        state.ShouldBe(TenantCommandLifecycleState.Confirmed);
        audit.ShouldBe(TenantCommandAuditState.AuditPending);
    }

    [Fact]
    public void Metadata_audit_available_requires_a_ready_receipt_built_from_the_matching_row()
    {
        TenantUpdateMetadataCommandSnapshot pending = MetadataPending();
        TenantDetail updated = MetadataDetail("Updated", "submitted");

        TenantUpdateMetadataCommandSnapshot withReadyRow = pending.ConfirmProjection(Proof(updated, "v2"), UpdateRow(pending.AttemptStartedAtUtc!.Value.AddSeconds(1)));
        withReadyRow.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        withReadyRow.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);

        // The row still proves this attempt's provenance, but its non-UTC timestamp makes the receipt
        // incomplete: the projection confirms, the audit dimension never claims availability.
        TenantUpdateMetadataCommandSnapshot withIncompleteRow = pending.ConfirmProjection(Proof(updated, "v2"), UpdateRow(pending.AttemptStartedAtUtc!.Value.AddSeconds(1).ToOffset(TimeSpan.FromHours(2))));
        withIncompleteRow.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        withIncompleteRow.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);

        TenantUpdateMetadataCommandSnapshot withoutRow = pending.ConfirmProjection(Proof(updated, "v2"));
        withoutRow.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        withoutRow.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
    }

    [Fact]
    public void Blocked_previewed_already_applied_and_duplicate_outcomes_dispatch_nothing_and_imply_no_audit()
    {
        TenantCreateCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantAddMemberCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantChangeRoleCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantRemoveMemberCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantUpdateMetadataCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantSetConfigurationCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantRemoveConfigurationCommandSnapshot.Blocked("Blocked.", TenantCommandFocusTarget.Submit).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);

        TenantRemoveMemberCommandSnapshot.Idle().DuplicatePrevented("Duplicate.").AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        LifecycleStarted().DuplicatePrevented("Duplicate.").AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantLifecycleCommandSnapshot.Idle(LifecycleDetail(TenantStatus.Active))
            .Previewed(new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant), LifecycleDetail(TenantStatus.Active), "tenant-sequence:41")
            .AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantSetConfigurationCommandSnapshot.Idle().Previewed(SetPreview()).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantSetConfigurationCommandSnapshot.Idle().AlreadyApplied(SetPreview(), "Already applied.").AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantRemoveConfigurationCommandSnapshot.Idle().Previewed(RemovePreview()).AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        TenantChangeRoleCommandSnapshot.Idle()
            .AlreadyApplied(new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantReader), TenantRole.TenantReader, 1, "Already applied.")
            .AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    [Fact]
    public void Lifecycle_timeouts_and_retention_expiry_are_delayed_while_unknown_dispatch_is_unavailable()
    {
        TenantLifecycleCommandSnapshot accepted = LifecycleStarted()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

        TenantLifecycleCommandSnapshot timedOut = accepted.StatusTimedOut();
        timedOut.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        timedOut.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.StatusTimeout");
        timedOut.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);

        TenantLifecycleCommandSnapshot expired = accepted.ApplyStatus(
            TenantCommandStatusResult.Pending("Status is propagating."),
            observedAtUtc: accepted.AttemptStartedAtUtc!.Value.AddMinutes(6));
        expired.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        expired.AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);

        LifecycleStarted().AmbiguousSubmission("Tenants.Lifecycle.SubmissionEvidence.Ambiguous")
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        accepted.Abandon().AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        accepted.ApplyStatus(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: false))
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Fact]
    public void Configuration_retention_expiry_is_delayed_while_abandonment_and_ambiguity_are_unavailable()
    {
        TenantSetConfigurationCommandSnapshot setAccepted = SetRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));
        setAccepted.ExpireRetention().AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        setAccepted.ExpireRetention().State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
        setAccepted.Abandon().AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        SetRequestSent().AmbiguousSubmission("Tenants.Configuration.Set.UnableToVerify.Status")
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        setAccepted.ApplyStatus(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: false))
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);

        TenantRemoveConfigurationCommandSnapshot removeAccepted = RemoveRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));
        removeAccepted.ExpireRetention().AuditState.ShouldBe(TenantCommandAuditState.AuditDelayed);
        removeAccepted.Abandon().AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        RemoveRequestSent().AmbiguousSubmission("Tenants.Configuration.Remove.UnableToVerify.Status")
            .AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Fact]
    public void A_failed_projection_read_never_rewrites_the_audit_dimension()
    {
        TenantRemoveConfigurationCommandSnapshot pending = RemoveRequestSent()
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
            .ApplyStatus(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));
        pending.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);

        TenantRemoveConfigurationCommandSnapshot failedRead = pending.ProjectionVerificationFailed(
            "Tenants.Configuration.Remove.UnableToVerify.ProjectionProof");

        failedRead.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
        failedRead.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
    }

    private static TenantCommandAuditState Accept(string flow)
        => flow switch
        {
            "create" => CreateAccepted().AuditState,
            "add-member" => AddAccepted().AuditState,
            "change-role" => ChangeAccepted().AuditState,
            "remove-member" => RemoveAccepted().AuditState,
            "edit-metadata" => MetadataAccepted().AuditState,
            "lifecycle" => LifecycleStarted().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).AuditState,
            "set-configuration" => SetRequestSent().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).AuditState,
            "remove-configuration" => RemoveRequestSent().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).AuditState,
            _ => throw new ArgumentOutOfRangeException(nameof(flow), flow, null),
        };

    private static TenantCommandAuditState ApplyStatus(string flow, TenantCommandStatusResult status, bool nudge = false)
        => flow switch
        {
            "create" => Nudge(CreateAccepted().ApplyStatus(status), nudge, static s => s.SignalRNudge()).AuditState,
            "add-member" => Nudge(AddAccepted().ApplyStatus(status), nudge, static s => s.SignalRNudge()).AuditState,
            "change-role" => Nudge(ChangeAccepted().ApplyStatus(status), nudge, static s => s.SignalRNudge()).AuditState,
            "remove-member" => Nudge(RemoveAccepted().ApplyStatus(status), nudge, static s => s.SignalRNudge()).AuditState,
            "edit-metadata" => Nudge(MetadataAccepted().ApplyStatus(status), nudge, static s => s.SignalRNudge()).AuditState,
            "lifecycle" => Nudge(
                LifecycleStarted().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).ApplyStatus(status),
                nudge,
                static s => s.SignalRNudge()).AuditState,
            "set-configuration" => Nudge(
                SetRequestSent().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).ApplyStatus(status),
                nudge,
                static s => s.SignalRNudge()).AuditState,
            "remove-configuration" => Nudge(
                RemoveRequestSent().Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1")).ApplyStatus(status),
                nudge,
                static s => s.SignalRNudge()).AuditState,
            _ => throw new ArgumentOutOfRangeException(nameof(flow), flow, null),
        };

    private static (TenantCommandLifecycleState State, TenantCommandAuditState Audit) Confirm(string flow)
    {
        var completed = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        switch (flow)
        {
            case "create":
                TenantCreateCommandSnapshot create = CreateAccepted().ApplyStatus(completed)
                    .ConfirmProjection(ListProof(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active), "projection-v2"), null);
                return (create.State, create.AuditState);
            case "add-member":
                TenantAddMemberCommandSnapshot add = AddAccepted().ApplyStatus(completed)
                    .ConfirmProjection(Proof(MemberDetail(new TenantMember("literal-user", TenantRole.TenantReader)), "v2"));
                return (add.State, add.AuditState);
            case "change-role":
                TenantChangeRoleCommandSnapshot change = ChangeAccepted().ApplyStatus(completed)
                    .ConfirmProjection(Proof(MemberDetail(new TenantMember("literal-user", TenantRole.TenantContributor)), "v2"));
                return (change.State, change.AuditState);
            case "remove-member":
                TenantRemoveMemberCommandSnapshot remove = RemoveAccepted().ApplyStatus(completed)
                    .ConfirmProjection(Proof(MemberDetail(), "tenant-sequence:2"));
                return (remove.State, remove.AuditState);
            case "edit-metadata":
                TenantUpdateMetadataCommandSnapshot metadata = MetadataPending()
                    .ConfirmProjection(Proof(MetadataDetail("Updated", "submitted"), "v2"));
                return (metadata.State, metadata.AuditState);
            case "lifecycle":
                TenantLifecycleCommandSnapshot lifecycle = LifecycleStarted()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed)
                    .ConfirmProjection(TenantDetailSnapshot.Ready(
                        LifecycleDetail(TenantStatus.Disabled),
                        eTag: null,
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current,
                        "tenant-sequence:42"));
                return (lifecycle.State, lifecycle.AuditState);
            case "set-configuration":
                TenantSetConfigurationIntent setIntent = SetIntent();
                TenantSetConfigurationCommandSnapshot set = SetRequestSent()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed)
                    .ConfirmProjection(TenantConfigurationProjectionProof.Create(
                        setIntent.TenantId,
                        TenantConfigurationProjectionProofKind.SetConfirmed,
                        "tenant-sequence:42",
                        setIntent.AttemptFingerprint));
                return (set.State, set.AuditState);
            case "remove-configuration":
                TenantRemoveConfigurationIntent removeIntent = RemoveIntent();
                TenantRemoveConfigurationCommandSnapshot removeConfiguration = RemoveRequestSent()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed)
                    .ConfirmProjection(TenantConfigurationProjectionProof.Create(
                        removeIntent.TenantId,
                        TenantConfigurationProjectionProofKind.RemoveConfirmed,
                        "tenant-sequence:42",
                        removeIntent.AttemptFingerprint));
                return (removeConfiguration.State, removeConfiguration.AuditState);
            default:
                throw new ArgumentOutOfRangeException(nameof(flow), flow, null);
        }
    }

    private static TenantCommandAuditState ApplyStatuses(string flow, TenantCommandStatusResult first, TenantCommandStatusResult second)
        => flow switch
        {
            "create" => CreateAccepted().ApplyStatus(first).ApplyStatus(second).AuditState,
            "add-member" => AddAccepted().ApplyStatus(first).ApplyStatus(second).AuditState,
            "change-role" => ChangeAccepted().ApplyStatus(first).ApplyStatus(second).AuditState,
            "remove-member" => RemoveAccepted().ApplyStatus(first).ApplyStatus(second).AuditState,
            "edit-metadata" => MetadataAccepted().ApplyStatus(first).ApplyStatus(second).AuditState,
            "lifecycle" => LifecycleStarted()
                .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                .ApplyStatus(first)
                .ApplyStatus(second)
                .AuditState,
            "set-configuration" => SetRequestSent()
                .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                .ApplyStatus(first)
                .ApplyStatus(second)
                .AuditState,
            "remove-configuration" => RemoveRequestSent()
                .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                .ApplyStatus(first)
                .ApplyStatus(second)
                .AuditState,
            _ => throw new ArgumentOutOfRangeException(nameof(flow), flow, null),
        };

    private static (TenantCommandLifecycleState State, TenantCommandAuditState Before, TenantCommandAuditState After) ProvenanceFailure(string failure)
    {
        var completed = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        switch (failure)
        {
            case "create":
            {
                // The tenant already existed at baseline, so a matching projection cannot prove this create.
                TenantCreateCommandSnapshot pending = TenantCreateCommandSnapshot.Idle()
                    .RequestSent(new CreateTenant("tenant.alpha", "Alpha", null), "projection-v1", baselineTenantAbsent: false)
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantCreateCommandSnapshot result = pending.ConfirmProjection(ListProof(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active), "projection-v2"), null);
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "add-member-baseline-met":
            case "add-member-baseline-missing":
            {
                TenantAddMemberCommandSnapshot pending = TenantAddMemberCommandSnapshot.Idle()
                    .RequestSent(
                        new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader),
                        baselineProjectionVersion: failure == "add-member-baseline-missing" ? null : "v1",
                        baselinePostconditionMet: failure == "add-member-baseline-met")
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantAddMemberCommandSnapshot result = pending.ConfirmProjection(Proof(MemberDetail(new TenantMember("literal-user", TenantRole.TenantReader)), "v2"));
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "change-role-missing-target":
            {
                TenantChangeRoleCommandSnapshot pending = ChangeAccepted().ApplyStatus(completed);
                TenantChangeRoleCommandSnapshot result = pending.ConfirmProjection(Proof(MemberDetail(), "v2"));
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "change-role-missing-baseline":
            {
                TenantChangeRoleCommandSnapshot pending = TenantChangeRoleCommandSnapshot.Idle()
                    .RequestSent(
                        new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantContributor),
                        TenantRole.TenantReader,
                        ownerCount: 1,
                        baselineProjectionVersion: null)
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantChangeRoleCommandSnapshot result = pending.ConfirmProjection(Proof(MemberDetail(new TenantMember("literal-user", TenantRole.TenantContributor)), "v2"));
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "remove-member-missing-baseline":
            {
                TenantRemoveMemberCommandSnapshot pending = TenantRemoveMemberCommandSnapshot.Idle()
                    .Previewed(
                        new RemoveUserFromTenant("tenant.alpha", "literal-user"),
                        TenantRole.TenantReader,
                        ownerCount: 1,
                        targetGlobalAdministratorFriction: false,
                        MemberDetail(new TenantMember("literal-user", TenantRole.TenantReader)))
                    .RequestSent(baselineProjectionVersion: null)
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantRemoveMemberCommandSnapshot result = pending.ConfirmProjection(Proof(MemberDetail(), "v2"));
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "edit-metadata-missing-baseline":
            {
                TenantUpdateMetadataCommandSnapshot pending = TenantUpdateMetadataCommandSnapshot.Idle("Original", "Original description")
                    .RequestSent(new UpdateTenant("tenant.alpha", "Updated", "submitted"), baselineProjectionVersion: null, AttemptStartedAtUtc)
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantUpdateMetadataCommandSnapshot result = pending.ConfirmProjection(Proof(MetadataDetail("Updated", "submitted"), "v2"));
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "edit-metadata-missing-provenance":
            {
                // Identical values need attempt-specific audit provenance; a non-matching row proves nothing.
                TenantUpdateMetadataCommandSnapshot pending = TenantUpdateMetadataCommandSnapshot.Idle("Updated", "submitted")
                    .RequestSent(new UpdateTenant("tenant.alpha", "Updated", "submitted"), baselineProjectionVersion: "v1", AttemptStartedAtUtc)
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantUpdateMetadataCommandSnapshot result = pending.ConfirmProjection(Proof(MetadataDetail("Updated", "submitted"), "v2"), UpdateRow(AttemptStartedAtUtc.AddSeconds(1)) with { EventReference = "another-message" });
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "lifecycle-missing-baseline":
            {
                TenantLifecycleCommandSnapshot pending = LifecycleStarted()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed) with { BaselineProjectionVersion = null };
                TenantLifecycleCommandSnapshot result = pending.ConfirmProjection(TenantDetailSnapshot.Ready(
                    LifecycleDetail(TenantStatus.Disabled),
                    eTag: null,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:42"));
                result.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.MissingBaseline");
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "lifecycle-unknown-status":
            {
                TenantLifecycleCommandSnapshot pending = LifecycleStarted()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantLifecycleCommandSnapshot result = pending.ConfirmProjection(TenantDetailSnapshot.Ready(
                    LifecycleDetail(TenantStatus.Unknown),
                    eTag: null,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:42"));
                result.SafeMessageKey.ShouldBe("Tenants.Lifecycle.UnableToVerify.ProofRead");
                return (result.State, pending.AuditState, result.AuditState);
            }

            case "lifecycle-no-proof-reader":
            {
                TenantLifecycleCommandSnapshot pending = LifecycleStarted()
                    .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"))
                    .ApplyStatus(completed);
                TenantLifecycleCommandSnapshot result = pending.ProjectionUnverified("Tenants.Lifecycle.UnableToVerify.ProofRead");
                return (result.State, pending.AuditState, result.AuditState);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure, null);
        }
    }

    private static T Nudge<T>(T snapshot, bool nudge, Func<T, T> apply)
        => nudge ? apply(snapshot) : snapshot;

    private static TenantCreateCommandSnapshot CreateAccepted()
        => TenantCreateCommandSnapshot.Idle()
            .RequestSent(new CreateTenant("tenant.alpha", "Alpha", null), "projection-v1", baselineTenantAbsent: true)
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

    private static TenantAddMemberCommandSnapshot AddAccepted()
        => TenantAddMemberCommandSnapshot.Idle()
            .RequestSent(new AddUserToTenant("tenant.alpha", "literal-user", TenantRole.TenantReader), baselineProjectionVersion: "v1")
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

    private static TenantChangeRoleCommandSnapshot ChangeAccepted()
        => TenantChangeRoleCommandSnapshot.Idle()
            .RequestSent(
                new ChangeUserRole("tenant.alpha", "literal-user", TenantRole.TenantContributor),
                TenantRole.TenantReader,
                ownerCount: 1,
                baselineProjectionVersion: "v1")
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

    private static TenantRemoveMemberCommandSnapshot RemoveAccepted()
        => TenantRemoveMemberCommandSnapshot.Idle()
            .Previewed(
                new RemoveUserFromTenant("tenant.alpha", "literal-user"),
                TenantRole.TenantReader,
                ownerCount: 1,
                targetGlobalAdministratorFriction: false,
                MemberDetail(new TenantMember("literal-user", TenantRole.TenantReader)))
            .RequestSent(baselineProjectionVersion: "tenant-sequence:1")
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

    private static TenantUpdateMetadataCommandSnapshot MetadataAccepted()
        => TenantUpdateMetadataCommandSnapshot.Idle("Original", "Original description")
            .RequestSent(new UpdateTenant("tenant.alpha", "Updated", "submitted"), baselineProjectionVersion: "v1", AttemptStartedAtUtc)
            .Accepted(TenantCommandSubmissionResult.Accepted(MessageId, "correlation-1"));

    private static TenantUpdateMetadataCommandSnapshot MetadataPending()
        => MetadataAccepted()
            .ApplyStatus(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true));

    private static TenantLifecycleCommandSnapshot LifecycleStarted()
    {
        TenantDetail detail = LifecycleDetail(TenantStatus.Active);
        var intent = new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant);
        return TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(intent, detail, "tenant-sequence:41")
            .RequestSent(intent, detail, "tenant-sequence:41", MessageId);
    }

    private static TenantSetConfigurationIntent SetIntent()
        => new("tenant.alpha", "billing", "mode", "billing.mode", "value-fingerprint");

    private static TenantSetConfigurationPreview SetPreview()
        => TenantSetConfigurationPreview.Create(
            SetIntent(),
            TenantStatus.Active,
            TenantSetConfigurationCurrentState.Different,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:41",
            isAuthorized: true);

    private static TenantSetConfigurationCommandSnapshot SetRequestSent()
        => TenantSetConfigurationCommandSnapshot.Idle()
            .Previewed(SetPreview())
            .RequestSent(SetPreview(), MessageId, DateTimeOffset.UtcNow);

    private static TenantRemoveConfigurationIntent RemoveIntent()
        => new("tenant.alpha", "billing", "billing.mode");

    private static TenantRemoveConfigurationPreview RemovePreview()
        => TenantRemoveConfigurationPreview.Create(
            RemoveIntent(),
            TenantStatus.Active,
            TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:41",
            isAuthorized: true);

    private static TenantRemoveConfigurationCommandSnapshot RemoveRequestSent()
        => TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(RemovePreview())
            .RequestSent(RemovePreview(), MessageId, DateTimeOffset.UtcNow);

    private static TenantDetail MemberDetail(params TenantMember[] members)
        => new(
            "tenant.alpha",
            "Alpha",
            null,
            TenantStatus.Active,
            [new TenantMember("owner-user", TenantRole.TenantOwner), .. members],
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static TenantDetail MetadataDetail(string name, string? description)
        => new(
            "tenant.alpha",
            name,
            description,
            TenantStatus.Active,
            [new TenantMember("owner-user", TenantRole.TenantOwner)],
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static TenantDetail LifecycleDetail(TenantStatus status)
        => new(
            "tenant.alpha",
            "Alpha",
            "Tenant alpha description",
            status,
            [],
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static TenantAuditRow UpdateRow(DateTimeOffset timestamp)
        => new(
            MessageId,
            "TenantUpdated",
            AuditEventCategory.Administrative,
            "operator-user",
            timestamp,
            "tenant.alpha",
            "tenant.alpha",
            "tenant.alpha",
            "TenantUpdated",
            string.Empty,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked);
}
