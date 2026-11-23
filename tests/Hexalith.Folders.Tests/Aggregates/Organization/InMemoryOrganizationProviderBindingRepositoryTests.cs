using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Tests.Helpers;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Aggregates.Organization;

public sealed class InMemoryOrganizationProviderBindingRepositoryTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ReadersDuringAppendShouldObserveThePublishedBindingLedgerAndCount()
    {
        InMemoryOrganizationProviderBindingRepository repository = new();
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();
        OrganizationStreamName stream = repository.CreateStreamName(command.ManagedTenantId, command.OrganizationId);
        OrganizationProviderBindingResult result = OrganizationAggregate.Handle(OrganizationState.Empty, command);
        ProviderBindingConfigured binding = result.Events.ShouldHaveSingleItem().ShouldBeOfType<ProviderBindingConfigured>();
        CancellationToken token = TestContext.Current.CancellationToken;
        CoordinatedEventList<IOrganizationEvent> events = new(result.Events, 1, Timeout, token);
        Task<OrganizationAclAppendOutcome> writer = Start(() => repository.AppendIfFingerprintAbsent(
            stream, command.IdempotencyKey, binding.IdempotencyFingerprint, events), token);
        using CountdownEvent started = new(3);
        Task<OrganizationState> state;
        Task<string?> fingerprint;
        Task<long> count;
        List<Task> workers = [writer];
        bool completedDuringAppend;
        try
        {
            await events.WriterHoldingGate.WaitAsync(Timeout, token);
            state = StartReader(() => repository.Load(stream));
            fingerprint = StartReader(() => repository.TryGetIdempotencyFingerprint(stream, command.IdempotencyKey, out string? value) ? value : null);
            count = StartReader(() => repository.EventsAppended);
            started.Wait(Timeout, token).ShouldBeTrue();
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
            completedDuringAppend = state.IsCompleted || fingerprint.IsCompleted || count.IsCompleted;
        }
        finally
        {
            events.ReleaseWriter();
            // Finish cleanup even if the test was cancelled before all readers started.
            await Task.WhenAll(workers).WaitAsync(Timeout, CancellationToken.None);
        }

        completedDuringAppend.ShouldBeFalse();
        (await writer.ConfigureAwait(true)).ShouldBe(OrganizationAclAppendOutcome.Appended);
        (await state.ConfigureAwait(true)).ProviderBindings[binding.ProviderBindingRef].IdempotencyFingerprint.ShouldBe(binding.IdempotencyFingerprint);
        (await fingerprint.ConfigureAwait(true)).ShouldBe(binding.IdempotencyFingerprint);
        (await count.ConfigureAwait(true)).ShouldBe(1);

        repository.AppendIfFingerprintAbsent(stream, command.IdempotencyKey, binding.IdempotencyFingerprint, result.Events)
            .ShouldBe(OrganizationAclAppendOutcome.FingerprintMatched);
        OrganizationProviderBindingResult conflict = OrganizationAggregate.Handle(
            OrganizationState.Empty, command with { CredentialReferenceId = "credential-b" });
        ProviderBindingConfigured other = conflict.Events.ShouldHaveSingleItem().ShouldBeOfType<ProviderBindingConfigured>();
        repository.AppendIfFingerprintAbsent(stream, command.IdempotencyKey, other.IdempotencyFingerprint, conflict.Events)
            .ShouldBe(OrganizationAclAppendOutcome.FingerprintConflict);
        repository.Load(stream).ProviderBindings[binding.ProviderBindingRef].CredentialReferenceId.ShouldBe(binding.CredentialReferenceId);
        repository.TryGetIdempotencyFingerprint(stream, command.IdempotencyKey, out string? retained).ShouldBeTrue();
        retained.ShouldBe(binding.IdempotencyFingerprint);
        repository.EventsAppended.ShouldBe(1);

        Task<T> StartReader<T>(Func<T> read)
        {
            Task<T> reader = Start(() =>
            {
                started.Signal();
                return read();
            }, token);
            workers.Add(reader);
            return reader;
        }
    }

    [Fact]
    public void AnAppendDuringInputEnumerationShouldRetainTheFirstBinding()
    {
        InMemoryOrganizationProviderBindingRepository repository = new();
        ConfigureProviderBinding command = ProviderBindingCommandFactory.Configure();
        OrganizationStreamName stream = repository.CreateStreamName(command.ManagedTenantId, command.OrganizationId);
        var first = OrganizationAggregate.Handle(OrganizationState.Empty, command).Events;
        var second = OrganizationAggregate.Handle(OrganizationState.Empty, command with { CredentialReferenceId = "credential-b" }).Events;
        CoordinatedEventList<IOrganizationEvent> input = new(second, 1, Timeout, TestContext.Current.CancellationToken);
        input.ReleaseWriter(() => repository.AppendIfFingerprintAbsent(stream, command.IdempotencyKey,
            first[0].IdempotencyFingerprint, first).ShouldBe(OrganizationAclAppendOutcome.Appended));

        repository.AppendIfFingerprintAbsent(stream, command.IdempotencyKey, second[0].IdempotencyFingerprint, input)
            .ShouldBe(OrganizationAclAppendOutcome.FingerprintConflict);
        repository.Load(stream).ProviderBindings[command.ProviderBindingRef].CredentialReferenceId.ShouldBe(command.CredentialReferenceId);
        repository.TryGetIdempotencyFingerprint(stream, command.IdempotencyKey, out string? retained).ShouldBeTrue();
        retained.ShouldBe(first[0].IdempotencyFingerprint);
        repository.EventsAppended.ShouldBe(1);
    }

    private static Task<T> Start<T>(Func<T> operation, CancellationToken cancellationToken)
        => Task.Factory.StartNew(operation, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
