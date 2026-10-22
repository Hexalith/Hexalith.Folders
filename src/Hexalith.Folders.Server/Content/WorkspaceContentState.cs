using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Server.Content;

/// <summary>One stable authoritative event prefix, never a derived projection.</summary>
/// <param name="State">Source-folded folder state.</param>
/// <param name="Head">Exact pre-append source head.</param>
/// <param name="Events">Retained raw source envelopes available to resolve rejections.</param>
/// <param name="ActorOwned">Whether this is the command-local actor fold.</param>
internal sealed record WorkspaceContentState(FolderState State, long Head, IReadOnlyList<StreamReadEvent> Events, bool ActorOwned);
