using System.Text;
using Hexalith.Folders.Queries.FileContext;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Queries.FileContext;

public sealed class WorkspaceFileContentClassificationTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("ordinary UTF-8 \u00e9\t\r\n", true)]
    [InlineData("\uFEFFallowed", true)]
    [InlineData("\uFEFF\uFEFFduplicate", false)]
    [InlineData("embedded\uFEFFmark", false)]
    [InlineData("binary\0", false)]
    [InlineData("control\u0001", false)]
    public void ClassificationPreservesBytesAndEnforcesStrictTextRule(string text, bool expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text), original = bytes.ToArray();
        WorkspaceFileContentClassification.IsContentReadable(bytes).ShouldBe(expected);
        bytes.ShouldBe(original);
    }
    [Fact]
    public void InvalidUtf8OtherEncodingAndBoundarySizesStayMetadataOnlyWithoutRepair()
    {
        WorkspaceFileContentClassification.IsContentReadable([0xc3, 0x28]).ShouldBeFalse();
        WorkspaceFileContentClassification.IsContentReadable([0xff, 0xfe, 0x61, 0x00]).ShouldBeFalse();
        WorkspaceFileContentClassification.IsContentReadable(Enumerable.Repeat((byte)'a', 1048576).ToArray()).ShouldBeTrue();
        WorkspaceFileContentClassification.IsContentReadable(Enumerable.Repeat((byte)'a', 1048577).ToArray()).ShouldBeFalse();
    }
}
