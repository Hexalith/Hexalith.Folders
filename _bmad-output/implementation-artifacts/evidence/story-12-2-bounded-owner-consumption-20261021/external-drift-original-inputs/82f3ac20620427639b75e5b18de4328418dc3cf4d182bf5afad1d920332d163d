using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.FrontComposer.Contracts.Lifecycle;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;

namespace Hexalith.Tenants.UI.Services.Gateways;

internal sealed class TenantCommandGateway(
    IEventStoreGatewayClient gatewayClient,
    IUlidFactory ulidFactory,
    HttpClient statusClient) : ITenantCommandGateway {
    private const string SystemTenant = "system";
    private const string TenantsDomain = "tenants";
    private const string GlobalAdministratorsDomain = "global-administrators";
    private const string GlobalAdministratorsAggregateId = "global-administrators";

    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public bool SupportsGlobalAdministratorDispatch => true;

    public bool SupportsTrackedGlobalAdministratorDispatch => true;

    public bool SupportsTrackedGlobalAdministratorRemoveDispatch => true;

    public bool SupportsCommandStatusLookup => true;

    public bool SupportsTrackedLifecycleDispatch => true;

    public bool SupportsTrackedSetConfigurationDispatch => true;

    public bool SupportsTrackedRemoveConfigurationDispatch => true;

    public async Task<TenantCommandSubmissionResult> CreateTenantAsync(
        CreateTenant request,
        string? messageId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Name)) {
            return TenantCommandSubmissionResult.Failed("Tenant id and name are required before the command can be submitted.") with { SafeMessageKey = "Tenants.Create.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(CreateTenant),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (EventStoreGatewayException ex) {
            return MapGatewayException(ex) with { MessageId = resolvedMessageId };
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or TimeoutException) {
            return TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.Create.Submission.Failed", MessageId = resolvedMessageId, IsAmbiguousFailure = true };
        }
    }

    public async Task<TenantCommandSubmissionResult> AddUserToTenantAsync(
        AddUserToTenant request,
        string? messageId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.UserId)
            || !IsAssignableTenantRole(request.Role)) {
            return TenantCommandSubmissionResult.Failed("Tenant id, user id, and role are required before the command can be submitted.") with { SafeMessageKey = "Tenants.AddMember.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(AddUserToTenant),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (EventStoreGatewayException ex) {
            return MapAddUserToTenantGatewayException(ex) with { MessageId = resolvedMessageId };
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or TimeoutException) {
            return TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.AddMember.Submission.Failed", MessageId = resolvedMessageId, IsAmbiguousFailure = true };
        }
    }

    public async Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(
        ChangeUserRole request,
        string? messageId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.UserId)
            || !IsAssignableTenantRole(request.NewRole)) {
            return TenantCommandSubmissionResult.Failed("Tenant id, user id, and new role are required before the command can be submitted.") with { SafeMessageKey = "Tenants.ChangeRole.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(ChangeUserRole),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (EventStoreGatewayException ex) {
            return MapChangeUserRoleGatewayException(ex) with { MessageId = resolvedMessageId };
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or TimeoutException) {
            return TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.ChangeRole.Submission.Failed", MessageId = resolvedMessageId, IsAmbiguousFailure = true };
        }
    }

    public async Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(
        RemoveUserFromTenant request,
        string? messageId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.UserId)) {
            return TenantCommandSubmissionResult.Failed("Tenant id and user id are required before the command can be submitted.") with { SafeMessageKey = "Tenants.RemoveMember.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(RemoveUserFromTenant),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (EventStoreGatewayException ex) {
            return MapRemoveUserFromTenantGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public async Task<TenantCommandSubmissionResult> UpdateTenantAsync(
        UpdateTenant request,
        string? messageId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Name)) {
            return TenantCommandSubmissionResult.Failed("Tenant id and name are required before the command can be submitted.") with { SafeMessageKey = "Tenants.EditMetadata.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(UpdateTenant),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (EventStoreGatewayException ex) {
            return MapUpdateTenantGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(
        SetTenantConfiguration request,
        CancellationToken cancellationToken = default)
        => SetTenantConfigurationTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> SetTenantConfigurationTrackedAsync(
        SetTenantConfiguration request,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.Key)
            || request.Value is null) {
            return TenantCommandSubmissionResult.Failed("Tenant id, configuration key, and value are required before the command can be submitted.") with { SafeMessageKey = "Tenants.Configuration.Set.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(SetTenantConfiguration),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken)) {
            return TenantCommandSubmissionResult.Ambiguous(
                resolvedMessageId,
                "Tenants.Configuration.Set.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex) {
            return MapSetTenantConfigurationGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(
        RemoveTenantConfiguration request,
        CancellationToken cancellationToken = default)
        => RemoveTenantConfigurationTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> RemoveTenantConfigurationTrackedAsync(
        RemoveTenantConfiguration request,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.Key)) {
            return TenantCommandSubmissionResult.Failed("Tenant id and configuration key are required before the command can be submitted.") with { SafeMessageKey = "Tenants.Configuration.Remove.Validation.RequiredFields" };
        }

        if (!TryResolveMessageId(messageId, out string resolvedMessageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(RemoveTenantConfiguration),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken)) {
            return TenantCommandSubmissionResult.Ambiguous(
                resolvedMessageId,
                "Tenants.Configuration.Remove.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex) {
            return MapRemoveTenantConfigurationGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public Task<TenantCommandSubmissionResult> SetGlobalAdministratorAsync(
        SetGlobalAdministrator request,
        CancellationToken cancellationToken = default)
        => SetGlobalAdministratorTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> SetGlobalAdministratorTrackedAsync(
        SetGlobalAdministrator request,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (!GlobalAdministratorUserId.IsSupported(request.UserId)) {
            return TenantCommandSubmissionResult.FailedWithKey(
                "Tenants.GlobalAdministrators.Grant.Validation.UserIdInvalid");
        }

        if (!TryValidateCallerMessageId(messageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var submit = new SubmitCommandRequest(
            messageId,
            SystemTenant,
            GlobalAdministratorsDomain,
            GlobalAdministratorsAggregateId,
            nameof(SetGlobalAdministrator),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(messageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken)) {
            return TenantCommandSubmissionResult.Ambiguous(
                messageId,
                "Tenants.GlobalAdministrators.Grant.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex) {
            return MapSetGlobalAdministratorGatewayException(ex) with { MessageId = messageId };
        }
    }

    public Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorAsync(
        RemoveGlobalAdministrator request,
        CancellationToken cancellationToken = default)
        => RemoveGlobalAdministratorTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> RemoveGlobalAdministratorTrackedAsync(
        RemoveGlobalAdministrator request,
        string messageId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (!GlobalAdministratorUserId.IsSupported(request.UserId)) {
            return TenantCommandSubmissionResult.FailedWithKey(
                "Tenants.GlobalAdministrators.Remove.Preview.Unavailable.Target");
        }

        if (!TryValidateCallerMessageId(messageId)) {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var submit = new SubmitCommandRequest(
            messageId,
            SystemTenant,
            GlobalAdministratorsDomain,
            GlobalAdministratorsAggregateId,
            nameof(RemoveGlobalAdministrator),
            JsonSerializer.SerializeToElement(request));

        try {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(messageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken)) {
            return TenantCommandSubmissionResult.Ambiguous(
                messageId,
                "Tenants.GlobalAdministrators.Remove.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex) {
            return MapRemoveGlobalAdministratorGatewayException(ex) with { MessageId = messageId };
        }
    }

    public Task<TenantCommandSubmissionResult> EnableTenantAsync(
        TenantLifecycleCommandRequest request,
        CancellationToken cancellationToken = default)
        => EnableTenantTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> EnableTenantTrackedAsync(
        TenantLifecycleCommandRequest request,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || request.Operation is not TenantLifecycleOperation.EnableTenant)
        {
            return TenantCommandSubmissionResult.Failed("Tenant id and lifecycle operation are required before the command can be submitted.") with { SafeMessageKey = "Tenants.Lifecycle.Validation.RequiredFields" };
        }

        if (string.IsNullOrWhiteSpace(messageId)
            || !TryResolveMessageId(messageId, out string resolvedMessageId))
        {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var command = new EnableTenant(request.TenantId);
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(EnableTenant),
            JsonSerializer.SerializeToElement(command));

        try
        {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken))
        {
            return TenantCommandSubmissionResult.Ambiguous(
                resolvedMessageId,
                "Tenants.Lifecycle.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex)
        {
            return MapLifecycleGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public Task<TenantCommandSubmissionResult> DisableTenantAsync(
        TenantLifecycleCommandRequest request,
        CancellationToken cancellationToken = default)
        => DisableTenantTrackedAsync(request, ulidFactory.NewUlid(), cancellationToken);

    public async Task<TenantCommandSubmissionResult> DisableTenantTrackedAsync(
        TenantLifecycleCommandRequest request,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || request.Operation is not TenantLifecycleOperation.DisableTenant)
        {
            return TenantCommandSubmissionResult.Failed("Tenant id and lifecycle operation are required before the command can be submitted.") with { SafeMessageKey = "Tenants.Lifecycle.Validation.RequiredFields" };
        }

        if (string.IsNullOrWhiteSpace(messageId)
            || !TryResolveMessageId(messageId, out string resolvedMessageId))
        {
            return TenantCommandSubmissionResult.FailedWithKey("Tenants.Commands.Unavailable.InvalidTrackingReference");
        }

        var command = new DisableTenant(request.TenantId);
        var submit = new SubmitCommandRequest(
            resolvedMessageId,
            SystemTenant,
            TenantsDomain,
            request.TenantId,
            nameof(DisableTenant),
            JsonSerializer.SerializeToElement(command));

        try
        {
            SubmitCommandResponse response = await gatewayClient
                .SubmitCommandAsync(submit, cancellationToken)
                .ConfigureAwait(false);

            return TenantCommandSubmissionResult.Accepted(resolvedMessageId, response.CorrelationId);
        }
        catch (Exception exception) when (IsAmbiguousTrackedSubmission(exception, cancellationToken))
        {
            return TenantCommandSubmissionResult.Ambiguous(
                resolvedMessageId,
                "Tenants.Lifecycle.SubmissionEvidence.Ambiguous");
        }
        catch (EventStoreGatewayException ex)
        {
            return MapLifecycleGatewayException(ex) with { MessageId = resolvedMessageId };
        }
    }

    public async Task<TenantCommandStatusResult> GetStatusAsync(
        TenantCommandTrackingHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        string expectedDomain = handle.ExpectedDomain
            ?? (string.Equals(handle.AggregateId, GlobalAdministratorsAggregateId, StringComparison.Ordinal)
                ? GlobalAdministratorsDomain : TenantsDomain);
        bool globalAdministratorStatus = string.Equals(
            expectedDomain,
            GlobalAdministratorsDomain,
            StringComparison.Ordinal);

        try
        {
            using HttpResponseMessage response = await statusClient
                .GetAsync($"api/v1/commands/status/{Uri.EscapeDataString(handle.MessageId)}", cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return TenantCommandStatusResult.Pending("Command status is not available yet.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.Status.Pending"
                        : "Tenants.Commands.Status.Pending",
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return (IsRetryableStatusCode(response.StatusCode)
                    ? TenantCommandStatusResult.RetryableFailure("Command status could not be verified yet.")
                    : TenantCommandStatusResult.Unknown("Command status request was rejected.")) with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                        : (IsRetryableStatusCode(response.StatusCode)
                            ? "Tenants.Commands.Status.RetryableFailure"
                            : "Tenants.Commands.Status.RequestRejected"),
                };
            }

            TenantCommandStatusResponse? status = await response.Content
                .ReadFromJsonAsync<TenantCommandStatusResponse>(WebJsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (status is null)
            {
                return TenantCommandStatusResult.RetryableFailure("Command status response was unavailable.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                        : "Tenants.Commands.Status.ResponseUnavailable",
                };
            }

            if (string.IsNullOrWhiteSpace(status.CorrelationId)
                || !string.Equals(status.CorrelationId, handle.CorrelationId, StringComparison.Ordinal))
            {
                return TenantCommandStatusResult.Unknown("Command status response did not match the tracked command.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"
                        : "Tenants.Commands.Status.TrackingMismatch",
                };
            }

            if (!Enum.TryParse(status.Status, ignoreCase: false, out CommandStatus parsedStatus))
            {
                return TenantCommandStatusResult.RetryableFailure("Command status response was unavailable.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                        : "Tenants.Commands.Status.ResponseUnavailable",
                };
            }

            if (!Enum.IsDefined(parsedStatus))
            {
                return TenantCommandStatusResult.Unknown("Command status response was unavailable.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                        : "Tenants.Commands.Status.ResponseUnavailable",
                };
            }

            if (status.StatusCode is { } statusCode && statusCode != (int)parsedStatus
                || status.TenantId is not null && !string.Equals(status.TenantId, SystemTenant, StringComparison.Ordinal)
                || status.Domain is not null && !string.Equals(status.Domain, expectedDomain, StringComparison.Ordinal))
            {
                return TenantCommandStatusResult.Unknown("Command status response did not match the tracked command.") with
                {
                    SafeMessageKey = globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"
                        : "Tenants.Commands.Status.TrackingMismatch",
                };
            }

            bool hasVerifiedCommandIdentity = !string.IsNullOrWhiteSpace(status.MessageId)
                && string.Equals(status.MessageId, handle.MessageId, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(handle.AggregateId)
                    || string.Equals(status.AggregateId, handle.AggregateId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(handle.AggregateId) && !hasVerifiedCommandIdentity)
            {
                return new TenantCommandStatusResult(
                    parsedStatus,
                    "Command status response did not match the tracked lifecycle command.",
                    EventCount: status.EventCount,
                    HasVerifiedCommandIdentity: false,
                    SafeMessageKey: globalAdministratorStatus
                        ? "Tenants.GlobalAdministrators.Grant.UnableToVerify.TrackingMismatch"
                        : "Tenants.Commands.Status.TrackingMismatch");
            }

            (string? safeMessage, string? safeMessageKey) = SafeMessageForStatus(
                parsedStatus, status.RejectionEventType, status.FailureReason);
            return new TenantCommandStatusResult(
                parsedStatus,
                safeMessage,
                SafeRejectionCode(status.RejectionEventType),
                status.EventCount,
                HasVerifiedCommandIdentity: hasVerifiedCommandIdentity,
                SafeMessageKey: globalAdministratorStatus
                    ? parsedStatus switch
                    {
                        CommandStatus.Rejected => "Tenants.GlobalAdministrators.Grant.Status.Rejected",
                        CommandStatus.PublishFailed => "Tenants.GlobalAdministrators.Grant.Status.PublishFailed",
                        CommandStatus.TimedOut => "Tenants.GlobalAdministrators.Grant.Status.TimedOut",
                        _ => safeMessageKey,
                    }
                    : safeMessageKey) {
                        CommittedEventSequence = hasVerifiedCommandIdentity
                            && parsedStatus is CommandStatus.Completed && status.StatusCode == (int)parsedStatus
                            && status.EventCount is > 0 && status.CommittedEventSequence is > 0
                            && status.EventCount <= status.CommittedEventSequence
                            && !string.IsNullOrWhiteSpace(handle.AggregateId)
                            && string.Equals(status.TenantId, SystemTenant, StringComparison.Ordinal)
                            && string.Equals(status.Domain, expectedDomain, StringComparison.Ordinal)
                            && string.Equals(status.AggregateId, handle.AggregateId, StringComparison.Ordinal)
                                ? status.CommittedEventSequence : null,
                    };
        }
        catch (JsonException)
        {
            return TenantCommandStatusResult.RetryableFailure("Command status response was unavailable.") with
            {
                SafeMessageKey = globalAdministratorStatus
                    ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                    : "Tenants.Commands.Status.ResponseUnavailable",
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TenantCommandStatusResult.RetryableFailure("Command status could not be verified yet.") with
            {
                SafeMessageKey = globalAdministratorStatus
                    ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                    : "Tenants.Commands.Status.RetryableFailure",
            };
        }
        catch (HttpRequestException)
        {
            return TenantCommandStatusResult.RetryableFailure("Command status could not be verified yet.") with
            {
                SafeMessageKey = globalAdministratorStatus
                    ? "Tenants.GlobalAdministrators.Grant.Status.Unknown"
                    : "Tenants.Commands.Status.RetryableFailure",
            };
        }
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;

    private static TenantCommandSubmissionResult MapGatewayException(EventStoreGatewayException exception) {
        if (IsTenantAlreadyExists(exception)) {
            return TenantCommandSubmissionResult.Rejected(
                "A tenant with this id already exists. Refresh the list or open the existing tenant if it is visible.",
                "TenantAlreadyExists") with { SafeMessageKey = "Tenants.Commands.Rejection.TenantAlreadyExists" };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to create tenants.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.Create.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The create tenant request was not accepted. Check the form fields and try again.") with { SafeMessageKey = "Tenants.Create.Submission.Invalid", IsAmbiguousFailure = exception.Retryable is true },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.Create.Submission.Unavailable", IsAmbiguousFailure = true },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.Create.Submission.Failed", IsAmbiguousFailure = exception.Retryable is true || exception.StatusCode < 400
                    || IsRetryableStatusCode((HttpStatusCode)exception.StatusCode) },
        };
    }

    private static TenantCommandSubmissionResult MapAddUserToTenantGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeAddMemberRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to add members to this tenant.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.AddMember.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The add member request was not accepted. Check the form fields and try again.") with { SafeMessageKey = "Tenants.AddMember.Submission.Invalid", IsAmbiguousFailure = exception.Retryable is true },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.AddMember.Submission.Unavailable", IsAmbiguousFailure = true },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.AddMember.Submission.Failed", IsAmbiguousFailure = exception.Retryable is true || exception.StatusCode < 400
                    || IsRetryableStatusCode((HttpStatusCode)exception.StatusCode) },
        };
    }

    private static TenantCommandSubmissionResult MapChangeUserRoleGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeChangeRoleRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to change member roles in this tenant.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.ChangeRole.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The change role request was not accepted. Check the form fields and try again.") with { SafeMessageKey = "Tenants.ChangeRole.Submission.Invalid", IsAmbiguousFailure = exception.Retryable is true },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.ChangeRole.Submission.Unavailable", IsAmbiguousFailure = true },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.ChangeRole.Submission.Failed", IsAmbiguousFailure = exception.Retryable is true || exception.StatusCode < 400
                    || IsRetryableStatusCode((HttpStatusCode)exception.StatusCode) },
        };
    }

    private static TenantCommandSubmissionResult MapRemoveUserFromTenantGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeRemoveMemberRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to remove members from this tenant.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.RemoveMember.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The remove member request was not accepted. Check the visible member evidence and try again.") with { SafeMessageKey = "Tenants.RemoveMember.Submission.Invalid" },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.RemoveMember.Submission.Unavailable" },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.RemoveMember.Submission.Failed" },
        };
    }

    private static TenantCommandSubmissionResult MapUpdateTenantGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeUpdateTenantRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to edit this tenant's metadata.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.EditMetadata.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The metadata update request was not accepted. Check the form fields and try again.") with { SafeMessageKey = "Tenants.EditMetadata.Submission.Invalid" },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.EditMetadata.Submission.Unavailable" },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.EditMetadata.Submission.Failed" },
        };
    }

    private static TenantCommandSubmissionResult MapSetTenantConfigurationGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeSetConfigurationRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to set configuration for this tenant.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.Configuration.Set.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The configuration change request was not accepted. Check the form fields and try again.") with { SafeMessageKey = "Tenants.Configuration.Set.Submission.Invalid" },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.Configuration.Set.Submission.Unavailable" },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.Configuration.Set.Submission.VerificationFailed" },
        };
    }

    private static TenantCommandSubmissionResult MapRemoveTenantConfigurationGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeRemoveConfigurationRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to remove configuration for this tenant.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.Configuration.Remove.Unavailable.Authorization" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The configuration removal request was not accepted. Check the visible key evidence and try again.") with { SafeMessageKey = "Tenants.Configuration.Remove.Submission.Invalid" },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.") with { SafeMessageKey = "Tenants.Configuration.Remove.Submission.Unavailable" },
            _ => TenantCommandSubmissionResult.Failed("Tenant command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.Configuration.Remove.Submission.VerificationFailed" },
        };
    }

    private static TenantCommandSubmissionResult MapLifecycleGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeLifecycleRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to submit tenant lifecycle commands.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.Lifecycle.Message.Rejected.InsufficientPermissions" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The lifecycle request was not accepted. Check the visible tenant evidence and try again.") with { SafeMessageKey = "Tenants.Lifecycle.Submission.Invalid" },
            (int)HttpStatusCode.RequestTimeout or (int)HttpStatusCode.TooManyRequests
                => TenantCommandSubmissionResult.Ambiguous(
                    string.Empty,
                    "Tenants.Lifecycle.SubmissionEvidence.Ambiguous"),
            >= 500
                => TenantCommandSubmissionResult.Ambiguous(
                    string.Empty,
                    "Tenants.Lifecycle.SubmissionEvidence.Ambiguous"),
            _ => TenantCommandSubmissionResult.Failed("The lifecycle command submission was rejected before dispatch could be verified.") with { SafeMessageKey = "Tenants.Lifecycle.Submission.Failed" },
        };
    }

    private static TenantCommandSubmissionResult MapSetGlobalAdministratorGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeGlobalAdministratorRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.RejectedWithKey(
                "Tenants.GlobalAdministrators.Grant.Submission.Rejected",
                rejection.Value.Code);
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.RejectedWithKey(
                    "Tenants.GlobalAdministrators.Grant.Submission.Rejected",
                    "InsufficientPermissions"),
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.FailedWithKey(
                    "Tenants.GlobalAdministrators.Grant.Submission.Invalid"),
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.FailedWithKey(
                    "Tenants.GlobalAdministrators.Grant.Submission.Unavailable"),
            _ => TenantCommandSubmissionResult.FailedWithKey(
                "Tenants.GlobalAdministrators.Grant.Submission.Failed"),
        };
    }

    private static TenantCommandSubmissionResult MapRemoveGlobalAdministratorGatewayException(EventStoreGatewayException exception) {
        (string Code, string Message, string Key)? rejection = SafeGlobalAdministratorRejection(exception);
        if (rejection is not null) {
            return TenantCommandSubmissionResult.Rejected(rejection.Value.Message, rejection.Value.Code) with
            {
                SafeMessageKey = rejection.Value.Key,
            };
        }

        return exception.StatusCode switch {
            (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden
                => TenantCommandSubmissionResult.Rejected("You are not authorized to change platform governance.", "InsufficientPermissions") with { SafeMessageKey = "Tenants.GlobalAdministrators.Remove.Submission.Unauthorized" },
            (int)HttpStatusCode.BadRequest
                => TenantCommandSubmissionResult.Failed("The global administrator removal request was not accepted. Check the visible user id and try again.") with { SafeMessageKey = "Tenants.GlobalAdministrators.Remove.Submission.Invalid" },
            (int)HttpStatusCode.ServiceUnavailable
                => TenantCommandSubmissionResult.Failed("Global administrator command gateway is unavailable.") with { SafeMessageKey = "Tenants.GlobalAdministrators.Remove.Submission.Unavailable" },
            _ => TenantCommandSubmissionResult.Failed("Global administrator command submission failed before it could be verified.") with { SafeMessageKey = "Tenants.GlobalAdministrators.Remove.Submission.Failed" },
        };
    }

    private static bool IsTenantAlreadyExists(EventStoreGatewayException exception)
        => Contains(exception.ReasonCode, "tenant-already-exists")
        || Contains(exception.Reason, "TenantAlreadyExists")
        || Contains(exception.Title, "TenantAlreadyExists")
        || Contains(exception.Type, "tenant-already-exists")
        || Contains(exception.Detail, "TenantAlreadyExists")
        || Contains(ProblemDetailsExtension(exception, GatewayProblemDetailsExtensions.RejectionType), "TenantAlreadyExists")
        || Contains(ProblemDetailsExtension(exception, GatewayProblemDetailsExtensions.CorrectiveAction), "already exists");

    private static (string? Message, string? Key) SafeMessageForStatus(
        CommandStatus status, string? rejectionEventType, string? failureReason)
        => status switch
        {
            CommandStatus.Rejected when SafeSharedStatusRejection(rejectionEventType) is { } rejection
                => (rejection.Message, rejection.Key),
            CommandStatus.Rejected when failureReason is "ConcurrencyConflict" or "BackpressureExceeded"
                => SafeFailureReason(failureReason)!.Value,
            CommandStatus.Rejected => ("The command was rejected.", "Tenants.Commands.Failure.DomainRejected"),
            CommandStatus.PublishFailed => (
                "The command was accepted, but publication could not be verified.",
                "Tenants.Commands.Failure.PublishFailed"),
            CommandStatus.TimedOut => (
                "The command status timed out before the result could be verified.",
                "Tenants.Commands.Failure.TimedOut"),
            _ => SafeFailureReason(failureReason) is { } failure
                ? (failure.Message, failure.Key) : (null, null),
        };

    private static string? SafeRejectionCode(string? rejectionEventType)
        => SafeSharedStatusRejection(rejectionEventType)?.Code;

    // GetStatusAsync is shared by every Tenants command (create-tenant, add-member, change-role,
    // remove-member)
    // and only carries a correlation id, so it cannot tell which command produced a rejection.
    // Command-UNIQUE rejection types (TenantAlreadyExists -> create, UserAlreadyInTenant -> add,
    // UserNotInTenant -> change-role) keep command-specific copy. Rejection types that are SHARED
    // across commands (InsufficientPermissions, TenantDisabled, TenantNotFound, RoleEscalation)
    // must stay command-neutral so one command's copy never leaks into another command's lifecycle
    // panel. This keeps the Story 2.2 shared-status discipline symmetric for member commands.
    private static (string Code, string Message, string Key)? SafeSharedStatusRejection(string? value) {
        if (Contains(value, "TenantAlreadyExists")) {
            return ("TenantAlreadyExists", "A tenant with this id already exists. Refresh the list or open the existing tenant if it is visible.", "Tenants.Commands.Rejection.TenantAlreadyExists");
        }

        if (Contains(value, "UserAlreadyInTenant")) {
            return ("UserAlreadyInTenant", "This user is already a member of the tenant. Refresh the member table before trying another action.", "Tenants.Commands.Rejection.UserAlreadyInTenant");
        }

        if (Contains(value, "UserNotInTenant")) {
            return ("UserNotInTenant", "The target user is not a visible member of this tenant. Refresh the member table before trying again.", "Tenants.Commands.Rejection.UserNotInTenant");
        }

        if (Contains(value, "RoleEscalation")) {
            return ("RoleEscalation", "The requested tenant role cannot be assigned by this command.", "Tenants.Commands.Rejection.RoleEscalation");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to submit this command.", "Tenants.Commands.Rejection.InsufficientPermissions");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so the command cannot be completed.", "Tenants.Commands.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        if (Contains(value, "ConfigurationLimitExceeded")) {
            return ("ConfigurationLimitExceeded", "The configuration change exceeded tenant configuration limits.", "Tenants.Commands.Rejection.ConfigurationLimitExceeded");
        }

        if (Contains(value, "ConfigurationKeyNotFound")) {
            return ("ConfigurationKeyNotFound", "The configuration key was not found in the current tenant projection. Refresh tenant detail before treating removal as complete.", "Tenants.Commands.Rejection.ConfigurationKeyNotFound");
        }

        if (Contains(value, "TenantLifecycleStateAlreadySet")) {
            return ("TenantLifecycleStateAlreadySet", "The tenant lifecycle already matches the requested state. Refresh tenant detail before trying another lifecycle action.", "Tenants.Commands.Rejection.TenantLifecycleStateAlreadySet");
        }

        if (Contains(value, "GlobalAdministratorAlreadyExists")) {
            return ("GlobalAdministratorAlreadyExists", "This user is already a global administrator. Refresh the platform authority projection before trying another action.", "Tenants.Commands.Rejection.GlobalAdministratorAlreadyExists");
        }

        if (Contains(value, "LastGlobalAdministrator")) {
            return ("LastGlobalAdministrator", "The last global administrator cannot be removed. Keep the current projection visible and add another global administrator before trying again.", "Tenants.Commands.Rejection.LastGlobalAdministrator");
        }

        if (Contains(value, "GlobalAdministratorNotFound")) {
            return ("GlobalAdministratorNotFound", "The target user is not a global administrator in the last-confirmed projection. Refresh platform authority before trying another action.", "Tenants.Commands.Rejection.GlobalAdministratorNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeAddMemberRejection(EventStoreGatewayException exception)
        => SafeAddMemberRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeAddMemberRejection(string? value) {
        if (Contains(value, "UserAlreadyInTenant")) {
            return ("UserAlreadyInTenant", "This user is already a member of the tenant. Refresh the member table before trying another action.", "Tenants.Commands.Rejection.UserAlreadyInTenant");
        }

        if (Contains(value, "RoleEscalation")) {
            return ("RoleEscalation", "The requested tenant role cannot be assigned by this command.", "Tenants.Commands.Rejection.RoleEscalation");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to add members to this tenant.", "Tenants.AddMember.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so members cannot be added.", "Tenants.AddMember.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeChangeRoleRejection(EventStoreGatewayException exception)
        => SafeChangeRoleRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeChangeRoleRejection(string? value) {
        if (Contains(value, "RoleEscalation")) {
            return ("RoleEscalation", "The requested tenant role cannot be assigned by this command.", "Tenants.Commands.Rejection.RoleEscalation");
        }

        if (Contains(value, "UserNotInTenant")) {
            return ("UserNotInTenant", "The target user is not a visible member of this tenant. Refresh the member table before trying again.", "Tenants.Commands.Rejection.UserNotInTenant");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to change member roles in this tenant.", "Tenants.ChangeRole.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so member roles cannot be changed.", "Tenants.ChangeRole.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeRemoveMemberRejection(EventStoreGatewayException exception)
        => SafeRemoveMemberRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeRemoveMemberRejection(string? value) {
        if (Contains(value, "UserNotInTenant")) {
            return ("UserNotInTenant", "The target user is not a visible member of this tenant. Refresh the member table before treating removal as already applied.", "Tenants.RemoveMember.Rejection.UserNotInTenant");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to remove members from this tenant.", "Tenants.RemoveMember.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so members cannot be removed.", "Tenants.RemoveMember.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeRemoveConfigurationRejection(EventStoreGatewayException exception)
        => SafeRemoveConfigurationRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeRemoveConfigurationRejection(string? value) {
        if (Contains(value, "ConfigurationKeyNotFound")) {
            return ("ConfigurationKeyNotFound", "The configuration key was not found in the current tenant projection. Refresh tenant detail before treating removal as complete.", "Tenants.Commands.Rejection.ConfigurationKeyNotFound");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to remove configuration for this tenant.", "Tenants.Configuration.Remove.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so configuration cannot be removed.", "Tenants.Configuration.Remove.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeUpdateTenantRejection(EventStoreGatewayException exception)
        => SafeUpdateTenantRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeUpdateTenantRejection(string? value) {
        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to edit this tenant's metadata.", "Tenants.EditMetadata.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so metadata cannot be edited.", "Tenants.EditMetadata.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeSetConfigurationRejection(EventStoreGatewayException exception)
        => SafeSetConfigurationRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeSetConfigurationRejection(string? value) {
        if (Contains(value, "ConfigurationLimitExceeded")) {
            return ("ConfigurationLimitExceeded", "The configuration value exceeds the tenant configuration limits.", "Tenants.Configuration.Set.Rejection.ConfigurationLimitExceeded");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to set configuration for this tenant.", "Tenants.Configuration.Set.Unavailable.Authorization");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so configuration cannot be changed.", "Tenants.Configuration.Set.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeLifecycleRejection(EventStoreGatewayException exception)
        => SafeLifecycleRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeLifecycleRejection(string? value) {
        if (Contains(value, "TenantLifecycleStateAlreadySet")) {
            return ("TenantLifecycleStateAlreadySet", "The tenant lifecycle already matches the requested state. Refresh tenant detail before trying another lifecycle action.", "Tenants.Commands.Rejection.TenantLifecycleStateAlreadySet");
        }

        if (Contains(value, "TenantDisabled")) {
            return ("TenantDisabled", "This tenant is disabled, so the requested tenant command cannot be completed.", "Tenants.Lifecycle.Rejection.TenantDisabled");
        }

        if (Contains(value, "TenantNotFound")) {
            return ("TenantNotFound", "The tenant was not found. Refresh the tenant detail before trying again.", "Tenants.Commands.Rejection.TenantNotFound");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to submit tenant lifecycle commands.", "Tenants.Lifecycle.Message.Rejected.InsufficientPermissions");
        }

        return null;
    }

    private static (string Code, string Message, string Key)? SafeGlobalAdministratorRejection(EventStoreGatewayException exception)
        => SafeGlobalAdministratorRejection(
            string.Join(
                "|",
                exception.ReasonCode,
                exception.Reason,
                exception.Title,
                exception.Type,
                exception.Detail));

    private static (string Code, string Message, string Key)? SafeGlobalAdministratorRejection(string? value) {
        if (Contains(value, "GlobalAdministratorAlreadyExists")) {
            return ("GlobalAdministratorAlreadyExists", "This user is already a global administrator. Refresh the platform authority projection before trying another action.", "Tenants.Commands.Rejection.GlobalAdministratorAlreadyExists");
        }

        if (Contains(value, "LastGlobalAdministrator")) {
            return ("LastGlobalAdministrator", "The last global administrator cannot be removed. Keep the current projection visible and add another global administrator before trying again.", "Tenants.Commands.Rejection.LastGlobalAdministrator");
        }

        if (Contains(value, "GlobalAdministratorNotFound")) {
            return ("GlobalAdministratorNotFound", "The target user is not a global administrator in the last-confirmed projection. Refresh platform authority before trying another action.", "Tenants.Commands.Rejection.GlobalAdministratorNotFound");
        }

        if (Contains(value, "InsufficientPermissions")) {
            return ("InsufficientPermissions", "You are not authorized to change platform governance.", "Tenants.GlobalAdministrators.Remove.Submission.Unauthorized");
        }

        return null;
    }

    private static bool IsAmbiguousTrackedSubmission(Exception exception, CancellationToken cancellationToken)
        => exception switch
        {
            EventStoreGatewayException gatewayException => gatewayException.Retryable == true
                || gatewayException.StatusCode is (int)HttpStatusCode.RequestTimeout or (int)HttpStatusCode.TooManyRequests
                || gatewayException.StatusCode >= 500,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            HttpRequestException or TimeoutException => true,
            _ => false,
        };

    private static bool IsAssignableTenantRole(TenantRole role)
        => role is TenantRole.TenantOwner or TenantRole.TenantContributor or TenantRole.TenantReader;

    private bool TryResolveMessageId(string? messageId, out string resolvedMessageId) {
        string candidate = string.IsNullOrWhiteSpace(messageId)
            ? ulidFactory.NewUlid()
            : messageId.Trim().ToUpperInvariant();
        if (!NUlid.Ulid.TryParse(candidate, out NUlid.Ulid parsed)) {
            resolvedMessageId = string.Empty;
            return false;
        }

        resolvedMessageId = parsed.ToString();
        return string.Equals(candidate, resolvedMessageId, StringComparison.Ordinal);
    }

    private static bool TryValidateCallerMessageId(string? messageId)
        => !string.IsNullOrWhiteSpace(messageId)
            && NUlid.Ulid.TryParse(messageId, out NUlid.Ulid parsed)
            && string.Equals(messageId, parsed.ToString(), StringComparison.Ordinal);

    // FailureReason is backend data, never presentation copy. Match only complete machine codes;
    // inspection, trimming or substring matching would turn arbitrary details into trusted evidence.
    private static (string Message, string Key)? SafeFailureReason(string? value)
        => value switch
        {
            "ConcurrencyConflict" => (
                "The command could not complete because the current state changed. Retry status lookup.",
                "Tenants.Commands.Failure.ConcurrencyConflict"),
            "BackpressureExceeded" => (
                "The command could not complete because the service is busy. Retry status lookup later.",
                "Tenants.Commands.Failure.BackpressureExceeded"),
            "DomainRejected" => ("The command was rejected.", "Tenants.Commands.Failure.DomainRejected"),
            null or "" => null,
            _ => ("The command status included an unavailable support detail.",
                "Tenants.Commands.Failure.UnavailableSupportDetail"),
        };

    private static bool Contains(string? value, string expected)
        => value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

    private static string? ProblemDetailsExtension(EventStoreGatewayException exception, string key)
        => exception.Extensions.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

}
