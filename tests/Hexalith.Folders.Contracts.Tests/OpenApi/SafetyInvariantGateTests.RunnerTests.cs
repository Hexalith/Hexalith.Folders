using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class SafetyInvariantGateTests
{
    [Theory]
    [InlineData("Hexalith.Folders.Contracts.Tests.dll", 0, "prerequisite_drift", 1, true)]
    [InlineData("Hexalith.Folders.Contracts.Tests.deps.json", 0, "prerequisite_drift", 1, true)]
    [InlineData("Hexalith.Folders.Contracts.Tests.runtimeconfig.json", 0, "prerequisite_drift", 1, true)]
    [InlineData("Hexalith.Folders.Contracts.dll", 0, "prerequisite_drift", 1, true)]
    [InlineData("Hexalith.Folders.Aspire.dll", 0, "prerequisite_drift", 1, true)]
    [InlineData(null, 0, "passed", 0, true)]
    [InlineData(null, 7, "failed", 7, true)]
    [InlineData("Hexalith.Folders.Contracts.dll", 0, "prerequisite_drift", 1, false)]
    [InlineData(null, 0, "passed", 0, false)]
    public async Task SafetyRunnerChecksTestPrerequisitesBeforeLaunchAsync(
        string? missingFile, int testExitCode, string expectedStatus, int expectedExitCode, bool skipBuild)
    {
        DirectoryInfo checkout = Directory.CreateTempSubdirectory("folders-safety-runner-");
        try
        {
            string tools = Directory.CreateDirectory(Path.Combine(checkout.FullName, "tests", "tools")).FullName;
            string script = Path.Combine(tools, Path.GetFileName(_gateScriptFilePath));
            File.Copy(_gateScriptFilePath, script);

            string binaries = Directory.CreateDirectory(Path.Combine(checkout.FullName, "tests", "Hexalith.Folders.Contracts.Tests", "bin", "Release", "net11.0")).FullName;
            string[] requiredFiles =
            [
                "Hexalith.Folders.Contracts.Tests.dll",
                "Hexalith.Folders.Contracts.Tests.deps.json",
                "Hexalith.Folders.Contracts.Tests.runtimeconfig.json",
                "Hexalith.Folders.Contracts.dll",
                "Hexalith.Folders.Aspire.dll",
            ];
            foreach (string file in requiredFiles.Where(file => file != missingFile))
            {
                File.WriteAllText(Path.Combine(binaries, file), string.Empty);
            }

            ProcessStartInfo start = new("pwsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            foreach (string option in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command" })
            {
                start.ArgumentList.Add(option);
            }

            start.ArgumentList.Add("""
                function global:dotnet {
                    if ($args[0] -in 'restore', 'build') {
                        $global:LASTEXITCODE = 0
                        "synthetic $($args[0]) invoked"
                        return
                    }
                    $global:LASTEXITCODE = [int]$env:FOLDERS_SAFETY_TEST_EXIT
                    'synthetic test host invoked Total: 1'
                }
                & $env:FOLDERS_SAFETY_RUNNER -SkipRestoreBuild:($env:FOLDERS_SAFETY_SKIP_BUILD -eq 'true')
                exit $LASTEXITCODE
                """);
            start.Environment["FOLDERS_SAFETY_RUNNER"] = script;
            start.Environment["FOLDERS_SAFETY_TEST_EXIT"] = testExitCode.ToString(CultureInfo.InvariantCulture);
            start.Environment["FOLDERS_SAFETY_SKIP_BUILD"] = skipBuild ? "true" : "false";

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Safety runner could not start.");
            CancellationToken cancellation = TestContext.Current.CancellationToken;
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellation);
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("Safety runner timed out.");
            }

            string output = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
            process.ExitCode.ShouldBe(expectedExitCode);
            output.Contains("synthetic restore invoked", StringComparison.Ordinal).ShouldBe(!skipBuild);
            output.Contains("synthetic build invoked", StringComparison.Ordinal).ShouldBe(!skipBuild);
            output.Contains("synthetic test host invoked", StringComparison.Ordinal).ShouldBe(missingFile is null);
            output.Contains("SAFETY-PREREQUISITE-DRIFT:", StringComparison.Ordinal).ShouldBe(missingFile is not null);
            output.Contains(checkout.FullName, StringComparison.Ordinal).ShouldBeFalse();

            string report = Path.Combine(checkout.FullName, "_bmad-output", "gates", "safety-invariants", "latest.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(report));
            document.RootElement.GetProperty("status").GetString().ShouldBe(expectedStatus);
            document.RootElement.GetProperty("exit_code").GetInt32().ShouldBe(expectedExitCode);
        }
        finally
        {
            checkout.Delete(recursive: true);
        }
    }
}
