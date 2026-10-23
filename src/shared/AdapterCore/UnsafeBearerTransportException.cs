namespace Hexalith.Folders.AdapterCore;

/// <summary>Reports an unsafe bearer destination using only constant, metadata-safe text.</summary>
internal sealed class UnsafeBearerTransportException : Exception
{
    /// <summary>Initializes the safe transport rejection.</summary>
    public UnsafeBearerTransportException()
        : base(BearerTransportPolicy.RejectionMessage)
    {
    }
}
