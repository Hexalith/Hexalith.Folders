using System.Text.Json;
using Hexalith.EventStore.Contracts.Routing;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

public sealed class FolderDurabilityCatalogBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialAndResumedSdkReceiptsRetainTheActualCompleteBinding(bool resumed)
    {
        var expected = Readback();
        byte[] bytes = resumed ? JsonSerializer.SerializeToUtf8Bytes(new { observation = "metadata-only", readback = expected })
            : JsonSerializer.SerializeToUtf8Bytes(expected);
        JsonSerializer.Serialize(FolderDurabilityCatalogBinding.Read(bytes, expected.RootDigest)).ShouldBe(JsonSerializer.Serialize(expected));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("epoch")]
    [InlineData("generation")]
    [InlineData("serving")]
    [InlineData("profile")]
    [InlineData("binding")]
    public void MissingOrConflictingAuthorityNeverGetsDefaulted(string fault)
    {
        var expected = Readback();
        var damaged = fault switch
        {
            "root" => expected with { RootDigest = new string('b', 64) },
            "epoch" => expected with { Epoch = 0 },
            "generation" => expected with { Generation = 0 },
            "serving" => expected with { Serving = false },
            "profile" => expected with { Profile = "Production" },
            _ => expected with { ActivationBindingDigest = null },
        };
        Should.Throw<InvalidOperationException>(() => FolderDurabilityCatalogBinding.Read(JsonSerializer.SerializeToUtf8Bytes(damaged), expected.RootDigest));
    }

    private static RouteCatalogGenerationReadback Readback() => new(3, 1, new string('a', 64), new string('c', 64),
        new string('d', 64), "Development", [], true) { AttemptId = "owned-attempt", ActivationBindingDigest = new string('e', 64) };
}
