using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hexalith.Folders.Contracts.Queries.Models;

/// <summary>The closed ProviderCapabilityName vocabulary from the v2 Contract Spine.</summary>
[Description("The ProviderCapabilityName vocabulary.")]
[JsonConverter(typeof(JsonStringEnumConverter<ProviderCapabilityName>))]
public enum ProviderCapabilityName
{
    /// <summary>The repository_creation value.</summary>
    [JsonStringEnumMemberName("repository_creation"), Description("The repository_creation value.")]
    RepositoryCreation,

    /// <summary>The existing_repository_binding value.</summary>
    [JsonStringEnumMemberName("existing_repository_binding"), Description("The existing_repository_binding value.")]
    ExistingRepositoryBinding,

    /// <summary>The branch_ref_policy value.</summary>
    [JsonStringEnumMemberName("branch_ref_policy"), Description("The branch_ref_policy value.")]
    BranchRefPolicy,

    /// <summary>The file_operations value.</summary>
    [JsonStringEnumMemberName("file_operations"), Description("The file_operations value.")]
    FileOperations,

    /// <summary>The commit_status value.</summary>
    [JsonStringEnumMemberName("commit_status"), Description("The commit_status value.")]
    CommitStatus,

    /// <summary>The provider_errors value.</summary>
    [JsonStringEnumMemberName("provider_errors"), Description("The provider_errors value.")]
    ProviderErrors,

    /// <summary>The failure_behavior value.</summary>
    [JsonStringEnumMemberName("failure_behavior"), Description("The failure_behavior value.")]
    FailureBehavior,

}
