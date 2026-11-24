using System.Text.Json;
using System.Text.Json.Serialization;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class FoldersServerJsonOptionsTests
{
    [Fact]
    public void ResponseOptionsUseCamelCaseAndOmitNullProperties()
        => JsonSerializer.Serialize(new ResponseSample("folder-a", null), FoldersServerJsonOptions.Response)
            .ShouldBe("""{"folderId":"folder-a"}""");

    [Fact]
    public void ResponseOptionsReadCaseInsensitivePropertyNames()
        => JsonSerializer.Deserialize<ResponseSample>("""{"FOLDERID":"folder-a"}""", FoldersServerJsonOptions.Response)!
            .FolderId.ShouldBe("folder-a");

    [Fact]
    public void ResponseOptionsAreReadOnlyBeforeFirstUse()
    {
        FoldersServerJsonOptions.Response.IsReadOnly.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(
            () => FoldersServerJsonOptions.Response.DefaultIgnoreCondition = JsonIgnoreCondition.Never);
    }

    private sealed record ResponseSample(string FolderId, string? WorkspaceId);
}
