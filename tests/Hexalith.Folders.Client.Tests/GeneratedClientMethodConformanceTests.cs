using System.Reflection;
using Hexalith.Folders.Client.Generated;
using Shouldly;
using Xunit;

namespace Hexalith.Folders.Client.Tests;

public sealed class GeneratedClientMethodConformanceTests
{
    [Fact]
    public void RequiredSignatureMatcherIgnoresParameterNamesAndOrder()
    {
        MethodInfo method = Fixture(nameof(RemangledAndReorderedArchiveAsync));

        GeneratedClientMethodConformance.HasRequiredSignature(
            method,
            typeof(Task<AcceptedCommand>),
            GeneratedClientMethodConformance.ArchiveFolderParameterTypes).ShouldBeTrue();
    }

    [Fact]
    public void RequiredSignatureMatcherAcceptsRemangledAndReorderedLifecycleParameters()
    {
        MethodInfo method = Fixture(nameof(RemangledAndReorderedLifecycleAsync));

        GeneratedClientMethodConformance.HasRequiredSignature(
            method,
            typeof(Task<FolderLifecycleStatus>),
            GeneratedClientMethodConformance.LifecycleStatusParameterTypes).ShouldBeTrue();
    }

    [Fact]
    public void RequiredSignatureMatcherIgnoresReferenceTypeNullabilityAnnotations()
    {
        MethodInfo method = Fixture(nameof(NullableAnnotatedLifecycleAsync));

        GeneratedClientMethodConformance.HasRequiredSignature(
            method,
            typeof(Task<FolderLifecycleStatus>),
            GeneratedClientMethodConformance.LifecycleStatusParameterTypes).ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(LifecycleWithNonNullableConsistencyClassAsync))]
    [InlineData(nameof(LifecycleWithMissingParameterAsync))]
    [InlineData(nameof(LifecycleWithChangedReturnTypeAsync))]
    public void RequiredSignatureMatcherRejectsLifecycleContractDrift(string fixtureName)
    {
        MethodInfo method = Fixture(fixtureName);

        GeneratedClientMethodConformance.HasRequiredSignature(
            method,
            typeof(Task<FolderLifecycleStatus>),
            GeneratedClientMethodConformance.LifecycleStatusParameterTypes)
            .ShouldBeFalse($"{fixtureName} is semantic contract drift and must not match.");
    }

    [Theory]
    [InlineData(nameof(ArchiveWithMissingParameterAsync))]
    [InlineData(nameof(ArchiveWithAddedParameterAsync))]
    [InlineData(nameof(ArchiveWithChangedParameterTypeAsync))]
    [InlineData(nameof(ArchiveWithRedistributedParameterMultiplicitiesAsync))]
    [InlineData(nameof(ArchiveWithOptionalParametersAsync))]
    [InlineData(nameof(ArchiveWithOptionalTrailingParameterAsync))]
    [InlineData(nameof(ArchiveWithChangedReturnTypeAsync))]
    [InlineData(nameof(StaticArchiveAsync))]
    [InlineData(nameof(GenericArchiveAsync))]
    public void RequiredSignatureMatcherRejectsSemanticContractDrift(string fixtureName)
    {
        MethodInfo method = Fixture(fixtureName);

        GeneratedClientMethodConformance.HasRequiredSignature(
            method,
            typeof(Task<AcceptedCommand>),
            GeneratedClientMethodConformance.ArchiveFolderParameterTypes)
            .ShouldBeFalse($"{fixtureName} is semantic contract drift and must not match.");
    }

    [Fact]
    public void OverloadDescriptionReportsObservedGeneratedSignatures()
    {
        string description = GeneratedClientMethodConformance.DescribeOverloads(typeof(IClient), "ArchiveFolderAsync");

        description.ShouldContain("Task<AcceptedCommand>");
        description.ShouldContain("ArchiveFolderRequest");
    }

    [Fact]
    public void SignatureDescriptionMarksOptionalParameters()
        => GeneratedClientMethodConformance
            .Describe(Fixture(nameof(ArchiveWithOptionalTrailingParameterAsync)))
            .ShouldContain("(optional)");

    [Fact]
    public void SignatureDescriptionRendersNonGenericTypesNestedInGenericTypes()
        => GeneratedClientMethodConformance
            .Describe(Fixture(nameof(NestedInGenericParameterAsync)))
            .ShouldContain("Enumerator");

    [Fact]
    public void OverloadDescriptionReportsAbsenceWhenTheOperationIsMissing()
        => GeneratedClientMethodConformance
            .DescribeOverloads(typeof(IClient), "OperationThatWasNeverGeneratedAsync")
            .ShouldContain("no overload named");

    private static MethodInfo Fixture(string name) =>
        typeof(GeneratedClientMethodConformanceTests)
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .ShouldNotBeNull();

    // The fixture methods below stay instance methods deliberately: calling the instance helper
    // FixtureNotInvokable keeps CA1822 (warnings-as-errors) satisfied without a suppression, and
    // instance-ness is itself part of what the matcher requires of a generated client method.
    private Task<AcceptedCommand> RemangledAndReorderedArchiveAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader) => throw FixtureNotInvokable();

    private Task<FolderLifecycleStatus> RemangledAndReorderedLifecycleAsync(
        CancellationToken abortSignal,
        ReadConsistencyClass? consistencyHint,
        string traceHeader,
        string opaqueFolder) => throw FixtureNotInvokable();

    // List<int>.Enumerator is a non-generic type nested in a generic one: it reports
    // IsGenericType == true while its Name carries no arity backtick, so rendering it exercises the
    // one shape that would otherwise slice Name at a negative index.
    private Task<AcceptedCommand> NestedInGenericParameterAsync(List<int>.Enumerator cursor) => throw FixtureNotInvokable();

    // Reference-type nullability is erased from runtime Type identity, so an annotated overload must
    // still match; the sibling LifecycleWithNonNullableConsistencyClassAsync pins the other half of
    // that documented rule -- nullable value types are distinct types and are compared exactly.
    private Task<FolderLifecycleStatus> NullableAnnotatedLifecycleAsync(
        CancellationToken abortSignal,
        ReadConsistencyClass? consistencyHint,
        string? traceHeader,
        string opaqueFolder) => throw FixtureNotInvokable();

    private Task<FolderLifecycleStatus> LifecycleWithNonNullableConsistencyClassAsync(
        CancellationToken abortSignal,
        ReadConsistencyClass consistencyHint,
        string traceHeader,
        string opaqueFolder) => throw FixtureNotInvokable();

    private Task<FolderLifecycleStatus> LifecycleWithMissingParameterAsync(
        CancellationToken abortSignal,
        ReadConsistencyClass? consistencyHint,
        string opaqueFolder) => throw FixtureNotInvokable();

    private Task<AcceptedCommand> LifecycleWithChangedReturnTypeAsync(
        CancellationToken abortSignal,
        ReadConsistencyClass? consistencyHint,
        string traceHeader,
        string opaqueFolder) => throw FixtureNotInvokable();

    private Task<AcceptedCommand> ArchiveWithMissingParameterAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader) => throw FixtureNotInvokable();

    private Task<AcceptedCommand> ArchiveWithAddedParameterAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader,
        bool dryRun) => throw FixtureNotInvokable();

    private Task<AcceptedCommand> ArchiveWithChangedParameterTypeAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        int idempotencyHeader) => throw FixtureNotInvokable();

    // Same arity and same distinct parameter-type set as the contract, but the multiplicities differ:
    // one string became a second request body. Only multiset counting rejects this shape -- a
    // presence-only or set-equality comparison would accept it.
    private Task<AcceptedCommand> ArchiveWithRedistributedParameterMultiplicitiesAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        ArchiveFolderRequest supplementalRequest,
        string taskHeader,
        string folder,
        string correlationHeader) => throw FixtureNotInvokable();

    private Task<AcceptedCommand> ArchiveWithOptionalParametersAsync(
        CancellationToken token = default,
        ArchiveFolderRequest? request = null,
        string taskHeader = "",
        string folder = "",
        string correlationHeader = "",
        string idempotencyHeader = "") => throw FixtureNotInvokable();

    private Task<AcceptedCommand> ArchiveWithOptionalTrailingParameterAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader = "") => throw FixtureNotInvokable();

    private Task<FolderLifecycleStatus> ArchiveWithChangedReturnTypeAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader) => throw FixtureNotInvokable();

    private static Task<AcceptedCommand> StaticArchiveAsync(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader) => throw new NotSupportedException(nameof(StaticArchiveAsync));

    private Task<AcceptedCommand> GenericArchiveAsync<T>(
        CancellationToken token,
        ArchiveFolderRequest request,
        string taskHeader,
        string folder,
        string correlationHeader,
        string idempotencyHeader) => throw FixtureNotInvokable();

    private NotSupportedException FixtureNotInvokable() => new(GetType().Name);
}
