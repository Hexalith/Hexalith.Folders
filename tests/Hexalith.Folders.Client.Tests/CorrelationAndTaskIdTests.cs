using System;
using System.Text.RegularExpressions;

using Hexalith.Folders.Client.Convenience;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class CorrelationAndTaskIdTests
{
    // OpaqueIdentifier spine pattern, which any sourced correlation ID must satisfy.
    private const string OpaqueIdentifierPattern = "^[A-Za-z0-9][A-Za-z0-9_-]{15,127}$";

    [Fact]
    public void ResolveCorrelationIdPrefersExplicitValue()
    {
        StubProvider provider = new("corr_from_provider_0001");

        CorrelationAndTaskId.ResolveCorrelationId("corr_explicit_value_0001", provider)
            .ShouldBe("corr_explicit_value_0001");
    }

    [Fact]
    public void ResolveCorrelationIdFallsBackToProviderWhenExplicitBlank()
    {
        StubProvider provider = new("corr_from_provider_0001");

        CorrelationAndTaskId.ResolveCorrelationId("   ", provider).ShouldBe("corr_from_provider_0001");
    }

    [Fact]
    public void ResolveCorrelationIdGeneratesUlidWhenNeitherExplicitNorProvider()
    {
        string generated = CorrelationAndTaskId.ResolveCorrelationId(null, provider: null);

        generated.Length.ShouldBe(26);
        Regex.IsMatch(generated, OpaqueIdentifierPattern).ShouldBeTrue();
    }

    [Fact]
    public void ResolveCorrelationIdGeneratesUlidWhenProviderReturnsBlank()
    {
        StubProvider provider = new("   ");

        string generated = CorrelationAndTaskId.ResolveCorrelationId(null, provider);

        generated.Length.ShouldBe(26);
        Regex.IsMatch(generated, OpaqueIdentifierPattern).ShouldBeTrue();
    }

    [Fact]
    public void NewCorrelationIdProducesDistinctUlidShapedValues()
    {
        string first = CorrelationAndTaskId.NewCorrelationId();
        string second = CorrelationAndTaskId.NewCorrelationId();

        first.ShouldNotBe(second);
        Regex.IsMatch(first, "^[0-9A-HJKMNP-TV-Z]{26}$").ShouldBeTrue();
    }

    [Fact]
    public void ResolveTaskIdReturnsExplicitValue()
    {
        CorrelationAndTaskId.ResolveTaskId("task_01HZY7Z6N7J4Q2X8Y9V0TSK001").ShouldBe("task_01HZY7Z6N7J4Q2X8Y9V0TSK001");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveTaskIdFailsClosedWhenMissing(string? taskId)
    {
        // The SDK never generates a task ID and never coerces a missing one to empty.
        _ = Should.Throw<InvalidOperationException>(() => CorrelationAndTaskId.ResolveTaskId(taskId));
    }

    [Fact]
    public void AddFoldersCorrelationIdProviderRegistersResolvableProvider()
    {
        ServiceCollection services = new();
        _ = services.AddFoldersCorrelationIdProvider<StubProvider>();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ICorrelationIdProvider>().ShouldBeOfType<StubProvider>();
    }

    [Fact]
    public void AddFoldersCorrelationIdProviderInstanceIsResolvable()
    {
        StubProvider instance = new("corr_instance_0001");
        ServiceCollection services = new();
        _ = services.AddFoldersCorrelationIdProvider(instance);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICorrelationIdProvider>().ShouldBeSameAs(instance);
    }

    [Fact]
    public void NewCorrelationIdEncodesCurrentTimestampInTheUlidPrefix()
    {
        const string crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string generated = CorrelationAndTaskId.NewCorrelationId();

        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // The first 10 Crockford Base32 characters encode the 48-bit Unix-millisecond timestamp.
        long decoded = 0;
        for (int index = 0; index < 10; index++)
        {
            decoded = (decoded << 5) | (uint)crockford.IndexOf(generated[index]);
        }

        decoded.ShouldBeGreaterThanOrEqualTo(before);
        decoded.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void AddFoldersCorrelationIdProviderDoesNotReplaceAnAlreadyRegisteredProvider()
    {
        // TryAdd semantics: the first registration wins so callers can register a concrete provider safely.
        StubProvider instance = new("corr_first_registration_0001");
        ServiceCollection services = new();
        _ = services.AddFoldersCorrelationIdProvider(instance);
        _ = services.AddFoldersCorrelationIdProvider<StubProvider>();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICorrelationIdProvider>().ShouldBeSameAs(instance);
    }

    private sealed class StubProvider(string? correlationId) : ICorrelationIdProvider
    {
        public StubProvider()
            : this("corr_default_provider_0001")
        {
        }

        public string? GetCorrelationId() => correlationId;
    }
}
