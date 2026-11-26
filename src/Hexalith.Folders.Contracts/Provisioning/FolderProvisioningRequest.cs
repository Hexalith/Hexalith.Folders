using System.Text.Json.Serialization;

namespace Hexalith.Folders.Server.Provisioning;

/// <summary>The scoped input for create-only Folder provisioning and receipt recovery.</summary>
/// <param name="TaskId">The task admitted by EventStore.</param>
/// <param name="StepKey">Its canonical numbered owner step key.</param>
/// <param name="AdmissionOperation">The exact versioned EventStore admission operation.</param>
/// <param name="RequestSha256">The normalized Folder provisioning intent digest bound by the committed task step.</param>
/// <param name="Fence">The caller's current positive execution fence.</param>
/// <param name="ExpectedVersion">Zero for an absent owner stream.</param>
/// <param name="DisplayName">The intended Folder display name.</param>
public sealed record FolderProvisioningRequest(
    string TaskId,
    string StepKey,
    string AdmissionOperation,
    string RequestSha256,
    long Fence,
    [property: JsonRequired] long ExpectedVersion,
    string DisplayName)
{
    /// <summary>The deployment-enrolled workload command for create-only provisioning.</summary>
    public const string CommandType = "Hexalith.Folders.Commands.ProvisionFolder";
}
