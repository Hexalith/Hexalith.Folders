using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderArchiveMetadataLeakageTests
{
    [Theory]
    [InlineData("github_pat_credential_material")]
    [InlineData("principal-token")]
    [InlineData("principal@example.com")]
    public void UnsafeActorEvidenceShouldRejectWithoutEchoingUnsafeIdentifier(string unsafeActor)
    {
        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.Archive(actorPrincipalId: unsafeActor));

        result.Code.ShouldBe(FolderResultCode.MalformedEvidence);
        result.ActorPrincipalId.ShouldBeNull();
        result.Events.ShouldBeEmpty();
    }

    [Fact]
    public void AcceptedArchiveEventShouldCarryOnlyMetadataEvidence()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());

        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();
        string serialized = string.Join(
            '|',
            archived.ManagedTenantId,
            archived.OrganizationId,
            archived.FolderId,
            archived.ArchiveReasonCode,
            archived.ActorPrincipalId,
            archived.CorrelationId,
            archived.TaskId,
            archived.IdempotencyKey);

        serialized.ShouldNotContain("token", Case.Insensitive);
        serialized.ShouldNotContain("secret", Case.Insensitive);
        serialized.ShouldNotContain("credential", Case.Insensitive);
        serialized.ShouldNotContain("repository", Case.Insensitive);
        serialized.ShouldNotContain("diff --git", Case.Insensitive);
        serialized.ShouldNotContain("://", Case.Sensitive);
        serialized.ShouldNotContain("\\", Case.Sensitive);
        serialized.ShouldNotContain("/", Case.Sensitive);
    }

    [Fact]
    public void ArchiveSafetySurfacesShouldNotEchoForbiddenSentinelCorpusValues()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());
        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();
        Dictionary<string, string> archiveSurfaces = new(StringComparer.Ordinal)
        {
            ["event"] = JsonSerializer.Serialize(archived),
            ["audit-record"] = string.Join('|', "ArchiveFolder", "accepted", archived.FolderId, archived.CorrelationId, archived.TaskId),
            ["projection"] = JsonSerializer.Serialize(new
            {
                archived = true,
                lifecycleState = "inaccessible",
                folderId = archived.FolderId,
                correlationId = archived.CorrelationId,
                taskId = archived.TaskId,
            }),
            ["problem-details"] = JsonSerializer.Serialize(new
            {
                category = "idempotency_conflict",
                code = "idempotency_conflict",
                correlationId = archived.CorrelationId,
                taskId = archived.TaskId,
            }),
            ["log-template"] = "ArchiveFolder completed: Result=accepted, CorrelationId=correlation-a",
            ["trace-tags"] = "operation=ArchiveFolder result=accepted tenant_scope=present",
            ["metric-labels"] = "operation=ArchiveFolder,result=accepted",
            ["generated-client-exception"] = "idempotency_conflict",
        };

        foreach (string sentinel in ForbiddenSentinelValues())
        {
            foreach (KeyValuePair<string, string> surface in archiveSurfaces)
            {
                surface.Value.ShouldNotContain(sentinel, Case.Sensitive, $"Surface {surface.Key} leaked sentinel corpus value.");
            }
        }
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static IReadOnlyList<string> ForbiddenSentinelValues()
    {
        string path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(sample => sample.GetProperty("value").GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Folders.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
