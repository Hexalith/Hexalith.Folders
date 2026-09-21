using Hexalith.Folders.Server;
using Hexalith.Folders.Server.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Server.Tests;

public sealed class Pd10AuthorizationAuditSinkTests
{
    [Fact]
    public async Task DefaultSinkIsRegisteredAndEmitsAllStructuredFieldsDespiteRequestCancellation()
    {
        ServiceCollection services = new();
        services.AddFoldersServer();
        ServiceDescriptor registration = services.Last(item => item.ServiceType == typeof(IPd10AuthorizationAuditSink));
        registration.ImplementationType.ShouldBe(typeof(LoggingPd10AuthorizationAuditSink));

        RecordingLogger logger = new();
        LoggingPd10AuthorizationAuditSink sink = new(logger);
        Pd10AuthorizationAuditRecord record = new(
            "actor-a",
            "tenant-a",
            "GetFolderLifecycleStatus",
            "status-permission-and-lock-inspection",
            "allow",
            "correlation-audit-log");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await sink.WriteAsync(record, cancellation.Token);

        IReadOnlyDictionary<string, object?> fields = logger.Fields.ShouldNotBeNull();
        fields["Actor"].ShouldBe(record.Actor);
        fields["Tenant"].ShouldBe(record.Tenant);
        fields["Operation"].ShouldBe(record.Operation);
        fields["OperationFamily"].ShouldBe(record.OperationFamily);
        fields["Result"].ShouldBe(record.Result);
        fields["CorrelationId"].ShouldBe(record.CorrelationId);
    }

    private sealed class RecordingLogger : ILogger<LoggingPd10AuthorizationAuditSink>
    {
        public IReadOnlyDictionary<string, object?>? Fields { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logLevel.ShouldBe(LogLevel.Information);
            Fields = ((IEnumerable<KeyValuePair<string, object?>>)state!)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        }
    }
}
