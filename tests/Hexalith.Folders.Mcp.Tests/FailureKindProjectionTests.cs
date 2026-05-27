using System;
using System.Reflection;
using System.Runtime.Serialization;

using Hexalith.Folders.Client.Generated;
using Hexalith.Folders.Mcp.Errors;

using Shouldly;

using Xunit;

namespace Hexalith.Folders.Mcp.Tests;

/// <summary>
/// Verifies the canonical <see cref="CanonicalErrorCategory"/> → MCP failure-kind projection: kind equals the
/// canonical category name verbatim for every post-SDK category, with <c>range_unsatisfiable</c> falling
/// through to <c>internal_error</c> as the documented drift signal. The expected value is derived
/// independently from the SDK enum's <see cref="EnumMemberAttribute"/>, so this is a real cross-check of the
/// explicit switch, not a tautology.
/// </summary>
public sealed class FailureKindProjectionTests
{
    public static TheoryData<CanonicalErrorCategory> AllCategories()
    {
        TheoryData<CanonicalErrorCategory> data = [];
        foreach (CanonicalErrorCategory category in Enum.GetValues<CanonicalErrorCategory>())
        {
            data.Add(category);
        }

        return data;
    }

    public static TheoryData<ProblemDetailsClientAction> AllClientActions()
    {
        TheoryData<ProblemDetailsClientAction> data = [];
        foreach (ProblemDetailsClientAction action in Enum.GetValues<ProblemDetailsClientAction>())
        {
            data.Add(action);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void ProjectsEveryCategoryToItsCanonicalNameVerbatim(CanonicalErrorCategory category)
    {
        string kind = FailureKindProjection.Project(category);

        if (category == CanonicalErrorCategory.Range_unsatisfiable)
        {
            // Absent from the oracle mcp_failure_kind set → internal_error (drift signal), per Story 5.2 parity.
            kind.ShouldBe("internal_error");
        }
        else
        {
            kind.ShouldBe(EnumMemberValue(category));
        }
    }

    [Fact]
    public void ProjectsSuccessToSuccessAndInternalErrorToInternalError()
    {
        FailureKindProjection.Project(CanonicalErrorCategory.Success).ShouldBe("success");
        FailureKindProjection.Project(CanonicalErrorCategory.Internal_error).ShouldBe("internal_error");
    }

    [Theory]
    [MemberData(nameof(AllClientActions))]
    public void ProjectsEveryClientActionToItsCanonicalToken(ProblemDetailsClientAction action)
        => FailureKindProjection.ClientAction(action).ShouldBe(EnumMemberValue(action));

    private static string EnumMemberValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        FieldInfo field = typeof(TEnum).GetField(value.ToString())!;
        return field.GetCustomAttribute<EnumMemberAttribute>()!.Value!;
    }
}
