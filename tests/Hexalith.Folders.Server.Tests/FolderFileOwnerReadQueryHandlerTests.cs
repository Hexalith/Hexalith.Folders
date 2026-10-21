using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FolderFileOwnerReadQueryHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactIdentityAndExplicitInverseMappingReadOnlyIsolatedPersistedFacts(bool inverse)
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        var before = fixture.Store.Snapshot();
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(inverse));
        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe(inverse ? FolderFileReferenceReadQuery.QueryType : FolderFileOwnerReadQuery.QueryType);
        var json = result.GetPayload(); json.GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Allowed);
        json.GetProperty("evidence").GetProperty("resourceVersion").GetString().ShouldBe("5");
        if (inverse)
        {
            json.GetProperty("ownerFileId").GetString().ShouldBe("owner-file");
            json.GetProperty("folderId").GetString().ShouldBe("folder-a");
            json.GetProperty("projectFileReferenceId").GetString().ShouldBe("référence-1");
        }
        fixture.Store.ReadKeys.Count.ShouldBe(2);
        fixture.Store.ReadKeys.ShouldAllBe(key => key.StartsWith("projection:folders:file-identities:", StringComparison.Ordinal));
        fixture.Store.Snapshot().ShouldBe(before);
        string wire = json.GetRawText();
        foreach (string forbidden in new[] { "path", "workspace", "content", "provider", "displayName" })
            wire.ShouldNotContain(forbidden, Case.Insensitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingNativeTenantAuthorityCannotDistinguishExistingFromAbsentIdentity(bool inverse)
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        fixture.Permissions.Evidence = _ => FolderFilePermissionEvidenceProvider.Current() with { NativeTenantRevision = null };
        QueryEnvelope original = FolderFileOwnerReadFixture.Query(inverse);
        var known = await fixture.DispatchAsync(original);
        QueryEnvelope absent = original with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(inverse
            ? (object)new FolderFileReferenceReadQuery("absent-reference") : new FolderFileOwnerReadQuery("absent-owner"), JsonSerializerOptions.Web)
        };
        var missing = await fixture.DispatchAsync(absent);
        known.Success.ShouldBeTrue(); missing.Success.ShouldBeTrue();
        missing.ProjectionType.ShouldBe(known.ProjectionType); missing.PayloadBytes.ShouldBe(known.PayloadBytes);
        fixture.Store.ReadKeys.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("known")]
    [InlineData("absent")]
    [InlineData("deleted")]
    public async Task LaggingIdentityGenerationCannotClassifyPositiveOrNegativeLookup(string shape)
    {
        var fixture = new FolderFileOwnerReadFixture();
        fixture.Row = fixture.Row with
        {
            LastSequence = 4,
            Files = shape switch
            { "absent" => [], "deleted" => [new("owner-file", "référence-1", false)], _ => fixture.Row.Files }
        };
        await fixture.SeedAsync();
        var result = await fixture.DispatchAsync();
        result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NegativeLookupMustSurviveFinalPersistedReadAndNativeAuthorityRecheck(bool inverse)
    {
        var fixture = new FolderFileOwnerReadFixture(); fixture.Row = fixture.Row with { Files = [] };
        await fixture.SeedAsync(); int revision = 1;
        fixture.Permissions.Evidence = _ => FolderFilePermissionEvidenceProvider.Current() with { NativeTenantRevision = "tenant-" + revision };
        fixture.Store.OnRead = ordinal => { if (ordinal == 2) revision = 3; };
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(inverse));
        result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        fixture.Permissions.Checks.ShouldBe(3); fixture.Store.ReadKeys.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateProjectsReferenceAcrossRetainedIncarnationsIsUnavailable(bool firstActive)
    {
        var fixture = new FolderFileOwnerReadFixture();
        fixture.Row = fixture.Row with { Files = [new("first", "référence-1", firstActive), new("second", "référence-1", true)] };
        await fixture.SeedAsync();
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(true));
        result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        result.GetPayload().GetProperty("ownerFileId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task RevokeRegrantDuringFinalPersistedReadCannotDisclosePriorMapping()
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        int revision = 1;
        fixture.Permissions.Evidence = _ => FolderFilePermissionEvidenceProvider.Current() with { PermissionVersion = "native-permission-" + revision };
        fixture.Store.OnRead = ordinal => { if (ordinal == 2) revision = 3; };
        var before = fixture.Store.Snapshot();
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(true));
        result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        result.GetPayload().GetProperty("ownerFileId").ValueKind.ShouldBe(JsonValueKind.Null);
        fixture.Permissions.Checks.ShouldBe(3);
        fixture.Store.Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task NativeFileReadDoesNotRequireAProjectsBinding()
    {
        var fixture = new FolderFileOwnerReadFixture();
        fixture.Row = fixture.Row with { Files = [new("owner-file", null, true)] };
        await fixture.SeedAsync();
        (await fixture.DispatchAsync()).GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Allowed);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("workload")]
    [InlineData("missing-verification")]
    public async Task ForeignOrUnverifiedCallerCannotReadIdentity(string change)
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        QueryEnvelope query = FolderFileOwnerReadFixture.Query();
        query = change switch
        {
            "actor" => query with { UserId = "other" },
            "tenant" => query with { TenantId = "tenant-b" },
            "workload" => query with { AuthenticatedWorkloadId = "other" },
            _ => query
        };
        var result = await fixture.DispatchAsync(query, verified: change != "missing-verification");
        result.ErrorMessage.ShouldBe("safe-denial"); result.PayloadBytes.ShouldBeNull(); fixture.Store.ReadKeys.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("wrong-parent")]
    public async Task ProtectedFileShapesNeverReturnIdentity(string change)
    {
        var fixture = new FolderFileOwnerReadFixture();
        fixture.Row = change switch
        {
            "missing" => fixture.Row with { Files = [] },
            "deleted" => fixture.Row with { Files = [new("owner-file", "référence-1", false)] },
            _ => fixture.Row with { FolderId = "other-folder" }
        };
        await fixture.SeedAsync(); var result = await fixture.DispatchAsync();
        if (change == "wrong-parent") result.GetPayload().GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        else { result.ErrorMessage.ShouldBe("safe-denial"); result.PayloadBytes.ShouldBeNull(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingNativeTenantOrChangedRevisionsNeverDiscloseMapping(bool changed)
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        fixture.Permissions.Evidence = checks => FolderFilePermissionEvidenceProvider.Current() with
        { NativeTenantRevision = changed ? "tenant-" + checks : null };
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(true));
        var json = result.GetPayload(); json.GetProperty("outcome").GetInt32().ShouldBe((int)FolderOwnerReadOutcome.Unavailable);
        json.GetProperty("ownerFileId").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("evidence").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InverseMappingAuthorityFaultUsesItsOwnSchemaAtEitherPass(int failedPass)
    {
        var fixture = new FolderFileOwnerReadFixture(); await fixture.SeedAsync();
        fixture.Permissions.Evidence = checks => checks == failedPass
            ? FolderPermissionEvidenceResult.FromStatus(FolderPermissionEvidenceStatus.Unavailable, null)
            : FolderFilePermissionEvidenceProvider.Current();
        var result = await fixture.DispatchAsync(FolderFileOwnerReadFixture.Query(true));
        result.ProjectionType.ShouldBe(FolderFileReferenceReadQuery.QueryType);
        result.GetPayload().GetProperty("ownerFileId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void OptionalFolderV1EvidencePreservesOriginalConstructorAndOutcomeWireValue()
    {
        var old = JsonSerializer.Deserialize<FolderOwnerReadResult>("{\"outcome\":0}", JsonSerializerOptions.Web)!;
        old.ShouldBe(new FolderOwnerReadResult(FolderOwnerReadOutcome.Allowed)); old.Evidence.ShouldBeNull();
        var updated = old with { Evidence = new("native-permission", "native-resource", "native-watermark", FolderFileOwnerReadFixture.Now, FolderFileOwnerReadFixture.Now) };
        JsonSerializer.SerializeToElement(updated, JsonSerializerOptions.Web).GetProperty("outcome").GetInt32().ShouldBe(0);
        updated.Deconstruct(out FolderOwnerReadOutcome outcome); outcome.ShouldBe(FolderOwnerReadOutcome.Allowed);
    }
}
