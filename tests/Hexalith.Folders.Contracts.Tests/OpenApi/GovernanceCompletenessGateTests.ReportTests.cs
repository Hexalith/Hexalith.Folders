using System.Diagnostics;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Contracts.Tests.OpenApi;

public sealed partial class GovernanceCompletenessGateTests
{
    [Theory]
    [InlineData("", 0, 0, "passed")]
    [InlineData("", 7, 7, "failed")]
    [InlineData("discovered", 0, 1, null)]
    [InlineData("passed", 0, 1, null)]
    [InlineData("failed", 7, 1, null)]
    [InlineData("publish", 0, 1, null)]
    [InlineData("directory", 0, 1, null)]
    [InlineData("final-publish", 0, 1, null)]
    [InlineData("stale-delete", 0, 1, null)]
    public async Task GovernanceReportPublicationPreservesExitCodesAndRejectsStaleResultsAsync(
        string fault, int testExitCode, int expectedExitCode, string? expectedStatus)
    {
        string directory = Path.Combine(Path.GetTempPath(), "folders-governance-report-" + Guid.NewGuid().ToString("N"));
        string script = Path.Combine(directory, "tests", "tools", "run-governance-completeness-gates.ps1");
        string reportDirectory = Path.Combine(directory, "_bmad-output", "gates", "governance-completeness");
        string report = Path.Combine(reportDirectory, "latest.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.Copy(GateScriptPath, script);
            string assembly = Path.Combine(directory, "tests", "Hexalith.Folders.Contracts.Tests", "bin", "Release", "net10.0", "Hexalith.Folders.Contracts.Tests.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(assembly)!);
            File.WriteAllText(assembly, string.Empty);
            Directory.CreateDirectory(reportDirectory);
            if (fault == "publish")
            {
                Directory.CreateDirectory(report);
            }
            else
            {
                File.WriteAllText(report, "{\"status\":\"passed\",\"stale_marker\":true}");
            }

            if (fault == "directory")
            {
                Directory.Delete(reportDirectory, recursive: true);
                File.WriteAllText(reportDirectory, "synthetic directory blocker");
            }

            ProcessStartInfo start = new("pwsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory,
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("""
                function global:dotnet {
                    if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'final-publish') {
                        $report = Join-Path (Get-Location) '_bmad-output/gates/governance-completeness/latest.json'
                        if ([System.IO.File]::Exists($report)) {
                            [System.IO.File]::Delete($report)
                            [System.IO.Directory]::CreateDirectory($report) | Out-Null
                        }
                    }
                    $global:LASTEXITCODE = [int]$env:FOLDERS_GOVERNANCE_TEST_EXIT
                    'Synthetic tests Total: 1'
                }
                function global:Remove-Item {
                    [CmdletBinding()]
                    param([string]$LiteralPath, [switch]$Force)
                    if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'stale-delete') {
                        throw 'private-marker: synthetic deletion failure'
                    }
                    Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Force:$Force
                }
                function global:Set-Content {
                    [CmdletBinding()]
                    param(
                        [string]$LiteralPath,
                        [Parameter(ValueFromPipeline = $true)]$Value,
                        [string]$Encoding
                    )
                    process {
                        if (($Value | ConvertFrom-Json).status -eq $env:FOLDERS_GOVERNANCE_REPORT_FAULT -or $env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'stale-delete') {
                            [System.IO.File]::WriteAllText($LiteralPath, '{')
                            throw 'private-marker: synthetic write failure'
                        }
                        Microsoft.PowerShell.Management\Set-Content -LiteralPath $LiteralPath -Value $Value -Encoding $Encoding
                    }
                }
                & $env:FOLDERS_GOVERNANCE_RUNNER -SkipRestoreBuild
                exit $LASTEXITCODE
                """);
            start.Environment["FOLDERS_GOVERNANCE_RUNNER"] = script;
            start.Environment["FOLDERS_GOVERNANCE_REPORT_FAULT"] = fault;
            start.Environment["FOLDERS_GOVERNANCE_TEST_EXIT"] = testExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Governance runner could not start.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException("Governance runner timed out.");
            }

            string output = await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true);
            process.ExitCode.ShouldBe(expectedExitCode);
            output.Contains("private-marker", StringComparison.Ordinal).ShouldBeFalse();
            output.Contains(directory, StringComparison.Ordinal).ShouldBeFalse();
            if (fault == "final-publish")
            {
                output.Split("Synthetic tests Total: 1", StringSplitOptions.None).Length.ShouldBe(4);
            }

            if (expectedStatus is null)
            {
                output.ShouldContain("GOVERNANCE-REPORT-WRITE-FAILED");
                File.Exists(report).ShouldBe(fault == "stale-delete");
                if (fault == "stale-delete")
                {
                    using JsonDocument stale = JsonDocument.Parse(File.ReadAllText(report));
                    stale.RootElement.GetProperty("stale_marker").GetBoolean().ShouldBeTrue();
                }
            }
            else
            {
                using JsonDocument result = JsonDocument.Parse(File.ReadAllText(report));
                result.RootElement.GetProperty("status").GetString().ShouldBe(expectedStatus);
                result.RootElement.GetProperty("exit_code").GetInt32().ShouldBe(testExitCode);
                result.RootElement.TryGetProperty("stale_marker", out _).ShouldBeFalse();
                AssertMetadataOnly(result.RootElement.GetRawText());
            }

            if (Directory.Exists(reportDirectory))
            {
                Directory.GetFiles(reportDirectory, ".latest.*.tmp").ShouldBeEmpty();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
