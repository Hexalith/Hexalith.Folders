using Hexalith.Folders.Authorization;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Authorization;

/// <summary>Supported owner metadata and same-actor lock avoidance stay fail closed and exactly scoped.</summary>
public sealed class GatewayOrganizationStreamSequenceReaderTests
{
    /// <summary>Verifies supported Metadata Produces The Actual Head.</summary>
    [Fact]
    public async Task SupportedMetadataProducesTheActualHead()
    {
        var source = new OwnerMetadataReaderStub();
        (await new GatewayOrganizationStreamSequenceReader(source).GetCurrentSequenceAsync("tenant", "organization", TestContext.Current.CancellationToken)).ShouldBe(0);
        source.Calls.ShouldBe(1);
    }

    /// <summary>Verifies in Turn Sequences Avoid Self Invocation And Never Cross Tenant.</summary>
    /// <param name="folder">The folder control.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InTurnSequencesAvoidSelfInvocationAndNeverCrossTenant(bool folder)
    {
        var source = new OwnerMetadataReaderStub();
        var reader = new GatewayOrganizationStreamSequenceReader(source);
        using (folder ? FolderActorSequenceScope.Enter("tenant", "organization", 7) : OrganizationActorSequenceScope.Enter("tenant", "organization", 7))
        {
            (await reader.GetCurrentSequenceAsync("tenant", "organization", TestContext.Current.CancellationToken)).ShouldBe(7);
            source.Calls.ShouldBe(0);
            (await reader.GetCurrentSequenceAsync("other-tenant", "organization", TestContext.Current.CancellationToken)).ShouldBe(0);
            source.Calls.ShouldBe(1);
        }
    }

    /// <summary>Verifies missing And Foreign Proof Never Produce Current Authority.</summary>
    /// <param name="foreign">The foreign control.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingAndForeignProofNeverProduceCurrentAuthority(bool foreign)
    {
        var source = new OwnerMetadataReaderStub { Missing = !foreign, Foreign = foreign };
        var reader = new GatewayOrganizationStreamSequenceReader(source);
        await Should.ThrowAsync<InvalidOperationException>(() => reader.GetCurrentSequenceAsync("tenant", "organization", TestContext.Current.CancellationToken));
    }
}
