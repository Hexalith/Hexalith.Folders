using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.Credentials;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.GitHub;

public sealed class DaprProviderCredentialSecretStoreClientTests
{
    [Theory]
    [InlineData("7", "Denied")]
    [InlineData("13", "Unavailable")]
    public async Task ActualGrpcDependencyFailuresReturnCanonicalSecretOutcomes(
        string grpcStatus, string expected)
    {
        using DaprClient client = new DaprClientBuilder().UseGrpcChannelOptions(new GrpcChannelOptions
        {
            HttpHandler = new FailedProviderCredentialSecretHandler(grpcStatus),
        }).Build();

        ProviderCredentialSecretLookupResult result = await new DaprProviderCredentialSecretStoreClient(client)
            .GetSecretAsync("folders-provider-credentials", "private-reference",
                new Dictionary<string, string>(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ToString().ShouldBe(expected);
        result.Values.ShouldBeEmpty();
        if (expected == "Unavailable")
            result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
