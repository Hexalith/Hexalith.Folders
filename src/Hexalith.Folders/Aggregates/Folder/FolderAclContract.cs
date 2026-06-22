using System.Text;

namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>
/// Bidirectional mapping between the canonical REST ACL contract (id-addressed entries with a coarse
/// permission level and grant/revoke effect) and the folder domain ACL model (composite-key access
/// overrides over supported actions). See story 8.1 DD2.
/// </summary>
/// <remarks>
/// The spine permission levels are coarse (read/write/administer) while the domain models ACL per
/// supported action. The three levels map to the three representative supported actions below; ACL
/// overrides on other supported actions are not expressible in the REST contract and are omitted
/// from <c>ListFolderAclEntries</c>.
/// </remarks>
public static class FolderAclContract
{
    /// <summary>Domain action backing the <c>read</c> permission level.</summary>
    public const string ReadAction = "read_metadata";

    /// <summary>Domain action backing the <c>write</c> permission level.</summary>
    public const string WriteAction = "mutate_files";

    /// <summary>Domain action backing the <c>administer</c> permission level.</summary>
    public const string AdministerAction = "manage_folder_access";

    /// <summary>Maps a REST permission level to its backing domain action, or null when unrecognized.</summary>
    /// <param name="permissionLevel">The REST permission level (read/write/administer).</param>
    /// <returns>The backing supported action, or null.</returns>
    public static string? PermissionLevelToAction(string? permissionLevel)
        => permissionLevel switch
        {
            "read" => ReadAction,
            "write" => WriteAction,
            "administer" => AdministerAction,
            _ => null,
        };

    /// <summary>Maps a domain action back to its REST permission level, or null when not expressible.</summary>
    /// <param name="action">The domain action.</param>
    /// <returns>The REST permission level, or null.</returns>
    public static string? ActionToPermissionLevel(string? action)
        => action switch
        {
            ReadAction => "read",
            WriteAction => "write",
            AdministerAction => "administer",
            _ => null,
        };

    /// <summary>Returns the canonical token for a principal kind.</summary>
    /// <param name="principalKind">The principal kind.</param>
    /// <returns>The canonical token.</returns>
    public static string PrincipalKindToken(FolderAccessPrincipalKind principalKind)
        => principalKind switch
        {
            FolderAccessPrincipalKind.User => "user",
            FolderAccessPrincipalKind.Group => "group",
            FolderAccessPrincipalKind.Role => "role",
            FolderAccessPrincipalKind.DelegatedServiceAgent => "delegated_service_agent",
            _ => throw new InvalidOperationException($"Undefined FolderAccessPrincipalKind value: {(int)principalKind}."),
        };

    /// <summary>Parses a principal-kind token.</summary>
    /// <param name="token">The token.</param>
    /// <param name="principalKind">The parsed principal kind.</param>
    /// <returns>True when the token is recognized.</returns>
    public static bool TryParsePrincipalKindToken(string? token, out FolderAccessPrincipalKind principalKind)
    {
        principalKind = token switch
        {
            "user" => FolderAccessPrincipalKind.User,
            "group" => FolderAccessPrincipalKind.Group,
            "role" => FolderAccessPrincipalKind.Role,
            "delegated_service_agent" => FolderAccessPrincipalKind.DelegatedServiceAgent,
            _ => FolderAccessPrincipalKind.User,
        };

        return token is "user" or "group" or "role" or "delegated_service_agent";
    }

    /// <summary>Formats a subject reference from a principal kind and id.</summary>
    /// <param name="principalKind">The principal kind.</param>
    /// <param name="principalId">The principal id.</param>
    /// <returns>The <c>{kind}:{principalId}</c> subject reference.</returns>
    public static string FormatSubjectRef(FolderAccessPrincipalKind principalKind, string principalId)
        => $"{PrincipalKindToken(principalKind)}:{principalId}";

    /// <summary>Parses a <c>{kind}:{principalId}</c> subject reference.</summary>
    /// <param name="subjectRef">The subject reference.</param>
    /// <param name="principalKindToken">The parsed principal-kind token.</param>
    /// <param name="principalKind">The parsed principal kind.</param>
    /// <param name="principalId">The parsed principal id.</param>
    /// <returns>True when the subject reference is well-formed and recognized.</returns>
    public static bool TryParseSubjectRef(
        string? subjectRef,
        out string principalKindToken,
        out FolderAccessPrincipalKind principalKind,
        out string principalId)
    {
        principalKindToken = string.Empty;
        principalKind = FolderAccessPrincipalKind.User;
        principalId = string.Empty;

        if (string.IsNullOrWhiteSpace(subjectRef))
        {
            return false;
        }

        int separator = subjectRef.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator >= subjectRef.Length - 1)
        {
            return false;
        }

        principalKindToken = subjectRef[..separator];
        principalId = subjectRef[(separator + 1)..];
        return TryParsePrincipalKindToken(principalKindToken, out principalKind);
    }

    /// <summary>
    /// Derives the deterministic, opaque, URL-safe ACL entry id for a (principal, permission level) pair
    /// within a folder. Reversible enough for round-tripping in <c>ListFolderAclEntries</c> and verifiable
    /// against the path id in <c>UpdateFolderAclEntry</c>.
    /// </summary>
    /// <param name="principalKindToken">The principal-kind token.</param>
    /// <param name="principalId">The principal id.</param>
    /// <param name="permissionLevel">The REST permission level.</param>
    /// <returns>The base64url-encoded ACL entry id.</returns>
    public static string DeriveAclEntryId(string principalKindToken, string principalId, string permissionLevel)
        => Base64UrlEncode($"{principalKindToken}|{principalId}|{permissionLevel}");

    private static string Base64UrlEncode(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
