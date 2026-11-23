using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

public sealed class EventStoreClaimTransformEvidenceTests
{
    [Fact]
    public void ConstructorAndRecordCopyShouldSnapshotSuppliedPermissionSets()
    {
        HashSet<string> source = new(StringComparer.Ordinal) { "read_metadata" };
        EventStoreClaimTransformEvidence evidence = new("tenant-a", "user-a", source, IsPresent: true, Malformed: false);
        HashSet<string> replacement = new(StringComparer.Ordinal) { "read_task_status" };
        EventStoreClaimTransformEvidence copy = evidence with { PermissionTokens = replacement };

        source.Clear();
        replacement.Clear();

        evidence.PermissionTokens.ShouldBe(["read_metadata"]);
        copy.PermissionTokens.ShouldBe(["read_task_status"]);
    }

    [Fact]
    public void AllowedShouldSnapshotNormalizedOrdinalPermissions()
    {
        HashSet<string> source = new(StringComparer.OrdinalIgnoreCase) { " read_metadata ", "read_metadata", " " };
        EventStoreClaimTransformEvidence evidence = EventStoreClaimTransformEvidence.Allowed("tenant-a", "user-a", source);
        source.Clear();
        source.Add("archive_folder");

        if (evidence.PermissionTokens is ISet<string> set)
        {
            Should.Throw<NotSupportedException>(() => set.Add("archive_folder"));
        }

        evidence.PermissionTokens.ShouldBe(["read_metadata"]);
        evidence.HasPermissionFor(" read_metadata ").ShouldBeTrue();
        evidence.HasPermissionFor("READ_METADATA").ShouldBeFalse();
        evidence.HasPermissionFor("archive_folder").ShouldBeFalse();
    }
}
