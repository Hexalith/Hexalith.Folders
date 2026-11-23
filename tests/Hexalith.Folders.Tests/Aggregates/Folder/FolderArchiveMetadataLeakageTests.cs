using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

using Hexalith.Folders.Tests.Shared.AuditLeakage;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

// Archive-path metadata-leakage regression coverage.
//
// Every nonblank sentinel in the authoritative shared corpus
// (tests/fixtures/audit-leakage-corpus.json) is driven through the archive actor-evidence
// seam, one xUnit theory row per corpus sample. That theory is the injection test.
//
// The accepted-surface theory does NOT inject sentinels: its surfaces are built from
// FolderCommandFactory.Archive() safe defaults, so it asserts that those constant accepted
// payloads carry no corpus value. It is a drift guard on the safe defaults, not proof that
// an injected sentinel would be filtered out of those channels.
//
// Rows are labelled by the corpus's stable `id` so no sentinel text ever reaches an xUnit
// display name and so inserting a corpus sample cannot silently reassign every downstream
// row label.
public sealed class FolderArchiveMetadataLeakageTests
{
    // Pre-existing inline unsafe actor values, retained as labelled rows. All three are
    // canonical-charset-valid, so the charset gate alone would let them through; each is
    // rejected only by FolderCommandValidator's ForbiddenMetadataSubstrings list
    // ("credential", "token", "@"). The corpus reaches that same list on the archive path
    // through exactly one sample (`repository-name-metadata`, via "repository"), so these
    // rows are the only archive-path coverage of the other substring terms. None of them
    // exercises the word-boundary list (ForbiddenMetadataWordTerms) — that is covered by
    // FolderAccessCommandValidationTests.IsSafeEvidenceIdentifierShouldAcceptIdentifiersContainingFalsePositiveSubstrings.
    private static readonly (string Label, string Value)[] LegacyUnsafeActorValues =
    [
        ("legacy:credential-substring", "github_pat_credential_material"),
        ("legacy:token-substring", "principal-token"),
        ("legacy:at-marker-substring", "principal@example.com"),
    ];

    // Surfaces in the archive surface map whose payload is JSON and therefore must be
    // walked decoded, not only scanned raw. ArchiveSurfaceMapKeepsDeclaredJsonSurfacesParseable
    // pins each of these to a payload that still parses as JSON: ContainsSentinel degrades to
    // a raw-text-only scan on unparsable input, so a surface that quietly stops being JSON
    // would keep every sweep green while escaped leakage became invisible.
    private static readonly string[] JsonArchiveSurfaceNames =
    [
        "events",
        "projections",
        "problem-details-examples",
    ];

    public static TheoryData<string> UnsafeActorEvidenceRows()
    {
        TheoryData<string> data = new();
        foreach ((string label, _) in LegacyUnsafeActorValues)
        {
            data.Add(new TheoryDataRow<string>(label) { Label = label });
        }

        foreach (AuditLeakageSample sample in AuditLeakageCorpus.Samples)
        {
            data.Add(new TheoryDataRow<string>(sample.Id) { Label = $"corpus:{sample.Id}" });
        }

        return data;
    }

    public static TheoryData<string> CorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (AuditLeakageSample sample in AuditLeakageCorpus.Samples)
        {
            data.Add(new TheoryDataRow<string>(sample.Id) { Label = $"corpus:{sample.Id}" });
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnsafeActorEvidenceRows))]
    public void UnsafeActorEvidenceShouldRejectWithoutEchoingUnsafeIdentifier(string sampleId)
    {
        ArgumentNullException.ThrowIfNull(sampleId);
        string unsafeActor = sampleId.StartsWith("legacy:", StringComparison.Ordinal)
            ? LegacyUnsafeActorValues.Single(sample => sample.Label == sampleId).Value
            : AuditLeakageCorpus.Get(sampleId).Value;

        FolderResult result = FolderAggregate.Handle(
            CreatedState(),
            FolderCommandFactory.Archive(actorPrincipalId: unsafeActor));

        result.Code.ShouldBe(FolderResultCode.MalformedEvidence);

        // Metadata-only assertion idiom. Shouldly's ShouldBeNull()/ShouldBeEmpty()/
        // ShouldNotContain() render the *actual* value into the failure message, which
        // would echo the hostile input into the assertion-messages channel the corpus
        // itself declares forbidden. Do not "simplify" these back into value-printing
        // assertions: that silently reopens the leak this suite exists to close.
        (result.ActorPrincipalId is null).ShouldBeTrue(
            "Rejected archive result must not echo the unsafe actor principal identifier.");
        result.Events.Count.ShouldBe(
            0,
            "A malformed-evidence archive rejection must append no events.");

        AuditLeakageMatcher.Contains(JsonSerializer.Serialize(result), unsafeActor).ShouldBeFalse(
            "Serialized archive rejection result echoed the unsafe actor evidence value.");
    }

    [Fact]
    public void AcceptedArchiveEventShouldCarryOnlyMetadataEvidence()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());

        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();
        AssertArchiveMetadataOnly(archived);
    }

    [Fact]
    public void ArchiveMetadataScanShouldDetectContaminatedFingerprintWithoutPrintingIt()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());
        FolderArchived archived = result.Events.OfType<FolderArchived>().Single() with
        {
            IdempotencyFingerprint = "secret-fingerprint-control",
        };
        ShouldAssertException exception = Should.Throw<ShouldAssertException>(() => AssertArchiveMetadataOnly(archived));
        exception.Message.Contains(archived.IdempotencyFingerprint, StringComparison.Ordinal).ShouldBeFalse(
            "Negative-control assertions must not print the contaminated fingerprint.");
    }

    private static void AssertArchiveMetadataOnly(FolderArchived archived)
    {
        string serialized = JsonSerializer.Serialize(archived);
        string[] forbiddenTerms = ["token", "secret", "credential", "repository", "diff --git", "://", "\\", "/"];
        foreach (string term in forbiddenTerms)
        {
            // Scan every serialized property, including future event members. Report only the
            // fixed rule, so a failing assertion never prints the contaminated event.
            serialized.Contains(term, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                "Serialized archive event must contain only safe metadata.");
        }
    }

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void ArchiveSafetySurfacesShouldNotEchoForbiddenSentinelCorpusValues(string sampleId)
    {
        AuditLeakageSample sample = AuditLeakageCorpus.Get(sampleId);
        foreach ((string channel, string payload) in AcceptedArchiveSurfaces())
        {
            AuditLeakageMatcher.LeaksOn(sample, channel, payload).ShouldBeFalse(
                $"Archive channel '{channel}' leaked sample '{sample.Id}'.");
        }
    }

    [Fact]
    public void ArchiveSurfaceMapKeepsDeclaredJsonSurfacesParseable()
    {
        // Structural guard, deliberately a [Fact]: it is about the surface map, not about any
        // one sentinel, so a failure names the map instead of whichever corpus row ran first.
        // A presence-only check would be inert — ContainsSentinel decides JSON-vs-raw by
        // parsing, so deleting the declared list changes no scan. What must be pinned is that
        // each declared JSON surface still PARSES, because an unparsable payload silently
        // downgrades that channel to a raw-text-only scan and hides \uXXXX-escaped leakage.
        IReadOnlyDictionary<string, string> surfaces = AcceptedArchiveSurfaces();
        string[] expectedChannels =
        [
            "audit-records",
            "events",
            "exception-metadata",
            "logs",
            "metric-labels",
            "problem-details-examples",
            "projections",
            "traces",
        ];
        surfaces.Keys.Order(StringComparer.Ordinal).ShouldBe(expectedChannels);

        foreach (string jsonSurface in JsonArchiveSurfaceNames)
        {
            surfaces.TryGetValue(jsonSurface, out string? payload).ShouldBeTrue(
                $"JSON archive surface '{jsonSurface}' is no longer present in the archive surface map.");

            IsParseableJson(payload!).ShouldBeTrue(
                $"Archive surface '{jsonSurface}' no longer parses as JSON, so it is scanned as raw text only.");
        }

        // The inverse direction: a newly added JSON surface must be declared, or it silently
        // gets the raw-text-only treatment this list exists to prevent.
        foreach (KeyValuePair<string, string> surface in surfaces)
        {
            if (IsParseableJson(surface.Value))
            {
                JsonArchiveSurfaceNames.ShouldContain(
                    surface.Key,
                    $"Archive surface '{surface.Key}' parses as JSON but is not declared in {nameof(JsonArchiveSurfaceNames)}.");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> AcceptedArchiveSurfaces()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());
        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["events"] = JsonSerializer.Serialize(archived),
            ["audit-records"] = string.Join('|', "ArchiveFolder", "accepted", archived.FolderId, archived.CorrelationId, archived.TaskId),
            ["projections"] = JsonSerializer.Serialize(new
            {
                archived = true,
                lifecycleState = "inaccessible",
                folderId = archived.FolderId,
                correlationId = archived.CorrelationId,
                taskId = archived.TaskId,
            }),
            ["problem-details-examples"] = JsonSerializer.Serialize(new
            {
                category = "idempotency_conflict",
                code = "idempotency_conflict",
                correlationId = archived.CorrelationId,
                taskId = archived.TaskId,
            }),
            ["logs"] = "ArchiveFolder completed: Result=accepted, CorrelationId=correlation-a",
            ["traces"] = "operation=ArchiveFolder result=accepted tenant_scope=present",
            ["metric-labels"] = "operation=ArchiveFolder,result=accepted",
            ["exception-metadata"] = "idempotency_conflict",
        };
    }

    private static bool IsParseableJson(string payload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }
}
