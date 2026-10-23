using System.Reflection;
using System.Security.Cryptography;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Hashing;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Forgejo;
using Hexalith.Folders.Providers.GitHub;
using Hexalith.Folders.Providers.Shared;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Helpers;

public sealed class HelperCompatibilityTests
{
    [Theory]
    [InlineData(null, "original")]
    [InlineData("", "")]
    [InlineData("payload", "payload")]
    public void TenantMergeCopiesOrdinalKeysAndOnlyOverwritesForNonNullPayload(string? payload, string expected)
    {
        Dictionary<string, string?> source = new(StringComparer.OrdinalIgnoreCase)
        {
            ["payload_tenant_id"] = "original",
            ["TENANT"] = "authoritative",
        };

        Func<IReadOnlyDictionary<string, string?>, string?, IReadOnlyDictionary<string, string?>> merge = ClientTenantValues.WithPayloadTenant;
        IReadOnlyDictionary<string, string?> result = merge(source, payload);

        result.ShouldNotBeSameAs(source);
        result["payload_tenant_id"].ShouldBe(expected);
        result["TENANT"].ShouldBe("authoritative");
        result.ContainsKey("tenant").ShouldBeFalse();
        source["payload_tenant_id"].ShouldBe("original");
        merge(new Dictionary<string, string?>(), null).ShouldBeEmpty();

        Dictionary<string, string?> ordinalSource = new(StringComparer.Ordinal)
        {
            ["payload_tenant_id"] = "original",
            ["PAYLOAD_TENANT_ID"] = "case-distinct",
        };
        IReadOnlyDictionary<string, string?> ordinalResult = merge(ordinalSource, payload);
        ordinalResult["payload_tenant_id"].ShouldBe(expected);
        ordinalResult["PAYLOAD_TENANT_ID"].ShouldBe("case-distinct");
        ordinalSource["payload_tenant_id"].ShouldBe("original");
        ordinalSource["PAYLOAD_TENANT_ID"].ShouldBe("case-distinct");
    }

    [Theory]
    [InlineData(LayeredAuthorizationOutcomeCodes.AuthenticationDenied, FolderResultCode.MissingAuthoritativeTenant)]
    [InlineData(LayeredAuthorizationOutcomeCodes.ClaimTransformDenied, FolderResultCode.TenantAccessDenied)]
    [InlineData(LayeredAuthorizationOutcomeCodes.TenantProjectionStale, FolderResultCode.StaleProjection)]
    [InlineData(LayeredAuthorizationOutcomeCodes.TenantProjectionUnavailable, FolderResultCode.UnavailableProjection)]
    [InlineData(LayeredAuthorizationOutcomeCodes.FolderAclDenied, FolderResultCode.FolderAclDenied)]
    [InlineData(LayeredAuthorizationOutcomeCodes.SafeNotFound, FolderResultCode.FolderAclDenied)]
    [InlineData(LayeredAuthorizationOutcomeCodes.FolderAclStale, FolderResultCode.StaleProjection)]
    [InlineData(LayeredAuthorizationOutcomeCodes.FolderAclUnavailable, FolderResultCode.UnavailableProjection)]
    [InlineData(LayeredAuthorizationOutcomeCodes.EventStoreValidatorDenied, FolderResultCode.TenantAccessDenied)]
    [InlineData(LayeredAuthorizationOutcomeCodes.DaprPolicyDenied, FolderResultCode.PolicyEvidenceUnavailable)]
    [InlineData(LayeredAuthorizationOutcomeCodes.AuthorizationEvidenceMalformed, FolderResultCode.MalformedEvidence)]
    [InlineData("unknown", FolderResultCode.TenantAccessDenied)]
    public void FolderAuthorizationKeepsEveryOutcomeAndFailsClosed(string outcome, FolderResultCode expected)
        => FolderAuthorizationResultMapper.Map(outcome).ShouldBe(expected);

    [Theory]
    [InlineData(LayeredAuthorizationOutcomeCodes.FolderAclDenied, OrganizationProviderBindingResultCode.MissingPermission)]
    [InlineData(LayeredAuthorizationOutcomeCodes.SafeNotFound, OrganizationProviderBindingResultCode.MissingPermission)]
    [InlineData(LayeredAuthorizationOutcomeCodes.DaprPolicyDenied, OrganizationProviderBindingResultCode.UnavailableProjection)]
    public void OrganizationAuthorizationRetainsItsDistinctPolicy(string outcome, OrganizationProviderBindingResultCode expected)
        => Method<Func<string, OrganizationProviderBindingResultCode>>(typeof(ConfigureProviderBindingService), "MapAuthorization")(outcome).ShouldBe(expected);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("folder-a._1", true)]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAV", true)]
    [InlineData("folder\n", false)]
    [InlineData("folder/a", false)]
    public void StreamSegmentsKeepTheirGrammar(string? value, bool expected)
    {
        FolderStreamName.IsValidSegment(value).ShouldBe(expected);
        OrganizationStreamName.IsValidSegment(value).ShouldBe(expected);
    }

    [Fact]
    public void StreamBoundariesAndReservedTenantDifferencesRemainObservable()
    {
        FolderStreamName.IsValidSegment(new string('a', 256)).ShouldBeTrue();
        OrganizationStreamName.IsValidSegment(new string('a', 256)).ShouldBeTrue();
        FolderStreamName.IsValidSegment(new string('a', 257)).ShouldBeFalse();
        OrganizationStreamName.IsValidSegment(new string('a', 257)).ShouldBeFalse();
        foreach (string value in new[] { "system", "SYSTEM", " system " })
        {
            FolderStreamName.TryCreate(value, "folder-a", out _, out FolderResultCode folderCode).ShouldBeFalse();
            folderCode.ShouldBe(value == "system" ? FolderResultCode.ReservedTenant : FolderResultCode.InvalidTenant);
            OrganizationStreamName.TryCreate(value, "organization-a", out _, out OrganizationAclResultCode organizationCode).ShouldBeFalse();
            organizationCode.ShouldBe(OrganizationAclResultCode.ReservedTenant);
        }

        Should.Throw<ArgumentException>(() => FolderStreamName.Create("tenant-a", "bad/id")).ParamName.ShouldBe("folderId");
        Should.Throw<ArgumentException>(() => OrganizationStreamName.Create("tenant-a", "bad/id")).ParamName.ShouldBe("managedTenantId");
    }

    [Fact]
    public void ProviderGuardsKeepOpaqueFingerprintAndReferenceBoundaries()
    {
        Func<string?, bool> opaque = ProviderAdapterGuards.IsSafeOpaqueValue;
        foreach (string? value in new[] { null, "", " ", "https://host", "a\n", "e\u0301", "\ud800", new string('a', 513) })
        {
            opaque(value).ShouldBeFalse();
        }

        opaque("opaque value:é").ShouldBeTrue();
        opaque(new string('a', 512)).ShouldBeTrue();

        Func<string?, bool> fingerprint = ProviderAdapterGuards.IsSafeFingerprint;
        fingerprint(new string('a', 64)).ShouldBeTrue();
        foreach (string? value in new[] { null, new string('a', 63), new string('a', 65), new string('A', 64), new string('g', 64) })
        {
            fingerprint(value).ShouldBeFalse();
        }

        Func<string?, bool> reference = ProviderAdapterGuards.IsSafeOperationReference;
        reference("ABC-_.:123").ShouldBeTrue();
        reference(new string('a', 128)).ShouldBeTrue();
        foreach (string? value in new[] { null, "", "a/b", "a b", "é", new string('a', 129) })
        {
            reference(value).ShouldBeFalse();
        }

        Func<HashSet<string>, string, bool> ancestor = ProviderAdapterGuards.HasAncestorConflict;
        HashSet<string> paths = new(StringComparer.Ordinal) { "dir/file" };
        ancestor(paths, "dir/file").ShouldBeFalse();
        ancestor(paths, "dir").ShouldBeTrue();
        ancestor(paths, "dir/file/child").ShouldBeTrue();
        ancestor(paths, "di").ShouldBeFalse();
        ancestor(paths, "DIR").ShouldBeFalse();
    }

    [Fact]
    public void LegacyEncodingKeepsNullAsEmptyUnicodeAndFieldBoundaries()
    {
        Action<IncrementalHash, string?> append = LengthPrefixedHash.AppendField;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        append(hash, null);
        append(hash, "");
        append(hash, "e\u0301");
        append(hash, "A");
        append(hash, "B");
        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant().ShouldBe("088fcc2fe85ef217f83b9982c5cde613f20e6cab4fa986cba6e269aece82ee93");
        FolderCommandValidator.ExternalRepositoryRefFingerprint("tenant-a", "binding-a", "external-a")
            .ShouldBe("2baf8c28b9711342c428b2e8ee2f4a12789acf0dd0e6cf1d69703d470b5fc849");
    }

    [Fact]
    public void LegacyIntegerEncodingKeepsSignedLittleEndianBoundaries()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        LengthPrefixedHash.AppendInt32(hash, int.MinValue);
        LengthPrefixedHash.AppendInt32(hash, 0);
        LengthPrefixedHash.AppendInt32(hash, int.MaxValue);
        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant().ShouldBe("01934b958b7325445e336fba43fd6440479c7bccfe7b1a98d45b0b0ad18b1587");
    }

    [Fact]
    public void CanonicalScalarsAndCollectionCountsKeepTheirExactBytes()
    {
        GitHubProviderSafeOperationEvidence.Compute("hxf-github:v1:scalar-vector", writer =>
        {
            writer.AppendBoolean(false);
            writer.AppendBoolean(true);
            writer.AppendUInt32(0);
            writer.AppendUInt32(uint.MaxValue);
            writer.AppendUInt64(0);
            writer.AppendUInt64(ulong.MaxValue);
            writer.AppendBytes([]);
            writer.AppendBytes([0, 255]);
            writer.AppendCollectionCount(0);
            writer.AppendCollectionCount(int.MaxValue);
        }).ShouldBe("1f8c8e3a71c7b12b9160ad903d25bab3ccd6e6c54d6d586f1bc2abdaca2f5014");

        ForgejoProviderSafeOperationEvidence.Compute("hxf-forgejo:v1:scalar-vector", writer =>
        {
            writer.AppendUInt32(0);
            writer.AppendUInt32(uint.MaxValue);
            writer.AppendBytes([]);
            writer.AppendBytes([0, 255]);
            writer.AppendCollectionCount(0);
            writer.AppendCollectionCount(int.MaxValue);
        }).ShouldBe("129719af6c4194e4a7c0f82703eb1715e3f0413710166c865ed7b78acd108fac");
    }

    [Fact]
    public void ProviderTargetAndCapabilityFingerprintsKeepTheirLegacyFieldOrder()
    {
        ProviderCapabilityDiscoveryRequest request = new(
            "tenant-a", "organization-a", "binding-a", "credential-a", "github", "github", "v1",
            new ProviderTargetEvidence("github", "1", "api-v1", "evidence-v1", false, null,
                new Dictionary<string, string> { ["zeta"] = "last", ["alpha"] = "é" }),
            [ProviderCredentialMode.AppInstallationReference],
            new ProviderAuthorizationEvidenceSnapshot("authorization-a", DateTimeOffset.UnixEpoch, "fresh"),
            "correlation-a");

        GitHubSafeTargetFingerprint.TryCreate(request, ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence github, out _).ShouldBeTrue();
        github.Metadata["safe_target_fingerprint"].ShouldBe("88927c3fc879e7b1f4d5ce2846d4f724ce9bc5a8f09a5e4786577352cf86719b");
        ForgejoSafeTargetFingerprint.TryCreate(request with { ProviderFamily = "forgejo", ProviderKey = "forgejo" },
            ProviderCredentialMode.AppInstallationReference, new Uri("https://forgejo.example/"), "9.0.3",
            out ProviderTargetEvidence forgejo, out _).ShouldBeTrue();
        forgejo.Metadata["safe_target_fingerprint"].ShouldBe("027daa2ba80f90853aba0a061790b2a94f75d912a6e208a5c7dcacb19b88903a");
        ProviderCapabilityDiscoveryResult profile = ProviderCapabilityProfileFactory.Create(
            request, "github", "github", [], new ProviderRateLimitPosture("bounded", false, null, new Dictionary<string, string>()),
            new Dictionary<string, string>(), new Dictionary<string, string>());
        profile.IsSuccess.ShouldBeTrue();
        profile.Profile.ShouldNotBeNull().Fingerprint.ShouldBe("ec3e47107dd381ad23a977a7584fa769bea2d7eb8dfdcb5bcfe22bf68b56c7be");

        ProviderCapabilityDiscoveryRequest reordered = request with
        {
            TargetEvidence = request.TargetEvidence with
            {
                Metadata = new Dictionary<string, string> { ["alpha"] = "é", ["zeta"] = "last" },
            },
        };
        GitHubSafeTargetFingerprint.TryCreate(reordered, ProviderCredentialMode.AppInstallationReference,
            out ProviderTargetEvidence reorderedGithub, out _).ShouldBeTrue();
        reorderedGithub.Metadata["safe_target_fingerprint"].ShouldBe(github.Metadata["safe_target_fingerprint"]);
        ForgejoSafeTargetFingerprint.TryCreate(reordered with { ProviderFamily = "forgejo", ProviderKey = "forgejo" },
            ProviderCredentialMode.AppInstallationReference, new Uri("https://forgejo.example/"), "9.0.3",
            out ProviderTargetEvidence reorderedForgejo, out _).ShouldBeTrue();
        reorderedForgejo.Metadata["safe_target_fingerprint"].ShouldBe(forgejo.Metadata["safe_target_fingerprint"]);
    }

    [Fact]
    public void ProviderEvidenceRetainsDomainValidationComparisonAndDisposalSemantics()
    {
        const string fingerprint = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        foreach (Func<string?, string?, bool> compare in new Func<string?, string?, bool>[]
            { GitHubProviderSafeOperationEvidence.FixedTimeEquals, ForgejoProviderSafeOperationEvidence.FixedTimeEquals })
        {
            compare(fingerprint, fingerprint.ToUpperInvariant()).ShouldBeTrue();
            compare(fingerprint, new string('0', 64)).ShouldBeFalse();
            compare(null, fingerprint).ShouldBeFalse();
            compare(fingerprint, new string('g', 64)).ShouldBeFalse();
            compare(fingerprint, "ab").ShouldBeFalse();
        }

        Should.Throw<ArgumentException>(() => GitHubProviderSafeOperationEvidence.Create("hxf-forgejo:v1:test", ["A"])).ParamName.ShouldBe("domain");
        Should.Throw<ArgumentException>(() => ForgejoProviderSafeOperationEvidence.Create("hxf-github:v1:test", ["A"])).ParamName.ShouldBe("domain");
        Should.Throw<ArgumentException>(() => GitHubProviderSafeOperationEvidence.Create("hxf-github:v1:é", ["A"])).ParamName.ShouldBe("domain");
        Should.Throw<ArgumentException>(() => ForgejoProviderSafeOperationEvidence.Create("hxf-forgejo:v1:é", ["A"])).ParamName.ShouldBe("domain");
        Should.Throw<ArgumentNullException>(() => GitHubProviderSafeOperationEvidence.Create("hxf-github:v1:test", null!)).ParamName.ShouldBe("fields");
        Should.Throw<NullReferenceException>(() => ForgejoProviderSafeOperationEvidence.Create("hxf-forgejo:v1:test", null!));
        Should.Throw<ArgumentNullException>(() => GitHubProviderSafeOperationEvidence.Create("invalid-domain", null!)).ParamName.ShouldBe("fields");
        Should.Throw<ArgumentException>(() => ForgejoProviderSafeOperationEvidence.Create("invalid-domain", null!)).ParamName.ShouldBe("domain");
        Should.Throw<ArgumentNullException>(() => GitHubProviderSafeOperationEvidence.Compute("invalid-domain", null!)).ParamName.ShouldBe("append");
        Should.Throw<ArgumentNullException>(() => ForgejoProviderSafeOperationEvidence.Compute("invalid-domain", null!)).ParamName.ShouldBe("append");
        Should.Throw<ArgumentException>(() => GitHubProviderSafeOperationEvidence.Create("hxf-github:v1:test", ["\ud800"]));
        Should.Throw<ArgumentException>(() => ForgejoProviderSafeOperationEvidence.Create("hxf-forgejo:v1:test", ["\ud800"]));

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        GitHubCanonicalEvidenceWriter github = new(hash);
        ForgejoCanonicalEvidenceWriter forgejo = new(hash);
        github.Dispose();
        forgejo.Dispose();
        github.AppendString("A");
        forgejo.AppendString("B");
        byte[] appendedDigest = hash.GetCurrentHash();
        Convert.ToHexString(appendedDigest).ToLowerInvariant().ShouldBe("3fa5a77b8e477895f9116793b5ab7b17369ef9da2f8175c9832cb19d07db803f");
        ArgumentOutOfRangeException githubException = Should.Throw<ArgumentOutOfRangeException>(() => github.AppendCollectionCount(-1));
        githubException.ParamName.ShouldBe("count");
        githubException.ActualValue.ShouldBeNull();
        hash.GetCurrentHash().ShouldBe(appendedDigest);
        ArgumentOutOfRangeException forgejoException = Should.Throw<ArgumentOutOfRangeException>(() => forgejo.AppendCollectionCount(-1));
        forgejoException.ParamName.ShouldBe("count");
        forgejoException.ActualValue.ShouldBe(-1);
        hash.GetCurrentHash().ShouldBe(appendedDigest);
    }

    private static T Method<T>(Type type, string name)
        where T : Delegate
        => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<T>();
}
