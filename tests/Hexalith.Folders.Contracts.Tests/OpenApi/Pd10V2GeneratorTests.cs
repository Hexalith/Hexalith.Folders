using System.Diagnostics;
using System.Security.Cryptography;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed class Pd10V2GeneratorTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string GeneratorPath = Path.Combine(RepositoryRoot, "scripts", "generate-pd10-v2-contract.py");
    private static readonly string SourcePath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v1.yaml");
    private static readonly string MatrixPath = Path.Combine(RepositoryRoot, "docs", "contract", "authorization-matrix.md");
    private static readonly string CandidatePath = Path.Combine(RepositoryRoot, "src", "Hexalith.Folders.Contracts", "openapi", "hexalith.folders.v2.yaml");

    [Fact]
    public void GeneratorReproducesTheCommittedCandidateByteForByte()
    {
        string output = NewTempPath("candidate.yaml");

        Run(SourcePath, MatrixPath, output, out string diagnostics).ShouldBe(0, diagnostics);

        File.ReadAllBytes(output).ShouldBe(File.ReadAllBytes(CandidatePath));
    }

    [Fact]
    public void GeneratorRejectsAnOutputAliasWithoutChangingHistoricalV1()
    {
        string before = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(SourcePath)));

        Run(SourcePath, MatrixPath, SourcePath, out string diagnostics).ShouldNotBe(0);

        diagnostics.ShouldContain("must not alias", Case.Insensitive);
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(SourcePath))).ShouldBe(before);
    }

    [Fact]
    public void GeneratorRejectsDuplicateMethodAndCandidateRoute()
    {
        string matrix = NewTempPath("authorization-matrix.md");
        string text = File.ReadAllText(MatrixPath);
        const string original = "| `GetEffectivePermissions` | GET | `/api/v2/folders/{folderId}/effective-permissions` |";
        const string duplicate = "| `GetEffectivePermissions` | GET | `/api/v2/folders/{folderId}/lifecycle-status` |";
        text.ShouldContain(original);
        File.WriteAllText(matrix, text.Replace(original, duplicate, StringComparison.Ordinal));

        Run(SourcePath, matrix, NewTempPath("candidate.yaml"), out string diagnostics).ShouldNotBe(0);

        diagnostics.ShouldContain("Duplicate generated route", Case.Sensitive);
    }

    private static int Run(string source, string matrix, string output, out string diagnostics)
    {
        ProcessStartInfo startInfo = new("python3")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(GeneratorPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(source);
        startInfo.ArgumentList.Add("--matrix");
        startInfo.ArgumentList.Add(matrix);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(output);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the PD10 v2 generator.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        diagnostics = stdout + stderr;
        return process.ExitCode;
    }

    private static string NewTempPath(string fileName)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"pd10-v2-generator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Folders.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Hexalith.Folders repository root.");
    }
}
