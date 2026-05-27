using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hexalith.Folders.Aggregates.Folder;

public static partial class WorkspacePathPolicyValidator
{
    private const int MaxNormalizedPathLength = 512;
    private const int MaxDisplayNameLength = 128;
    private const int MaxPathPolicyClassLength = 80;

    public static WorkspacePathPolicyResult Validate(PathMetadata? metadata)
    {
        if (metadata is null)
        {
            return WorkspacePathPolicyResult.Denied(WorkspacePathPolicyDecision.MissingPathMetadata);
        }

        if (!string.Equals(metadata.UnicodeNormalization, "NFC", StringComparison.Ordinal))
        {
            return WorkspacePathPolicyResult.Denied(WorkspacePathPolicyDecision.InvalidUnicodeNormalization);
        }

        if (string.IsNullOrWhiteSpace(metadata.PathPolicyClass)
            || metadata.PathPolicyClass.Length > MaxPathPolicyClassLength
            || !PathPolicyClassPattern().IsMatch(metadata.PathPolicyClass))
        {
            return WorkspacePathPolicyResult.Denied(WorkspacePathPolicyDecision.InvalidPathPolicyClass);
        }

        WorkspacePathPolicyDecision? displayNameDecision = ValidateDisplayName(metadata.DisplayName);
        if (displayNameDecision is not null)
        {
            return WorkspacePathPolicyResult.Denied(displayNameDecision.Value);
        }

        WorkspacePathPolicyDecision? pathDecision = ValidateNormalizedPath(metadata.NormalizedPath);
        if (pathDecision is not null)
        {
            return WorkspacePathPolicyResult.Denied(pathDecision.Value);
        }

        return WorkspacePathPolicyResult.Accepted(
            ComputePathMetadataDigest(metadata),
            metadata.PathPolicyClass);
    }

    internal static string ComputePathMetadataDigest(PathMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, "display_name");
        AppendField(hash, metadata.DisplayName);
        AppendField(hash, "normalized_path");
        AppendField(hash, metadata.NormalizedPath);
        AppendField(hash, "path_policy_class");
        AppendField(hash, metadata.PathPolicyClass);
        AppendField(hash, "unicode_normalization");
        AppendField(hash, metadata.UnicodeNormalization);

        return $"pathmeta_{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    internal static string CanonicalPathMetadataFingerprint(PathMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return string.Join(
            '\n',
            "displayName=" + metadata.DisplayName,
            "normalizedPath=" + metadata.NormalizedPath,
            "pathPolicyClass=" + metadata.PathPolicyClass,
            "unicodeNormalization=" + metadata.UnicodeNormalization);
    }

    private static WorkspacePathPolicyDecision? ValidateNormalizedPath(string? normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath) || normalizedPath.Length > MaxNormalizedPathLength)
        {
            return WorkspacePathPolicyDecision.OverLength;
        }

        if (!string.Equals(normalizedPath, normalizedPath.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
        {
            return WorkspacePathPolicyDecision.UnicodeNormalizationAmbiguity;
        }

        foreach (char c in normalizedPath)
        {
            if (char.IsControl(c))
            {
                return WorkspacePathPolicyDecision.ControlCharacter;
            }

            if (IsInvisibleFormatChar(c))
            {
                return WorkspacePathPolicyDecision.InvisibleCharacter;
            }

            if (c > 0x7F)
            {
                return WorkspacePathPolicyDecision.UnicodeNormalizationAmbiguity;
            }
        }

        if (normalizedPath.Contains('\\', StringComparison.Ordinal))
        {
            return WorkspacePathPolicyDecision.MixedSeparators;
        }

        if (normalizedPath.StartsWith("/", StringComparison.Ordinal)
            || normalizedPath.StartsWith("//", StringComparison.Ordinal)
            || DriveRootPattern().IsMatch(normalizedPath)
            || DevicePathPattern().IsMatch(normalizedPath))
        {
            return WorkspacePathPolicyDecision.AbsolutePath;
        }

        if (ContainsPercentEncodedDotSegment(normalizedPath))
        {
            return WorkspacePathPolicyDecision.PercentDotSegmentSmuggling;
        }

        string[] segments = normalizedPath.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0)
            {
                return WorkspacePathPolicyDecision.EmptySegment;
            }

            if (segment is "." or "..")
            {
                return segment == ".."
                    ? WorkspacePathPolicyDecision.Traversal
                    : WorkspacePathPolicyDecision.DotSegment;
            }

            string platformBaseName = segment;
            int extensionSeparator = segment.IndexOf('.', StringComparison.Ordinal);
            if (extensionSeparator > 0)
            {
                platformBaseName = segment[..extensionSeparator];
            }

            if (segment.EndsWith(' ') || segment.EndsWith('.') || platformBaseName.EndsWith(' ') || platformBaseName.EndsWith('.'))
            {
                return WorkspacePathPolicyDecision.TrailingSpaceOrDotAmbiguity;
            }

            if (ReservedWindowsBaseNamePattern().IsMatch(segment))
            {
                return WorkspacePathPolicyDecision.ReservedPlatformName;
            }
        }

        return AllowedPathPattern().IsMatch(normalizedPath)
            ? null
            : WorkspacePathPolicyDecision.WorkspaceRootEscape;
    }

    private static WorkspacePathPolicyDecision? ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > MaxDisplayNameLength)
        {
            return WorkspacePathPolicyDecision.InvalidDisplayName;
        }

        if (!string.Equals(displayName, displayName.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
        {
            return WorkspacePathPolicyDecision.UnicodeNormalizationAmbiguity;
        }

        foreach (char c in displayName)
        {
            if (char.IsControl(c))
            {
                return WorkspacePathPolicyDecision.ControlCharacter;
            }

            if (IsInvisibleFormatChar(c))
            {
                return WorkspacePathPolicyDecision.InvisibleCharacter;
            }
        }

        return displayName.Contains('/', StringComparison.Ordinal) || displayName.Contains('\\', StringComparison.Ordinal)
            ? WorkspacePathPolicyDecision.InvalidDisplayName
            : null;
    }

    private static bool ContainsPercentEncodedDotSegment(string normalizedPath)
        => PercentEncodedDotPattern().IsMatch(normalizedPath);

    private static bool IsInvisibleFormatChar(char c)
        => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format
            || c == '​'
            || c == '‌'
            || c == '‍'
            || c == '﻿';

    private static void AppendField(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    [GeneratedRegex("^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPathPattern();

    [GeneratedRegex("^[A-Za-z]:/", RegexOptions.CultureInvariant)]
    private static partial Regex DriveRootPattern();

    [GeneratedRegex("^(?://|/\\\\|\\\\\\\\|\\\\\\?|\\\\\\.)", RegexOptions.CultureInvariant)]
    private static partial Regex DevicePathPattern();

    [GeneratedRegex(@"^(?:CON|NUL|PRN|AUX|COM[1-9]|LPT[1-9])(?:\..*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedWindowsBaseNamePattern();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex PathPolicyClassPattern();

    [GeneratedRegex("%(?:2e|2E)(?:%(?:2e|2E))?", RegexOptions.CultureInvariant)]
    private static partial Regex PercentEncodedDotPattern();
}
