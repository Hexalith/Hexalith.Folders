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

        headers["X-Hexalith-Task-Id"].ShouldBe("task-001");
        headers[TestRequestHeaders.CorrelationId].ShouldBe("correlation-001");
        headers[TestRequestHeaders.IdempotencyKey].ShouldBe("idempotency-001");
    }

    [Theory]
    [InlineData("X-Hexalith-Task-Id", "café", "correlation-001", "idempotency-001")]
    [InlineData("X-Correlation-Id", "task-001", "café", "idempotency-001")]
    [InlineData("Idempotency-Key", "task-001", "correlation-001", "café")]
    public void RejectsNonAsciiIdentifiersOnlyAtTheHttpBoundary(
        string expectedHeader, string taskId, string correlationId, string idempotencyKey)
    {
        TestFolderContext context = FoldersTestDataFactory.FolderContext(new TestFolderContextOverrides(
            TaskId: taskId,
            CorrelationId: correlationId,
            IdempotencyKey: idempotencyKey));
        context.TaskId.ShouldBe(taskId);
        context.CorrelationId.ShouldBe(correlationId);
        context.IdempotencyKey.ShouldBe(idempotencyKey);

        ArgumentException error = Should.Throw<ArgumentException>(() => TestRequestHeaders.FromFolderContext(context));

        error.Message.ShouldContain(expectedHeader);
        error.Message.ShouldNotContain("café");
    }
}
