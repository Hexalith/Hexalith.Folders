using System.Text;

namespace Hexalith.Folders.Client.Generation;

/// <summary>
/// Applies the deterministic SDK-shape corrections that NSwag cannot express from OpenAPI alone.
/// </summary>
internal static class GeneratedClientPostProcessor
{
    /// <summary>
    /// Makes both declared successful range responses flow through the common generated result abstraction.
    /// </summary>
    /// <param name="clientPath">The generated NSwag client source path.</param>
    public static void Process(string clientPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientPath);

        string source = File.ReadAllText(clientPath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        const string concreteRangeReturn = "System.Threading.Tasks.Task<FileRangeReadCompleteResult> ReadFileRangeAsync";
        const string commonRangeReturn = "System.Threading.Tasks.Task<FileRangeReadResult> ReadFileRangeAsync";
        int concreteRangeCount = CountOccurrences(source, concreteRangeReturn);
        if (concreteRangeCount == 4)
        {
            source = source.Replace(concreteRangeReturn, commonRangeReturn, StringComparison.Ordinal);
        }
        else if (concreteRangeCount != 0 || CountOccurrences(source, commonRangeReturn) != 4)
        {
            throw new InvalidOperationException("Generated-client range return shape was neither raw nor already post-processed.");
        }

        const string exceptionalPartial = "                            throw new HexalithFoldersApiException<FileRangeReadPartialResult>(\"Partial authorized byte range when end-of-file is reached before the requested exclusive end offset.\", status_, objectResponse_.Text, headers_, objectResponse_.Object, null);";
        const string successfulPartial = "                            return objectResponse_.Object;";
        int exceptionalPartialCount = CountOccurrences(source, exceptionalPartial);
        if (exceptionalPartialCount == 1)
        {
            source = source.Replace(exceptionalPartial, successfulPartial, StringComparison.Ordinal);
        }
        else if (exceptionalPartialCount != 0)
        {
            throw new InvalidOperationException("Generated-client partial-range branch occurred an unexpected number of times.");
        }

        AssertPartialRangeIsSuccessful(source);

        string[] strictWireTypes =
        [
            "PathMetadata",
            "VisiblePathMetadata",
            "ContentAllowedPathMetadata",
            "FileMutationRequest",
            "AddFileRequest",
            "ChangeFileRequest",
            "RemoveFileRequest",
            "FileRangeReadCompleteResult",
            "FileRangeReadPartialResult",
            "FileSearchResult",
            "FileSafeResourceUnavailableProblem",
            "FileRangeUnsatisfiableProblem",
            "FilePolicyUnavailableProblem",
            "FileContentEvidenceInvalidProblem",
            "FileContentEvidenceInvalidOrValidationProblem",
            "FileInlineTransportRequiredProblem",
            "FileContentLimitExceededProblem",
            "FileContentLimitExceededOrWorkspaceTransitionProblem",
            "FileMutationUnavailableProblem",
            "FileContextUnavailableProblem",
        ];
        foreach (string typeName in strictWireTypes)
        {
            string declaration = $"    public partial class {typeName}";
            string decorated = $"    [Newtonsoft.Json.JsonConverter(typeof(Hexalith.Folders.Client.Serialization.Oq2WireObjectConverter))]\n{declaration}";
            if (!source.Contains(decorated, StringComparison.Ordinal))
            {
                source = ReplaceExactly(source, declaration, decorated, expectedCount: 1);
            }
        }

        WriteAtomically(clientPath, source);
    }

    private static void AssertPartialRangeIsSuccessful(string source)
    {
        const string partialReader = "var objectResponse_ = await ReadObjectResponseAsync<FileRangeReadPartialResult>(response_, headers_, cancellationToken).ConfigureAwait(false);";
        int partialReaderOffset = source.IndexOf(partialReader, StringComparison.Ordinal);
        if (partialReaderOffset < 0
            || source.IndexOf(partialReader, partialReaderOffset + partialReader.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException("Generated-client partial-range response reader occurred an unexpected number of times.");
        }

        int nextBranchOffset = source.IndexOf("                        else", partialReaderOffset, StringComparison.Ordinal);
        string partialBranch = source[partialReaderOffset..(nextBranchOffset < 0 ? source.Length : nextBranchOffset)];
        if (CountOccurrences(partialBranch, "return objectResponse_.Object;") != 1
            || partialBranch.Contains("throw new HexalithFoldersApiException<FileRangeReadPartialResult>", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated-client HTTP 206 branch is not a single successful partial-range return.");
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static string ReplaceExactly(string source, string oldValue, string newValue, int expectedCount)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(oldValue, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += oldValue.Length;
        }

        if (count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Generated-client post-processing expected {expectedCount} occurrence(s) but found {count}: {oldValue}");
        }

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
