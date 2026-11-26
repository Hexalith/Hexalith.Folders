using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>Identifies the owner-local create-only provisioning and recovery contract.</summary>
public static class FolderProvisioningContract
{
    /// <summary>The EventStore Folder command discriminator.</summary>
    public const string CommandType = FoldersServerModule.ProvisionFolderCommandType;

    /// <summary>The authenticated receipt lookup route.</summary>
    public const string LookupPath = "/internal/folders/v1/provisioning/lookup";

    /// <summary>The signed workload operation for looking up an owner receipt.</summary>
    public const string LookupOperation = "folders:provisioning-lookup";

    /// <summary>Derives an address in the provisioning identity namespace from exact owner scope.</summary>
    public static string DeriveFolderId(string tenantId, string canonicalStepKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalStepKey);
        return "fld-p-" + Hash(string.Create(CultureInfo.InvariantCulture,
            $"{tenantId.Length}:{tenantId}{canonicalStepKey.Length}:{canonicalStepKey}"))[..40];
    }

    /// <summary>Hashes the v1 admitted owner intent. The normalized display name is its only mutable input.</summary>
    public static string ComputeRequestSha256(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return Hash("hexalith.folders.provision-folder.v1\n" + displayName.Trim());
    }

    internal static bool IsCanonicalStep(string? taskId, string? stepKey)
    {
        if (!FolderCanonicalSegmentIdentifier.IsValid(taskId) || stepKey is null)
        {
            return false;
        }

        string prefix = taskId + "/step/";
        return stepKey.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(stepKey.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            && number > 0
            && stepKey == prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    internal static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
