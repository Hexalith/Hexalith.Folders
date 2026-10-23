using Hexalith.Folders.Providers.Forgejo;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

public sealed class ForgejoCanonicalEvidenceTests
{
    [Fact]
    public void CanonicalStringEncodingPinsNullEmptyUnicodeAndOrderVectors()
    {
        const string domain = "hxf-forgejo:v1:test-vector";

        ForgejoProviderSafeOperationEvidence.Create(domain, [null])
            .ShouldBe("9fcd5fa2ecc5b9105759aedf1a7a4ca301d8c1cee7a285fe1f2fb73abaf18a31");
        ForgejoProviderSafeOperationEvidence.Create(domain, [string.Empty])
            .ShouldBe("bb44f6b0a99e009c57f48e89732d2bd480abfde8523b168171cc67aca379eff5");
        ForgejoProviderSafeOperationEvidence.Create(domain, ["e\u0301"])
            .ShouldBe("1911ea3381f1297463639fdcd053c07af656f6445c34913800b4d182f3f6a93d");
        ForgejoProviderSafeOperationEvidence.Create(domain, ["A", "B"])
            .ShouldBe("46bdb23fe7fe8bfa2305fc559872185fd2a520de0bde966cd9278f0f74364313");
        ForgejoProviderSafeOperationEvidence.Create(domain, ["A", "B"])
            .ShouldNotBe(ForgejoProviderSafeOperationEvidence.Create(domain, ["B", "A"]));
    }

    [Theory]
    [InlineData("mutation-target", "0bfcf206fa1d1319693242f0779ca27d12173439e5cdf5ff545afb02eea59683")]
    [InlineData("commit-target", "af94d0f7b05a46d347ebeafff58d8011cdb767cb6bb32a4740aa44bb340a04da")]
    [InlineData("status-target", "79b6d1c46c5f7887eae1d328a0b0fe745eb0f186b6424694378b4e05ef385d56")]
    [InlineData("path", "83e6b306cca3e6cf9bf0fbe79df87e568236a5f762f3be45a05070c84007bebb")]
    [InlineData("content", "95b148fc799507657dc6c02a20cd7885489c321c16bcb00898db52498b477108")]
    [InlineData("change-set", "9db8af86c514abd4f8621e39b6a8b3635d495b68c6098e8cef02d66d451f576a")]
    [InlineData("staged-changes", "d907b2d19c4f44ffbc75628e601cfb2adf9797f12a0625c5e8b4b4427441a8ae")]
    [InlineData("commit-message", "0583fe83b2c5f326ef11c3dca404554f05bcab045d6bc77f0002b383f5fc7f81")]
    [InlineData("expected-head", "03fcddb8cba4c368789552c3f5c450e698a1125c3f5d5dd553d1ffa91295e97c")]
    [InlineData("full-ref", "55639d6caf54f496f1154bc61cefbb6ae9f3e80f2a573219af6c57d73170bb42")]
    [InlineData("status-expected-head", "1e3ce9f8fcbde94dac19c2c2019ddb8ea04a064c34d8e0713279eaa74fe8d689")]
    [InlineData("intended-commit", "1cd2daf75280854d42c10047c93271a6fd2ef1b1a92648ab1f6ba7f077c0878f")]
    [InlineData("status-window", "3dd691efd132135208290c9a54ff480058434ff42e2dc9194fa1d81fce90fc5a")]
    [InlineData("operation-target", "03ea63f0710ea28eaf47923dbe9421ccd1d2a25aa9c0f92c0441582327969ea3")]
    [InlineData("staged-outcome", "8f95376ff9c74ae18fe3537d6311f33ea02f7b6dc15ba70bae226dd366801a50")]
    [InlineData("commit-outcome", "5a85903a6a9717a023ce237ccaf58ad9d5cce648428eedcdf18f133ffe53667c")]
    [InlineData("status-observation", "a392060d72c655046801af97358b140a1d4223e89706afbd8ee7167edff768f7")]
    [InlineData("created-commit", "18e20f3070bf2e546f45e94a10bd56f1ee934d387031b56fd137bbd6f001a1e8")]
    [InlineData("commit-failure", "f6ac95060cd84b12b913121417b49ac27646b3a4c67098a82fb7308ace49ae66")]
    public void SourceAndOutcomeDomainsRemainVersionedAndSeparated(string purpose, string expected)
        => ForgejoProviderSafeOperationEvidence.Create($"hxf-forgejo:v1:{purpose}", ["A"]).ShouldBe(expected);
}
