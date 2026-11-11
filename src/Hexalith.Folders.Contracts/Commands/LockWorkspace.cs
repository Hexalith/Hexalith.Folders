using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Hexalith.McpCli.Abstractions;
using Hexalith.Folders.Contracts.Commands.Models;

namespace Hexalith.Folders.Contracts.Commands;

/// <summary>Accept task-scoped workspace lock acquisition. Requires canonical extensions.taskId; the caller profile must allowlist taskId. Payload TaskId must match extensions.taskId.</summary>
/// <param name="LockIntent">The lockIntent value.</param>
/// <param name="RequestedLeaseSeconds">Sets the initial lease expiry so `expiresAt` equals the lock `effectiveAt` plus `requestedLeaseSeconds`; the lock is expired at `now &gt;= expiresAt` unless a successful authorized renewal has already established later effective and expiry timestamps. Renewal timing and the expired-to-stale boundary are governed by docs/exit-criteria/c7-lock-authorization-timing.md version 1.0.0; default lease duration and clock-skew policy remain deferred.</param>
/// <param name="FolderId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="WorkspaceId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="IdempotencyKey">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
/// <param name="TaskId">Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values.</param>
[Description("Accept task-scoped workspace lock acquisition. Requires canonical extensions.taskId; the caller profile must allowlist taskId. Payload TaskId must match extensions.taskId.")]
[HexalithCommand("Accept task-scoped workspace lock acquisition. Requires canonical extensions.taskId; the caller profile must allowlist taskId. Payload TaskId must match extensions.taskId.", Domain = "folders",
    WireType = "Hexalith.Folders.Commands.LockWorkspace",
    AggregateIdProperty = nameof(FolderId), IdempotencyKeyProperty = nameof(IdempotencyKey))]
public sealed record LockWorkspace(
    [property: JsonPropertyName("lockIntent"), Description("The lock intent."), JsonRequired, Required]
    LockWorkspaceLockIntent LockIntent,
    [property: JsonPropertyName("requestedLeaseSeconds"), Description("Sets the initial lease expiry so `expiresAt` equals the lock `effectiveAt` plus `requestedLeaseSeconds`; the lock is expired at `now >= expiresAt` unless a successful authorized renewal has already established later effective and expiry timestamps. Renewal timing and the expired-to-stale boundary are governed by docs/exit-criteria/c7-lock-authorization-timing.md version 1.0.0; default lease duration and clock-skew policy remain deferred."), JsonRequired, Required, Range(1, 86400)]
    int RequestedLeaseSeconds,
    [property: HexalithIdentifier, JsonPropertyName("folderId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string FolderId,
    [property: HexalithIdentifier, JsonPropertyName("workspaceId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string WorkspaceId,
    [property: HexalithIdentifier, JsonPropertyName("idempotencyKey"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string IdempotencyKey,
    [property: HexalithIdentifier, JsonPropertyName("taskId"), Description("Synthetic opaque identifier (ULID-shaped). It is not a path, credential, token, or tenant authority. The pattern rejects dots and colons to keep keys disjoint from cache-key tenant-prefix notation and JWT-shaped values."), JsonRequired, Required, StringLength(128, MinimumLength = 16), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$")]
    string TaskId);
