namespace Hexalith.Folders.EventStore.Tests;

/// <summary>Exact descriptors captured from baseline 4de8e5d0 using the public 3.130.2 encoder before migration.</summary>
/// <param name="CommandType">The existing discriminator.</param>
/// <param name="OperationId">The existing semantic operation.</param>
/// <param name="AggregateId">The existing namespace-specific target.</param>
/// <param name="Payload">The representative legacy input, including nulls, ordered batches and scopes.</param>
/// <param name="CanonicalIntent">The complete legacy encoded bytes in base64.</param>
/// <param name="Sha256">The SHA-256 digest of those bytes.</param>
internal sealed record LegacyFoldersIntentDescriptor(string CommandType, string OperationId, string AggregateId,
    string Payload, string CanonicalIntent, string Sha256);
