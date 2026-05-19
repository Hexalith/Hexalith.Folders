using System.Text.Json;
using System.Text.RegularExpressions;
using Hexalith.Folders.Aggregates.Folder;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

public sealed class FolderCreationMetadataLeakageTests
{
    [Theory]
    [InlineData("github_pat_credential_material")]
    [InlineData("provider-token-secret")]
    [InlineData("repo-internal-name")]
    [InlineData("branch-main")]
    [InlineData("C:/tenant/private/path.txt")]
    [InlineData("raw file content sentinel")]
    [InlineData("diff --git a/secret b/secret")]
    [InlineData("generated context payload")]
    [InlineData("person@example.test")]
    [InlineData("group display name")]
    [InlineData("unauthorized-resource-name")]
    [InlineData("tok​en")] // ZWSP-injected; IsSafeMetadata must reject after normalize-and-format-char strip
    [InlineData("crédential")] // NFD-decomposed combining acute; canonical form is "crédential" — still contains "credential" only after NFC; this input contains "cre"+combiner — should fail control/format check
    public void UnsafeMetadataShouldNotAppearInStructuredResultOrEvents(string sentinel)
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: sentinel, description: sentinel, tags: [sentinel]));

        result.Code.ShouldBe(FolderResultCode.InvalidFolderMetadata);
        result.Events.ShouldBeEmpty();
        JsonSerializer.Serialize(result).ShouldNotContain(sentinel);
    }

    [Fact]
    public void AcceptedEventShouldContainOnlySafeMetadata()
    {
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Operations", description: "review notes", tags: ["ops", "audit"]));

        // Serialize the concrete `FolderCreated` so DisplayName/Description/Tags/etc. are
        // included; the `IFolderEvent` interface intentionally omits those fields, so the
        // earlier `JsonSerializer.Serialize(result.Events)` (typed as `IReadOnlyList<IFolderEvent>`)
        // only emitted interface members and could not detect a leak in non-interface fields.
        FolderCreated created = result.Events.OfType<FolderCreated>().Single();
        string json = JsonSerializer.Serialize(created);

        json.ShouldContain("Operations");
        json.ShouldContain("tenant-a");
        json.ShouldContain("folder-a");
        // Assert on dangerous data substrings rather than bare words — `RepositoryBindingState`
        // is a legitimate field name containing "Repo", so a bare `ShouldNotContain("repo")`
        // would be a false positive. The assertions below match the validator's forbidden
        // metadata list (credentials/tokens/diffs/etc.) which would only appear if a forbidden
        // value leaked through validation into the event payload.
        json.ShouldNotContain("credential", Case.Insensitive);
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("secret", Case.Insensitive);
        json.ShouldNotContain("repo-", Case.Insensitive);
        json.ShouldNotContain("branch-main", Case.Insensitive);
        json.ShouldNotContain("diff --git", Case.Insensitive);
        json.ShouldNotContain("@", Case.Insensitive);
    }

    [Fact]
    public void AcceptedEventIdempotencyFingerprintShouldBeOpaqueDigestNotPlaintext()
    {
        // After D1 the fingerprint is a SHA-256 hex digest: 64 lowercase hex chars, no
        // separators, no embedded metadata. This test would have passed under the previous
        // raw `|`-joined plaintext fingerprint too (the digest produces lowercase hex), so
        // we additionally assert the format precisely and confirm the display name and
        // forbidden-substring sentinels do not survive the canonicalization-and-hash step.
        FolderResult result = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Customer Smith", description: "audit-review"));

        FolderCreated created = result.Events.OfType<FolderCreated>().Single();

        created.IdempotencyFingerprint.ShouldMatch("^[0-9a-f]{64}$");
        created.IdempotencyFingerprint.ShouldNotContain("customer", Case.Insensitive);
        created.IdempotencyFingerprint.ShouldNotContain("smith", Case.Insensitive);
        created.IdempotencyFingerprint.ShouldNotContain("audit");
        created.IdempotencyFingerprint.ShouldNotContain("|");
        created.IdempotencyFingerprint.ShouldNotContain(",");
    }

    [Fact]
    public void IdempotencyFingerprintShouldBeStableForSemanticallyEquivalentCommands()
    {
        // Two commands with the same canonical metadata must produce the same fingerprint;
        // the canonicalization step (NFC, lowercase, trim) is exercised here so future
        // changes to the canonicalization rules cannot silently divert equivalent commands.
        FolderResult first = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Operations"));
        FolderResult second = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "  operations  "));

        FolderCreated firstEvent = first.Events.OfType<FolderCreated>().Single();
        FolderCreated secondEvent = second.Events.OfType<FolderCreated>().Single();

        firstEvent.IdempotencyFingerprint.ShouldBe(secondEvent.IdempotencyFingerprint);
    }

    [Fact]
    public void IdempotencyFingerprintShouldDifferForDifferentDisplayNames()
    {
        FolderResult first = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Folder A"));
        FolderResult second = FolderAggregate.Handle(
            FolderState.Empty,
            FolderCommandFactory.Create(displayName: "Folder B"));

        FolderCreated firstEvent = first.Events.OfType<FolderCreated>().Single();
        FolderCreated secondEvent = second.Events.OfType<FolderCreated>().Single();

        firstEvent.IdempotencyFingerprint.ShouldNotBe(secondEvent.IdempotencyFingerprint);
    }
}
