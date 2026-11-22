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
    [InlineData("literal-path", 0, 0, "passed")]
    [InlineData("directory-alias", 0, 0, "passed")]
    [InlineData("missing-root", 0, 1, "prerequisite_drift")]
    [InlineData("missing-dotnet", 0, 1, "prerequisite_drift")]
    [InlineData("unavailable-sdk", 155, 155, "prerequisite_drift")]
    [InlineData("missing-assembly", 0, 1, "prerequisite_drift")]
    [InlineData("zero-tests", 0, 1, "prerequisite_drift")]
    [InlineData("all-skipped", 0, 1, "prerequisite_drift")]
    [InlineData("test-root-missing", 7, 7, "prerequisite_drift")]
    [InlineData("test-yaml-malformed", 7, 7, "prerequisite_drift")]
    [InlineData("test-boolean-invalid", 7, 7, "prerequisite_drift")]
    public async Task GovernanceReportPublicationPreservesExitCodesAndRejectsStaleResultsAsync(
        string fault, int testExitCode, int expectedExitCode, string? expectedStatus)
    {
        string name = "folders-governance-report-" + Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Path.GetTempPath(), fault == "literal-path" ? name + "[checkout]" : name);
        string script = Path.Combine(directory, "tests", "tools", "run-governance-completeness-gates.ps1");
        string? alias = fault == "directory-alias" ? directory + "-alias" : null;
        Assert.SkipWhen(alias is not null && OperatingSystem.IsWindows(), "Unprivileged symbolic-link fixtures run on Unix.");
        string reportDirectory = Path.Combine(directory, "_bmad-output", "gates", "governance-completeness");
        string report = Path.Combine(reportDirectory, "latest.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.Copy(GateScriptPath, script);
            if (alias is not null)
            {
                Directory.CreateSymbolicLink(alias, directory);
            }

            if (fault != "missing-root")
            {
                File.WriteAllText(Path.Combine(directory, "Hexalith.Folders.slnx"), "<Solution />");
            }

            string assembly = Path.Combine(directory, "tests", "Hexalith.Folders.Contracts.Tests", "bin", "Release", ReadRootTargetFramework(), "Hexalith.Folders.Contracts.Tests.dll");
            if (fault != "missing-assembly")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assembly)!);
                File.WriteAllText(assembly, string.Empty);
            }

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
                WorkingDirectory = Path.GetTempPath(),
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("""
                if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'missing-dotnet') {
                    function global:Get-Command {
                        [CmdletBinding()]
                        param([string]$Name)
                    }
                }
                function global:dotnet {
                    if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'directory-alias' -and (Get-Location).Path -cne $env:FOLDERS_GOVERNANCE_EXPECTED_CHECKOUT) {
                        $global:LASTEXITCODE = 7
                        'Unexpected checkout path'
                        return
                    }
                    if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'unavailable-sdk' -and $args[0] -eq '--version') {
                        $global:LASTEXITCODE = [int]$env:FOLDERS_GOVERNANCE_TEST_EXIT
                        'private-marker: unavailable SDK details'
                        return
                    }
                    if ($env:FOLDERS_GOVERNANCE_REPORT_FAULT -eq 'final-publish') {
                        $report = Join-Path (Get-Location) '_bmad-output/gates/governance-completeness/latest.json'
                        if ([System.IO.File]::Exists($report)) {
                            [System.IO.File]::Delete($report)
                            [System.IO.Directory]::CreateDirectory($report) | Out-Null
                        }
                    }
                    $global:LASTEXITCODE = [int]$env:FOLDERS_GOVERNANCE_TEST_EXIT
                    switch ($env:FOLDERS_GOVERNANCE_REPORT_FAULT) {
                        'zero-tests' {
                            'Unrelated diagnostic Total: 20'
                            '   Hexalith.Folders.Contracts.Tests  Total: 0, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.1s'
                        }
                        'all-skipped' {
                            '   Hexalith.Folders.Contracts.Tests  Total: 2, Errors: 0, Failed: 0, Skipped: 1, Not Run: 1, Time: 0.1s'
                        }
                        'test-root-missing' {
                            '      ---- System.InvalidOperationException : GOVERNANCE-PREREQUISITE-DRIFT: repository root was not found.'
                            '      synthetic private-marker stack details'
                        }
                        'test-yaml-malformed' {
                            '      System.InvalidOperationException : GOVERNANCE-PREREQUISITE-DRIFT: yaml-malformed: private-marker'
                        }
                        'test-boolean-invalid' {
                            '      System.FormatException : GOVERNANCE-PREREQUISITE-DRIFT: boolean-invalid: private-marker'
                        }
                        default {
                            '   Hexalith.Folders.Contracts.Tests  Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.1s'
                        }
                    }
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
                & $env:FOLDERS_GOVERNANCE_RUNNER -SkipRestoreBuild:($env:FOLDERS_GOVERNANCE_REPORT_FAULT -ne 'unavailable-sdk')
                exit $LASTEXITCODE
                """);
            start.Environment["FOLDERS_GOVERNANCE_RUNNER"] = alias is null
                ? script
                : Path.Combine(alias, Path.GetRelativePath(directory, script));
            start.Environment["FOLDERS_GOVERNANCE_EXPECTED_CHECKOUT"] = alias ?? directory;
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
                output.Split("Hexalith.Folders.Contracts.Tests  Total: 1", StringSplitOptions.None).Length.ShouldBe(4);
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
                result.RootElement.GetProperty("exit_code").GetInt32().ShouldBe(expectedExitCode);
                result.RootElement.TryGetProperty("stale_marker", out _).ShouldBeFalse();
                AssertMetadataOnly(result.RootElement.GetRawText());
                if (expectedStatus == "prerequisite_drift")
                {
                    output.ShouldContain("GOVERNANCE-PREREQUISITE-DRIFT:");
                }
            }

            if (Directory.Exists(reportDirectory))
            {
                Directory.GetFiles(reportDirectory, ".latest.*.tmp").ShouldBeEmpty();
            }
        }
        finally
        {
            if (alias is not null && Directory.Exists(alias))
            {
                Directory.Delete(alias);
            }

            Directory.Delete(directory, recursive: true);
        }
    }
}
