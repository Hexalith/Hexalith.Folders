using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Commands.Models;

/// <summary>The closed PathMetadataPathPolicyClass vocabulary from the v2 Contract Spine.</summary>
[Description("The PathMetadataPathPolicyClass vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<PathMetadataPathPolicyClass>))]
public enum PathMetadataPathPolicyClass
{
    /// <summary>The content_allowed value.</summary>
    [JsonStringEnumMemberName("content_allowed"), Description("The content_allowed value.")]
    ContentAllowed,

    /// <summary>The metadata_only value.</summary>
    [JsonStringEnumMemberName("metadata_only"), Description("The metadata_only value.")]
    MetadataOnly,

    /// <summary>The excluded value.</summary>
    [JsonStringEnumMemberName("excluded"), Description("The excluded value.")]
    Excluded,

    /// <summary>The restricted value.</summary>
    [JsonStringEnumMemberName("restricted"), Description("The restricted value.")]
    Restricted,

}
