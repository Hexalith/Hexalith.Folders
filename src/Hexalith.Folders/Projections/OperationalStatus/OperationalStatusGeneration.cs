using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Domain names for SDK-owned generation selection and ordinary writer fencing.</summary>
public static class OperationalStatusGeneration
{
    /// <summary>The writer identity shared by authenticated subscriptions and named dispatch.</summary>
    public const string WriterId = "folders-operational-status";

    /// <summary>Names the same tenant family for rebuild, delivery, and selected reads.</summary>
    public static SharedProjectionScope Scope(string tenantId)
        => new(OperationalStatusKeys.StoreName, tenantId, "folders", OperationalStatusKeys.ProjectionType, [WriterId])
        { CopyOnWrite = true };
}
