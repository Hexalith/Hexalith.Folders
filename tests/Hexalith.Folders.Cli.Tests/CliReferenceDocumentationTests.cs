using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Hexalith.Folders.Cli.Composition;
using Hexalith.Folders.Cli.Credentials;
using Hexalith.Folders.Cli.Tests.TestSupport;
using Hexalith.Folders.Parity.Testing;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Cli.Tests;

/// <summary>
/// Keeps the consumer CLI reference aligned with the command tree that the CLI builds.
/// </summary>
public sealed partial class CliReferenceDocumentationTests
{
    [Fact]
    public void ReferenceDocumentsEveryLeafCommandExactlyOnce()
    {
        CliApplication application = new(new CliDependencies
        {
            Console = new TestCliConsole(),
            Credentials = new CredentialResolver(
                environment: static _ => null,
                credentialsFilePath: Path.Combine(Path.GetTempPath(), $"hexalith-creds-{Guid.NewGuid():N}.json")),
            ClientFactory = static (_, _) => throw new InvalidOperationException("Building the command tree must not create a client."),
        });

        string[] leafCommands = [.. LeafCommands(application.BuildRootCommand(), prefix: null).Order(StringComparer.Ordinal)];
        string[] documentedCommands = [.. DocumentedCommandRow()
            .Matches(File.ReadAllText(CliReferencePath()))
            .Select(static match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)];

        leafCommands.ShouldContain("context index-search");
        documentedCommands.ShouldBe(leafCommands);
    }

    private static IEnumerable<string> LeafCommands(Command command, string? prefix)
    {
        foreach (Command subcommand in command.Subcommands)
        {
            string path = prefix is null ? subcommand.Name : $"{prefix} {subcommand.Name}";
            if (subcommand.Subcommands.Count == 0)
            {
                yield return path;
                continue;
            }

            foreach (string leaf in LeafCommands(subcommand, path))
            {
                yield return leaf;
            }
        }
    }

    private static string CliReferencePath()
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ParityOracle.OraclePath)!, "..", "..", "docs", "sdk", "cli-reference.md"));

    [GeneratedRegex(@"^\|\s*`([a-z]+(?: [a-z-]+)+)`\s*\|\s*`[A-Z][A-Za-z]+`\s*\|", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentedCommandRow();
}
