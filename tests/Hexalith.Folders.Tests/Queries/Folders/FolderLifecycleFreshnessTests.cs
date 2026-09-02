using System.Reflection;

using Hexalith.Folders.Queries.Folders;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Queries.Folders;

public sealed class FolderLifecycleFreshnessTests
{
    [Fact]
    public void AuthorizationOutcomeTokensShouldBeCanonicalAndFailClosed()
    {
        // Pin the closed member set: a third member added without a matching token would
        // otherwise be silently mapped to `denied_safe` by the fail-closed conversion.
        Enum.GetValues<FolderLifecycleAuthorizationOutcome>().Length.ShouldBe(2);
        ((int)FolderLifecycleAuthorizationOutcome.DeniedSafe).ShouldBe(0);
        FolderLifecycleAuthorizationOutcome.DeniedSafe.ToToken().ShouldBe("denied_safe");
        FolderLifecycleAuthorizationOutcome.Allowed.ToToken().ShouldBe("allowed");
        ((FolderLifecycleAuthorizationOutcome)int.MaxValue).ToToken().ShouldBe("denied_safe");
    }

    [Fact]
    public void PublicAuthorizationOutcomeShouldRemainAPositionalString()
    {
        // FolderLifecycleStatusQueryResult is a packable positional record: the parameter's type
        // AND its ordinal position are both part of the compatibility surface, because positional
        // construction and deconstruction bind by index. Widening the outcome to the internal enum
        // or reordering the parameter is therefore a source/binary break for package consumers even
        // though the token itself is never serialized over REST.
        PropertyInfo? property = typeof(FolderLifecycleStatusQueryResult).GetProperty(
            nameof(FolderLifecycleStatusQueryResult.AuthorizationOutcome));
        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(string));

        ParameterInfo? parameter = typeof(FolderLifecycleStatusQueryResult)
            .GetConstructors()
            .Single()
            .GetParameters()
            .SingleOrDefault(static candidate => string.Equals(
                candidate.Name,
                "AuthorizationOutcome",
                StringComparison.Ordinal));
        parameter.ShouldNotBeNull();
        parameter.Position.ShouldBe(6);
        parameter.ParameterType.ShouldBe(typeof(string));
    }

    [Fact]
    public void UnavailableFallbackShouldPreserveSpecificReasonAndFreshnessContext()
    {
        FolderLifecycleFreshness freshness = new(
            "snapshot_per_task",
            FolderLifecycleStatusTestSupport.Now,
            "watermark_to_suppress",
            Stale: false,
            "source_specific_reason");

        FolderLifecycleFreshness unavailable = freshness.ToUnavailableWithFallback("generic_fallback");

        unavailable.ReadConsistency.ShouldBe("snapshot_per_task");
        unavailable.ObservedAt.ShouldBe(FolderLifecycleStatusTestSupport.Now);
        unavailable.ProjectionWatermark.ShouldBeNull();
        unavailable.Stale.ShouldBeTrue();
        unavailable.ReasonCode.ShouldBe("source_specific_reason");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnavailableFallbackShouldUseFallbackWhenInheritedReasonHasNoValue(string? inheritedReason)
    {
        FolderLifecycleFreshness freshness = FolderLifecycleStatusTestSupport.Freshness(reasonCode: inheritedReason);

        FolderLifecycleFreshness unavailable = freshness.ToUnavailableWithFallback("generic_fallback");

        unavailable.ReasonCode.ShouldBe("generic_fallback");
        unavailable.ProjectionWatermark.ShouldBeNull();
        unavailable.Stale.ShouldBeTrue();
    }

    [Fact]
    public void HandlerUnavailableShouldOverrideInheritedReasonAndPreserveFreshnessContext()
    {
        FolderLifecycleFreshness freshness = new(
            "read_your_writes",
            FolderLifecycleStatusTestSupport.Now,
            "watermark_to_suppress",
            Stale: false,
            "source_specific_reason");

        FolderLifecycleFreshness unavailable = freshness.ToUnavailableForHandler("handler_reason");

        unavailable.ReadConsistency.ShouldBe("read_your_writes");
        unavailable.ObservedAt.ShouldBe(FolderLifecycleStatusTestSupport.Now);
        unavailable.ProjectionWatermark.ShouldBeNull();
        unavailable.Stale.ShouldBeTrue();
        unavailable.ReasonCode.ShouldBe("handler_reason");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnavailableTransformationsShouldRejectReasonsWithoutValues(string? reasonCode)
    {
        FolderLifecycleFreshness freshness = FolderLifecycleStatusTestSupport.Freshness();

        Should.Throw<ArgumentException>(() => freshness.ToUnavailableWithFallback(reasonCode!));
        Should.Throw<ArgumentException>(() => freshness.ToUnavailableForHandler(reasonCode!));
    }
}
