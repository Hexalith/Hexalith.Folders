using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Server.Provisioning;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Checks restricted stream identity, exact intent normalization, and old creation replay.</summary>
public sealed class FolderProvisioningContractTests
{
    [Fact]
    public void IdentityUsesTenantAndNumberedStepInsteadOfDisplayName()
    {
        const string task = "01M8AJJBDA94DA9GXDSN2K0RZX";
        const string step = task + "/step/2";
        FolderProvisioningContract.IsCanonicalStep(task, step).ShouldBeTrue();
        FolderProvisioningContract.IsCanonicalStep(task, task + "/step/02").ShouldBeFalse();
        FolderProvisioningContract.IsCanonicalStep(task, task + "/step/0").ShouldBeFalse();
        FolderProvisioningContract.IsCanonicalStep(task, task + "/step/provision").ShouldBeFalse();

        string address = FolderProvisioningContract.DeriveFolderId("tenant-a", step);
        address.ShouldStartWith("fld-p-");
        address.ShouldBe(FolderProvisioningContract.DeriveFolderId("tenant-a", step));
        address.ShouldNotBe(FolderProvisioningContract.DeriveFolderId("tenant-b", step));
        address.ShouldNotBe(FolderProvisioningContract.DeriveFolderId("tenant-a", task + "/step/3"));
    }

    [Fact]
    public void DisplayNameDigestPreservesIntentAfterTrimming()
    {
        string digest = FolderProvisioningContract.ComputeRequestSha256("Folder A");
        digest.ShouldBe(FolderProvisioningContract.ComputeRequestSha256(" Folder A "));
        digest.ShouldNotBe(FolderProvisioningContract.ComputeRequestSha256("Folder B"));
        digest.ShouldNotBe(FolderProvisioningContract.ComputeRequestSha256("folder A"));
    }

    [Fact]
    public void GatewayIntentMatchesTheCaseSensitiveOwnerPayloadAndIgnoresLeaseTakeover()
    {
        var request = new FolderProvisioningRequest(
            "task-a", "task-a/step/1", "test.provision@1",
            FolderProvisioningContract.ComputeRequestSha256("Folder A"), 1, 0, "Folder A");
        byte[] original = Encode(request);
        var adapter = FolderServerCatalogAdapterSource.Create()
            .Single(adapter => adapter.CommandType == FolderProvisioningContract.CommandType);
        IdempotencyCanonicalIntent intent = Adapt(original);
        Adapt(Encode(request)).SemanticPayload.ShouldBe(intent.SemanticPayload);
        var newFence = Adapt(Encode(request with { Fence = 2 }));
        newFence.SemanticPayload.ShouldBe(intent.SemanticPayload);
        newFence.SemanticOptions.ShouldBe(intent.SemanticOptions);
        Adapt(Encode(request with { DisplayName = "Folder B" })).SemanticPayload.ShouldNotBe(intent.SemanticPayload);

        var command = new CommandEnvelope("message-a", "tenant-a", "folders", "folder-a",
            FolderProvisioningContract.CommandType, original, "correlation-a", null, "worker-a", null);
        FolderDomainProcessor.ReadProvisioningCommand(command).ShouldBe(request);
        byte[] wrongCase = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original)
            .Replace("\"displayName\"", "\"DisplayName\"", StringComparison.Ordinal));
        Should.Throw<JsonException>(() => FolderDomainProcessor.ReadProvisioningCommand(command with { Payload = wrongCase }));

        static byte[] Encode(FolderProvisioningRequest request)
            => JsonSerializer.SerializeToUtf8Bytes(request, FolderProvisioningService.WireJson);

        IdempotencyCanonicalIntent Adapt(byte[] payload)
            => adapter.CreateIntent(new(FolderProvisioningContract.CommandType, "tenant-a", "folders",
                "folder-a", payload, null));
    }

    [Theory]
    [InlineData("""{"reasonCode":"missing-stream"}""", true)]
    [InlineData("""{"reasonCode":"forbidden-replay-scope"}""", false)]
    [InlineData("""{"detail":"Event stream does not exist."}""", false)]
    public void MissingStreamRequiresTheAuthoritativeReasonCode(string body, bool missing)
    {
        ArgumentNullException.ThrowIfNull(body);
        using JsonDocument problem = JsonDocument.Parse(body);
        FolderProvisioningStreamPresence.IsMissingProblem(problem.RootElement).ShouldBe(missing);
    }

    [Fact]
    public void OrdinaryCreateKeepsLegacyPayloadAndRestrictedCreateRequiresAnExplicitAclChoice()
    {
        var ordinary = new FolderCreated("tenant-a", "tenant-a", "folder-a", "Folder A", null, null, [],
            FolderLifecycleState.Active, FolderRepositoryBindingState.Unbound, "actor-a", "correlation-a",
            "task-a", "key-a", "fingerprint-a", DateTimeOffset.UnixEpoch);
        string bytes = JsonSerializer.Serialize(ordinary, JsonSerializerOptions.Web);
        bytes.ShouldNotContain("provisioningReceipt");
        bytes.ShouldNotContain("inheritOrganizationPermissions");

        FolderStreamName stream = FolderStreamName.Create("tenant-a", "folder-a");
        FolderState replayed = FolderStateApply.Apply(FolderState.Empty, ordinary, stream);
        replayed.IsCreated.ShouldBeTrue();
        replayed.InheritOrganizationPermissions.ShouldBeNull();

        var receipt = new FolderProvisioningReceipt("receipt-a", "tenant-a", "folder-a", "actor-a", "task-a",
            "task-a/step/1", "test.provision@1", FolderProvisioningContract.ComputeRequestSha256("Folder A"), 3);
        var restricted = ordinary with { ProvisioningReceipt = receipt, InheritOrganizationPermissions = false };
        FolderState state = FolderStateApply.Apply(FolderState.Empty, restricted, stream);
        state.ProvisioningReceipt.ShouldBe(receipt);
        state.InheritOrganizationPermissions.ShouldBe(false);
        Should.Throw<InvalidOperationException>(() => FolderStateApply.Apply(FolderState.Empty,
            restricted with { InheritOrganizationPermissions = null }, stream));
    }
}
