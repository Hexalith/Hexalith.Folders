using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;
using Hexalith.FrontComposer.Contracts.Attributes;

namespace Hexalith.Folders.UI.Services;

/// <summary>
/// C6 matrix mapper: resolves SDK <see cref="LifecycleState"/> values to the operator-visible
/// <see cref="OperatorDispositionLabel"/>, the FrontComposer <see cref="BadgeSlot"/>, and the
/// English label / wire-name strings consumed by the Folders operations console.
/// Drift against the server-side state machine is enforced by
/// <c>DispositionLabelParityTests</c> in <c>Hexalith.Folders.Tests</c>.
/// </summary>
public static class DispositionLabelMapper
{
    private static readonly FrozenDictionary<LifecycleState, string> _wireNames = BuildWireNames();

    public static OperatorDispositionLabel ResolveDisposition(
        LifecycleState state,
        bool hasProjectionLagEvidence = false)
        => state switch
        {
            LifecycleState.Requested => OperatorDispositionLabel.Auto_recovering,
            LifecycleState.Preparing => OperatorDispositionLabel.Auto_recovering,
            LifecycleState.Ready => hasProjectionLagEvidence
                ? OperatorDispositionLabel.Degraded_but_serving
                : OperatorDispositionLabel.Available,
            LifecycleState.Locked => OperatorDispositionLabel.Degraded_but_serving,
            LifecycleState.Changes_staged => OperatorDispositionLabel.Degraded_but_serving,
            LifecycleState.Dirty => OperatorDispositionLabel.Awaiting_human,
            LifecycleState.Committed => OperatorDispositionLabel.Auto_recovering,
            LifecycleState.Failed => OperatorDispositionLabel.Terminal_until_intervention,
            LifecycleState.Inaccessible => OperatorDispositionLabel.Terminal_until_intervention,
            LifecycleState.Unknown_provider_outcome => OperatorDispositionLabel.Awaiting_human,
            LifecycleState.Reconciliation_required => OperatorDispositionLabel.Awaiting_human,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workspace lifecycle state."),
        };

    public static BadgeSlot ResolveSlot(OperatorDispositionLabel label)
        => label switch
        {
            OperatorDispositionLabel.Auto_recovering => BadgeSlot.Info,
            OperatorDispositionLabel.Available => BadgeSlot.Success,
            OperatorDispositionLabel.Degraded_but_serving => BadgeSlot.Warning,
            OperatorDispositionLabel.Awaiting_human => BadgeSlot.Warning,
            OperatorDispositionLabel.Terminal_until_intervention => BadgeSlot.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown operator-disposition label."),
        };

    public static string ResolveLabel(OperatorDispositionLabel label)
        => label switch
        {
            OperatorDispositionLabel.Auto_recovering => "Auto-recovering",
            OperatorDispositionLabel.Available => "Available",
            OperatorDispositionLabel.Degraded_but_serving => "Degraded but serving",
            OperatorDispositionLabel.Awaiting_human => "Awaiting human",
            OperatorDispositionLabel.Terminal_until_intervention => "Terminal until intervention",
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown operator-disposition label."),
        };

    public static string ResolveTechnicalStateLabel(LifecycleState state)
        => _wireNames.TryGetValue(state, out string? wireName)
            ? wireName
            : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown workspace lifecycle state.");

    private static FrozenDictionary<LifecycleState, string> BuildWireNames()
    {
        Dictionary<LifecycleState, string> map = [];
        foreach (LifecycleState value in Enum.GetValues<LifecycleState>())
        {
            string name = Enum.GetName(value)
                ?? throw new InvalidOperationException($"LifecycleState value {value} has no declared name.");
            FieldInfo field = typeof(LifecycleState).GetField(name)
                ?? throw new InvalidOperationException($"LifecycleState field '{name}' is missing.");
            EnumMemberAttribute attribute = field.GetCustomAttribute<EnumMemberAttribute>()
                ?? throw new InvalidOperationException($"LifecycleState.{name} is missing the [EnumMember] attribute.");
            map[value] = attribute.Value
                ?? throw new InvalidOperationException($"LifecycleState.{name} has a null EnumMember.Value.");
        }

        return map.ToFrozenDictionary();
    }
}
