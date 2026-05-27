using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using Hexalith.Folders.Cli.Commands;
using Hexalith.Folders.Cli.Commands.Audit;
using Hexalith.Folders.Cli.Commands.Commit;
using Hexalith.Folders.Cli.Commands.Context;
using Hexalith.Folders.Cli.Commands.File;
using Hexalith.Folders.Cli.Commands.Folder;
using Hexalith.Folders.Cli.Commands.Provider;
using Hexalith.Folders.Cli.Commands.Workspace;
using Hexalith.Folders.Cli.Composition;

namespace Hexalith.Folders.Cli;

/// <summary>
/// Builds the Folders CLI command tree and runs it against injected dependencies. This is the testable
/// composition seam: tests construct it with a fake <c>IClient</c> factory, an injected credential resolver,
/// and a capturing console so no socket is opened and <c>~/.hexalith</c> is never touched.
/// </summary>
internal sealed class CliApplication
{
    private readonly CliDependencies _dependencies;

    /// <summary>Initializes a new instance of the <see cref="CliApplication"/> class.</summary>
    /// <param name="dependencies">The injected collaborators.</param>
    public CliApplication(CliDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;
    }

    /// <summary>Builds the root command with the seven group subcommands and recursive global options.</summary>
    /// <returns>The configured root command.</returns>
    public RootCommand BuildRootCommand()
    {
        GlobalOptionsBinding global = GlobalOptionsBinding.Create();
        CommandPipeline pipeline = new(_dependencies);

        RootCommand root = new("Hexalith Folders CLI — a thin adapter over the Folders SDK (Client).");
        global.AddToRoot(root);

        root.Subcommands.Add(ProviderCommand.Create(pipeline, global));
        root.Subcommands.Add(FolderCommand.Create(pipeline, global));
        root.Subcommands.Add(WorkspaceCommand.Create(pipeline, global));
        root.Subcommands.Add(FileCommand.Create(pipeline, global));
        root.Subcommands.Add(CommitCommand.Create(pipeline, global));
        root.Subcommands.Add(ContextCommand.Create(pipeline, global));
        root.Subcommands.Add(AuditCommand.Create(pipeline, global));
        return root;
    }

    /// <summary>
    /// Parses and runs the supplied arguments. Any parse error (missing required option, unrecognized
    /// argument such as an idempotency flag on a query, or a bad constrained value) is a pre-SDK usage error
    /// and returns exit code 64 before any command action runs.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        RootCommand root = BuildRootCommand();
        ParseResult parseResult = root.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (System.CommandLine.Parsing.ParseError error in parseResult.Errors)
            {
                _dependencies.Console.Error.WriteLine(error.Message);
            }

            return FoldersExitCodes.UsageError;
        }

        return await parseResult.InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
