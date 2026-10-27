using Hexalith.Folders.Client.Convenience;
using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Client.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;
namespace Hexalith.Folders.Client.Tests;

public sealed class NativeFileUploadTests
{
    [Theory]
    [InlineData("expectedFileVersion")]
    [InlineData("lockFence")]
    [InlineData("completed")]
    public void MissingRequiredEvidenceFieldsCannotDecodeAsZeroOrFalse(string member)
    {
        var json = JObject.Parse("{\"status\":\"absent\",\"expectedFileVersion\":0,\"lockFence\":1,\"completed\":false}");
        json.Remove(member);
        Should.Throw<JsonSerializationException>(() => JsonConvert.DeserializeObject<NativeEvidence>(json.ToString()));
    }
    [Fact]
    public void MissingReplayDecisionCannotDecodeAsUnique()
        => Should.Throw<JsonSerializationException>(() => JsonConvert.DeserializeObject<NativeAcceptedCommand>(
            "{\"acceptedAt\":\"2026-10-26T00:00:00Z\",\"correlationId\":\"c\",\"taskId\":\"t\",\"status\":\"accepted\"}"));
    [Fact]
    public void LegacyLookupRetainsTheExactGeneratedCanonicalKey()
    {
        var path = new PathMetadata
        {
            NormalizedPath = "mailbox/message/1-a.pdf",
            DisplayName = "a.pdf",
            PathPolicyClass = PathMetadataPathPolicyClass.Metadata_only,
            UnicodeNormalization = PathMetadataUnicodeNormalization.NFC
        };
        var mutation = new FileMutationRequest
        {
            OperationId = "mailbox-attachment:" + new string('a', 64),
            PathMetadata = path,
            FileOperationKind = FileMutationRequestFileOperationKind.Add,
            ContentHashReference = "hashref_" + new string('b', 64)
        };
        string generated = FileUpload.ComputeIdempotencyKey(mutation, "workspace:" + new string('c', 64), "task:" + new string('d', 64));
        NativeFileUpload.ComputeLegacyAddIdempotencyKey(mutation.OperationId, path, mutation.ContentHashReference,
            "workspace:" + new string('c', 64), "task:" + new string('d', 64)).ShouldBe(generated);
        path.NormalizedPath = "mailbox/message/1-Facture été 日本語.pdf"; path.DisplayName = "Facture été 日本語.pdf";
        string unicode = NativeFileUpload.ComputeLegacyAddIdempotencyKey(mutation.OperationId, path, mutation.ContentHashReference,
            "workspace:" + new string('c', 64), "task:" + new string('d', 64));
        unicode.ShouldStartWith("sha256:"); unicode.ShouldNotBe(generated);
        NativeFileUpload.ComputeLegacyAddIdempotencyKey(mutation.OperationId, path, mutation.ContentHashReference,
            "workspace:" + new string('c', 64), "task:" + new string('d', 64)).ShouldBe(unicode);
    }
}
