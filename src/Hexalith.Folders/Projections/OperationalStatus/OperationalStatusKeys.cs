using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Collision-safe, opaque storage identities for tenant-scoped status metadata.</summary>
public static class OperationalStatusKeys
{
    /// <summary>The shared SDK read-model component.</summary>
    public const string StoreName = "statestore";
    /// <summary>The named SDK projection route.</summary>
    public const string ProjectionType = "operational-status";
    /// <summary>The C3 maximum view retention.</summary>
    public static TimeSpan Retention { get; } = TimeSpan.FromDays(400);
    /// <summary>Gets the folder checkpoint key.</summary>
    public static string Checkpoint(string tenantId, string folderId) => Key("checkpoint", tenantId, folderId);
    /// <summary>Gets the lifecycle key.</summary>
    public static string Lifecycle(string tenantId, string folderId) => Key("lifecycle", tenantId, folderId);
    /// <summary>Gets the workspace key.</summary>
    public static string Workspace(string tenantId, string folderId, string workspaceId) => Key("workspace", tenantId, folderId, workspaceId);
    /// <summary>Gets the task key.</summary>
    public static string Task(string tenantId, string folderId, string taskId) => Key("task", tenantId, folderId, taskId);
    /// <summary>Gets bounded source evidence for one original position.</summary>
    public static string Receipt(string tenantId, string folderId, long sequence) => Key("receipt", tenantId, folderId, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
    /// <summary>Gets the unique original position for one message identity.</summary>
    public static string Message(string tenantId, string folderId, string messageId) => Key("message", tenantId, folderId, messageId);
    /// <summary>Gets full-row integrity evidence independently of view retention.</summary>
    public static string Evidence(string viewKey) => Key("evidence", viewKey);
    /// <summary>Hashes a length-preserving encoding of components.</summary>
    internal static string Digest(params string[] components)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(components))));
    private static string Key(string concern, params string[] components)
        => $"projection:folders:operational-status:{concern}:{Digest(components)}";
}
