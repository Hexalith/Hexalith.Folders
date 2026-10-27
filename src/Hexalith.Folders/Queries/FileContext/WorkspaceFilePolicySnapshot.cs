using System.Security.Cryptography;
using System.Text;
using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Queries.FileContext;

/// <summary>Immutable bounded server-owned OQ2 policy; caller classifications never grant access.</summary>
public sealed class WorkspaceFilePolicySnapshot
{
    private readonly string[] _includes;
    private readonly string[] _excludes;
    private readonly string[] _restricted;
    private readonly string[] _metadataOnly;
    /// <summary>Creates a validated immutable snapshot from independently configured rules.</summary>
    public WorkspaceFilePolicySnapshot(string version, IEnumerable<string> includes, IEnumerable<string> excludes,
        IEnumerable<string> restricted, string protectionMode, IEnumerable<string>? metadataOnly = null)
    {
        ArgumentNullException.ThrowIfNull(includes); ArgumentNullException.ThrowIfNull(excludes);
        ArgumentNullException.ThrowIfNull(restricted);
        _includes = includes.ToArray(); _excludes = excludes.ToArray(); _restricted = restricted.ToArray();
        _metadataOnly = metadataOnly?.ToArray() ?? [];
        string[] rules = [.. _includes, .. _excludes, .. _restricted, .. _metadataOnly];
        if (version != "1.1.0" || _includes.Length == 0 || rules.Length is < 1 or > 100
            || rules.Any(rule => !IsRule(rule)) || rules.Sum(rule => rule.Length) > 25600
            || protectionMode is not ("non_confidential" or "confidential_cleartext_required"))
            throw new InvalidOperationException("Workspace file policy is unavailable.");
        Version = version; ProtectionMode = protectionMode;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Append(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        Append(version); Append(protectionMode);
        Span<byte> count = stackalloc byte[4];
        foreach (string[] section in new[] { _includes, _excludes, _restricted, _metadataOnly })
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(count, section.Length);
            hash.AppendData(count);
            foreach (string value in section) Append(value);
        }
        Digest = Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    /// <summary>Gets the approved file-policy profile.</summary>
    public string Version { get; }
    /// <summary>Gets the immutable rule digest.</summary>
    public string Digest { get; }
    /// <summary>Gets the independently configured operational-value protection requirement.</summary>
    public string ProtectionMode { get; }
    /// <summary>Classifies a structurally valid exact path before any body lookup or result shaping.</summary>
    public string Classify(PathMetadata path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!WorkspacePathPolicyValidator.Validate(path).IsAccepted
            || path.PathPolicyClass is not ("content_allowed" or "metadata_only" or "excluded" or "restricted")) return "restricted";
        return ClassifyRules(path.NormalizedPath);
    }
    /// <summary>Applies the same current rules to a bounded historical address for read-only accepted/absence evidence.</summary>
    internal string ClassifyHistoricalEvidence(PathMetadata path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.NormalizedPath is not { Length: >= 1 and <= 500 } value || value.StartsWith('/') || value.EndsWith('/')
            || value.Contains('\\') || value.Any(character => char.IsControl(character)
                || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.Format)
            || value.Split('/').Any(segment => segment.Length == 0 || segment is "." or "..")
            || path.PathPolicyClass is not ("content_allowed" or "metadata_only")) return "restricted";
        return ClassifyRules(value);
    }
    private string ClassifyRules(string path)
    {
        string[] segments = path.Split('/');
        if (segments.Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase))) return "restricted";
        if (!_includes.Any(rule => Matches(rule, path)) || _excludes.Any(rule => Matches(rule, path))) return "excluded";
        if (_restricted.Any(rule => Matches(rule, path))) return "restricted";
        return _metadataOnly.Any(rule => Matches(rule, path)) ? "metadata_only" : "content_allowed";
    }
    /// <summary>Matches only OQ2's finite anchored segment glob language.</summary>
    public static bool Matches(string pattern, string path)
    {
        ArgumentNullException.ThrowIfNull(pattern); ArgumentNullException.ThrowIfNull(path);
        if (!IsValidGlob(pattern)) return false;
        string[] rules = pattern.Split('/'), segments = path.Split('/');
        var seen = new Dictionary<(int Rule, int Segment), bool>();
        bool Match(int rule, int segment)
        {
            if (seen.TryGetValue((rule, segment), out bool result)) return result;
            result = rule == rules.Length ? segment == segments.Length
                : rules[rule] == "**" ? Match(rule + 1, segment) || segment < segments.Length && Match(rule, segment + 1)
                : segment < segments.Length && SegmentMatches(rules[rule], segments[segment]) && Match(rule + 1, segment + 1);
            seen[(rule, segment)] = result; return result;
        }
        return Match(0, 0);
    }
    /// <summary>Validates the bounded context glob language before any protected namespace lookup.</summary>
    public static bool IsValidGlob(string? pattern) => IsRule(pattern, 512);
    private static bool IsRule(string? rule) => IsRule(rule, 256);
    private static bool IsRule(string? rule, int maximum)
        => rule is { Length: >= 1 } && rule.Length <= maximum && !rule.StartsWith('/') && !rule.EndsWith('/')
            && rule.Split('/').All(segment => segment.Length > 0 && segment is not ("." or "..")
                && (segment == "**" || !segment.Contains("**", StringComparison.Ordinal))
                && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '*' or '?'));
    private static bool SegmentMatches(string rule, string segment)
    {
        bool[] previous = new bool[segment.Length + 1]; previous[0] = true;
        foreach (char token in rule)
        {
            bool[] next = new bool[segment.Length + 1];
            if (token == '*') next[0] = previous[0];
            for (int index = 1; index <= segment.Length; index++)
                next[index] = token == '*' ? previous[index] || next[index - 1]
                    : previous[index - 1] && (token == '?' || char.ToUpperInvariant(token) == char.ToUpperInvariant(segment[index - 1]));
            previous = next;
        }
        return previous[segment.Length];
    }
}
