using System.Text.RegularExpressions;
using Shouldly;

namespace Hexalith.Folders.Testing.Tests.Helpers;

/// <summary>
/// Shared assertions used by Stories 1.4 and 1.5 artifact tests to confirm that the Contract Spine
/// has not absorbed operation groups owned by later stories (workspace/lock, file/context,
/// commit/status, audit, operations-console). Strengthened beyond simple prefix matching so that
/// e.g. <c>/api/v1/files-context</c> is also caught.
/// </summary>
internal static class SpineContractAssertions
{
    private static readonly string[] ForbiddenDownstreamPathFragments =
    [
        "workspaces",
        "locks",
        "files",
        "context",
        "commits",
        "audit",
        "ops-console",
        "operations-console",
    ];

    public static void AssertNoDownstreamOperationGroups(string spine)
    {
        foreach (string fragment in ForbiddenDownstreamPathFragments)
        {
            // Match `/api/v1/<fragment>` followed by `/`, `-`, or end-of-segment so that
            // e.g. `/api/v1/files`, `/api/v1/files/`, and `/api/v1/files-context` all trip
            // the assertion, while unrelated identifiers (`/api/v1/folders`) do not.
            Regex pattern = new(
                @$"/api/v1/{Regex.Escape(fragment)}(?:[/\-""\s:]|$)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            pattern.IsMatch(spine).ShouldBeFalse(
                $"/api/v1/{fragment} (or a variant) belongs to a downstream Contract Spine story; matching fragment '{fragment}' must not appear in the spine.");
        }
    }
}
