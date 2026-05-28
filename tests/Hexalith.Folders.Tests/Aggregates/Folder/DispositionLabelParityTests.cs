using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.UI.Services;

using SdkLifecycleState = Hexalith.Folders.Client.Generated.LifecycleState;
using SdkOperatorDispositionLabel = Hexalith.Folders.Client.Generated.OperatorDispositionLabel;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Folder;

/// <summary>
/// Story 6.3 / AC #6 — drift sentinel that fails the build if the server-side state machine
/// (<see cref="FolderStateTransitions"/>) and the UI-side <see cref="DispositionLabelMapper"/>
/// disagree on the C6 matrix. The wire-name string (snake_case) is the join key — both sides
/// produce it (server via <see cref="JsonStringEnumMemberNameAttribute"/> /
/// <see cref="FolderStateTransitions.ToWireName"/>, SDK via
/// <see cref="EnumMemberAttribute"/>).
/// </summary>
public sealed class DispositionLabelParityTests
{
    public static TheoryData<SdkLifecycleState, bool> AllStatesWithLagBranch()
    {
        TheoryData<SdkLifecycleState, bool> data = new();
        foreach (SdkLifecycleState value in Enum.GetValues<SdkLifecycleState>())
        {
            data.Add(value, false);
            data.Add(value, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatesWithLagBranch))]
    public void ServerAndUiDispositionMapsAgree_ForEveryLifecycleState(
        SdkLifecycleState sdkState,
        bool hasProjectionLagEvidence)
    {
        string wireName = SdkLifecycleStateWireName(sdkState);
        FolderWorkspaceLifecycleState serverState = ServerStateFromWireName(wireName);

        SdkOperatorDispositionLabel uiDisposition = DispositionLabelMapper
            .ResolveDisposition(sdkState, hasProjectionLagEvidence);
        FolderOperatorDisposition serverDisposition = FolderStateTransitions
            .GetOperatorDisposition(serverState, hasProjectionLagEvidence);

        string uiWire = SdkOperatorDispositionLabelWireName(uiDisposition);
        string serverWire = ServerOperatorDispositionWireName(serverDisposition);

        serverWire.ShouldBe(uiWire);
    }

    [Theory]
    [MemberData(nameof(ServerLifecycleStates))]
    public void ServerWireNamesMatchSdkEnumMemberValues(FolderWorkspaceLifecycleState serverState)
    {
        string serverWire = FolderStateTransitions.ToWireName(serverState);
        SdkLifecycleState sdkState = SdkStateFromWireName(serverWire);
        string sdkWire = SdkLifecycleStateWireName(sdkState);
        sdkWire.ShouldBe(serverWire);
    }

    public static TheoryData<FolderWorkspaceLifecycleState> ServerLifecycleStates()
    {
        TheoryData<FolderWorkspaceLifecycleState> data = new();
        foreach (FolderWorkspaceLifecycleState value in Enum.GetValues<FolderWorkspaceLifecycleState>())
        {
            data.Add(value);
        }

        return data;
    }

    private static FolderWorkspaceLifecycleState ServerStateFromWireName(string wireName)
    {
        foreach (FolderWorkspaceLifecycleState value in Enum.GetValues<FolderWorkspaceLifecycleState>())
        {
            if (FolderStateTransitions.ToWireName(value) == wireName)
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"No FolderWorkspaceLifecycleState produces wire-name '{wireName}'. Server-side rename detected — update the C6 mapping on both sides.");
    }

    private static SdkLifecycleState SdkStateFromWireName(string wireName)
    {
        foreach (SdkLifecycleState value in Enum.GetValues<SdkLifecycleState>())
        {
            if (SdkLifecycleStateWireName(value) == wireName)
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"No SDK LifecycleState produces wire-name '{wireName}'. Server-side rename without an SDK regenerate detected.");
    }

    private static string SdkLifecycleStateWireName(SdkLifecycleState value)
        => ReadEnumMemberValue(typeof(SdkLifecycleState), Enum.GetName(value)!);

    private static string SdkOperatorDispositionLabelWireName(SdkOperatorDispositionLabel value)
        => ReadEnumMemberValue(typeof(SdkOperatorDispositionLabel), Enum.GetName(value)!);

    private static string ServerOperatorDispositionWireName(FolderOperatorDisposition value)
    {
        string name = Enum.GetName(value)!;
        FieldInfo field = typeof(FolderOperatorDisposition).GetField(name)!;
        JsonStringEnumMemberNameAttribute attribute = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!;
        return attribute.Name;
    }

    private static string ReadEnumMemberValue(Type enumType, string memberName)
    {
        FieldInfo field = enumType.GetField(memberName)!;
        EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()!;
        return attribute.Value!;
    }
}
