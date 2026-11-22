using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Hexalith.Folders.Contracts.Tests;

/// <summary>
/// Resolves YAML 1.2 core scalars from representation nodes before gate validation.
/// </summary>
internal static partial class YamlCoreScalar
{
    /// <summary>
    /// Checks whether the node represents a string in the YAML core schema.
    /// </summary>
    /// <param name="node">The candidate YAML node.</param>
    /// <param name="allowEmpty">Whether an empty string is valid.</param>
    /// <returns>Whether the node represents an accepted string.</returns>
    internal static bool IsString(YamlNode node, bool allowEmpty = false)
    {
        if (node is not YamlScalarNode scalar || scalar.Value is null
            || (!allowEmpty && string.IsNullOrWhiteSpace(scalar.Value)))
        {
            return false;
        }

        string tag = scalar.Tag.ToString();
        if (tag is "tag:yaml.org,2002:str" or "!")
        {
            return true;
        }

        if (!scalar.Tag.IsEmpty && tag != "?")
        {
            return false;
        }

        // The representation model keeps untagged plain scalars as text. Resolve the
        // YAML 1.2 core non-string forms without culture-dependent numeric parsing.
        return scalar.Style is not (ScalarStyle.Plain or ScalarStyle.Any)
            || (scalar.Value.Length > 0 && !NonStringYamlCoreScalarPattern().IsMatch(scalar.Value));
    }

    [GeneratedRegex(@"\A(?:~|null|Null|NULL|true|True|TRUE|false|False|FALSE|0o[0-7]+|0x[0-9a-fA-F]+|[-+]?(?:\.[0-9]+|[0-9]+(?:\.[0-9]*)?)(?:[eE][-+]?[0-9]+)?|[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN))\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NonStringYamlCoreScalarPattern();

    /// <summary>
    /// Reads a uniquely declared boolean key using YAML core schema tags and spellings.
    /// </summary>
    /// <param name="mapping">The mapping that declares the key.</param>
    /// <param name="key">The scalar key.</param>
    /// <param name="value">The boolean value when the declaration is valid.</param>
    /// <returns>Whether the declaration contains exactly one valid boolean.</returns>
    internal static bool TryReadBoolean(YamlMappingNode mapping, string key, out bool value)
    {
        value = false;
        KeyValuePair<YamlNode, YamlNode>[] entries = mapping.Children
            .Where(entry => entry.Key is YamlScalarNode scalar && scalar.Value == key)
            .ToArray();
        if (entries.Length != 1
            || !IsString(entries[0].Key)
            || entries[0].Value is not YamlScalarNode node
            || !(node.Tag.ToString() == "tag:yaml.org,2002:bool"
                || ((node.Tag.IsEmpty || node.Tag.ToString() == "?") && node.Style is ScalarStyle.Plain or ScalarStyle.Any)))
        {
            return false;
        }

        if (node.Value is "true" or "True" or "TRUE")
        {
            value = true;
            return true;
        }

        return node.Value is "false" or "False" or "FALSE";
    }
}
