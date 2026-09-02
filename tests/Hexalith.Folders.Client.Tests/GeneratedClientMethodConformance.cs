using System.Globalization;
using System.Reflection;
using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Client.Tests;

/// <summary>
/// Matches generated client methods by behaviorally significant signature properties.
/// </summary>
internal static class GeneratedClientMethodConformance
{
    /// <summary>
    /// The required parameter types of the archive operation: the folder identifier plus the
    /// idempotency, correlation, and task headers as strings, the request body, and cancellation.
    /// Header identity is pinned by the oracle-driven <c>TransportParityConformanceTests</c>;
    /// this contract pins type, multiplicity, and requiredness only.
    /// </summary>
    internal static readonly Type[] ArchiveFolderParameterTypes =
    [
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(ArchiveFolderRequest),
        typeof(CancellationToken),
    ];

    /// <summary>
    /// The required parameter types of the lifecycle-status operation: the folder identifier plus the
    /// correlation header as strings, the nullable freshness class, and cancellation.
    /// Header identity is pinned by the oracle-driven <c>TransportParityConformanceTests</c>;
    /// this contract pins type, multiplicity, and requiredness only.
    /// </summary>
    internal static readonly Type[] LifecycleStatusParameterTypes =
    [
        typeof(string),
        typeof(string),
        typeof(ReadConsistencyClass?),
        typeof(CancellationToken),
    ];

    /// <summary>
    /// Determines whether a method has the expected return type and required parameter-type multiset.
    /// </summary>
    /// <remarks>
    /// Reference-type nullability annotations are not part of runtime <see cref="Type"/> identity, so
    /// <c>string</c> and <c>string?</c> compare equal here; nullable value types such as
    /// <c>ReadConsistencyClass?</c> are distinct types and are therefore compared exactly.
    /// </remarks>
    /// <param name="method">The generated client method to inspect.</param>
    /// <param name="expectedReturnType">The operation's required return type.</param>
    /// <param name="expectedParameterTypes">The operation's required parameter types, including multiplicity.</param>
    /// <returns><see langword="true"/> when the semantic signature matches; otherwise, <see langword="false"/>.</returns>
    internal static bool HasRequiredSignature(
        MethodInfo method,
        Type expectedReturnType,
        IReadOnlyCollection<Type> expectedParameterTypes)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(expectedReturnType);
        ArgumentNullException.ThrowIfNull(expectedParameterTypes);

        ParameterInfo[] actualParameters = method.GetParameters();

        return !method.IsStatic
            && !method.ContainsGenericParameters
            && method.ReturnType == expectedReturnType
            && actualParameters.Length == expectedParameterTypes.Count
            && actualParameters.All(static parameter => !parameter.IsOptional && !parameter.HasDefaultValue)
            && expectedParameterTypes.All(expectedType =>
                actualParameters.Count(parameter => parameter.ParameterType == expectedType)
                == expectedParameterTypes.Count(type => type == expectedType));
    }

    /// <summary>
    /// Renders the metadata-only signatures of every same-named overload so a conformance failure
    /// reports what was actually generated instead of only what was required.
    /// </summary>
    /// <param name="clientType">The generated client surface to inspect.</param>
    /// <param name="methodName">The operation method name.</param>
    /// <returns>A human-readable description of the observed overloads.</returns>
    internal static string DescribeOverloads(Type clientType, string methodName)
    {
        ArgumentNullException.ThrowIfNull(clientType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        string[] descriptions = [.. clientType.GetMethods()
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Select(Describe)];

        return descriptions.Length == 0
            ? string.Create(CultureInfo.InvariantCulture, $"no overload named '{methodName}' is exposed")
            : string.Join("; ", descriptions);
    }

    /// <summary>
    /// Renders one method's metadata-only signature, marking optional parameters so a requiredness
    /// regression is legible in the failure message.
    /// </summary>
    /// <param name="method">The method to describe.</param>
    /// <returns>A human-readable signature description.</returns>
    internal static string Describe(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        string parameters = string.Join(
            ", ",
            method.GetParameters().Select(static parameter => string.Create(
                CultureInfo.InvariantCulture,
                $"{FormatType(parameter.ParameterType)}{(parameter.IsOptional || parameter.HasDefaultValue ? " (optional)" : string.Empty)}")));

        return string.Create(CultureInfo.InvariantCulture, $"{FormatType(method.ReturnType)}({parameters})");
    }

    private static string FormatType(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is Type underlying)
        {
            return FormatType(underlying) + "?";
        }

        // A non-generic type nested inside a generic one reports IsGenericType == true while its
        // Name carries no arity backtick, so the marker is located before it is sliced away.
        int arity = type.IsGenericType ? type.Name.IndexOf('`', StringComparison.Ordinal) : -1;

        if (arity < 0)
        {
            return type.Name;
        }

        string name = type.Name[..arity];
        string arguments = string.Join(", ", type.GetGenericArguments().Select(FormatType));

        return string.Create(CultureInfo.InvariantCulture, $"{name}<{arguments}>");
    }
}
