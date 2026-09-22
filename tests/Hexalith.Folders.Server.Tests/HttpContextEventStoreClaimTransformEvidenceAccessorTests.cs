using Hexalith.Folders.Authorization;
using Hexalith.Folders.Server.Authentication;

using Microsoft.AspNetCore.Http;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class HttpContextEventStoreClaimTransformEvidenceAccessorTests
{
    [Fact]
    public void PreauthorizedEvidenceIsBoundToTheDeclaredHistoricalActionPair()
    {
        HttpContextEventStoreClaimTransformEvidenceAccessor accessor = new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        });
        PreauthorizedRequestContext.Begin(new(
            "tenant_000000001",
            "actor_0000000001",
            "folder_000000001",
            "watermark_000001",
            "organization_0001",
            "manage_folder_access",
            "bind_repository",
            null));
        try
        {
            EventStoreClaimTransformEvidence evidence = accessor.GetEvidence("bind_repository");

            evidence.IsPresent.ShouldBeTrue();
            evidence.Malformed.ShouldBeFalse();
            evidence.HasPermissionFor("manage_folder_access").ShouldBeTrue();
            evidence.HasPermissionFor("bind_repository").ShouldBeTrue();

            EventStoreClaimTransformEvidence arbitrary = accessor.GetEvidence("archive_folder");
            arbitrary.Malformed.ShouldBeTrue();
            arbitrary.HasPermissionFor("archive_folder").ShouldBeFalse();
        }
        finally
        {
            PreauthorizedRequestContext.End();
        }
    }
}
