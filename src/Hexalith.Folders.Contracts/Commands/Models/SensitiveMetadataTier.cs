using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed SensitiveMetadataTier vocabulary from the v2 Contract Spine.</summary>
[Description("The SensitiveMetadataTier vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<SensitiveMetadataTier>))]
public enum SensitiveMetadataTier
{
    /// <summary>The public_metadata value.</summary>
    [JsonStringEnumMemberName("public_metadata"), Description("The public_metadata value.")]
    PublicMetadata,

    /// <summary>The tenant_sensitive value.</summary>
    [JsonStringEnumMemberName("tenant_sensitive"), Description("The tenant_sensitive value.")]
    TenantSensitive,

    /// <summary>The credential_sensitive value.</summary>
    [JsonStringEnumMemberName("credential_sensitive"), Description("The credential_sensitive value.")]
    CredentialSensitive,

    /// <summary>The secret value.</summary>
    [JsonStringEnumMemberName("secret"), Description("The secret value.")]
    Secret,

}
