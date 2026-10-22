using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Observes the isolated Task 36 callback and canonical identity evidence.</summary>
internal static class Task36QualificationEvidence
{
    internal static int FoldCount(string source)
    {
        string path = Environment.GetEnvironmentVariable("FOLDERS_TASK36_FOLD_LOG")
            ?? throw new InvalidOperationException("Task 36 qualification requires its test-only fold observer.");
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

    internal static string CheckpointKey(SharedProjectionScope scope, string owner)
        => "shared-checkpoint:" + scope.ComputeHash() + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner)));

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
