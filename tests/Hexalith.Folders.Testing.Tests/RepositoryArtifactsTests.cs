using System.Text;
using Hexalith.Folders.Testing.Tests.Helpers;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests;

public sealed class RepositoryArtifactsTests
{
    [Fact]
    public void LocatesNearestSolutionBeyondTwentyAncestors()
    {
        DirectoryInfo outer = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(outer.FullName, "Hexalith.Folders.slnx"), "");
            DirectoryInfo inner = outer.CreateSubdirectory("inner");
            File.WriteAllText(Path.Combine(inner.FullName, "Hexalith.Folders.slnx"), "");
            DirectoryInfo deep = inner.CreateSubdirectory(Path.Combine(Enumerable.Repeat("dir", 24).ToArray()));
            RepositoryArtifacts.FindRoot(deep.FullName).ShouldBe(inner.FullName);
        }
        finally
        {
            outer.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReportsStartAndMissingSolutionAtFilesystemRoot()
    {
        string start = Path.GetPathRoot(Path.GetTempPath())!;
        var error = Should.Throw<InvalidOperationException>(() => RepositoryArtifacts.FindRoot(start));
        error.Message.ShouldContain(start);
        error.Message.ShouldContain("Hexalith.Folders.slnx");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsUtf8WithOptionalBom(bool includeBom)
    {
        string path = Path.GetTempFileName();
        try
        {
            const string content = "C6 — état";
            File.WriteAllText(path, content, new UTF8Encoding(includeBom));
            RepositoryArtifacts.ReadText(path).ShouldBe(content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("C328")]
    [InlineData("FFFE43003600")]
    [InlineData("FEFF00430036")]
    [InlineData("43003600")]
    [InlineData("0000FEFF00000043")]
    public void RejectsInvalidDocumentEncodingAndIdentifiesFile(string hex)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Convert.FromHexString(hex));
            var error = Should.Throw<InvalidDataException>(() => RepositoryArtifacts.ReadText(path));
            error.Message.ShouldContain(path);
            error.Message.ShouldContain("UTF-8");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
