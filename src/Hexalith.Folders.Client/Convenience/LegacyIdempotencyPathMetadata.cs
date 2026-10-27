using Hexalith.Folders.Client.Generated;
namespace Hexalith.Folders.Client.Convenience;

/// <summary>Provides the unchanged historical canonical metadata shape only for accepted-key hashing.</summary>
/// <remarks>Never returned to callers or used for mutation serialization; native paths still require full validation.</remarks>
internal sealed class LegacyIdempotencyPathMetadata : PathMetadata;
