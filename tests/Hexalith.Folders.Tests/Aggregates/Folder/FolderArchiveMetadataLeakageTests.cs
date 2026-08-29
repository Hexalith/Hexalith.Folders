using System.Globalization;
using System.Text;
using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

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
        "event",
        "projection",
        "problem-details",
    ];

    private static readonly IReadOnlyList<CorpusSentinel> CorpusSentinels = LoadCorpusSentinels();

    private static readonly IReadOnlyList<NegativeControl> NegativeControls = LoadNegativeControls();

    public static TheoryData<string> UnsafeActorEvidenceRows()
    {
        TheoryData<string> data = new();
        foreach ((string label, string value) in LegacyUnsafeActorValues)
        {
            data.Add(new TheoryDataRow<string>(value) { Label = label });
        }

        foreach (CorpusSentinel sentinel in CorpusSentinels)
        {
            data.Add(new TheoryDataRow<string>(sentinel.Value) { Label = $"corpus:{sentinel.Id}" });
        }

        return data;
    }

    public static TheoryData<string> CorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (CorpusSentinel sentinel in CorpusSentinels)
        {
            data.Add(new TheoryDataRow<string>(sentinel.Value) { Label = $"corpus:{sentinel.Id}" });
        }

        return data;
    }

    public static TheoryData<string> NegativeControlRows()
    {
        TheoryData<string> data = new();
        foreach (NegativeControl control in NegativeControls)
        {
            // The control id is safe provenance; the contaminated payload never reaches a
            // display name because the row carries only the id.
            data.Add(new TheoryDataRow<string>(control.Id) { Label = $"negative-control:{control.Id}" });
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnsafeActorEvidenceRows))]
    public void UnsafeActorEvidenceShouldRejectWithoutEchoingUnsafeIdentifier(string unsafeActor)
    {
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

        ContainsSentinel(JsonSerializer.Serialize(result), unsafeActor).ShouldBeFalse(
            "Serialized archive rejection result echoed the unsafe actor evidence value.");
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

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void ArchiveSafetySurfacesShouldNotEchoForbiddenSentinelCorpusValues(string sentinel)
    {
        IReadOnlyDictionary<string, string> surfaces = AcceptedArchiveSurfaces();

        foreach (KeyValuePair<string, string> surface in surfaces)
        {
            // Metadata-only: the surface name identifies the failing channel; the sentinel
            // text is deliberately absent from the message (see the idiom note above).
            ContainsSentinel(surface.Value, sentinel).ShouldBeFalse(
                $"Archive surface '{surface.Key}' leaked a forbidden sentinel corpus value.");
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

    [Theory]
    [MemberData(nameof(NegativeControlRows))]
    public void LeakageDetectorMustReportQuarantinedNegativeControls(string negativeControlId)
    {
        // Positive control: without this the whole sweep above could pass vacuously with a
        // detector that never reports anything.
        NegativeControl control = NegativeControls.Single(
            candidate => string.Equals(candidate.Id, negativeControlId, StringComparison.Ordinal));
        string sentinel = SentinelById(control.SampleId);

        ContainsSentinel(control.ContaminatedPayload, sentinel).ShouldBeTrue(
            $"Quarantined negative control '{control.Id}' must be reported as contaminated.");

        // The same sentinel behind \uXXXX escapes in a JSON property name is invisible to a
        // raw substring scan; only the decoded walk can see it.
        ContainsSentinel(EscapedJsonPropertyNamePayload(sentinel), sentinel).ShouldBeTrue(
            $"Escaped JSON property name carrying negative control '{control.Id}' must be reported as contaminated.");

        ContainsSentinel(EscapedNestedJsonStringPayload(sentinel), sentinel).ShouldBeTrue(
            $"Escaped nested JSON string carrying negative control '{control.Id}' must be reported as contaminated.");
    }

    [Fact]
    public void LeakageDetectorScansNonJsonPayloadsAsRawText()
    {
        string sentinel = SentinelById("secret-shaped-access-key");

        // Non-JSON surfaces (log templates, trace tags, metric labels) must be scanned as
        // raw text, not skipped and not thrown on.
        ContainsSentinel($"operation=ArchiveFolder actor={sentinel}", sentinel).ShouldBeTrue(
            "Non-JSON payloads must be scanned as raw text.");
        ContainsSentinel("operation=ArchiveFolder result=accepted", sentinel).ShouldBeFalse(
            "A clean non-JSON payload must not be reported as contaminated.");
    }

    [Fact]
    public void LeakageDetectorWalkScansNonStringJsonNodes()
    {
        // Deliberately calls the element walk instead of ContainsSentinel: the raw substring
        // pre-scan in ContainsSentinel would satisfy these assertions before the walk ever
        // runs, leaving the non-string branch untested. Deleting that branch must turn this
        // test red.
        using JsonDocument document = JsonDocument.Parse("{\"attempts\":20240229,\"stale\":true}");

        JsonElementContainsSentinel(document.RootElement.GetProperty("attempts"), "20240229").ShouldBeTrue(
            "Numeric JSON nodes must be scanned, not skipped as non-strings.");
        JsonElementContainsSentinel(document.RootElement.GetProperty("stale"), "true").ShouldBeTrue(
            "Boolean JSON nodes must be scanned, not skipped as non-strings.");
        JsonElementContainsSentinel(document.RootElement.GetProperty("attempts"), "19700101").ShouldBeFalse(
            "A clean numeric JSON node must not be reported as contaminated.");
    }

    private static IReadOnlyDictionary<string, string> AcceptedArchiveSurfaces()
    {
        FolderResult result = FolderAggregate.Handle(CreatedState(), FolderCommandFactory.Archive());
        FolderArchived archived = result.Events.OfType<FolderArchived>().Single();

        return new Dictionary<string, string>(StringComparer.Ordinal)
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
    }

    // Leakage detector. Reports true when `payload` carries `sentinel` either literally or
    // behind JSON escaping, in a property name or in any value node. Non-JSON payloads are
    // scanned as raw text instead of throwing; non-string JSON nodes are compared against
    // their raw token text instead of being skipped.
    private static bool ContainsSentinel(string payload, string sentinel)
    {
        if (payload.Contains(sentinel, StringComparison.Ordinal))
        {
            return true;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            // Not JSON: the raw scan above is the complete check for this surface.
            return false;
        }

        using (document)
        {
            return JsonElementContainsSentinel(document.RootElement, sentinel);
        }
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

    private static bool JsonElementContainsSentinel(JsonElement element, string sentinel)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Name.Contains(sentinel, StringComparison.Ordinal)
                        || JsonElementContainsSentinel(property.Value, sentinel))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (JsonElementContainsSentinel(item, sentinel))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.String:
                return element.GetString()?.Contains(sentinel, StringComparison.Ordinal) == true;

            default:
                return element.GetRawText().Contains(sentinel, StringComparison.Ordinal);
        }
    }

    private static string EscapedJsonPropertyNamePayload(string sentinel)
        => RequireWellFormedJson(
            "{\"" + EscapeEveryCharacter(sentinel) + "\":\"value\"}",
            "escaped JSON property name");

    private static string EscapedNestedJsonStringPayload(string sentinel)
        => RequireWellFormedJson(
            "{\"items\":[{\"note\":\"" + EscapeEveryCharacter(sentinel) + "\"}]}",
            "escaped nested JSON string");

    // Every UTF-16 code unit is emitted as a \uXXXX escape, surrogate halves included, so
    // the raw substring scan cannot see any part of the sentinel and no character — quote,
    // backslash, control character — can break the surrounding JSON.
    private static string EscapeEveryCharacter(string value)
    {
        StringBuilder builder = new(value.Length * 6);
        foreach (char character in value)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
        }

        return builder.ToString();
    }

    // The negative-control assertions read a `false` from ContainsSentinel as "the detector
    // failed to report contamination". ContainsSentinel returns false for unparsable input
    // too, so a malformed constructed payload would blame the control instead of this
    // helper. Fail loudly here instead. Metadata-only: the payload is never printed.
    private static string RequireWellFormedJson(string payload, string description)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"The '{description}' test helper produced invalid JSON, so the detector assertion below would misreport it as clean.");
        }

        return payload;
    }

    private static string SentinelById(string id)
    {
        // `sample_id` is declared safe provenance by the corpus, so naming the missing id is
        // metadata-only. A bare Single() would fail with an opaque LINQ message instead.
        CorpusSentinel? sentinel = CorpusSentinels
            .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));

        return sentinel is null
            ? throw new InvalidOperationException(
                $"audit-leakage-corpus.json no longer declares a usable sentinel sample with id '{id}'.")
            : sentinel.Value;
    }

    private static FolderState CreatedState()
    {
        FolderResult created = FolderAggregate.Handle(FolderState.Empty, FolderCommandFactory.Create());
        return FolderState.Empty.Apply(created.Events, FolderStreamName.Create("tenant-a", "folder-a"));
    }

    private static IReadOnlyList<CorpusSentinel> LoadCorpusSentinels()
    {
        string path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "audit-leakage-corpus.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        // Blank ids are filtered out along with blank values: an id is the row label, and
        // two blank ids would collapse into one duplicate label instead of two rows.
        List<CorpusSentinel> sentinels = document.RootElement
            .GetProperty("sentinel_samples")
            .EnumerateArray()
            .Select(sample => new CorpusSentinel(
                sample.GetProperty("id").GetString() ?? string.Empty,
                sample.GetProperty("value").GetString() ?? string.Empty))
            .Where(sentinel => !string.IsNullOrWhiteSpace(sentinel.Value) && !string.IsNullOrWhiteSpace(sentinel.Id))
            .ToList();

        if (sentinels.Count == 0)
        {
            throw new InvalidOperationException("audit-leakage-corpus.json declared no usable sentinel samples.");
        }

        // Ids are row labels and the lookup key for SentinelById. Duplicates would collide
        // two xUnit labels into one and make the lookup silently resolve the first match.
        if (sentinels.Select(sentinel => sentinel.Id).Distinct(StringComparer.Ordinal).Count() != sentinels.Count)
        {
            throw new InvalidOperationException("audit-leakage-corpus.json declared duplicate sentinel sample ids.");
        }

        return sentinels;
    }

    private static IReadOnlyList<NegativeControl> LoadNegativeControls()
    {
        string path = Path.Combine(RepositoryRoot(), "tests", "fixtures", "quarantine", "safety-negative-controls.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        List<NegativeControl> controls = document.RootElement
            .GetProperty("negative_controls")
            .EnumerateArray()
            .Select(control => new NegativeControl(
                control.GetProperty("id").GetString() ?? string.Empty,
                control.GetProperty("sample_id").GetString() ?? string.Empty,
                control.GetProperty("contaminated_payload").GetString() ?? string.Empty))
            .Where(control => !string.IsNullOrWhiteSpace(control.ContaminatedPayload)
                && !string.IsNullOrWhiteSpace(control.Id)
                && !string.IsNullOrWhiteSpace(control.SampleId))
            .ToList();

        if (controls.Count == 0)
        {
            throw new InvalidOperationException("safety-negative-controls.json declared no usable negative controls.");
        }

        // Same reasoning as the corpus ids above; the row lookup below uses Single(), which
        // would otherwise fail with an opaque LINQ message instead of naming the fixture.
        if (controls.Select(control => control.Id).Distinct(StringComparer.Ordinal).Count() != controls.Count)
        {
            throw new InvalidOperationException("safety-negative-controls.json declared duplicate negative-control ids.");
        }

        return controls;
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

    private sealed record CorpusSentinel(string Id, string Value);

    private sealed record NegativeControl(string Id, string SampleId, string ContaminatedPayload);
}
