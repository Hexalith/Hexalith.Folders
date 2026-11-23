using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Folders.Aggregates.Folder;

using Shouldly;

using Xunit;

using Hexalith.Folders.Tests.Shared.AuditLeakage;

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
// name or serialized theory arguments.
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

    public static TheoryData<string> CorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (AuditLeakageSample sample in AuditLeakageCorpus.Samples)
        {
            data.Add(new TheoryDataRow<string>(sample.Id) { Label = $"corpus:{sample.Id}" });
        }

        return data;
    }

    public static TheoryData<string> NoncanonicalRawCorpusSentinelRows()
    {
        TheoryData<string> data = new();
        foreach (AuditLeakageSample sample in AuditLeakageCorpus.Samples)
        {
            if (string.Equals(sample.Id, CanonicalShapedCorpusSentinelId, StringComparison.Ordinal))
            {
                continue;
            }

            data.Add(new TheoryDataRow<string>(sample.Id) { Label = $"corpus:{sample.Id}" });
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void NoncanonicalWrappedSentinelIsDroppedFromEveryRejectionIdentifier(string sampleId)
    {
        string sentinel = AuditLeakageCorpus.Get(sampleId).Value;
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
        AuditLeakageMatcher.Contains(json, sentinel).ShouldBeFalse(
            "Serialized rejection event echoed a corpus sentinel value.");
        AuditLeakageMatcher.Contains(json, NoncanonicalWrapperPrefix).ShouldBeFalse(
            "Serialized rejection event echoed the noncanonical wrapper text.");
    }

    [Theory]
    [MemberData(nameof(NoncanonicalRawCorpusSentinelRows))]
    public void RawSentinelIsDroppedFromEveryRejectionIdentifier(string sampleId)
    {
        string sentinel = AuditLeakageCorpus.Get(sampleId).Value;
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
        string.Equals(rejected.CommandType, FoldersServerModule.ArchiveFolderCommandType, StringComparison.Ordinal)
            .ShouldBeTrue("The rejection must retain its canonical archive command type.");

        AuditLeakageMatcher.Contains(JsonSerializer.Serialize(rejected), sentinel).ShouldBeFalse(
            "Serialized rejection event echoed a raw corpus sentinel value.");
    }

    [Theory]
    [MemberData(nameof(CorpusSentinelRows))]
    public void RawSentinelCommandTypeFollowsDocumentedNormalizationPolicy(string sampleId)
    {
        string sentinel = AuditLeakageCorpus.Get(sampleId).Value;

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
        string sentinel = AuditLeakageCorpus.Get(CanonicalShapedCorpusSentinelId).Value;

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

        AssertEveryIdentifierIsNull(CreateWithIdentifier("safe-identifier\n"));
        AssertEveryIdentifierIsNull(CreateWithIdentifier("safe-identifier\r\n"));

        string crLfInjected = "safe-identifier\r\ninjected-log-line";
        string overLength = new('a', FolderCommandRejected.MaxCanonicalIdentifierLength + 1);

        FolderCommandRejected crLfRejected = CreateWithIdentifier(crLfInjected);
        AssertEveryIdentifierIsNull(crLfRejected);
        AuditLeakageMatcher.Contains(JsonSerializer.Serialize(crLfRejected), "injected-log-line").ShouldBeFalse(
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

    // Mirrors the production CommandTypeRegex in FolderCommandRejected. Duplicated on
    // purpose: this suite characterizes the documented pass-through policy, so a production
    // change that diverges from this shape must surface as a failing row rather than pass
    // silently through a shared constant.
    [GeneratedRegex("\\A[A-Za-z0-9._-]+\\z", RegexOptions.CultureInvariant)]
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
}
