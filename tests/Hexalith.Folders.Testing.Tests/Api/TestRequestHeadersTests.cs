using Hexalith.Folders.Testing.Factories;
using Hexalith.Folders.Testing.Http;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Testing.Tests.Api;

public sealed class TestRequestHeadersTests
{
    [Fact]
    public void HeadersCarryCorrelationIdempotencyAndTaskContext()
    {
        TestFolderContext context = FoldersTestDataFactory.FolderContext(
            new TestFolderContextOverrides(
                TaskId: "task-001",
                CorrelationId: "correlation-001",
                IdempotencyKey: "idempotency-001"));

        IReadOnlyDictionary<string, string> headers = TestRequestHeaders.FromFolderContext(context);

        headers[TestRequestHeaders.TaskId].ShouldBe("task-001");
        headers[TestRequestHeaders.CorrelationId].ShouldBe("correlation-001");
        headers[TestRequestHeaders.IdempotencyKey].ShouldBe("idempotency-001");
    }
}
