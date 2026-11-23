using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Client.Generation.Shared;

/// <summary>Checks the node shapes expected by the Contract Spine readers.</summary>
internal static class YamlNodeExtensions
{
    /// <summary>Returns a mapping or reports which field has the wrong shape.</summary>
    public static YamlMappingNode ShouldBeMapping(this YamlNode node, string name) =>
        node as YamlMappingNode ?? throw new InvalidOperationException($"{name} must be a mapping.");

    /// <summary>Returns a sequence or reports which field has the wrong shape.</summary>
    public static YamlSequenceNode ShouldBeSequence(this YamlNode node, string name) =>
        node as YamlSequenceNode ?? throw new InvalidOperationException($"{name} must be a sequence.");

    /// <summary>Returns a scalar or reports which field has the wrong shape.</summary>
    public static YamlScalarNode ShouldBeScalar(this YamlNode node, string name) =>
        node as YamlScalarNode ?? throw new InvalidOperationException($"{name} must be a scalar.");
}
