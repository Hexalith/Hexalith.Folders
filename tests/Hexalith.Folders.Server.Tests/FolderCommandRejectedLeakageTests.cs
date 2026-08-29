using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

// Corpus-driven regression coverage for the rejection-event canonicalization seam.
//
// FolderCommandRejected.Create canonicalizes caller-supplied identifiers that travel on the
// /process wire response and into downstream log/trace/audit surfaces. It is this event's
// only construction entry point, but NOT the repository's only sanitizer: the sibling
// rejection events emitted by FolderDomainProcessor call the shared CanonicalIdentifierOrNull
// / NormalizeCommandTypeForRejection helpers directly and are not covered here. Every
// nonblank sentinel in tests/fixtures/audit-leakage-corpus.json is driven through that seam
// twice: once wrapped in a deterministic noncanonical wrapper, and once raw. The raw rows
// are the charset regression trap — widening SafeIdentifierRegex to accept uppercase must
// turn them red. They do NOT trap the length cap: the longest corpus value is 46 characters,
// far under MaxCanonicalIdentifierLength, so removing the cap leaves every raw row green.
// The cap is trapped separately by NamedShapeThreatsAreDroppedFromRejectionIdentifiers,
// which pins the cap's literal value as well as its behavior.
//
// Rows are labelled by the corpus's stable `id` so no sentinel text reaches an xUnit display
// name. The corpus/quarantine readers below are deliberately local; consolidating them into
// one shared labelled reader is recorded in this story's spec frontmatter `deferred:` list.
public sealed partial class FolderCommandRejectedLeakageTests
{
    private const string NoncanonicalWrapperPrefix = "noncanonical::";

    private const string NoncanonicalWrapperSuffix = "::value";

    // The one corpus sample whose value ("synthetic-repository-name") already matches
    // ^[a-z0-9._-]+$. It is indistinguishable from a legitimate folder identifier, so
    // dropping it is NOT a production invariant and must not be asserted as one. Excluded
    // by stable corpus id rather than by charset probing so the exclusion stays reviewable;
    // CanonicalShapedCorpusSentinelIsPreservedAndSolelyExcluded pins the exclusion as
    // behavior so it cannot silently stop applying.
    private const string CanonicalShapedCorpusSentinelId = "repository-name-metadata";

    private static readonly IReadOnlyList<CorpusSentinel> CorpusSentinels = LoadCorpusSentinels();

    private static readonly IReadOnlyList<NegativeControl> NegativeControls = LoadNegativeControls();

    public static TheoryData<string> CorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (CorpusSentinel sentinel in CorpusSentinels)
        {
            data.Add(new TheoryDataRow<string>(sentinel.Value) { Label = $"corpus:{sentinel.Id}" });
        }

        return data;
    }

    public static TheoryData<string> NoncanonicalRawCorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (CorpusSentinel sentinel in CorpusSentinels)
        {
            if (string.Equals(sentinel.Id, CanonicalShapedCorpusSentinelId, StringComparison.Ordinal))
            {
                continue;
            }

            data.Add(new TheoryDataRow<string>(sentinel.Value) { Label = $"corpus:{sentinel.Id}" });
        }

        return data;
    }

    public static TheoryData<string> NegativeControlRows()
    {
        TheoryData<string> data = new();
        foreach (NegativeControl control in NegativeControls)
        {
            data.Add(new TheoryDataRow<string>(control.Id) { Label = $"negative-control:{control.Id}" });
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void NoncanonicalWrappedSentinelIsDroppedFromEveryRejectionIdentifier(string sentinel)
    {
        string wrapped = NoncanonicalWrapperPrefix + sentinel + NoncanonicalWrapperSuffix;

        // The wrapper row must be dropped for its shape ("::" fails both canonical regexes),
        // never for tripping the length cap — otherwise a longer corpus sample would silently
        // retire this row's shape coverage while keeping it green.
        (wrapped.Length <= FolderCommandRejected.MaxCanonicalIdentifierLength).ShouldBeTrue(
            "The wrapped corpus row exceeded the canonical length cap, so it no longer proves shape rejection.");

        FolderCommandRejected rejected = FolderCommandRejected.Create(
            code: nameof(FolderResultCode.MalformedEvidence),
            commandType: wrapped,
            managedTenantId: wrapped,
            organizationId: wrapped,
            folderId: wrapped,
            actorPrincipalId: wrapped,
            correlationId: wrapped,
            taskId: wrapped,
            idempotencyKey: wrapped);

        AssertEveryIdentifierIsNull(rejected);

        // Metadata-only, same idiom as AssertEveryIdentifierIsNull: ShouldBe renders the
        // ACTUAL CommandType into the failure message, and on the regression this row exists
        // to catch that actual value IS the hostile wrapped sentinel.
        string.Equals(
            rejected.CommandType,
            FolderCommandRejected.UnknownCommandTypeSentinel,
            StringComparison.Ordinal).ShouldBeTrue(
            "A noncanonical command type must collapse to the fixed unknown sentinel.");

        string json = JsonSerializer.Serialize(rejected);
        ContainsSentinel(json, sentinel).ShouldBeFalse(
            "Serialized rejection event echoed a corpus sentinel value.");
        ContainsSentinel(json, NoncanonicalWrapperPrefix).ShouldBeFalse(
            "Serialized rejection event echoed the noncanonical wrapper text.");
    }

    [Theory]
    [MemberData(nameof(NoncanonicalRawCorpusSentinelRows))]
    public void RawSentinelIsDroppedFromEveryRejectionIdentifier(string sentinel)
    {
        // commandType stays canonical here on purpose: NormalizeCommandTypeForRejection
        // deliberately passes uppercase dotted values through (characterized by
        // RawSentinelCommandTypeFollowsDocumentedNormalizationPolicy below), so asserting
        // that a raw uppercase sentinel is canonicalized in commandType would pin behavior
        // production does not have.
        FolderCommandRejected rejected = FolderCommandRejected.Create(
            code: nameof(FolderResultCode.MalformedEvidence),
            commandType: FoldersServerModule.ArchiveFolderCommandType,
            managedTenantId: sentinel,
            organizationId: sentinel,
            folderId: sentinel,
            actorPrincipalId: sentinel,
            correlationId: sentinel,
            taskId: sentinel,
            idempotencyKey: sentinel);

        AssertEveryIdentifierIsNull(rejected);
        rejected.CommandType.ShouldBe(FoldersServerModule.ArchiveFolderCommandType);

        ContainsSentinel(JsonSerializer.Serialize(rejected), sentinel).ShouldBeFalse(
            "Serialized rejection event echoed a raw corpus sentinel value.");
    }

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void RawSentinelCommandTypeFollowsDocumentedNormalizationPolicy(string sentinel)
    {
        ArgumentNullException.ThrowIfNull(sentinel);

        // Characterization, not endorsement. NormalizeCommandTypeForRejection deliberately
        // lets any ^[A-Za-z0-9._-]+$ value under the length cap ride the wire, so a raw
        // uppercase corpus sentinel is echoed verbatim in CommandType while the identifier
        // fields drop it. Pinned here so tightening — or further loosening — that policy
        // shows up as a red row instead of silently changing what reaches downstream
        // log/alert keyspaces. RelaxedCommandTypeShape mirrors the production CommandTypeRegex
        // on purpose; a divergence surfaces as a failing row.
        FolderCommandRejected rejected = FolderCommandRejected.Create(
            code: nameof(FolderResultCode.MalformedEvidence),
            commandType: sentinel,
            managedTenantId: null,
            organizationId: null,
            folderId: null,
            actorPrincipalId: null,
            correlationId: null,
            taskId: null,
            idempotencyKey: null);

        bool passesThroughByPolicy = sentinel.Length <= FolderCommandRejected.MaxCanonicalIdentifierLength
            && RelaxedCommandTypeShape().IsMatch(sentinel);

        if (passesThroughByPolicy)
        {
            // Metadata-only: string.Equals rather than ShouldBe so the sentinel is never
            // rendered into the failure message.
            string.Equals(rejected.CommandType, sentinel, StringComparison.Ordinal).ShouldBeTrue(
                "A command type matching the documented relaxed shape must pass through unchanged.");
        }
        else
        {
            // Metadata-only for the same reason as the wrapped theory above.
            string.Equals(
                rejected.CommandType,
                FolderCommandRejected.UnknownCommandTypeSentinel,
                StringComparison.Ordinal).ShouldBeTrue(
                "A command type outside the documented relaxed shape must collapse to the fixed sentinel.");
        }
    }

    [Fact]
    public void CanonicalShapedCorpusSentinelIsPreservedAndSolelyExcluded()
    {
        // Pins the RawSentinelIsDroppedFromEveryRejectionIdentifier exclusion as behavior
        // instead of leaving it as a comment: `synthetic-repository-name` already matches
        // ^[a-z0-9._-]+$, so CanonicalIdentifierOrNull keeps it, and dropping it is not a
        // production invariant. A corpus rename or reshape turns this red rather than
        // silently retiring the exclusion.
        string sentinel = SentinelById(CanonicalShapedCorpusSentinelId);

        FolderCommandRejected rejected = CreateWithIdentifier(sentinel);

        string.Equals(rejected.ManagedTenantId, sentinel, StringComparison.Ordinal).ShouldBeTrue(
            $"Corpus sample '{CanonicalShapedCorpusSentinelId}' must still be canonical-identifier shaped and preserved.");
        string.Equals(rejected.CorrelationId, sentinel, StringComparison.Ordinal).ShouldBeTrue(
            $"Corpus sample '{CanonicalShapedCorpusSentinelId}' must still be canonical-identifier shaped and preserved.");

        NoncanonicalRawCorpusSentinelRows().Count.ShouldBe(
            CorpusSentinelRows().Count - 1,
            "Exactly one corpus sample may be excluded from the raw-sentinel drop rows.");
    }

    [Fact]
    public void NamedShapeThreatsAreDroppedFromRejectionIdentifiers()
    {
        // The two shape threats named in FolderCommandRejected's own header comment.
        //
        // The cap's VALUE is pinned first: every input and expectation below is derived from
        // the constant, so raising it moves both sides in lockstep and would leave this test
        // green while a multi-kilobyte caller identifier reached the rejection payload and the
        // twelve other production sites that share FoldersServerModule.MaxCanonicalIdentifierLength.
        FolderCommandRejected.MaxCanonicalIdentifierLength.ShouldBe(128);

        string crLfInjected = "safe-identifier\r\ninjected-log-line";
        string overLength = new('a', FolderCommandRejected.MaxCanonicalIdentifierLength + 1);

        FolderCommandRejected crLfRejected = CreateWithIdentifier(crLfInjected);
        AssertEveryIdentifierIsNull(crLfRejected);
        ContainsSentinel(JsonSerializer.Serialize(crLfRejected), "injected-log-line").ShouldBeFalse(
            "A CR/LF-carrying identifier must not reach the serialized rejection event.");

        FolderCommandRejected overLengthRejected = CreateWithIdentifier(overLength);
        AssertEveryIdentifierIsNull(overLengthRejected);

        // Boundary control: at exactly the cap the value survives, so the assertion above
        // pins the cap rather than an all-destroying factory.
        string atLimit = new('a', FolderCommandRejected.MaxCanonicalIdentifierLength);
        FolderCommandRejected atLimitRejected = CreateWithIdentifier(atLimit);
        atLimitRejected.ManagedTenantId.ShouldBe(atLimit);
        atLimitRejected.CorrelationId.ShouldBe(atLimit);
    }

    [Fact]
    public void CanonicalRejectionPreservesEverySuppliedValue()
    {
        // Positive control. Without this, every drop assertion above is equally satisfied by
        // a factory that destroys all caller-supplied evidence.
        FolderCommandRejected rejected = FolderCommandRejected.Create(
            code: nameof(FolderResultCode.MalformedEvidence),
            commandType: FoldersServerModule.ArchiveFolderCommandType,
            managedTenantId: "tenant-a",
            organizationId: "organization-a",
            folderId: "folder-a",
            actorPrincipalId: "principal-a",
            correlationId: "correlation-a",
            taskId: "task-a",
            idempotencyKey: "idempotency-archive-a");

        rejected.Code.ShouldBe(nameof(FolderResultCode.MalformedEvidence));
        rejected.CommandType.ShouldBe(FoldersServerModule.ArchiveFolderCommandType);
        rejected.ManagedTenantId.ShouldBe("tenant-a");
        rejected.OrganizationId.ShouldBe("organization-a");
        rejected.FolderId.ShouldBe("folder-a");
        rejected.ActorPrincipalId.ShouldBe("principal-a");
        rejected.CorrelationId.ShouldBe("correlation-a");
        rejected.TaskId.ShouldBe("task-a");
        rejected.IdempotencyKey.ShouldBe("idempotency-archive-a");
    }

    [Theory]
    [MemberData(nameof(NegativeControlRows))]
    public void LeakageDetectorMustReportQuarantinedNegativeControls(string negativeControlId)
    {
        // Positive control for the detector itself: without it the sweeps above could pass
        // vacuously with a detector that never reports anything.
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

        ContainsSentinel($"rejection commandType=unknown_command_type actor={sentinel}", sentinel).ShouldBeTrue(
            "Non-JSON payloads must be scanned as raw text.");
        ContainsSentinel("rejection commandType=unknown_command_type result=rejected", sentinel).ShouldBeFalse(
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

    // Mirrors the production CommandTypeRegex in FolderCommandRejected. Duplicated on
    // purpose: this suite characterizes the documented pass-through policy, so a production
    // change that diverges from this shape must surface as a failing row rather than pass
    // silently through a shared constant.
    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RelaxedCommandTypeShape();

    private static FolderCommandRejected CreateWithIdentifier(string identifier)
        => FolderCommandRejected.Create(
            code: nameof(FolderResultCode.MalformedEvidence),
            commandType: FoldersServerModule.ArchiveFolderCommandType,
            managedTenantId: identifier,
            organizationId: identifier,
            folderId: identifier,
            actorPrincipalId: identifier,
            correlationId: identifier,
            taskId: identifier,
            idempotencyKey: identifier);

    // Metadata-only assertion idiom. Shouldly's ShouldBeNull() renders the *actual* value
    // into the failure message, which would echo the hostile input into the
    // assertion-messages channel the corpus itself declares forbidden. Do not "simplify"
    // these back into value-printing assertions: that silently reopens the leak this suite
    // exists to close.
    private static void AssertEveryIdentifierIsNull(FolderCommandRejected rejected)
    {
        (rejected.ManagedTenantId is null).ShouldBeTrue("ManagedTenantId must be dropped to null.");
        (rejected.OrganizationId is null).ShouldBeTrue("OrganizationId must be dropped to null.");
        (rejected.FolderId is null).ShouldBeTrue("FolderId must be dropped to null.");
        (rejected.ActorPrincipalId is null).ShouldBeTrue("ActorPrincipalId must be dropped to null.");
        (rejected.CorrelationId is null).ShouldBeTrue("CorrelationId must be dropped to null.");
        (rejected.TaskId is null).ShouldBeTrue("TaskId must be dropped to null.");
        (rejected.IdempotencyKey is null).ShouldBeTrue("IdempotencyKey must be dropped to null.");
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
            return false;
        }

        using (document)
        {
            return JsonElementContainsSentinel(document.RootElement, sentinel);
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
