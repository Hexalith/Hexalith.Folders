using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Observes the isolated Task 36 callback and canonical identity evidence.</summary>
internal static class Task36QualificationEvidence
{
    internal static int FoldCount(string path, string source)
    {
        string[] rows = File.ReadAllLines(path);
        if (!rows.Any(row => row.Contains("\"registered\"", StringComparison.Ordinal)))
            throw new InvalidOperationException("The live fold observer has not registered.");
        return rows.Select(row => JsonDocument.Parse(row)).Count(row =>
            row.RootElement.GetProperty("action").GetString() == "fold"
            && row.RootElement.GetProperty("source").GetString() == source);
    }

    internal static SharedProjectionDelivery Delivery(string tenant, string owner, ProjectionEventDto source)
        => new(owner, source.GlobalPosition,
            JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest(tenant, "folders", owner, [source]), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            JsonSerializer.SerializeToUtf8Bytes(source.GlobalPosition), OwnerSequenceNumber: source.SequenceNumber);

    // This byte formula is evidence-only: compare the reconstructed canonical source to the SDK-sealed leaf.
    internal static byte[] CapturedDigest(SharedProjectionDelivery delivery)
    {
        byte[] stream = Encoding.UTF8.GetBytes(delivery.SourceStream);
        Span<byte> header = stackalloc byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(header, stream.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header[4..], delivery.EnvelopePosition);
        BinaryPrimitives.WriteInt64LittleEndian(header[12..], delivery.OwnerSequenceNumber);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], delivery.CanonicalPayload.Length);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header);
        hash.AppendData(stream);
        hash.AppendData(delivery.CanonicalPayload);
        return hash.GetHashAndReset();
    }

    internal static string CheckpointKey(SharedProjectionScope scope, string owner)
        => "shared-checkpoint:" + scope.ComputeHash() + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner)));

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
