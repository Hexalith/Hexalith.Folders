namespace Hexalith.Folders.Server.Content;

/// <summary>Classifies a bounded native conflict without disclosing source metadata.</summary>
internal sealed class WorkspaceNativeConflictException(string code) : Exception("Native operation conflict.")
{
    internal string Code { get; } = code;
}
