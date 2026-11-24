using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Octokit.Internal;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed partial class OctokitGitHubApiClientTests
{
    [Theory]
    [InlineData(null, 30)]
    [InlineData("00:00:01", 1)]
    [InlineData("00:02:00", 120)]
    public void FactoryBindsValidatedDeadline(string? configured, int seconds)
    {
        using ServiceProvider provider = ConfiguredTransport(configured);
        IOptions<GitHubTransportOptions> options = provider.GetRequiredService<IOptions<GitHubTransportOptions>>();
        options.Value.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
        provider.GetRequiredService<IGitHubApiClientFactory>().ShouldBeOfType<OctokitGitHubApiClientFactory>();
    }

    [Fact]
    public async Task FactoryAppliesConfiguredDeadlineToOctokitObservation()
    {
        using ServiceProvider provider = ConfiguredTransport("00:00:01");
        GitHubTransportOptions options = provider.GetRequiredService<IOptions<GitHubTransportOptions>>().Value;
        RecordingGitHubHttpMessageHandler handler = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(true);
            throw new InvalidOperationException("The simulated transport must finish by cancellation.");
        });
        OctokitGitHubApiClientFactory factory = new(() => new HttpClientAdapter(() => handler), () => handler, options.RequestTimeout);
        await using GitHubCredentialLease credential = GitHubCredentialLease.CreateForTesting("token-sentinel");
        IGitHubApiClient client = await factory.CreateAsync(
            new GitHubApiClientRequest(
                "Hexalith-Folders", "2022-11-28", ProviderCredentialMode.AppInstallationReference,
                "provider-binding-a", "correlation-a"),
            credential,
            TestContext.Current.CancellationToken);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));

        GitHubRepositoryBindingResult result = await client.ValidateRepositoryBindingAsync(BindingRequest(), deadline.Token);

        result.FailureCondition.ShouldBe(GitHubApiFailureCondition.TimeoutDuringObservation);
        handler.Requests.ShouldHaveSingleItem();
        deadline.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:02:01")]
    public void FactoryRejectsOutOfRangeGitHubDeadline(string configured)
    {
        using ServiceProvider provider = ConfiguredTransport(configured);

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IGitHubApiClientFactory>());
    }

    private static ServiceProvider ConfiguredTransport(string? configured)
    {
        ServiceCollection services = new();
        if (configured is not null)
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Folders:Providers:GitHub:RequestTimeout"] = configured,
                }).Build());
        }

        services.AddFoldersProviderReadiness();
        return services.BuildServiceProvider();
    }
}
