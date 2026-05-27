using System;
using System.CommandLine;
using System.IO;

using Hexalith.Folders.Cli.Errors;
using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.File;

/// <summary>
/// The <c>file</c> command group: add, change, and remove. <c>add</c>/<c>change</c> go through the Story 5.1
/// upload convenience (<see cref="FoldersFileUploadExtensions.UploadFileAsync(IClient, FileUploadDescriptor, ReadOnlyMemory{byte}, string, string, string, System.Threading.CancellationToken)"/>),
/// which selects the inline transport and signals <see cref="FileUploadStreamingRequiredException"/> for
/// over-boundary content; the CLI never hand-builds the <c>FileMutationRequest</c> union. File content is
/// read from a caller path (or stdin) and never echoed to any output channel.
/// </summary>
internal static class FileCommand
{
    /// <summary>Creates the <c>file</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("file", "Add, change, and remove files in a locked workspace.");
        command.Subcommands.Add(Upload("add", "Add a file (mutating).", FileMutationRequestFileOperationKind.Add, pipeline, global));
        command.Subcommands.Add(Upload("change", "Change a file (mutating).", FileMutationRequestFileOperationKind.Change, pipeline, global));
        command.Subcommands.Add(Remove(pipeline, global));
        return command;
    }

    private static Command Upload(
        string name,
        string description,
        FileMutationRequestFileOperationKind kind,
        CommandPipeline pipeline,
        GlobalOptionsBinding global)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string> operationId = CommandOptions.RequiredId("--operation-id", "Opaque per-file operation identifier.");
        Option<string> file = CommandOptions.RequiredId("--file", "Path to the local file content, or - to read bytes from stdin.");
        Option<string> path = CommandOptions.RequiredId("--path", "Workspace-root-relative normalized path.");
        Option<string> displayName = CommandOptions.RequiredId("--display-name", "Human-readable file name.");
        Option<string> mediaType = CommandOptions.RequiredId("--media-type", "RFC 6838 media type (type/subtype).");
        Option<string?> contentMediaType = new("--content-media-type") { Description = "Optional original media type when it differs from --media-type." };
        Option<string> pathPolicyClass = new("--path-policy-class") { Description = "Path policy classification.", DefaultValueFactory = _ => "metadata_only" };

        return CommandFactory.Mutation(
            name,
            description,
            pipeline,
            global,
            [folderId, workspaceId, operationId, file, path, displayName, mediaType, contentMediaType, pathPolicyClass],
            async (parseResult, client, sourcing, ct) =>
            {
                ReadOnlyMemory<byte> content = ReadFileBytes(parseResult.GetValue(file)!);
                FileUploadDescriptor descriptor = new()
                {
                    FolderId = parseResult.GetValue(folderId)!,
                    WorkspaceId = parseResult.GetValue(workspaceId)!,
                    OperationId = parseResult.GetValue(operationId)!,
                    MediaType = parseResult.GetValue(mediaType)!,
                    ContentMediaType = parseResult.GetValue(contentMediaType),
                    FileOperationKind = kind,
                    PathMetadata = new PathMetadata
                    {
                        NormalizedPath = parseResult.GetValue(path)!,
                        DisplayName = parseResult.GetValue(displayName)!,
                        PathPolicyClass = parseResult.GetValue(pathPolicyClass)!,
                        UnicodeNormalization = PathMetadataUnicodeNormalization.NFC,
                    },
                };

                return await client
                    .UploadFileAsync(descriptor, content, sourcing.IdempotencyKey, sourcing.CorrelationId, sourcing.TaskId, ct)
                    .ConfigureAwait(false);
            });
    }

    private static Command Remove(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Option<string> folderId = CommandOptions.RequiredId("--folder-id", "Opaque folder identifier.");
        Option<string> workspaceId = CommandOptions.RequiredId("--workspace-id", "Opaque workspace identifier.");
        Option<string?> body = CommandOptions.Request();
        return CommandFactory.Mutation(
            "remove",
            "Remove a file (mutating). Supply the metadata-only removal body via --request.",
            pipeline,
            global,
            [folderId, workspaceId, body],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.RemoveFileAsync(
                parseResult.GetValue(folderId)!,
                parseResult.GetValue(workspaceId)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<FileMutationRequest>(parseResult.GetValue(body)),
                ct)));
    }

    private static ReadOnlyMemory<byte> ReadFileBytes(string source)
    {
        try
        {
            if (source == "-")
            {
                using Stream stdin = Console.OpenStandardInput();
                using MemoryStream buffer = new();
                stdin.CopyTo(buffer);
                return buffer.ToArray();
            }

            return System.IO.File.ReadAllBytes(source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            // Metadata-only: never echo the path or content.
            throw new CliUsageException("The --file content could not be read.");
        }
    }
}
