using Hexalith.Tenants.Contracts.Identity;
using static Hexalith.Tenants.UI.Tests.TenantCommandProofFixtures;
using System.Globalization;

using Bunit;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Tenants.Metadata;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.EventStore.Client.Projections;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed class EditTenantMetadataFlowTests : FluentBunitContext
{
    [Fact]
    public void Edit_metadata_flow_renders_confirmed_metadata_stable_selectors_and_accessible_fields()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-edit-metadata-flow']");
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Alpha");
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();

        cut.Find("label[for='tenants-edit-metadata-name']").TextContent.ShouldContain("Name");
        cut.Find("[data-testid='tenants-edit-metadata-name']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-edit-metadata-name-help");
        cut.Find("[data-testid='tenants-edit-metadata-description']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-edit-metadata-description-help");
        cut.Find("[data-testid='tenants-edit-metadata-submit']");
        cut.Find("[data-testid='tenants-edit-metadata-cancel']");
        cut.Find("[data-testid='tenants-edit-metadata-refresh']");
        cut.Find("[data-testid='tenants-edit-metadata-lifecycle']");
        cut.Find("[data-testid='tenants-edit-metadata-state']");
        cut.Find("[data-testid='tenants-edit-metadata-audit']");
        cut.Find("[data-testid='tenants-edit-metadata-recovery']");
    }

    [Fact]
    public void Permission_reflection_keeps_read_only_surface_with_inline_unavailable_reason()
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsAuthorized, false)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.MissingPermission));

        cut.Find("[data-testid='tenants-edit-metadata-unavailable-reason']").TextContent.ShouldContain("not authorized", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Alpha");
    }

    [Theory]
    [InlineData(TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale, TenantStatus.Active, "Refresh current")]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Current, TenantStatus.Disabled, "lifecycle state")]
    [InlineData(TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Unknown, TenantStatus.Active, "Refresh current")]
    [InlineData(TenantDetailSurfaceKind.Degraded, ReadModelFreshnessState.Current, TenantStatus.Active, "Refresh current")]
    [InlineData(TenantDetailSurfaceKind.Unavailable, ReadModelFreshnessState.Current, TenantStatus.Active, "Refresh current")]
    [InlineData(TenantDetailSurfaceKind.Unknown, ReadModelFreshnessState.Current, TenantStatus.Active, "Refresh current")]
    public void Edit_metadata_fails_closed_for_stale_unknown_or_disabled_projection(
        TenantDetailSurfaceKind surfaceKind,
        ReadModelFreshnessState freshness,
        TenantStatus status,
        string expectedReason)
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description") with { Status = status })
            .Add(p => p.SurfaceKind, surfaceKind)
            .Add(p => p.Freshness, freshness));

        string reason = cut.Find("[data-testid='tenants-edit-metadata-unavailable-reason']").TextContent;
        reason.ShouldContain(expectedReason, Case.Insensitive);
        if (surfaceKind is not TenantDetailSurfaceKind.Ready || freshness is not ReadModelFreshnessState.Current)
        {
            reason.ShouldNotContain("not authorized", Case.Insensitive);
        }

        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Stale)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void Edit_metadata_requires_current_projection_lifecycle(ProjectionLifecycleState lifecycle)
    {
        RegisterServices(new StubTenantCommandGateway());

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, lifecycle));

        cut.Find("[data-testid='tenants-edit-metadata-unavailable-reason']").TextContent
            .ShouldContain("projection-confirmed lifecycle", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
    }

    [Fact]
    public void Edit_metadata_fails_closed_when_command_surface_is_unavailable()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, false));

        cut.Find("[data-testid='tenants-edit-metadata-unavailable-reason']").TextContent
            .ShouldContain("command support is unavailable", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        gateway.UpdateTenantCallCount.ShouldBe(0);
    }

    [Fact]
    public void Name_validation_blocks_gateway_submission_with_safe_field_message()
    {
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("");
        cut.Find("form").Submit();

        gateway.UpdateTenantCallCount.ShouldBe(0);
        cut.Find("[data-testid='tenants-edit-metadata-validation']").TextContent.ShouldContain("complete tenant name");
        cut.Find("[data-testid='tenants-edit-metadata-name']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-edit-metadata-name-help tenants-edit-metadata-validation");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("\"payload\"", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("bearer ", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation", Case.Insensitive);
    }

    [Fact]
    public void Reentrant_submit_while_gateway_is_pending_preserves_the_active_attempt_without_redispatch()
    {
        TaskCompletionSource<TenantCommandSubmissionResult> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubTenantCommandGateway gateway = new()
        {
            UpdateTenantSubmissionAsync = _ => pending.Task,
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1"));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.UpdateTenantCallCount.ShouldBe(1));
        TenantUpdateMetadataCommandSnapshot active = cut.Instance.Snapshot;
        active.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        UpdateTenant activeIntent = active.Intent.ShouldNotBeNull();
        active.BaselineProjectionVersion.ShouldBe("projection-v1");

        cut.Find("form").Submit();

        gateway.UpdateTenantCallCount.ShouldBe(1);
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.RequestSent);
        cut.Instance.Snapshot.Intent.ShouldBe(activeIntent);
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBe("projection-v1");

        pending.SetResult(TenantCommandSubmissionResult.Failed("Safe failure."));
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
    }

    [Fact]
    public void Successful_submit_confirms_only_after_projection_evidence_and_preserves_last_confirmed_metadata_until_then()
    {
        int projectionCalls = 0;
        string? currentProjectionVersion = "v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.ProjectionVersionProvider, () => currentProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                projectionCalls++;
                if (projectionCalls == 1)
                {
                    return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, "Alpha", "Tenant alpha description"), "tenant-sequence:2"));
                }

                currentProjectionVersion = "v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("[data-testid='tenants-edit-metadata-description']").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        gateway.LastUpdateTenantRequest.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        gateway.LastUpdateTenantRequest.ShouldNotBeNull().Name.ShouldBe("Updated");
        gateway.LastUpdateTenantRequest.ShouldNotBeNull().Description.ShouldBeNull();
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Alpha");
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldNotContain("Updated");
        cut.Find("[data-testid='tenants-edit-metadata-state']").TextContent.ShouldContain("Projection pending");
        cut.Find("[data-testid='tenants-edit-metadata-state']").TextContent.ShouldNotContain("success", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation-update", Case.Insensitive);

        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Updated");
        cut.Find("[data-testid='tenants-edit-metadata-live-region']").GetAttribute("aria-live").ShouldBe("polite");
        // A changed-value confirmation has no attempt-matched Ready receipt: the projection is confirmed, but
        // audit proof is reported as missing in-panel support rather than pending forever or available.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        cut.Find("[data-testid='tenants-edit-metadata-audit']").TextContent.ShouldContain("Missing implementation support");
        cut.Find("[data-testid='tenants-edit-metadata-audit']").TextContent.ShouldNotContain("Audit available");
    }

    [Fact]
    public void Confirmed_clear_to_null_description_shows_empty_state_and_not_the_ambient_detail_description()
    {
        string? currentProjectionVersion = "v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        // The component Detail parameter keeps a non-empty description; only projection evidence
        // proves the clear-to-null. The confirmed display must reflect the confirmed (empty) value,
        // never the still-populated ambient Detail.Description.
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Original description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.ProjectionVersionProvider, () => currentProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                currentProjectionVersion = "v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-description']").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot.LastConfirmedDescription.ShouldBeNull();
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("No description is confirmed");
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldNotContain("Original description");
    }

    [Fact]
    public void Deliberate_second_edit_after_confirmation_mints_a_new_message_id()
    {
        // Regression guard: reusing the confirmed attempt's messageId makes an idempotent command bus
        // dedupe the second edit away, so the new name is silently never applied while the UI reconciles
        // against the first command's status.
        string currentProjectionVersion = "v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Original description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.ProjectionVersionProvider, () => currentProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                currentProjectionVersion = "v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Alpha renamed");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.UpdateTenantCallCount.ShouldBe(1);

        // The editor stays open after confirmation, so the next edit is submitted straight from it.
        // A second, genuinely different edit is a new logical attempt and must not inherit the old identity.
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Alpha renamed twice");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.UpdateTenantCallCount.ShouldBe(2));
        NUlid.Ulid.TryParse(gateway.LastUpdateTenantMessageId, out _).ShouldBeTrue();
        gateway.LastUpdateTenantRequest.ShouldNotBeNull().Name.ShouldBe("Alpha renamed twice");
    }

    [Fact]
    public void New_attempt_uses_rendered_projection_version_instead_of_a_sticky_prior_proof()
    {
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            // The page has already advanced to v2, while its proof reader still retains v1 from an earlier attempt.
            .Add(p => p.ProjectionVersion, "projection-v2")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                // This proof only catches up to state that existed before submit. It must not qualify the attempt.
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBe("projection-v2");
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public void Exact_failed_attempt_retry_preserves_its_causal_baseline_and_start_when_ambient_projection_advances()
    {
        const string messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
        int submissionCount = 0;
        string proofProjectionVersion = "projection-v2";
        StubTenantCommandGateway gateway = new()
        {
            UpdateTenantSubmissionAsync = _ => Task.FromResult(++submissionCount == 1
                ? TenantCommandSubmissionResult.Failed("Submission outcome is indeterminate.") with
                {
                    MessageId = messageId,
                }
                : TenantCommandSubmissionResult.Accepted(messageId, "correlation-update")),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), proofProjectionVersion))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        NUlid.Ulid.TryParse(gateway.LastUpdateTenantMessageId, out _).ShouldBeTrue();
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBe("projection-v1");
        DateTimeOffset originalAttemptStartedAtUtc = cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldNotBeNull();

        // Unrelated aggregate activity advances the ambient page projection before the indeterminate
        // command is retried. Reusing the command id means this is still the original causal attempt.
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v2")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), proofProjectionVersion))));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.UpdateTenantCallCount.ShouldBe(2);
        gateway.LastUpdateTenantMessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.MessageId.ShouldBe(messageId);
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBe("projection-v1");
        cut.Instance.Snapshot.AttemptStartedAtUtc.ShouldBe(originalAttemptStartedAtUtc);
    }

    [Fact]
    public void Exact_unable_to_verify_resubmit_refreshes_existing_tracking_without_redispatch()
    {
        int statusCalls = 0;
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? new TenantCommandStatusResult(CommandStatus.TimedOut, "Status timed out.", HasVerifiedCommandIdentity: true)
                : new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        gateway.UpdateTenantCallCount.ShouldBe(1);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(2);
        cut.Instance.Snapshot.MessageId.ShouldBe("message-1");
    }

    [Fact]
    public void Exact_degraded_resubmit_refreshes_existing_tracking_without_redispatch()
    {
        int statusCalls = 0;
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? new TenantCommandStatusResult(CommandStatus.PublishFailed, "Publish failed.", HasVerifiedCommandIdentity: true)
                : new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Degraded));
        gateway.UpdateTenantCallCount.ShouldBe(1);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBe(2);
        cut.Instance.Snapshot.MessageId.ShouldBe("message-1");
    }

    [Fact]
    public void Same_metadata_submission_is_still_sent_but_null_projection_proof_fails_closed()
    {
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 0, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active))
            .Add(p => p.ProjectionEvidenceProvider, _ => Task.FromResult<TenantDetailSnapshot?>(Proof(null, "tenant-sequence:2"))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.AlreadyApplied);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");
        activity.ShouldBe([true]);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        // A null proof is a provenance failure, not a failed audit read: the zero-event command implied no audit
        // record, and the missing proof does not invent one.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
    }

    [Fact]
    public void Identical_metadata_confirms_from_the_exact_current_projection_backed_audit_row()
    {
        int auditProofCalls = 0;
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => "projection-v2")
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active))
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "projection-v2")))
            .Add(p => p.AuditEvidenceProvider, (request, messageId, attemptStartedAtUtc) =>
            {
                auditProofCalls++;
                messageId.ShouldBe("message-1");
                DateTimeOffset timestamp = attemptStartedAtUtc.ShouldNotBeNull();
                return Task.FromResult<TenantAuditRow?>(AuditProof(messageId!, request.TenantId, timestamp));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        auditProofCalls.ShouldBe(1);
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.AlreadyApplied);
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        activity.ShouldBe([true, false]);
    }

    [Fact]
    public void Projection_pending_without_an_evidence_provider_fails_closed_and_retains_exact_command_ownership()
    {
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("provenance could not be verified", Case.Insensitive);
        activity.ShouldBe([true]);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        // No proof reader is a provenance failure, not a failed audit read: the audit dimension keeps what the
        // Completed status established.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
    }

    [Fact]
    public void Match_without_version_advancement_fails_closed_to_unable_to_verify()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "v1"))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("update provenance could not be verified", Case.Insensitive);
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public void Reconciliation_fails_closed_when_authorization_is_revoked_after_acceptance()
    {
        int statusCalls = 0;
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)
                : new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));

        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsAuthorized, false)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.MissingPermission)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));
        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.SafeMessage.ShouldNotBeNull().ShouldContain("not authorized", Case.Insensitive);
    }

    [Fact]
    public void Reconciliation_fails_closed_when_projection_freshness_degrades_after_acceptance()
    {
        int statusCalls = 0;
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)
                : new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));

        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));
        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.SafeMessage.ShouldNotBeNull().ShouldContain("refresh current tenant detail", Case.Insensitive);

        // The projection surface could not confirm, and no audit read happened: the audit record stays pending.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
    }

    [Fact]
    public void Reconciliation_fails_closed_when_projection_lifecycle_regresses_after_acceptance()
    {
        int statusCalls = 0;
        string? proofProjectionVersion = "projection-v1";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)
                : new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));

        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Rebuilding)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => proofProjectionVersion)
            .Add(p => p.ProjectionEvidenceProvider, request =>
            {
                proofProjectionVersion = "projection-v2";
                return Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"));
            }));
        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        cut.Instance.Snapshot.SafeMessage.ShouldNotBeNull().ShouldContain("projection-confirmed lifecycle", Case.Insensitive);
    }

    [Fact]
    public void Audit_evidence_provider_exception_maps_to_localized_unable_to_verify_state()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => "projection-v2")
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "projection-v2")))
            .Add(p => p.AuditEvidenceProvider, (_, _, _) =>
                Task.FromException<TenantAuditRow?>(new InvalidOperationException("raw audit failure"))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.OperationalFailure");
        cut.Markup.ShouldNotContain("raw audit failure", Case.Insensitive);

        // A failed proof read never rewrites the audit dimension.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
    }

    [Fact]
    public void Projection_provider_exception_maps_to_localized_unable_to_verify_state()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionEvidenceProvider, _ => throw new InvalidOperationException("raw proof failure")));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.OperationalFailure");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("projection proof could not be verified", Case.Insensitive);
        cut.Markup.ShouldNotContain("raw proof failure", Case.Insensitive);

        // A failed projection read is projection truth only: the audit dimension keeps its pending record.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("pending");
    }

    [Fact]
    public void Status_provider_exception_maps_to_stage_specific_localized_unable_to_verify_state()
    {
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = _ => Task.FromException<TenantCommandStatusResult>(
                new InvalidOperationException("raw status failure")),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Status.UnableToVerify.OperationalFailure");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("command status could not be verified", Case.Insensitive);
        cut.Markup.ShouldNotContain("raw status failure", Case.Insensitive);
        activity.ShouldBe([true]);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        // Unlike a projection read, a failed status read after dispatch leaves the audit status unverifiable.
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operational_cancellation_propagates_instead_of_becoming_unable_to_verify(
        bool cancelDuringProjectionProof)
    {
        StubTenantCommandGateway gateway = new()
        {
            StatusAsync = _ => cancelDuringProjectionProof
                ? Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true))
                : Task.FromException<TenantCommandStatusResult>(new OperationCanceledException()),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionEvidenceProvider, _ => Task.FromException<TenantDetailSnapshot?>(
                new OperationCanceledException())));

        System.Reflection.FieldInfo snapshotField = typeof(EditTenantMetadataFlow)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        snapshotField.SetValue(cut.Instance, TenantUpdateMetadataCommandSnapshot
            .Idle("Alpha", "Tenant alpha description", Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .RequestSent(new UpdateTenant("tenant.alpha", "Updated", null), "projection-v1")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-1", "correlation-update")));

        System.Reflection.MethodInfo refresh = typeof(EditTenantMetadataFlow)
            .GetMethod("RefreshStatusAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await Should.ThrowAsync<OperationCanceledException>(() => cut.InvokeAsync(async () =>
        {
            await ((Task)refresh.Invoke(cut.Instance, null)!).ConfigureAwait(false);
        }));

        cut.Instance.Snapshot.State.ShouldBe(cancelDuringProjectionProof
            ? TenantCommandLifecycleState.ProjectionPending
            : TenantCommandLifecycleState.Accepted);
        cut.Instance.Snapshot.SafeMessageKey.ShouldBeNull();
    }

    [Fact]
    public void Missing_baseline_shows_localized_unable_to_verify_safe_message()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, (string?)null)
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "tenant-sequence:2"))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        cut.Instance.Snapshot.BaselineProjectionVersion.ShouldBeNull();
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingBaseline");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("pre-submit baseline", Case.Insensitive);
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public void In_flight_retry_with_tracking_reuses_status_lookup_and_does_not_dispatch_again()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1"));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.UpdateTenantCallCount.ShouldBe(1));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        int statusCallsAfterSubmit = gateway.StatusCallCount;

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => gateway.UpdateTenantCallCount.ShouldBe(1));
        gateway.StatusCallCount.ShouldBeGreaterThan(statusCallsAfterSubmit);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        NUlid.Ulid.TryParse(gateway.LastUpdateTenantMessageId, out _).ShouldBeTrue();
    }

    [Fact]
    public void In_flight_retry_without_tracking_blocks_with_in_flight_reason_and_does_not_dispatch_again()
    {
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        activity.ShouldContain(true);

        System.Reflection.FieldInfo snapshotField = typeof(EditTenantMetadataFlow)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        TenantUpdateMetadataCommandSnapshot snapshot = (TenantUpdateMetadataCommandSnapshot)snapshotField.GetValue(cut.Instance)!;
        TenantDetail confirmed = Detail("tenant.alpha", "Previously confirmed", "Confirmed description");
        snapshotField.SetValue(cut.Instance, snapshot with
        {
            MessageId = null,
            CorrelationId = null,
            LastConfirmedName = confirmed.Name,
            LastConfirmedDescription = confirmed.Description,
            LastConfirmedDetailProjection = confirmed,
        });
        cut.Render();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        gateway.UpdateTenantCallCount.ShouldBe(1);
        cut.Instance.Snapshot.MessageId.ShouldBe(snapshot.MessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBe(snapshot.CorrelationId);
        cut.Instance.Snapshot.LastConfirmedName.ShouldBe(snapshot.LastConfirmedName);
        activity.Last().ShouldBeTrue();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Fact]
    public async Task LostRendererTrackingIsRecoveredForTheOriginalStatusLookup()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1"));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));

        System.Reflection.FieldInfo snapshotField = typeof(EditTenantMetadataFlow)
            .GetField("_snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        TenantUpdateMetadataCommandSnapshot snapshot = (TenantUpdateMetadataCommandSnapshot)snapshotField.GetValue(cut.Instance)!;
        snapshotField.SetValue(cut.Instance, snapshot with { MessageId = null, CorrelationId = null });

        int statusCallsAfterLostTracking = gateway.StatusCallCount;
        System.Reflection.MethodInfo refresh = typeof(EditTenantMetadataFlow)
            .GetMethod("RefreshStatusAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await cut.InvokeAsync(async () =>
        {
            await ((Task)refresh.Invoke(cut.Instance, null)!).ConfigureAwait(false);
        });
        cut.Render();

        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
        cut.Instance.Snapshot.MessageId.ShouldBe(snapshot.MessageId);
        cut.Instance.Snapshot.CorrelationId.ShouldBe(snapshot.CorrelationId);
        gateway.UpdateTenantCallCount.ShouldBe(1);
        gateway.StatusCallCount.ShouldBeGreaterThan(statusCallsAfterLostTracking);
        TenantCommandFlowGuard.RetainsCommandActivity(cut.Instance.Snapshot.State).ShouldBeTrue();
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
    }

    [Theory]
    [InlineData(CommandStatus.Rejected, TenantCommandLifecycleState.Rejected, "rejected", "assertive")]
    [InlineData(CommandStatus.PublishFailed, TenantCommandLifecycleState.Degraded, "degraded", "assertive")]
    [InlineData(CommandStatus.TimedOut, TenantCommandLifecycleState.UnableToVerify, "Unable to verify", "assertive")]
    public void Status_refresh_keeps_terminal_lifecycle_states_distinct_and_support_safe(
        CommandStatus status,
        TenantCommandLifecycleState expectedState,
        string expectedText,
        string expectedLiveRegion)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(status, "Safe status message.", "SafeCode", HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);

        int projectionRefreshRequestedCount = 0;
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.OnProjectionRefreshRequested, () => projectionRefreshRequestedCount++)
            .Add(p => p.ProjectionEvidenceProvider, _ => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail("tenant.alpha", "Updated", null), "tenant-sequence:2"))));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(expectedState));
        cut.Find("[data-testid='tenants-edit-metadata-state']").TextContent.ShouldContain(expectedText, Case.Insensitive);
        cut.Find("[data-testid='tenants-edit-metadata-live-region']").GetAttribute("aria-live").ShouldBe(expectedLiveRegion);
        cut.Markup.ShouldNotContain("correlation-update", Case.Insensitive);
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("\"payload\"", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("bearer ", Case.Insensitive);
        cut.Instance.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);

        // Every terminal outcome -- not only ProjectionPending -- must still nudge the parent to re-query,
        // or the ambient tenant detail can go stale after a rejected/degraded/unable-to-verify command.
        projectionRefreshRequestedCount.ShouldBe(1);
    }

    [Fact]
    public void Gateway_submission_failure_remains_failed_assertive_and_support_safe()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed(
                "Metadata command submission failed. Retry from current tenant detail."),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-edit-metadata-state']").TextContent.ShouldContain("failed", Case.Insensitive);
        cut.Find("[data-testid='tenants-edit-metadata-live-region']").GetAttribute("aria-live").ShouldBe("assertive");
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("Retry from current tenant detail");
        cut.Markup.ShouldNotContain("raw payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("\"payload\"", Case.Insensitive);
        cut.Markup.ShouldNotContain("access_token", Case.Insensitive);
        cut.Markup.ShouldNotContain("bearer ", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation", Case.Insensitive);
    }

    [Fact]
    public void Gateway_submission_failure_preserves_and_localizes_safe_message_key()
    {
        const string key = "Tenants.Commands.Unavailable.InvalidTrackingReference";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.FailedWithKey(key),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
        cut.Instance.Snapshot.SafeMessage.ShouldBeNull();
        cut.Instance.Snapshot.SafeMessageKey.ShouldBe(key);
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldContain("tracking identifier", Case.Insensitive);
        cut.Markup.ShouldNotContain(key, Case.Sensitive);
    }

    [Fact]
    public void Missing_safe_message_key_falls_back_to_generic_localized_unable_to_verify_copy()
    {
        const string missingKey = "Tenants.EditMetadata.Unknown.DriftedKey";
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.FailedWithKey(missingKey),
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
        cut.Find("[data-testid='tenants-edit-metadata-safe-message']").TextContent
            .ShouldBe("Unable to verify the metadata command result.");
        cut.Markup.ShouldNotContain(missingKey, Case.Sensitive);
    }

    [Fact]
    public void Cancel_and_escape_close_editor_without_submitting_and_request_focus_return()
    {
        int closeCount = 0;
        StubTenantCommandGateway gateway = new();
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-cancel']").Click();
        cut.FindAll("[data-testid='tenants-edit-metadata-name']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-flow']").KeyDown("Escape");
        cut.FindAll("[data-testid='tenants-edit-metadata-name']").ShouldBeEmpty();

        closeCount.ShouldBe(2);
        gateway.UpdateTenantCallCount.ShouldBe(0);
    }

    [Fact]
    public void Metadata_styles_preserve_forced_colors_focus_and_status_shape_hooks()
    {
        string styles = File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Metadata",
            "EditTenantMetadataFlow.razor.css"));

        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain(":focus-visible");
        styles.ShouldContain("border-inline-start");
        styles.ShouldContain("tenants-edit-metadata__state--confirmed");
    }

    [Fact]
    public void Command_activity_callback_wraps_in_flight_submission_for_parent_locking()
    {
        TaskCompletionSource<TenantCommandSubmissionResult> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<bool> activity = [];
        StubTenantCommandGateway gateway = new()
        {
            UpdateTenantSubmissionAsync = _ => pending.Task,
        };
        RegisterServices(gateway);

        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.OnCommandActivityChanged, active => activity.Add(active)));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => activity.ShouldContain(true));

        pending.SetResult(TenantCommandSubmissionResult.Failed("Safe failure."));

        cut.WaitForAssertion(() => activity.ShouldContain(false));
    }

    [Theory]
    [InlineData("failed-after-send", TenantCommandLifecycleState.Failed, TenantCommandAuditState.AuditUnavailable)]
    [InlineData("ambiguous", TenantCommandLifecycleState.RequestSent, TenantCommandAuditState.AuditUnavailable)]
    [InlineData("rejected", TenantCommandLifecycleState.Rejected, TenantCommandAuditState.NotStarted)]
    public void Submission_outcomes_derive_the_audit_dimension_from_the_canonical_table(
        string outcome,
        TenantCommandLifecycleState expectedState,
        TenantCommandAuditState expectedAuditState)
    {
        // A failure reported after the message was sent carries its id and may have reached the server; a
        // rejection dispatched nothing that could be audited.
        StubTenantCommandGateway gateway = new()
        {
            Submission = outcome switch
            {
                "failed-after-send" => TenantCommandSubmissionResult.Failed("Metadata command submission failed.") with
                {
                    MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                },
                "ambiguous" => TenantCommandSubmissionResult.Ambiguous(
                    "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                    "Tenants.Commands.Unavailable.InvalidTrackingReference"),
                "rejected" => TenantCommandSubmissionResult.Rejected("Metadata command rejected.", "InsufficientPermissions"),
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(expectedState));
        cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").Count
            .ShouldBe(expectedAuditState is TenantCommandAuditState.NotStarted ? 0 : 1);

        // Without a correlation id nothing can be re-queried, so no Refresh recovery is offered.
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='refresh']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Refresh_is_offered_only_while_the_attempt_can_be_requeried(bool requeryable)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = requeryable
                ? TenantCommandSubmissionResult.Accepted("message-1", "correlation-update")
                : TenantCommandSubmissionResult.Failed("Metadata command submission failed.") with
                {
                    MessageId = "message-1",
                },
            Status = new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(requeryable
            ? TenantCommandAuditState.AuditPending
            : TenantCommandAuditState.AuditUnavailable));
        cut.WaitForAssertion(
            () => cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='refresh']").Count.ShouldBe(requeryable ? 1 : 0),
            TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']");
    }

    /// <summary>
    /// Verifies that an activity-lease refusal reports a retry, whose identity may already have reached the server,
    /// as unverifiable, and a first attempt, which sent nothing, as not started.
    /// </summary>
    /// <param name="isRetry">Whether the refused submission retries an ambiguous attempt.</param>
    /// <param name="expectedAuditState">The audit state the refusal must report.</param>
    [Theory]
    [InlineData(true, TenantCommandAuditState.AuditUnavailable)]
    [InlineData(false, TenantCommandAuditState.NotStarted)]
    public void ActivityLeaseRefusalReportsARetryAsUnverifiableAndAFirstAttemptAsNotStarted(
        bool isRetry,
        TenantCommandAuditState expectedAuditState)
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Metadata command submission failed.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        int acquisitions = 0;
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.CommandActivityLease, active => Task.FromResult(!active || (isRetry && ++acquisitions == 1))));
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");

        if (isRetry)
        {
            cut.Find("form").Submit();
            cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));
            cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        }

        // The retry reuses an identity that may already have reached the server; the refused lease blocks it
        // before dispatch, and the audit dimension reports the unknown status instead of "not started". A first
        // attempt refused before dispatch sent nothing, so no audit state is implied.
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            cut.Instance.Snapshot.SafeMessage.ShouldBe(
                "Another command is already in progress for this tenant, so this one was not submitted. Wait for its outcome, then try again.");
        });
        cut.Instance.Snapshot.AuditState.ShouldBe(expectedAuditState);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").Count
            .ShouldBe(isRetry ? 1 : 0);
        gateway.UpdateTenantCallCount.ShouldBe(isRetry ? 1 : 0);
    }

    [Fact]
    public void Continue_read_only_closes_the_editor_once_no_command_is_in_flight()
    {
        int closeCount = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true),
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => "projection-v2")
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, request.Name, request.Description), "projection-v2")))
            .Add(p => p.OnCloseRequested, () => closeCount++));

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport));

        // Continue read-only runs the Cancel close path: the editor closes and the evidence stays on screen.
        int focusCallsBeforeContinue = FocusCalls().Count;
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='continuereadonly']").Click();

        cut.FindAll("[data-testid='tenants-edit-metadata-name']").ShouldBeEmpty();
        closeCount.ShouldBe(1);

        // With the editor closed there is nothing left to continue from, so the recovery is withdrawn, and focus
        // returns to Open, which renders again.
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='continuereadonly']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-edit-metadata-open']");
        cut.WaitForAssertion(() => FocusCalls().Count.ShouldBeGreaterThan(focusCallsBeforeContinue), TimeSpan.FromSeconds(5));
        FocusCalls().Last().ShouldBe(ElementReferenceId(cut.Instance, "_openElement"));
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("missingsupport");
        gateway.UpdateTenantCallCount.ShouldBe(1);
    }

    [Fact]
    public void Continue_read_only_is_withheld_while_the_owned_command_is_in_flight()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Ambiguous(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "Tenants.Commands.Unavailable.InvalidTrackingReference"),
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();

        // The ambiguous submission keeps the attempt in flight with an unverifiable status: the control renders,
        // but Continue read-only would leave the in-flight attempt behind, so it is not offered.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));
        TenantCommandFlowGuard.RetainsCommandActivity(cut.Instance.Snapshot.State).ShouldBeTrue();
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']");
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='continuereadonly']").ShouldBeEmpty();
    }

    [Fact]
    public void Closing_the_editor_while_open_cannot_render_moves_focus_to_the_lifecycle_section()
    {
        RegisterServices(new StubTenantCommandGateway());
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();

        // The host re-renders with stale freshness while the editor is open, so Open can no longer render.
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(p => p.Freshness, ReadModelFreshnessState.Stale)
            .Add(p => p.ProjectionVersion, "v1"));
        int focusCallsBeforeCancel = FocusCalls().Count;

        cut.Find("[data-testid='tenants-edit-metadata-cancel']").Click();

        cut.FindAll("[data-testid='tenants-edit-metadata-name']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        cut.WaitForAssertion(() => FocusCalls().Count.ShouldBeGreaterThan(focusCallsBeforeCancel), TimeSpan.FromSeconds(5));
        FocusCalls().Last().ShouldBe(ElementReferenceId(cut.Instance, "_lifecycleElement"));
    }

    [Fact]
    public async Task Refresh_clicks_merged_into_a_running_lookup_wait_for_it_then_run_their_own_and_never_exhaust_the_retry_limit()
    {
        TaskCompletionSource lookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusCalls = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = async _ =>
            {
                if (Interlocked.Increment(ref statusCalls) == 2)
                {
                    lookupStarted.SetResult();
                    await releaseLookup.Task.ConfigureAwait(false);
                }

                return new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true);
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Detail(request.TenantId, "Alpha", "Tenant alpha description"), "tenant-sequence:2"))));
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending));
        const string refresh = "[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='refresh']";
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        // The editor's Refresh starts a slow lookup; the shared control's Refresh then merges into it.
        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());
        cut.Find(refresh).Click();
        cut.Find(refresh).Click();

        // The merged click waits for the running lookup; nothing counted as an unchanged retry yet.
        cut.Find(refresh);
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();

        releaseLookup.SetResult();
        cut.WaitForAssertion(() => cut.FindAll(refresh).ShouldHaveSingleItem(), TimeSpan.FromSeconds(5));
        cut.FindAll("[data-testid='tenants-audit-availability-retry-limit']").ShouldBeEmpty();
        // The editor's lookup started before the click, so the merged click runs one lookup of its own after it:
        // a click is never counted as a retry that no lookup served. Three clicks while it waited are one request.
        cut.WaitForAssertion(() => Volatile.Read(ref statusCalls).ShouldBe(3), TimeSpan.FromSeconds(5));

        // Once the merged refresh has settled, no later lookup followed the one it ran.
        await merged.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref statusCalls).ShouldBe(3);
        gateway.UpdateTenantCallCount.ShouldBe(1);
    }

    [Fact]
    public void Retry_whose_command_gateway_disappears_after_the_lease_reports_the_possibly_delivered_attempt_as_unverifiable()
    {
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Failed("Metadata command submission failed.") with
            {
                MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            },
        };
        RegisterServices(gateway);
        bool gatewayAvailable = true;
        Services.AddTransient<ITenantCommandGateway>(_ => gatewayAvailable ? gateway : null!);
        int acquisitions = 0;
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1")
            .Add(p => p.CommandActivityLease, active =>
            {
                // The retry takes the lease, and the command surface disappears before dispatch.
                if (active && ++acquisitions == 2)
                {
                    gatewayAvailable = false;
                }

                return Task.FromResult(true);
            }));
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed));

        cut.Find("form").Submit();

        // The blocked retry reuses an identity that may already have reached the server: its status is unknown.
        cut.WaitForAssertion(() =>
        {
            cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify);
            cut.Instance.Snapshot.SafeMessage.ShouldBe("Tenant command support is unavailable.");
        });
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        gateway.UpdateTenantCallCount.ShouldBe(1);
    }

    [Fact]
    public void Audit_refresh_that_resolves_to_not_started_moves_focus_to_the_lifecycle_section()
    {
        int statusCalls = 0;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            // The first lookup cannot be read; the user's Refresh then finds the command still processing.
            StatusAsync = _ => Task.FromResult(++statusCalls == 1
                ? TenantCommandStatusResult.Unknown("Command status is unavailable.")
                : new TenantCommandStatusResult(CommandStatus.Processing, HasVerifiedCommandIdentity: true)),
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));
        int focusCallsBeforeRefresh = FocusCalls().Count;

        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='refresh']").Click();

        // The polite NotStarted state unmounts the control and its focused Refresh button, so focus lands on the
        // lifecycle section instead of falling back to the document body.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.NotStarted));
        cut.Instance.Snapshot.LiveRegionPoliteness.ShouldBe(TenantCommandLiveRegionPoliteness.Polite);
        cut.FindAll("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']").ShouldBeEmpty();
        cut.WaitForAssertion(() => FocusCalls().Count.ShouldBeGreaterThan(focusCallsBeforeRefresh), TimeSpan.FromSeconds(5));
        FocusCalls().Last().ShouldBe(ElementReferenceId(cut.Instance, "_lifecycleElement"));
    }

    [Fact]
    public void Retry_failed_before_dispatch_keeps_the_reused_identity_unverifiable()
    {
        int submissions = 0;
        StubTenantCommandGateway gateway = new()
        {
            // The first attempt failed after the message was sent; the retry of that identity then fails before
            // dispatch and reports no message id of its own.
            UpdateTenantSubmissionAsync = _ => Task.FromResult(++submissions == 1
                ? TenantCommandSubmissionResult.Failed("Metadata command submission failed.") with
                {
                    MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                }
                : TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference")),
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderEditableFlow();
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable));

        cut.Find("form").Submit();

        // The retry sent nothing, but the earlier attempt with the same identity may have reached the server: the
        // audit dimension stays unknown instead of disappearing as "not started".
        cut.WaitForAssertion(() => gateway.UpdateTenantCallCount.ShouldBe(2));
        gateway.LastUpdateTenantMessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Failed);
        cut.Instance.Snapshot.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditUnavailable);
        cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("unavailable");
    }

    [Fact]
    public async Task A_refresh_merged_into_a_lookup_that_confirms_the_attempt_runs_no_follow_up_lookup()
    {
        TaskCompletionSource lookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusCalls = 0;
        bool projected = false;
        StubTenantCommandGateway gateway = new()
        {
            Submission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-update"),
            StatusAsync = async _ =>
            {
                if (Interlocked.Increment(ref statusCalls) == 2)
                {
                    lookupStarted.SetResult();
                    await releaseLookup.Task.ConfigureAwait(false);
                    return new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true);
                }

                return new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true);
            },
        };
        RegisterServices(gateway);
        IRenderedComponent<EditTenantMetadataFlow> cut = RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "projection-v1")
            .Add(p => p.ProjectionVersionProvider, () => Volatile.Read(ref projected) ? "projection-v2" : "projection-v1")
            .Add(p => p.ProjectionEvidenceProvider, request => Task.FromResult<TenantDetailSnapshot?>(Proof(Volatile.Read(ref projected)
                ? Detail(request.TenantId, request.Name, request.Description)
                : Detail(request.TenantId, "Alpha", "Tenant alpha description"), Volatile.Read(ref projected) ? "projection-v2" : "projection-v1"))));
        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Updated");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending));
        const string refresh = "[data-testid='tenants-edit-metadata-audit'] [data-recovery-verb='refresh']";
        cut.WaitForAssertion(() => cut.Find(refresh), TimeSpan.FromSeconds(5));

        // The editor's Refresh starts a slow lookup; the shared control's Refresh merges into it and waits.
        cut.Find("[data-testid='tenants-edit-metadata-refresh']").Click();
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task merged = cut.Find(refresh).ClickAsync(new MouseEventArgs());

        // The running lookup completes and its projection read confirms the attempt.
        Volatile.Write(ref projected, true);
        releaseLookup.SetResult();
        await merged.WaitAsync(TimeSpan.FromSeconds(5));

        // A confirmed attempt is no longer refreshable: the merged request runs no lookup of its own, so the
        // confirmation is never re-applied as projection-pending and confirmed a second time.
        cut.WaitForAssertion(() => cut.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        cut.Instance.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        Volatile.Read(ref statusCalls).ShouldBe(2);
        gateway.StatusCallCount.ShouldBe(2);
        gateway.UpdateTenantCallCount.ShouldBe(1);
    }

    private List<string> FocusCalls()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.FirstOrDefault())
            .OfType<ElementReference>()
            .Select(reference => reference.Id ?? string.Empty)];

    private static string ElementReferenceId(EditTenantMetadataFlow flow, string fieldName)
        => ((ElementReference)typeof(EditTenantMetadataFlow)
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(flow)!).Id;

    private IRenderedComponent<EditTenantMetadataFlow> RenderEditableFlow()
        => RenderMetadata(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Detail, Detail("tenant.alpha", "Alpha", "Tenant alpha description"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.ProjectionVersion, "v1"));

    private IRenderedComponent<EditTenantMetadataFlow> RenderMetadata(Action<ComponentParameterCollectionBuilder<EditTenantMetadataFlow>> parameters)
        => Render<CascadingValue<string>>(p => p.Add(x => x.Name, "CurrentTenantCallerScope")
            .Add(x => x.Value, "test-caller").AddChildContent(parameters)).FindComponent<EditTenantMetadataFlow>();

    private void RegisterServices(StubTenantCommandGateway gateway)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(gateway);
    }

    private static TenantDetail Detail(string tenantId, string name, string? description)
        => new(
            tenantId,
            name,
            description,
            TenantStatus.Active,
            [new TenantMember("owner-user", TenantRole.TenantOwner)],
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));

    private static TenantAuditRow AuditProof(string messageId, string tenantId, DateTimeOffset timestamp)
        => new(
            messageId,
            "TenantUpdated",
            AuditEventCategory.Administrative,
            "operator-user",
            timestamp,
            tenantId,
            tenantId,
            tenantId,
            "TenantUpdated",
            string.Empty,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked);

    private static string ProjectRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Hexalith.Tenants.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate project root.");
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public TenantCommandSubmissionResult Submission { get; init; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TenantCommandStatusResult Status { get; init; }
            = TenantCommandStatusResult.Unknown("Command status is unavailable.");

        public Func<UpdateTenant, Task<TenantCommandSubmissionResult>>? UpdateTenantSubmissionAsync { get; init; }

        public Func<TenantCommandTrackingHandle, Task<TenantCommandStatusResult>>? StatusAsync { get; init; }

        public UpdateTenant? LastUpdateTenantRequest { get; private set; }

        public string? LastUpdateTenantMessageId { get; private set; }

        public int UpdateTenantCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
        {
            UpdateTenantCallCount++;
            LastUpdateTenantRequest = request;
            LastUpdateTenantMessageId = messageId;
            return UpdateTenantSubmissionAsync is null ? Task.FromResult(Submission) : UpdateTenantSubmissionAsync(request);
        }

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            handle.AggregateId.ShouldNotBeNullOrWhiteSpace();
            handle.ExpectedDomain.ShouldBe(TenantIdentity.Domain);
            StatusCallCount++;
            return StatusAsync is null ? Task.FromResult(Status) : StatusAsync(handle);
        }
    }

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            ["Tenants.EditMetadata.Title"] = "Tenant metadata",
            ["Tenants.EditMetadata.Description"] = "Edit the confirmed metadata for tenant {0} through a command and projection-confirmed refresh.",
            ["Tenants.EditMetadata.Open"] = "Edit metadata",
            ["Tenants.EditMetadata.ConfirmedName.Label"] = "Last confirmed name",
            ["Tenants.EditMetadata.ConfirmedDescription.Label"] = "Last confirmed description",
            ["Tenants.EditMetadata.Name.Label"] = "Name",
            ["Tenants.EditMetadata.Name.Help"] = "Use the tenant display name to submit with this command.",
            ["Tenants.EditMetadata.Description.Label"] = "Description",
            ["Tenants.EditMetadata.Description.Help"] = "Leave empty to clear the tenant description.",
            ["Tenants.EditMetadata.Description.Empty"] = "No description is confirmed.",
            ["Tenants.EditMetadata.Submit"] = "Submit metadata update",
            ["Tenants.EditMetadata.Refresh"] = "Refresh status",
            ["Tenants.EditMetadata.Cancel"] = "Cancel",
            ["Tenants.EditMetadata.Lifecycle.Title"] = "Metadata command lifecycle",
            ["Tenants.EditMetadata.Validation.NameRequired"] = "Enter the complete tenant name before submitting metadata changes.",
            ["Tenants.EditMetadata.Unavailable.Authorization"] = "You are not authorized to edit this tenant's metadata.",
            ["Tenants.EditMetadata.Unavailable.Freshness"] = "Refresh current tenant detail before editing metadata.",
            ["Tenants.EditMetadata.Unavailable.ProjectionLifecycle"] = "Editing metadata requires a current, projection-confirmed lifecycle.",
            ["Tenants.EditMetadata.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow metadata editing.",
            ["Tenants.EditMetadata.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.EditMetadata.Unavailable.InFlight"] = "A tenant command is already in progress.",
            ["Tenants.EditMetadata.Unavailable.Identity"] = "Tenant identity is unavailable, so metadata editing fails closed.",
            ["Tenants.Commands.Unavailable.AggregateInFlight"] = "Another command is already in progress for this tenant, so this one was not submitted. Wait for its outcome, then try again.",
            ["Tenants.Commands.Unavailable.InvalidTrackingReference"] = "This command could not be submitted because its tracking identifier was not valid. Refresh the tenant and start the action again.",
            ["Tenants.EditMetadata.State.Idle"] = "No metadata command submitted.",
            ["Tenants.EditMetadata.State.RequestSent"] = "Metadata update request sent.",
            ["Tenants.EditMetadata.State.Accepted"] = "Accepted by EventStore; waiting for metadata processing.",
            ["Tenants.EditMetadata.State.ProjectionPending"] = "Projection pending; submitted metadata is not confirmed visible yet.",
            ["Tenants.EditMetadata.State.Confirmed"] = "Projection confirmed the submitted metadata.",
            ["Tenants.EditMetadata.State.Rejected"] = "Metadata update command rejected.",
            ["Tenants.EditMetadata.State.AlreadyApplied"] = "Already applied is not used for metadata updates.",
            ["Tenants.EditMetadata.State.DuplicatePrevented"] = "Duplicate metadata submission prevented.",
            ["Tenants.EditMetadata.State.Failed"] = "Metadata command submission failed.",
            ["Tenants.EditMetadata.State.Degraded"] = "Metadata command result is degraded and needs review.",
            ["Tenants.EditMetadata.State.UnableToVerify"] = "Unable to verify the metadata command result.",
            ["Tenants.EditMetadata.Confirm.UnableToVerify.MissingBaseline"] = "Metadata projection matched without a pre-submit baseline, so this attempt cannot be confirmed.",
            ["Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance"] = "Metadata projection matches the request, but update provenance could not be verified. Refresh status or continue read-only.",
            ["Tenants.EditMetadata.Status.UnableToVerify.OperationalFailure"] = "Command status could not be verified. Retry status lookup or continue read-only.",
            ["Tenants.EditMetadata.Confirm.UnableToVerify.OperationalFailure"] = "Metadata projection proof could not be verified. Refresh status or continue read-only.",
            ["Tenants.Audit.EntryPoint.Accessible.Command"] = "Inspect audit for tenant {1} ({0})",
            ["Tenants.Audit.EntryPoint.CommandReason"] = "Command-specific proof is not available here; open the tenant audit list and use the visible audit state.",
            ["Tenants.Audit.EntryPoint.Label"] = "Audit evidence",
            ["Tenants.Audit.EntryPoint.Unavailable.ScopeRequired"] = "Tenant scope is required before audit evidence can be opened.",
            ["Tenants.Audit.EntryPoint.Unavailable.StaleScope"] = "Refresh tenant scope before opening audit evidence.",
            ["Tenants.Audit.Availability.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Audit.Availability.Action.InspectAudit"] = "Inspect audit",
            ["Tenants.Audit.Availability.Action.Refresh"] = "Retry status lookup",
            ["Tenants.Audit.Availability.ActionsLabel"] = "Audit availability recovery actions",
            ["Tenants.Audit.Availability.Reason.MissingSupport"] = "In-panel audit verification is not available for this command, so this panel cannot match an audit record to the attempt. The recorded outcome above is unchanged.",
            ["Tenants.Audit.Availability.Reason.Unavailable"] = "The audit status could not be read or verified after the command was sent. This does not mean the record does not exist, and no proof is claimed.",
            ["Tenants.Audit.Availability.State.Delayed"] = "Audit delayed",
            ["Tenants.Audit.Availability.State.MissingSupport"] = "Missing implementation support",
            ["Tenants.Audit.Availability.State.Pending"] = "Audit pending",
            ["Tenants.Audit.Availability.State.Unavailable"] = "Audit unavailable",
            ["Tenants.EditMetadata.Recovery.Idle"] = "Open the form when current projection evidence is available.",
            ["Tenants.EditMetadata.Recovery.RequestSent"] = "Wait for command status and projection refresh.",
            ["Tenants.EditMetadata.Recovery.Accepted"] = "Wait, refresh status, or continue read-only until projection confirms the metadata.",
            ["Tenants.EditMetadata.Recovery.ProjectionPending"] = "Refresh the tenant detail; do not display success until submitted metadata is confirmed.",
            ["Tenants.EditMetadata.Recovery.Confirmed"] = "Continue read-only or inspect audit when evidence becomes available.",
            ["Tenants.EditMetadata.Recovery.Rejected"] = "Refresh projection evidence, request permission, start correction, or escalate.",
            ["Tenants.EditMetadata.Recovery.Failed"] = "Retry after checking current projection evidence or escalate.",
            ["Tenants.EditMetadata.Recovery.Degraded"] = "Wait, retry status lookup, inspect audit when available, or escalate.",
            ["Tenants.EditMetadata.Recovery.UnableToVerify"] = "Refresh, retry status lookup, continue read-only, or escalate.",
            ["Tenants.Audit.Availability.State.Available"] = "Audit available",
            ["Tenants.Audit.Availability.Reason.Pending"] = "The command's events are stored, but its audit record is not readable yet. It normally appears shortly, and no proof is claimed until it does.",
            ["Tenants.Audit.Availability.Reason.Delayed"] = "The audit record is taking longer than expected to become readable. No proof is claimed until it can be read.",
            ["Tenants.Audit.Availability.RetryLimit"] = "Repeated retries left this state unchanged, so retrying is no longer offered here.",
            ["Tenants.Audit.Receipt.Availability.Unavailable.Reason"] = "This audit read could not verify the requested evidence. This does not mean the record does not exist, and the recorded outcome is unchanged.",
            ["Tenants.Audit.Recovery.Action.Escalate"] = "Escalate without diagnostics",
        };

        public LocalizedString this[string name]
        {
            get
            {
                bool found = Values.TryGetValue(name, out string? value);
                return new(name, found ? value! : name, resourceNotFound: !found);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                bool found = Values.TryGetValue(name, out string? value);
                return new(
                    name,
                    string.Format(CultureInfo.CurrentCulture, found ? value! : name, arguments),
                    resourceNotFound: !found);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Values.Select(v => new LocalizedString(v.Key, v.Value));
    }
}
